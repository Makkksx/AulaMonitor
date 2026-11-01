using Microsoft.Win32;

namespace AulaMonitor.Services;

/// <summary>
/// Интервал опроса хранится в HKCU\Software\AulaMonitor\PollIntervalMs. Важно: каждый
/// опрос — это HID-трафик на донгл, и если интервал меньше таймера сна самой клавиатуры,
/// она физически не успевает уснуть (каждый опрос сбрасывает её собственный таймер).
/// Дефолт стоит с запасом выше типичного таймера сна (обычно 30-60с в 2.4G/BT).
/// </summary>
public static class PollingPreferences
{
    private const string KeyPath = @"Software\AulaMonitor";
    private const string ValueName = "PollIntervalMs";
    public const int DefaultMs = 45_000;

    public static readonly (int Ms, string Label)[] Choices =
    {
        (3_500, "3.5 сек"),
        (10_000, "10 сек"),
        (20_000, "20 сек"),
        (45_000, "45 сек"),
        (60_000, "60 сек"),
        (120_000, "2 мин"),
    };

    public static int Get()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is int ms and > 0 ? ms : DefaultMs;
        }
        catch
        {
            return DefaultMs;
        }
    }

    public static void Set(int intervalMs)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key?.SetValue(ValueName, intervalMs, RegistryValueKind.DWord);
        }
        catch
        {
            // Если ключ реестра недоступен — приложение просто продолжит работать
            // с текущим значением в памяти до перезапуска.
        }
    }
}
