using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

using AulaMonitor;

namespace AulaMonitor.Hid;

/// <summary>
/// Bluetooth LE: клавиатура ищется среди сопряжённых BTHLE-устройств по имени,
/// заряд читается через стандартный GATT Battery Service, а если клавиатура его
/// не отдаёт — через DEVPKEY_Device_BatteryLevel, который Windows сама кеширует
/// для HID-устройств.
/// </summary>
public sealed class BluetoothChannel : IBatteryChannel
{
    public LinkChannel Channel => LinkChannel.Bluetooth;

    // BLE-устройства не видны в общем списке HID-путей обычным перечислением —
    // их ищем отдельно через SetupDi по классу BTHLE внутри Read(), поэтому
    // TryLocate тут всегда возвращает false и напрямую не используется BatteryReader'ом
    // так же, как остальные каналы (см. BatteryReader.Poll).
    public bool TryLocate(string[] hidPaths, out ChannelTarget target)
    {
        target = default;
        return false;
    }

    public BatterySnapshot? Read(ChannelTarget target, ReboundSmoother smoother) =>
        Read(smoother.Current, smoother);

    public BatterySnapshot? Read(int fallbackPercent, ReboundSmoother smoother)
    {
        try
        {
            Task<(bool connected, int percent, string name)> task = Task.Run(() => ScanAsync(fallbackPercent));
            if (!task.Wait(300) || !task.Result.connected)
                return null;

            var (_, percent, name) = task.Result;
            int smoothed = smoother.Smooth(percent, ChargeState.OnBattery);
            return new BatterySnapshot(smoothed, ChargeState.OnBattery, LinkState.Active, LinkChannel.Bluetooth, name);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(bool connected, int percent, string name)> ScanAsync(int fallbackPercent)
    {
        IntPtr deviceSet = Win32Hid.SetupDiGetClassDevs(IntPtr.Zero, "BTHLE", IntPtr.Zero, 0x06);
        if (deviceSet == IntPtr.Zero || deviceSet == new IntPtr(-1))
            return (false, 0, KeyboardCatalog.GenericBluetooth);

        try
        {
            var data = new DeviceInfoData { Size = Marshal.SizeOf<DeviceInfoData>() };
            uint index = 0;

            while (Win32Hid.SetupDiEnumDeviceInfo(deviceSet, index++, ref data))
            {
                DevicePropertyKey nameKey = Win32Hid.FriendlyNameKey;
                byte[] nameBuf = new byte[512];
                if (!Win32Hid.SetupDiGetDevicePropertyW(deviceSet, ref data, ref nameKey, out _, nameBuf, (uint)nameBuf.Length, out _, 0))
                    continue;

                string deviceName = Encoding.Unicode.GetString(nameBuf).TrimEnd('\0');
                if (!deviceName.Contains("AULA", StringComparison.OrdinalIgnoreCase))
                    continue;

                string matchedName = KeyboardCatalog.IdentifyBluetooth(deviceName);

                var instanceId = new StringBuilder(256);
                Win32Hid.SetupDiGetDeviceInstanceIdW(deviceSet, ref data, instanceId, instanceId.Capacity, out _);

                int devTag = instanceId.ToString().IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
                if (devTag < 0 || instanceId.Length < devTag + 16) continue;

                string hexAddress = instanceId.ToString().Substring(devTag + 4, 12);
                if (!ulong.TryParse(hexAddress, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong address))
                    continue;

                using BluetoothLEDevice? ble = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
                if (ble is not { ConnectionStatus: BluetoothConnectionStatus.Connected })
                    continue;

                int percent = await ReadGattBatteryLevel(ble);
                if (percent <= 0)
                {
                    DevicePropertyKey batteryKey = Win32Hid.BatteryLevelKey;
                    byte[] batteryBuf = new byte[16];
                    if (Win32Hid.SetupDiGetDevicePropertyW(deviceSet, ref data, ref batteryKey, out _, batteryBuf, (uint)batteryBuf.Length, out _, 0)
                        && batteryBuf is [> 0 and <= 100, ..])
                    {
                        percent = batteryBuf[0];
                    }
                }

                return (true, percent > 0 ? percent : fallbackPercent, matchedName);
            }
        }
        catch
        {
            // Игнорируем — BLE-перечисление не критично для работы остальных каналов.
        }
        finally
        {
            Win32Hid.SetupDiDestroyDeviceInfoList(deviceSet);
        }

        return (false, 0, KeyboardCatalog.GenericBluetooth);
    }

    private static async Task<int> ReadGattBatteryLevel(BluetoothLEDevice ble)
    {
        try
        {
            var service = await ble.GetGattServicesForUuidAsync(GattServiceUuids.Battery);
            if (service.Status != GattCommunicationStatus.Success) return 0;

            foreach (var s in service.Services)
            {
                var chars = await s.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel);
                if (chars.Status != GattCommunicationStatus.Success) continue;

                foreach (var c in chars.Characteristics)
                {
                    var value = await c.ReadValueAsync();
                    if (value.Status != GattCommunicationStatus.Success) continue;
                    return DataReader.FromBuffer(value.Value).ReadByte();
                }
            }
        }
        catch
        {
            // Нет доступа к GATT — попробуем DEVPKEY-фоллбек в вызывающем коде.
        }

        return 0;
    }
}
