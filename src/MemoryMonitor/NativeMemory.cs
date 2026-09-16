using System.Runtime.InteropServices;

namespace MemoryMonitor;

/// <summary>
/// Thin wrapper around GlobalMemoryStatusEx: reads true system-wide RAM
/// usage directly from the OS with a single syscall, no polling library
/// or WMI overhead.
/// </summary>
internal static class NativeMemory
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public readonly record struct Snapshot(double UsedMB, double TotalMB, uint PercentUsed);

    public static Snapshot GetSnapshot()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new InvalidOperationException("GlobalMemoryStatusEx failed.");
        }

        double totalMB = status.ullTotalPhys / 1024.0 / 1024.0;
        double usedMB = (status.ullTotalPhys - status.ullAvailPhys) / 1024.0 / 1024.0;
        return new Snapshot(usedMB, totalMB, status.dwMemoryLoad);
    }
}
