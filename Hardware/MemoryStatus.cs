using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RamExpert2.Hardware;

/// <summary>Uso de memoria en un instante, según GlobalMemoryStatusEx.</summary>
public readonly record struct MemoryStatus(ulong TotalPhys, ulong AvailPhys, ulong CommitLimit, ulong CommitAvail)
{
    public ulong UsedPhys => TotalPhys - AvailPhys;
    public ulong CommitUsed => CommitLimit - CommitAvail;
    public double LoadPercent => TotalPhys == 0 ? 0 : 100.0 * UsedPhys / TotalPhys;

    public static MemoryStatus Read()
    {
        var status = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return new MemoryStatus(status.TotalPhys, status.AvailPhys, status.TotalPageFile, status.AvailPageFile);
    }

    /// <summary>Memoria física instalada según el firmware (incluye la reservada por hardware).</summary>
    public static ulong ReadInstalledBytes() =>
        NativeMethods.GetPhysicallyInstalledSystemMemory(out ulong kilobytes) ? kilobytes * 1024 : 0;
}
