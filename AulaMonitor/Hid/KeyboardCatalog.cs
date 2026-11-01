using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AulaMonitor.Hid;

/// <summary>Один профиль клавиатуры из каталога: её VID/PID на разных каналах.</summary>
public sealed class KeyboardProfile
{
    private const string FallbackLabel = "AULA Keyboard";
    public string Name { get; init; } = FallbackLabel;
    public ushort DongleVid { get; init; } = 0x3554;
    public List<ushort> DonglePids { get; init; } = new();
    public ushort UsbVid { get; init; } = 0x258A;
    public List<ushort> UsbPids { get; init; } = new();
    public List<string> BluetoothNameHints { get; init; } = new();
}

internal sealed class KeyboardProfileDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("dongleVid")] public string? DongleVid { get; set; }
    [JsonPropertyName("donglePids")] public List<string>? DonglePids { get; set; }
    [JsonPropertyName("usbVid")] public string? UsbVid { get; set; }
    [JsonPropertyName("usbPids")] public List<string>? UsbPids { get; set; }
    [JsonPropertyName("bluetoothNameHints")] public List<string>? BluetoothNameHints { get; set; }
}

/// <summary>
/// База знаний "какой PID соответствует какой модели". Все клавиатуры в списке
/// используют один и тот же донгл-чипсет Compx (VID 0x3554), поэтому даже модель,
/// которой нет в явном списке, всё равно определяется и опрашивается — просто под
/// именем "AULA Wireless Keyboard" вместо конкретной модели.
/// </summary>
public static class KeyboardCatalog
{
    public const string FallbackName = "AULA Keyboard";
    public const string GenericWireless = "AULA Wireless Keyboard";
    public const string GenericUsb = "AULA Wired Keyboard";
    public const string GenericBluetooth = "AULA Bluetooth Keyboard";

    private static readonly List<KeyboardProfile> Profiles = new();

    static KeyboardCatalog()
    {
        // Известные модели на одном ODM-чипсете (Compx, VID донгла 0x3554). Модель
        // определяется автоматически по VID/PID подключённого устройства.
        // PID для AU75 эмпирически не подтверждён отдельно от F75 — у этого семейства
        // клавиатур часто один и тот же донгл-модуль под разными именами на коробке,
        // поэтому AU75 заведена на тот же PID, что и F75. Если у конкретного экземпляра
        // PID всё же другой — сработает generic-фоллбек ниже по VID донгла.
        Profiles.Add(new KeyboardProfile
        {
            Name = "AULA AU75",
            DonglePids = new List<ushort> { 0xFA09 },
            UsbPids = new List<ushort> { 0x010C },
            BluetoothNameHints = new List<string> { "AU75", "AULA AU75", "AULA-AU75" }
        });

        Profiles.Add(new KeyboardProfile
        {
            Name = "AULA F75",
            DonglePids = new List<ushort> { 0xFA09 },
            UsbPids = new List<ushort> { 0x010C },
            BluetoothNameHints = new List<string> { "F75", "AULA F75", "AULA-F75" }
        });

        Profiles.Add(new KeyboardProfile
        {
            Name = "AULA F87",
            DonglePids = new List<ushort> { 0xFA0A, 0xFA10 },
            UsbPids = new List<ushort> { 0x010D },
            BluetoothNameHints = new List<string> { "F87", "AULA F87", "AULA-F87" }
        });

        Profiles.Add(new KeyboardProfile
        {
            Name = "AULA F99",
            DonglePids = new List<ushort> { 0xFA11 },
            UsbPids = new List<ushort> { 0x010E },
            BluetoothNameHints = new List<string> { "F99", "AULA F99", "AULA-F99" }
        });

        Profiles.Add(new KeyboardProfile
        {
            Name = "AULA F68",
            DonglePids = new List<ushort> { 0xFA08 },
            UsbPids = new List<ushort> { 0x010B },
            BluetoothNameHints = new List<string> { "F68", "AULA F68", "AULA-F68" }
        });

        LoadUserOverrides();
    }

