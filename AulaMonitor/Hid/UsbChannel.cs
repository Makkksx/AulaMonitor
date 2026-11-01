using System;
using Microsoft.Win32.SafeHandles;

using AulaMonitor;

namespace AulaMonitor.Hid;

/// <summary>
/// Проводной режим. Заряд читается через HID feature-репорт: запрос 520 байт с
/// определёнными смещениями, ответ — заряд в байте 134. Как и у донгла, протокол
/// подобран эмпирически. Провод всегда заряжает, поэтому состояние по определению
/// Active/Charging.
/// </summary>
public sealed class UsbChannel : IBatteryChannel
{
    public LinkChannel Channel => LinkChannel.Usb;

    public bool TryLocate(string[] hidPaths, out ChannelTarget target)
    {
        foreach (string path in hidPaths)
        {
            if (!KeyboardCatalog.LooksLikeUsb(path)) continue;
            target = new ChannelTarget(path, KeyboardCatalog.IdentifyUsb(path), LinkChannel.Usb);
            return true;
        }

        target = default;
        return false;
    }

    public BatterySnapshot? Read(ChannelTarget target, ReboundSmoother smoother)
    {
        int raw = QueryUsbBattery(target.DevicePath, smoother.Current);
        int smoothed = smoother.Smooth(raw, ChargeState.Charging);
        return new BatterySnapshot(smoothed, ChargeState.Charging, LinkState.Active, LinkChannel.Usb, target.KeyboardName);
    }

    private static int QueryUsbBattery(string devicePath, int fallbackPercent)
    {
        try
        {
            using SafeFileHandle handle = Win32Hid.CreateFile(
                devicePath, 0xC0000000, 0x3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) return fallbackPercent;

            byte[] request = new byte[520];
            request[0] = 0x06;
            request[1] = 0x84;
            request[4] = 0x01;
            request[6] = 0x80;

            if (!Win32Hid.HidD_SetFeature(handle, request, request.Length))
                return fallbackPercent;

            byte[] response = new byte[520];
            response[0] = 0x06;
            if (!Win32Hid.HidD_GetFeature(handle, response, response.Length))
                return fallbackPercent;

            if (response.Length <= 134) return fallbackPercent;

            int value = response[134];
            return value is >= 10 and <= 100 ? value : fallbackPercent;
        }
        catch
        {
            return fallbackPercent;
        }
    }
}
