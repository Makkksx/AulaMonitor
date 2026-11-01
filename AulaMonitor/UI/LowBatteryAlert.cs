using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace AulaMonitor.UI;

/// <summary>
/// Предупреждение о низком заряде — НЕ toast-уведомление, а обычное окно поверх всех
/// остальных (TopMost), в том же углу экрана, что и DetailCard. Фокусировка внимания
/// Windows фильтрует только то, что идёт через её собственный конвейер уведомлений
/// (toast/Центр уведомлений) — обычное окно приложения она не видит и никак не может
/// скрыть. Поэтому это надёжнее любого toast-сценария, включая Alarm, который на
/// практике тоже подавляется. Показывается без активации (WS_EX_NOACTIVATE) — не
/// забирает фокус у текущего активного окна. Закрывается только по кнопке/Esc — не
/// исчезает само и не закрывается по потере фокуса, в отличие от DetailCard.
/// </summary>
public sealed class LowBatteryAlert : Form
{
    private static LowBatteryAlert? _active;

    private readonly string _keyboardName;
    private readonly int _percent;

    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // NOACTIVATE — окно не получает фокус ни при создании, ни при клике по
            // некликабельной области, так что активное окно пользователя (где он,
            // например, печатает) не теряет выделение/курсор. TOOLWINDOW — чтобы окно
            // не создавало отдельную кнопку в панели задач (оно и так TopMost и заметно).
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public static void ShowFor(string keyboardName, int percent)
    {
        if (_active is { IsDisposed: false })
        {
            // Уже показано — не трогаем, оно и так TopMost и на виду, активировать
            // (забирать фокус) повторно незачем.
            return;
        }

        _active = new LowBatteryAlert(keyboardName, percent);
        _active.FormClosed += (_, _) => _active = null;
        _active.Show(); // ShowWithoutActivation + WS_EX_NOACTIVATE — не забирает фокус у активного окна
    }

    private LowBatteryAlert(string keyboardName, int percent)
    {
        _keyboardName = keyboardName;
        _percent = percent;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(340, 190);
        BackColor = Palette.Role.CardBackground;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;

        // Тот же угол, что у DetailCard, — предсказуемое место, а не в центре экрана.
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        // Кнопки "Закрыть" нет — закрывается кликом в любом месте окна (без лишнего
        // элемента, который на крошечном попапе выглядел бы тесно) или по Esc.
        Cursor = Cursors.Hand;
        Click += (_, _) => Close();

        Shown += (_, _) => SystemSounds.Exclamation.Play();
    }

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    protected override void WndProc(ref Message m)
    {
        // Без этого клик по кнопке "Закрыть" всё равно активировал бы окно (забрал
        // фокус), несмотря на WS_EX_NOACTIVATE в CreateParams — тот отвечает только
        // за создание/показ окна, а не за реакцию на клик по уже показанному.
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref m);
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

        using (var border = new Pen(Palette.Role.BatteryLow, 1.5f))
        using (var borderPath = BatteryGlyphPainter.RoundedRect(0.75f, 0.75f, Width - 1.5f, Height - 1.5f, 14f))
            g.DrawPath(border, borderPath);

        using var titleFont = new Font(Palette.Raw.FontUi, 12f, FontStyle.Bold);
        using var bodyFont = new Font(Palette.Raw.FontUi, 9.5f, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Palette.Role.BatteryLow);
        using var bodyBrush = new SolidBrush(Palette.Role.TextSecondary);

        g.DrawString("Низкий заряд клавиатуры", titleFont, titleBrush, 20, 20);
        g.DrawString(_keyboardName, bodyFont, bodyBrush, 20, 46);

        var glyphBounds = new RectangleF(20, 78, 60, 30);
        BatteryGlyphPainter.Paint(g, glyphBounds, _percent, charging: false, dimmed: false, Palette.Role.BatteryLow);

        using var percentFont = new Font(Palette.Raw.FontMono, 20f, FontStyle.Bold);
        using var percentBrush = new SolidBrush(Palette.Role.TextPrimary);
        g.DrawString($"{_percent}%", percentFont, percentBrush, glyphBounds.Right + 16, 80);

        using var hintBrush = new SolidBrush(Palette.Role.TextMuted);
        g.DrawString("Подключите клавиатуру для зарядки", bodyFont, hintBrush, 20, 122);

        using var dismissFont = new Font(Palette.Raw.FontUi, 7.5f, FontStyle.Italic);
        using var dismissBrush = new SolidBrush(Palette.Role.TextMuted);
        string dismissText = "Нажмите в любом месте, чтобы закрыть";
        SizeF dismissSize = g.MeasureString(dismissText, dismissFont);
        g.DrawString(dismissText, dismissFont, dismissBrush, Width - dismissSize.Width - 14, Height - dismissSize.Height - 10);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
