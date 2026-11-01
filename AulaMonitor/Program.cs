using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using AulaMonitor.UI;

namespace AulaMonitor;

internal static class Program
{
    private const string MutexName = "AulaMonitor.SingleInstance";

    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject?.ToString());
        Application.ThreadException += (_, e) => LogCrash(e.Exception.ToString());

        // Не даём запустить вторую копию: второй NotifyIcon только продублирует иконку
        // в трее и будет слать дублирующиеся уведомления о низком заряде.
        using var singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Aula Monitor уже запущен — смотри иконку в трее.",
                "Aula Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new AppShell());
        }
        catch (Exception ex)
        {
            LogCrash(ex.ToString());
        }

        GC.KeepAlive(singleInstance);
    }

    private static void LogCrash(string? error)
    {
        if (string.IsNullOrEmpty(error)) return;
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), error);
        }
        catch
        {
            // Если и запись лога не удалась — тут уже ничего не поделать.
        }
    }
}
