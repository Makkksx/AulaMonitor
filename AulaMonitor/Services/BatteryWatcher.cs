using System;
using System.Threading;
using AulaMonitor;

namespace AulaMonitor.Services;

/// <summary>
/// Фоновый таймер, который дёргает BatteryReader.Poll() с заданным интервалом и
/// публикует каждый новый снимок через событие. Сам по себе не знает про UI —
/// подписчик решает, что делать со снимком (красить иконку, обновлять тултип и т.д.).
/// </summary>
public sealed class BatteryWatcher : IDisposable
{
    private readonly BatteryReader _reader;
    private readonly int _intervalMs;
    private readonly System.Threading.Timer _timer;
    private int _polling; // 0/1 как флаг через Interlocked — страховка от наложения опросов

    public event Action<BatterySnapshot>? SnapshotReady;

    public BatterySnapshot Latest { get; private set; } = BatterySnapshot.Offline(Hid.KeyboardCatalog.FallbackName);

    public BatteryWatcher(int intervalMs)
    {
        _intervalMs = intervalMs;
        _reader = new BatteryReader();
        _timer = new System.Threading.Timer(_ => PollOnce(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start() => _timer.Change(0, _intervalMs);

    public void Stop() => _timer.Change(Timeout.Infinite, Timeout.Infinite);

    /// <summary>Опросить клавиатуру прямо сейчас, не дожидаясь очередного тика таймера.</summary>
    public void PollNow() => PollOnce();

    private void PollOnce()
    {
        // Если предыдущий опрос (например, зависший HID-запрос) ещё не вернулся —
        // не запускаем второй поверх него.
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;

        try
        {
            BatterySnapshot snapshot = _reader.Poll();
            Latest = snapshot;
            SnapshotReady?.Invoke(snapshot);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    public void Dispose() => _timer.Dispose();
}
