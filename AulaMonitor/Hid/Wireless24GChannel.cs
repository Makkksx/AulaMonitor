using System;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

using AulaMonitor;

namespace AulaMonitor.Hid;

/// <summary>
/// 2.4G-донгл на чипсете Compx. Протокол опроса заряда — результат реверс-инжиниринга
/// (не из официальной документации Aula): на feature-репорт с командой 0x13 0x4A донгл
/// отвечает тем же префиксом, пятый байт ответа — процент заряда, шестой — 0x10 при
/// зарядке. Контрольная сумма в последнем байте запроса — сумма первых 19 байт по модулю 256.
/// </summary>
public sealed class Wireless24GChannel : IBatteryChannel
{
    public LinkChannel Channel => LinkChannel.Wireless24G;

    private int _missedReads;

    /// <summary>Сбросить счётчик пропущенных ответов — вызывается, когда активен другой канал.</summary>
    public void ResetMissCount() => _missedReads = 0;

    public bool TryLocate(string[] hidPaths, out ChannelTarget target)
    {
        foreach (string path in hidPaths)
        {
            if (!KeyboardCatalog.LooksLikeDongle(path)) continue;
            target = new ChannelTarget(path, KeyboardCatalog.IdentifyDongle(path), LinkChannel.Wireless24G);
            return true;
        }

        target = default;
        return false;
    }

    public BatterySnapshot? Read(ChannelTarget target, ReboundSmoother smoother)
    {
        BatterySnapshot? fresh = QueryDongle(target.DevicePath, target.KeyboardName, smoother.Current);
        if (fresh.HasValue)
        {
            _missedReads = 0;
            int smoothed = smoother.Smooth(fresh.Value.Percent, fresh.Value.Charge);
            return fresh.Value with { Percent = smoothed };
        }

        _missedReads++;

        // Один пропуск — ещё не повод считать клавиатуру уснувшей, донгл иногда
        // просто не успевает ответить в срок. Два подряд (~несколько сотен мс каждый
        // при тайм-ауте 600мс) — скорее всего клавиатура ушла в сон.
        LinkState inferredState = _missedReads < 2 ? LinkState.Active : LinkState.Idle;

        return new BatterySnapshot(smoother.Current, ChargeState.OnBattery, inferredState, LinkChannel.Wireless24G, target.KeyboardName);
    }

    private static BatterySnapshot? QueryDongle(string devicePath, string keyboardName, int fallbackPercent)
    {
        SafeFileHandle? openHandle = null;
        try
        {
            Task<BatterySnapshot?> task = Task.Run(() =>
            {
                using SafeFileHandle handle = Win32Hid.CreateFile(
                    devicePath, 0xC0000000, 0x3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                openHandle = handle;
                if (handle.IsInvalid) return (BatterySnapshot?)null;

                byte[] request = BuildQuery();
                if (!Win32Hid.WriteFile(handle, request, (uint)request.Length, out _, IntPtr.Zero))
                    return null;

                byte[] response = new byte[64];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!Win32Hid.ReadFile(handle, response, (uint)response.Length, out uint read, IntPtr.Zero) || read < 7)
                        continue;
                    if (response[0] != 0x13 || response[1] != 0x4A)
                        continue;

                    int percent = Math.Clamp((int)response[5], 0, 100);
                    if (percent <= 0) percent = fallbackPercent;

                    ChargeState charge = response[6] == 0x10 ? ChargeState.Charging : ChargeState.OnBattery;
                    return new BatterySnapshot(percent, charge, LinkState.Active, LinkChannel.Wireless24G, keyboardName);
                }

                return null;
            });

            if (task.Wait(600))
                return task.Result;

            // Таймаут: обрываем зависший ReadFile, чтобы не держать поток из пула занятым.
            if (openHandle is { IsClosed: false, IsInvalid: false })
                Win32Hid.CancelIoEx(openHandle, IntPtr.Zero);
        }
        catch
        {
            // Устройство могло исчезнуть между перечислением путей и открытием хэндла.
        }

        return null;
    }

    private static byte[] BuildQuery()
    {
        byte[] req = new byte[20];
        req[0] = 0x13;
        req[1] = 0x4A;
        byte sum = 0;
        for (int i = 0; i < 19; i++) sum += req[i];
        req[19] = sum;
        return req;
    }
}
