using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AulaMonitor.Hid;

[StructLayout(LayoutKind.Sequential)]
internal struct DevicePropertyKey
{
    public Guid Format;
    public uint Id;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DeviceInfoData
{
    public int Size;
    public Guid ClassGuid;
    public int DeviceInstance;
    public IntPtr Reserved;
}

/// <summary>
/// Тонкая обёртка над Win32 API (setupapi/hid/kernel32), нужная для перечисления
/// HID-путей и прямого чтения feature-репортов. Всё содержимое этого класса —
/// задокументированные системные вызовы Windows, никакой логики Aula тут нет.
/// </summary>
internal static class Win32Hid
{
    public static readonly DevicePropertyKey BatteryLevelKey = new()
    {
        Format = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
        Id = 2
    };

    public static readonly DevicePropertyKey FriendlyNameKey = new()
    {
        Format = new Guid("b725f130-47ef-101a-a5f1-02608c9eebac"),
        Id = 10
    };

    [DllImport("hid.dll")]
    public static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("cfgmgr32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_List_Size(out uint length, ref Guid interfaceClassGuid, string? deviceId, uint flags);

    [DllImport("cfgmgr32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_List(ref Guid interfaceClassGuid, string? deviceId, char[] buffer, uint bufferLength, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref DeviceInfoData deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDevicePropertyW(IntPtr deviceInfoSet, ref DeviceInfoData deviceInfoData, ref DevicePropertyKey propertyKey, out uint propertyType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize, uint flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDeviceInstanceIdW(IntPtr deviceInfoSet, ref DeviceInfoData deviceInfoData, StringBuilder? instanceId, int instanceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(SafeFileHandle file, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(SafeFileHandle file, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetFeature(SafeFileHandle device, byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_SetFeature(SafeFileHandle device, byte[] reportBuffer, int reportBufferLength);

    /// <summary>Возвращает пути всех HID-интерфейсов, видимых системе прямо сейчас.</summary>
    public static string[] EnumerateHidPaths()
    {
        HidD_GetHidGuid(out Guid hidGuid);

        if (CM_Get_Device_Interface_List_Size(out uint length, ref hidGuid, null, 0) != 0 || length == 0)
            return Array.Empty<string>();

        char[] buffer = new char[length];
        if (CM_Get_Device_Interface_List(ref hidGuid, null, buffer, length, 0) != 0)
            return Array.Empty<string>();

        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
