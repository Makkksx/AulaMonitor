using System;
using Microsoft.Win32;

namespace AulaMonitor.Services;

/// <summary>Управляет записью автозапуска в HKCU\...\Run.</summary>
public static class StartupRegistrar
{
    private const string AppName = "AulaMonitor";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enabled)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Нет прав на реестр — молча не включаем автозапуск, не роняем приложение.
        }
    }
}
