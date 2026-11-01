using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AulaMonitor.Services;

/// <summary>
/// Трей-приложение после старта .NET-рантайма держит в памяти заметно больше, чем
/// реально нужно для его работы. Один раз через пару секунд после старта и один раз
/// после закрытия окна статуса отдаём неиспользуемые страницы обратно системе.
/// </summary>
public static class MemoryTrimmer
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr process);

    public static void Trim()
    {
        try
        {
            GC.Collect(0, GCCollectionMode.Optimized, blocking: false);
            using Process proc = Process.GetCurrentProcess();
            EmptyWorkingSet(proc.Handle);
        }
        catch
        {
            // Чисто косметическая оптимизация — если не получилось, ничего не ломаем.
        }
    }
}
