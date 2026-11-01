using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AulaMonitor.Services;
using AulaMonitor;

namespace AulaMonitor.UI;

/// <summary>
/// Корень приложения: иконка в трее, её меню, окно статуса и правила показа
/// аварийного уведомления о низком заряде. Everything здесь работает через события
/// BatteryWatcher — сам AppShell не знает, как клавиатура опрашивается, только
/// что делать с готовым снимком.
/// </summary>
public sealed class AppShell : ApplicationContext
{
    private const int LowBatteryThreshold = 20;
    private const int LowBatteryResetMargin = 5; // гистерезис против дребезга на границе

    private readonly SynchronizationContext _ui;
    private readonly TrayGlyphFactory _glyphs = new();
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _openCardItem;
    private readonly ToolStripMenuItem _statusLineItem;
    private readonly ToolStripMenuItem _pollIntervalMenu;
    private readonly ToolStripMenuItem _autoStartItem;

    private BatteryWatcher _watcher;
    private DetailCard? _openCard;
    private bool _lowBatteryAlarmed;

    public AppShell()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _watcher = new BatteryWatcher(PollingPreferences.Get());
        _watcher.SnapshotReady += snapshot => _ui.Post(_ => Render(snapshot), null);

        var menu = new ContextMenuStrip();

        _openCardItem = new ToolStripMenuItem("Статус клавиатуры...", null, (_, _) => ToggleCard())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        menu.Items.Add(_openCardItem);

        _statusLineItem = new ToolStripMenuItem("Определение...") { Enabled = false };
        menu.Items.Add(_statusLineItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Обновить статус", null, (_, _) => _watcher.PollNow()));

        _pollIntervalMenu = new ToolStripMenuItem("Частота опроса");
        RebuildPollIntervalMenu();
        menu.Items.Add(_pollIntervalMenu);

        bool autoStart = StartupRegistrar.IsEnabled();
        _autoStartItem = new ToolStripMenuItem("Запускать с Windows", null, ToggleAutoStart) { Checked = autoStart };
        menu.Items.Add(_autoStartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Выход", null, (_, _) => Shutdown()));

        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true,
            Text = "Aula Monitor"
        };
        _trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleCard(); };
        _trayIcon.DoubleClick += (_, _) => ToggleCard();

        _watcher.Start();

        // После прогрева JIT отдаём системе память, которая для фонового трей-процесса не нужна.
        Task.Delay(2500).ContinueWith(_ => MemoryTrimmer.Trim());
    }

    private void ToggleCard()
    {
        if (_openCard is { IsDisposed: false })
        {
            _openCard.Close();
            _openCard = null;
            return;
        }

        _openCard = new DetailCard(_watcher.Latest);
        _openCard.FormClosed += (_, _) => _openCard = null;
        _openCard.Show();
    }

    private void RebuildPollIntervalMenu()
    {
        _pollIntervalMenu.DropDownItems.Clear();
        int current = PollingPreferences.Get();

        foreach (var (ms, label) in PollingPreferences.Choices)
        {
            var item = new ToolStripMenuItem(label) { Checked = ms == current };
            item.Click += (_, _) => ApplyPollInterval(ms);
            _pollIntervalMenu.DropDownItems.Add(item);
        }
    }

    /// <summary>
    /// Пересоздаёт фоновый опрос с новым интервалом. Слишком короткий интервал (короче
    /// собственного таймера сна клавиатуры) не даёт ей уснуть — каждый опрос это HID-трафик,
    /// который клавиатура считает активностью и сбрасывает свой таймер.
    /// </summary>
    private void ApplyPollInterval(int ms)
    {
        PollingPreferences.Set(ms);

        _watcher.Dispose();
        _watcher = new BatteryWatcher(ms);
        _watcher.SnapshotReady += snapshot => _ui.Post(_ => Render(snapshot), null);
        _watcher.Start();

        RebuildPollIntervalMenu();
    }

    private void Render(BatterySnapshot snapshot)
    {
        _openCard?.UpdateSnapshot(snapshot);

        Icon icon = _glyphs.ForSnapshot(snapshot);
        if (_trayIcon.Icon != icon) _trayIcon.Icon = icon;

        string name = string.IsNullOrWhiteSpace(snapshot.KeyboardName) ? "клавиатуры" : snapshot.KeyboardName;
        _openCardItem.Text = $"Статус {name}...";

        if (snapshot.Link == LinkState.Offline)
        {
            string offlineText = $"{name}: нет связи";
            _statusLineItem.Text = offlineText;
            _trayIcon.Text = Truncate(offlineText);
            return; // заряд в offline-снимке не отражает реальный уровень — проверку ниже не вызываем
        }

        string line = snapshot.Link == LinkState.Idle
            ? $"{name}: {snapshot.Percent}% (сон)"
            : $"{name}: {snapshot.Percent}% ({ChannelLabel(snapshot.Channel)}, {(snapshot.Charge == ChargeState.Charging ? "заряжается" : "от батареи")})";

        _statusLineItem.Text = line;
        _trayIcon.Text = Truncate(line);

        // Проверка уровня заряда выполняется и для Active, и для Idle — клавиатура
        // подавляющую часть времени проводит во сне (это и есть цель работающего таймера
        // сна), и именно там в последний раз читается заряд перед тем, как она сядет.
        CheckLowBattery(snapshot, name);
    }

    private static string ChannelLabel(LinkChannel channel) => channel switch
    {
        LinkChannel.Usb => "провод",
        LinkChannel.Wireless24G => "2.4G",
        LinkChannel.Bluetooth => "Bluetooth",
        _ => ""
    };

    private void CheckLowBattery(BatterySnapshot snapshot, string name)
    {
        bool onBattery = snapshot.Charge == ChargeState.OnBattery;

        if (snapshot.Percent <= LowBatteryThreshold && onBattery)
        {
            if (!_lowBatteryAlarmed)
            {
                // Toast — best-effort запись в Центр уведомлений, может быть подавлен
                // Фокусировкой внимания. LowBatteryAlert — обычное окно поверх экрана,
                // Фокусировка на него не действует в принципе, это основной канал.
                AlarmNotice.Show($"Низкий заряд: {name}", $"Заряд {snapshot.Percent}%. Подключите клавиатуру для зарядки.");
                LowBatteryAlert.ShowFor(name, snapshot.Percent);
                _lowBatteryAlarmed = true;
            }
        }
        else if (snapshot.Percent > LowBatteryThreshold + LowBatteryResetMargin || !onBattery)
        {
            _lowBatteryAlarmed = false;
        }
    }

    // NotifyIcon.Text ограничен 127 символами в .NET 6+ (было 63 в .NET Framework —
    // устаревший лимит оттуда исторически и обрезал более длинный русский текст).
    private const int TooltipLimit = 127;

    private static string Truncate(string text) =>
        text.Length > TooltipLimit ? text[..(TooltipLimit - 3)] + "..." : text;

    private void ToggleAutoStart(object? sender, EventArgs e)
    {
        bool next = !_autoStartItem.Checked;
        StartupRegistrar.SetEnabled(next);
        _autoStartItem.Checked = next;
    }

    private void Shutdown()
    {
        _watcher.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _openCard?.Close();
        _glyphs.Dispose();
        ExitThread();
    }
}
