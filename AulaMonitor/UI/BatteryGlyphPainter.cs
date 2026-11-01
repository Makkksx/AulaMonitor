using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AulaMonitor.UI;

/// <summary>
/// Общая геометрия батарейки (контур + заливка + молния при зарядке), используется
/// и для маленькой иконки в трее, и для крупной карточки статуса — один код,
/// просто вписывается в прямоугольник любого размера.
/// </summary>
public static class BatteryGlyphPainter
{
    public static void Paint(
        Graphics g,
        RectangleF bounds,
        int? percent,
        bool charging,
        bool dimmed,
        Color outlineColor,
        Color? haloColor = null)
    {
        // Пропорции корпуса батарейки внутри bounds: контакт занимает ~8% справа.
        float nubW = bounds.Width * 0.08f;
        float bodyW = bounds.Width - nubW;
        float bodyH = bounds.Height;
        float bodyX = bounds.X;
        float bodyY = bounds.Y;
        float radius = bodyH * 0.22f;
        float strokeWidth = Math.Max(1.2f, bodyH * 0.12f);

        float nubH = bodyH * 0.45f;
        float nubX = bodyX + bodyW;
        float nubY = bodyY + (bodyH - nubH) / 2f;

        bool active = percent.HasValue;

        if (haloColor is { } halo)
        {
            using GraphicsPath haloPath = RoundedRect(bodyX, bodyY, bodyW, bodyH, radius);
            using Pen haloPen = new(halo, strokeWidth * 1.8f);
            g.DrawPath(haloPen, haloPath);
        }

        using (GraphicsPath outlinePath = RoundedRect(bodyX, bodyY, bodyW, bodyH, radius))
        using (Pen outlinePen = new(outlineColor, strokeWidth))
        {
            g.DrawPath(outlinePen, outlinePath);
        }

        using (GraphicsPath nubPath = RoundedRect(nubX, nubY, nubW, nubH, strokeWidth * 0.5f))
        using (Brush nubBrush = new SolidBrush(outlineColor))
        {
            g.FillPath(nubBrush, nubPath);
        }

        if (!active) return;

        int clamped = Math.Clamp(percent!.Value, 0, 100);
        Color fillColor = Palette.BatteryColor(clamped, charging);
        if (dimmed) fillColor = Color.FromArgb(140, fillColor.R, fillColor.G, fillColor.B);

        float pad = strokeWidth * 1.4f;
        float innerX = bodyX + pad, innerY = bodyY + pad;
        float innerH = bodyH - pad * 2f, innerMaxW = bodyW - pad * 2f;
        float fillW = Math.Max(innerMaxW * clamped / 100f, clamped > 0 ? innerH * 0.3f : 0f);

        if (fillW > 0)
        {
            float fillRadius = Math.Min(radius * 0.6f, fillW / 2f);
            using GraphicsPath fillPath = RoundedRect(innerX, innerY, fillW, innerH, fillRadius);
            using Brush fillBrush = new SolidBrush(fillColor);
            g.FillPath(fillBrush, fillPath);
        }

        if (charging)
        {
            PaintBolt(g, bodyX, bodyY, bodyH, dimmed);
        }
    }

    private static void PaintBolt(Graphics g, float bodyX, float bodyY, float bodyH, bool dimmed)
    {
        float s = bodyH / 14f; // масштаб относительно "эталонной" высоты 14px, на которой подбирались точки
        using Brush bolt = new SolidBrush(Color.FromArgb(dimmed ? 160 : 255, 255, 255, 255));
        using Pen boltEdge = new(Color.FromArgb(dimmed ? 90 : 160, 0, 0, 0), Math.Max(0.5f, 0.75f * s));

        PointF P(float dx, float dy) => new(bodyX + dx * s, bodyY + dy * s);
        PointF[] shape =
        {
            P(12.5f, 1.5f), P(7.5f, 8f), P(11f, 8f),
            P(9.5f, 12.5f), P(16f, 5.5f), P(12f, 5.5f),
        };
        g.FillPolygon(bolt, shape);
        g.DrawPolygon(boltEdge, shape);
    }

    public static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        if (r <= 0 || w <= 0 || h <= 0)
        {
            if (w > 0 && h > 0) path.AddRectangle(new RectangleF(x, y, w, h));
            return path;
        }

        float d = Math.Min(r * 2f, Math.Min(w, h));
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
