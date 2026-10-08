using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ChlorideTweaks.Services
{
    /// <summary>One live sample of the machine's resource usage.</summary>
    public sealed record ResourceSample(
        double? CpuPercent,
        double? DiskPercent,
        double MemUsedGb,
        double MemTotalGb,
        double MemPercent);

    /// <summary>
    /// Lightweight CPU / memory / disk sampler for the Home screen.
    /// CPU comes from GetSystemTimes, memory from GlobalMemoryStatusEx, and
    /// disk activity from kernel IOCTL_DISK_PERFORMANCE via DeviceIoControl -
    /// all instant, allocation-free Win32 P/Invokes that work on every Windows
    /// locale without WMI cold-start delays or hangs.
    /// </summary>
    public static class ResourceMonitorService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint IOCTL_DISK_PERFORMANCE = 0x00070020;

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemTimes(
            out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            byte[] lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private static readonly object _gate = new();

        // CPU delta baseline + smoothed value.
        private static bool _cpuPrimed;
        private static ulong _lastIdle, _lastKernel, _lastUser;
        private static double? _cpuSmoothed;

        // Disk delta baseline + smoothed value.
        private static bool _diskPrimed;
        private static long _lastDiskIdleTime;
        private static long _lastDiskQueryTime;
        private static double _diskSmoothed;

        private const double CpuSmoothing = 0.4;
        private const double DiskSmoothing = 0.35;

        /// <summary>Primes the CPU and Disk baselines. Instant; safe to call at startup.</summary>
        public static void Initialize()
        {
            lock (_gate)
            {
                try
                {
                    if (GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user))
                    {
                        _lastIdle = ToUInt64(idle);
                        _lastKernel = ToUInt64(kernel);
                        _lastUser = ToUInt64(user);
                        _cpuPrimed = true;
                    }
                }
                catch
                {
                }

                try
                {
                    if (TryReadDiskPerformance(out long idleTime, out long queryTime))
                    {
                        _lastDiskIdleTime = idleTime;
                        _lastDiskQueryTime = queryTime;
                        _diskPrimed = true;
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>Takes one sample. Intended to run on a background thread.</summary>
        public static ResourceSample Sample()
        {
            var mem = SampleMemory();
            return new ResourceSample(
                SampleCpuPercent(),
                SampleDiskPercent(),
                mem.usedGb, mem.totalGb, mem.percent);
        }

        private static double? SampleCpuPercent()
        {
            lock (_gate)
            {
                if (!_cpuPrimed) return 0;
                try
                {
                    if (!GetSystemTimes(out FileTime idleFt, out FileTime kernelFt, out FileTime userFt))
                        return _cpuSmoothed ?? 0;

                    ulong idle = ToUInt64(idleFt), kernel = ToUInt64(kernelFt), user = ToUInt64(userFt);
                    ulong totalDelta = (kernel - _lastKernel) + (user - _lastUser);
                    ulong idleDelta = idle - _lastIdle;
                    _lastIdle = idle;
                    _lastKernel = kernel;
                    _lastUser = user;

                    if (totalDelta == 0) return _cpuSmoothed ?? 0;
                    double percent = Math.Clamp(100.0 * (1.0 - (double)idleDelta / totalDelta), 0, 100);
                    _cpuSmoothed = _cpuSmoothed is { } prev
                        ? prev + (percent - prev) * CpuSmoothing
                        : percent;
                    return _cpuSmoothed;
                }
                catch
                {
                    return _cpuSmoothed ?? 0;
                }
            }
        }

        /// <summary>Gets a snapshot of physical memory usage.</summary>
        public static (double usedGb, double totalGb, double percent) GetMemorySnapshot() => SampleMemory();

        private static (double usedGb, double totalGb, double percent) SampleMemory()
        {
            try
            {
                var status = new MemoryStatusEx
                {
                    Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
                };
                if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
                    return (0, 0, 0);

                const double gib = 1073741824.0;
                return ((status.TotalPhys - status.AvailPhys) / gib,
                        status.TotalPhys / gib,
                        status.MemoryLoad);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        private static double? SampleDiskPercent()
        {
            lock (_gate)
            {
                try
                {
                    if (!TryReadDiskPerformance(out long idleTime, out long queryTime))
                        return _diskSmoothed;

                    if (!_diskPrimed)
                    {
                        _lastDiskIdleTime = idleTime;
                        _lastDiskQueryTime = queryTime;
                        _diskPrimed = true;
                        return _diskSmoothed;
                    }

                    long queryDelta = queryTime - _lastDiskQueryTime;
                    long idleDelta = idleTime - _lastDiskIdleTime;
                    _lastDiskIdleTime = idleTime;
                    _lastDiskQueryTime = queryTime;

                    if (queryDelta <= 0)
                        return _diskSmoothed;

                    double busyPct = Math.Clamp(100.0 * (1.0 - (double)idleDelta / queryDelta), 0.0, 100.0);
                    _diskSmoothed = _diskSmoothed + (busyPct - _diskSmoothed) * DiskSmoothing;
                    return _diskSmoothed;
                }
                catch
                {
                    return _diskSmoothed;
                }
            }
        }

        private static bool TryReadDiskPerformance(out long idleTime, out long queryTime)
        {
            idleTime = 0;
            queryTime = 0;

            string sysDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\").TrimEnd('\\');
            string[] candidates = { @"\\.\PhysicalDrive0", @"\\.\" + sysDrive };

            byte[] buffer = new byte[96];
            foreach (string devicePath in candidates)
            {
                using SafeFileHandle handle = CreateFile(
                    devicePath,
                    0, // dwDesiredAccess = 0 queries device metadata/perf without requiring exclusive access
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                    continue;

                if (DeviceIoControl(
                        handle,
                        IOCTL_DISK_PERFORMANCE,
                        IntPtr.Zero,
                        0,
                        buffer,
                        (uint)buffer.Length,
                        out uint bytesReturned,
                        IntPtr.Zero) && bytesReturned >= 64)
                {
                    // DISK_PERFORMANCE layout:
                    //   offset 32: LARGE_INTEGER IdleTime (100ns ticks)
                    //   offset 56: LARGE_INTEGER QueryTime (100ns ticks)
                    idleTime = BitConverter.ToInt64(buffer, 32);
                    queryTime = BitConverter.ToInt64(buffer, 56);
                    return true;
                }
            }

            return false;
        }

        private static ulong ToUInt64(FileTime ft) => ((ulong)ft.High << 32) | ft.Low;
    }
}
