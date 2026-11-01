using AulaMonitor.Hid;

namespace AulaMonitor;

/// <summary>
/// Опрашивает все три канала по приоритету (провод важнее 2.4G, 2.4G важнее Bluetooth —
/// так совпадает с тем, как сама клавиатура решает, какой режим сейчас активен) и
/// возвращает единый снимок состояния. Один общий ReboundSmoother на все каналы —
/// заряд один и тот же вне зависимости от того, как он прочитан в этот раз.
/// </summary>
public sealed class BatteryReader
{
    private readonly ReboundSmoother _smoother;
    private readonly UsbChannel _usb = new();
    private readonly Wireless24GChannel _wireless = new();
    private readonly BluetoothChannel _bluetooth = new();

    private string _lastKnownName = KeyboardCatalog.FallbackName;

    public BatteryReader(ReboundSmoother? smoother = null)
    {
        _smoother = smoother ?? new ReboundSmoother();
    }

    public BatterySnapshot Poll()
    {
        string[] hidPaths = Win32Hid.EnumerateHidPaths();

        if (_usb.TryLocate(hidPaths, out ChannelTarget usbTarget))
        {
            _wireless.ResetMissCount();
            _lastKnownName = usbTarget.KeyboardName;
            if (_usb.Read(usbTarget, _smoother) is { } usbSnapshot)
                return usbSnapshot;
        }

        if (_wireless.TryLocate(hidPaths, out ChannelTarget wirelessTarget))
        {
            _lastKnownName = wirelessTarget.KeyboardName;
            if (_wireless.Read(wirelessTarget, _smoother) is { } wirelessSnapshot)
                return wirelessSnapshot;
        }

        if (_bluetooth.Read(_smoother.Current, _smoother) is { } bluetoothSnapshot)
        {
            _wireless.ResetMissCount();
            _lastKnownName = bluetoothSnapshot.KeyboardName;
            return bluetoothSnapshot;
        }

        return BatterySnapshot.Offline(_lastKnownName);
    }
}
