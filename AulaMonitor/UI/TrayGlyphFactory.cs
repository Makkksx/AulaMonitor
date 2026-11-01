using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using AulaMonitor;

namespace AulaMonitor.UI;

/// <summary>
/// Рендерит и кэширует иконки трея — battery glyph из BatteryGlyphPainter в 32x32.
/// Контур подстраивается под тему панели задач Windows (светлая/тёмная), плюс тёмный
/// полупрозрачный "ореол" на случай ошибки определения темы или смены темы на лету.
/// Кэш по ключу (процент, заряд, притушенность) — чтобы не плодить GDI-хэндлы каждый тик.
/// </summary>
public sealed class TrayGlyphFactory : IDisposable
{
    private readonly Dictionary<int, Icon> _cache = new();
    private readonly Dictionary<int, IntPtr> _handles = new();
    private readonly Color _outlineBase;
    private readonly Icon _offlineIcon;
    private readonly IntPtr _offlineHandle;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public TrayGlyphFactory()
    {
        _outlineBase = DetectOutlineColor();

        using Bitmap bmp = Render(percent: null, charging: false, dimmed: false);
        _offlineHandle = bmp.GetHicon();
        _offlineIcon = Icon.FromHandle(_offlineHandle);
    }

    private static Color DetectOutlineColor()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("SystemUsesLightTheme") is int light)
            {
                return light != 0
                    ? Color.FromArgb(45, 45, 45)     // светлая панель -> тёмный контур
                    : Color.FromArgb(235, 235, 235); // тёмная панель -> светлый контур
            }
        }
        catch { }

        return Color.FromArgb(140, 140, 140);
    }

    public Icon ForSnapshot(BatterySnapshot snapshot)
    {
        if (snapshot.Link == LinkState.Offline)
            return _offlineIcon;

        bool charging = snapshot.Charge == ChargeState.Charging;
        bool dimmed = snapshot.Link == LinkState.Idle;
        int percent = Math.Clamp(snapshot.Percent, 0, 100);
        int key = percent * 4 + (charging ? 2 : 0) + (dimmed ? 1 : 0);

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out Icon? cached))
                return cached;

            using Bitmap bmp = Render(percent, charging, dimmed);
            IntPtr handle = bmp.GetHicon();
            Icon icon = Icon.FromHandle(handle);
            _handles[key] = handle;
            _cache[key] = icon;
            return icon;
        }
    }

    private Bitmap Render(int? percent, bool charging, bool dimmed)
    {
        var bmp = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        int outlineAlpha = dimmed ? 110 : 255;
        Color outline = Color.FromArgb(outlineAlpha, _outlineBase.R, _outlineBase.G, _outlineBase.B);
        Color halo = Color.FromArgb(dimmed ? 50 : 90, 0, 0, 0);

        // Корпус занимает центральную горизонтальную полосу иконки 32x32.
        var bounds = new RectangleF(2.5f, 9f, 27f, 14f);
        BatteryGlyphPainter.Paint(g, bounds, percent, charging, dimmed, outline, halo);

        return bmp;
    }

    public void Dispose()
    {
        lock (_cache)
        {
            foreach (var h in _handles.Values) if (h != IntPtr.Zero) DestroyIcon(h);
            foreach (var icon in _cache.Values) icon.Dispose();
            _cache.Clear();
            _handles.Clear();
        }

        if (_offlineHandle != IntPtr.Zero) DestroyIcon(_offlineHandle);
        _offlineIcon.Dispose();
    }
}
