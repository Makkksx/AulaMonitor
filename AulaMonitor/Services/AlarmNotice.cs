using Microsoft.Toolkit.Uwp.Notifications;

namespace AulaMonitor.Services;

/// <summary>
/// Системное toast-уведомление о низком заряде — запись в Центре уведомлений.
/// Режим "Не беспокоить" / Фокусировка внимания может его подавлять, поэтому это
/// дополнительный канал: основное предупреждение показывает LowBatteryAlert (обычное
/// окно поверх экрана), на которое Фокусировка не действует.
/// </summary>
public static class AlarmNotice
{
    public static void Show(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .SetToastScenario(ToastScenario.Alarm)
                .Show();
        }
        catch
        {
            // Если toast-подсистема недоступна (нетипичная сборка Windows, сломанный
            // AUMID и т.п.) — не должны ронять фоновый опрос клавиатуры из-за этого.
        }
    }
}
