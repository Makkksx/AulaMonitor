using System.Drawing;

namespace AulaMonitor.UI;

/// <summary>
/// Цветовые токены интерфейса — тёмная грифельно-изумрудная палитра. Разделена на
/// сырые значения (Raw) и смысловые алиасы (Role), чтобы смысл цвета не терялся
/// за хекс-кодом при чтении остального UI-кода.
/// </summary>
public static class Palette
{
    public static class Raw
    {
        public static readonly Color Slate950 = Color.FromArgb(19, 21, 24);
        public static readonly Color Slate900 = Color.FromArgb(25, 28, 32);
        public static readonly Color Slate850 = Color.FromArgb(29, 33, 38);
        public static readonly Color Slate800 = Color.FromArgb(41, 46, 52);
        public static readonly Color Slate500 = Color.FromArgb(124, 132, 140);
        public static readonly Color Slate300 = Color.FromArgb(188, 194, 200);
        public static readonly Color Slate100 = Color.FromArgb(234, 237, 239);

        public static readonly Color Emerald = Color.FromArgb(58, 196, 140);
        public static readonly Color Amber = Color.FromArgb(235, 173, 40);
        public static readonly Color Rose = Color.FromArgb(226, 59, 56);
        public static readonly Color Cyan = Color.FromArgb(64, 196, 232);

        public const string FontUi = "Segoe UI";
        public const string FontMono = "Consolas";
    }

    public static class Role
    {
        public static Color CardBackground => Raw.Slate950;
        public static Color PanelBackground => Raw.Slate900;
        public static Color ChipBackground => Raw.Slate850;
        public static Color Border => Raw.Slate800;

        public static Color TextPrimary => Raw.Slate100;
        public static Color TextSecondary => Raw.Slate300;
        public static Color TextMuted => Raw.Slate500;

        public static Color BatteryHigh => Raw.Emerald;
        public static Color BatteryMid => Raw.Amber;
        public static Color BatteryLow => Raw.Rose;
        public static Color Charging => Raw.Cyan;
        public static Color Idle => Raw.Slate500;
        public static Color Offline => Raw.Slate800;
    }

    /// <summary>Цвет по уровню заряда: зелёный >70%, жёлтый 30-70%, красный <30%.</summary>
    public static Color BatteryColor(int percent, bool charging)
    {
        if (charging) return Role.Charging;
        if (percent < 30) return Role.BatteryLow;
        if (percent <= 70) return Role.BatteryMid;
        return Role.BatteryHigh;
    }
}