    /// <summary>
    /// Пользователь может дописать свою модель в keyboards.json рядом с exe —
    /// без пересборки программы. Запись с тем же именем перекрывает встроенную,
    /// новая — добавляется с приоритетом (проверяется первой).
    /// </summary>
    private static void LoadUserOverrides()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "keyboards.json");
            if (!File.Exists(path)) return;

            var items = JsonSerializer.Deserialize<List<KeyboardProfileDto>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null) return;

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name)) continue;

                var profile = new KeyboardProfile
                {
                    Name = item.Name,
                    DongleVid = ParseId(item.DongleVid, 0x3554),
                    DonglePids = (item.DonglePids ?? new()).Select(p => ParseId(p, 0)).Where(p => p != 0).ToList(),
                    UsbVid = ParseId(item.UsbVid, 0x258A),
                    UsbPids = (item.UsbPids ?? new()).Select(p => ParseId(p, 0)).Where(p => p != 0).ToList(),
                    BluetoothNameHints = (item.BluetoothNameHints ?? new()).Where(h => !string.IsNullOrWhiteSpace(h)).ToList()
                };

                int existing = Profiles.FindIndex(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0) Profiles[existing] = profile;
                else Profiles.Insert(0, profile);
            }
        }
        catch
        {
            // Битый keyboards.json не должен ронять встроенный каталог.
        }
    }

    private static ushort ParseId(string? value, ushort fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ushort.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort hex) ? hex : fallback;
        return ushort.TryParse(value, out ushort dec) ? dec : fallback;
    }

    public static bool LooksLikeDongle(string hidPath) =>
        hidPath.Contains("Col01", StringComparison.OrdinalIgnoreCase) &&
        (hidPath.Contains("VID_3554", StringComparison.OrdinalIgnoreCase) ||
         Profiles.Any(p => hidPath.Contains($"VID_{p.DongleVid:X4}", StringComparison.OrdinalIgnoreCase)));

    public static bool LooksLikeUsb(string hidPath) =>
        hidPath.Contains("VID_258A", StringComparison.OrdinalIgnoreCase) ||
        Profiles.Any(p => hidPath.Contains($"VID_{p.UsbVid:X4}", StringComparison.OrdinalIgnoreCase));

    public static string IdentifyDongle(string hidPath)
    {
        foreach (var p in Profiles)
        {
            if (!hidPath.Contains($"VID_{p.DongleVid:X4}", StringComparison.OrdinalIgnoreCase)) continue;
            if (p.DonglePids.Any(pid => hidPath.Contains($"PID_{pid:X4}", StringComparison.OrdinalIgnoreCase)))
                return p.Name;
        }
        return hidPath.Contains("VID_3554", StringComparison.OrdinalIgnoreCase) ? GenericWireless : FallbackName;
    }

    public static string IdentifyUsb(string hidPath)
    {
        foreach (var p in Profiles)
        {
            if (!hidPath.Contains($"VID_{p.UsbVid:X4}", StringComparison.OrdinalIgnoreCase)) continue;
            if (p.UsbPids.Any(pid => hidPath.Contains($"PID_{pid:X4}", StringComparison.OrdinalIgnoreCase)))
                return p.Name;
        }
        return hidPath.Contains("VID_258A", StringComparison.OrdinalIgnoreCase) ? GenericUsb : FallbackName;
    }

    public static string IdentifyBluetooth(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return GenericBluetooth;
        foreach (var p in Profiles)
            if (p.BluetoothNameHints.Any(h => deviceName.Contains(h, StringComparison.OrdinalIgnoreCase)))
                return p.Name;
        return deviceName.Contains("AULA", StringComparison.OrdinalIgnoreCase) ? deviceName : GenericBluetooth;
    }
}
