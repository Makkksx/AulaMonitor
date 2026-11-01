using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AulaMonitor;

namespace AulaMonitor.UI;

/// <summary>
/// Всплывающая карточка статуса — открывается кликом по иконке трея, закрывается
/// сама при потере фокуса или по Esc. Дизайн: крупная батарейка слева (та же
/// геометрия, что в трее, только больше), процент и описание состояния справа,
/// канал связи — бейджем в правом верхнем углу.
/// </summary>
public sealed class DetailCard : Form
{
    private static readonly Size CardSize = new(300, 170);

    private static readonly Font FontTitle = new(Palette.Raw.FontUi, 10f, FontStyle.Bold);
    private static readonly Font FontSubtitle = new(Palette.Raw.FontUi, 8f, FontStyle.Regular);
    private static readonly Font FontChannel = new(Palette.Raw.FontMono, 8.5f, FontStyle.Regular);
    private static readonly Font FontPercent = new(Palette.Raw.FontMono, 22f, FontStyle.Bold);
    private static readonly Font FontStateTitle = new(Palette.Raw.FontUi, 9.8f, FontStyle.Bold);
    private static readonly Font FontStateDesc = new(Palette.Raw.FontUi, 8.2f, FontStyle.Regular);

    private BatterySnapshot _snapshot;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int WM_ACTIVATE = 0x0006;
    private const int WA_INACTIVE = 0;

    public DetailCard(BatterySnapshot snapshot)
    {
        _snapshot = snapshot;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = CardSize;
        BackColor = Palette.Role.CardBackground;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;

        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Deactivate += (_, _) => Close();
    }

    public void UpdateSnapshot(BatterySnapshot snapshot)
    {
        _snapshot = snapshot;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var bg = new SolidBrush(Palette.Role.CardBackground))
        using (var bgPath = BatteryGlyphPainter.RoundedRect(0, 0, Width, Height, 14f))
            g.FillPath(bg, bgPath);

        using (var border = new Pen(Palette.Role.Border))
        using (var borderPath = BatteryGlyphPainter.RoundedRect(0.5f, 0.5f, Width - 1f, Height - 1f, 14f))
            g.DrawPath(border, borderPath);

        DrawHeader(g);
        DrawBody(g);
    }

    private void DrawHeader(Graphics g)
    {
        const int top = 16, left = 18;

        using var titleBrush = new SolidBrush(Palette.Role.TextPrimary);
        using var subtitleBrush = new SolidBrush(Palette.Role.TextMuted);
        g.DrawString("Aula Monitor", FontTitle, titleBrush, left, top);
        g.DrawString(_snapshot.KeyboardName, FontSubtitle, subtitleBrush, left, top + 16);

        string channelText = _snapshot.Link == LinkState.Offline ? "Нет" : _snapshot.Channel switch
        {
            LinkChannel.Usb => "USB",
            LinkChannel.Wireless24G => "2.4G",
            LinkChannel.Bluetooth => "BT",
            _ => "Нет"
        };

        SizeF textSize = g.MeasureString(channelText, FontChannel);
        int pillW = (int)textSize.Width + 22;
        const int pillH = 22;
        int pillX = Width - 18 - pillW;

        using (var pillBg = new SolidBrush(Palette.Role.ChipBackground))
        using (var pillBorder = new Pen(Palette.Role.Border))
        using (var pillPath = BatteryGlyphPainter.RoundedRect(pillX, top + 2, pillW, pillH, 6f))
        {
            g.FillPath(pillBg, pillPath);
            g.DrawPath(pillBorder, pillPath);
        }

        Color dotColor = _snapshot.Link switch
        {
            LinkState.Active => Palette.BatteryColor(_snapshot.Percent, _snapshot.Charge == ChargeState.Charging),
            LinkState.Idle => Palette.Role.Idle,
            _ => Palette.Role.Offline
        };
        using (var dotBrush = new SolidBrush(dotColor))
            g.FillEllipse(dotBrush, pillX + 6, top + 2 + 8, 6, 6);

        using var pillTextBrush = new SolidBrush(Palette.Role.TextSecondary);
        g.DrawString(channelText, FontChannel, pillTextBrush, pillX + 16, top + 2 + 3);
    }

    private void DrawBody(Graphics g)
    {
        const int bodyX = 18, bodyY = 58;
        int bodyW = Width - 36, bodyH = 96;

        using (var panelBg = new SolidBrush(Palette.Role.PanelBackground))
        using (var panelPath = BatteryGlyphPainter.RoundedRect(bodyX, bodyY, bodyW, bodyH, 10f))
            g.FillPath(panelBg, panelPath);

        bool offline = _snapshot.Link == LinkState.Offline;
        bool charging = _snapshot.Charge == ChargeState.Charging;
        bool idle = _snapshot.Link == LinkState.Idle;

        // Крупная батарейка слева
        var glyphBounds = new RectangleF(bodyX + 18, bodyY + 32, 56, 30);
        Color outline = offline ? Palette.Role.TextMuted : Palette.Role.TextSecondary;
        BatteryGlyphPainter.Paint(
            g, glyphBounds,
            offline ? (int?)null : _snapshot.Percent,
            charging, idle, outline);

        // Процент под батарейкой
        string percentText = offline ? "--" : $"{_snapshot.Percent}%";
        using var percentBrush = new SolidBrush(Palette.Role.TextPrimary);
        SizeF percentSize = g.MeasureString(percentText, FontPercent);
        float percentX = glyphBounds.X + (glyphBounds.Width - percentSize.Width) / 2f;
        g.DrawString(percentText, FontPercent, percentBrush, percentX, glyphBounds.Bottom + 4);

        // Текст состояния справа от батарейки
        int textX = bodyX + 18 + 56 + 18;
        int textY = bodyY + 20;

        string stateTitle = _snapshot.Link switch
        {
            LinkState.Offline => "Отключено",
            LinkState.Idle => "Сон",
            _ => charging ? "Заряжается" : "Работает от батареи"
        };

        Color stateColor = _snapshot.Link switch
        {
            LinkState.Offline => Palette.Role.TextMuted,
            LinkState.Idle => Palette.Role.Idle,
            _ => charging ? Palette.Role.Charging : Palette.Role.BatteryHigh
        };

        using (var stateBrush = new SolidBrush(stateColor))
            g.DrawString(stateTitle, FontStateTitle, stateBrush, textX, textY);

        string stateDesc = _snapshot.Link switch
        {
            LinkState.Offline => "Нет подключения",
            LinkState.Idle => "Проснётся при нажатии клавиши",
            _ => charging
                ? "Заряжается по кабелю"
                : _snapshot.Channel switch
                {
                    LinkChannel.Usb => "Проводное подключение",
                    LinkChannel.Bluetooth => "Подключение по Bluetooth",
                    _ => "Активное беспроводное подключение"
                }
        };

        // Рамка с переносом строк — без неё длинный текст обрезается по краю окна
        // вместо переноса на вторую строку.
        var descRect = new RectangleF(textX, textY + 20, Width - textX - 14, bodyY + bodyH - (textY + 20) - 6);
        using var descBrush = new SolidBrush(Palette.Role.TextMuted);
        using var descFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisWord,
            FormatFlags = StringFormatFlags.LineLimit
        };
        g.DrawString(stateDesc, FontStateDesc, descBrush, descRect, descFormat);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetForegroundWindow(Handle);
        Activate();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ACTIVATE && (int)m.WParam == WA_INACTIVE)
        {
            Close();
            return;
        }
        base.WndProc(ref m);
    }
}
