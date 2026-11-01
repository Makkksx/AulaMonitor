using AulaMonitor;

namespace AulaMonitor.Hid;

/// <summary>Адрес устройства, найденного конкретным каналом связи.</summary>
public readonly record struct ChannelTarget(string DevicePath, string KeyboardName, LinkChannel Channel);

/// <summary>
/// Один способ достучаться до клавиатуры: провод, 2.4G-донгл или Bluetooth.
/// Каждая реализация знает только свой канал и ничего не знает про остальные —
/// их порядок опроса и переключение между ними решает BatteryReader.
/// </summary>
public interface IBatteryChannel
{
    LinkChannel Channel { get; }

    /// <summary>Есть ли среди видимых системе HID-путей устройство этого канала.</summary>
    bool TryLocate(string[] hidPaths, out ChannelTarget target);

    /// <summary>Читает текущий снимок состояния у найденного устройства.</summary>
    BatterySnapshot? Read(ChannelTarget target, ReboundSmoother smoother);
}
