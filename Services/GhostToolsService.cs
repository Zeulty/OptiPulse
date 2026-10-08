using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Native replacement for the old GhostTools toolbox that used to be
    /// deployed to C:\GhostTools (vbs/ps1 scripts plus EmptyStandbyList.exe).
    /// The desktop context-menu entries now launch OptiPulse itself with
    /// hidden command-line switches (--reduce-memory / --cleanup-temp, handled
    /// in App.OnStartup), and the memory purge and temp cleanup run fully
    /// in-process without writing any files to disk.
    /// </summary>
    internal static class GhostToolsService
    {
        // ----- NtSetSystemInformation: standby / modified list purge -----

        private const int SystemMemoryListInformation = 80;
        private const int MemoryPurgeModificationList = 3;
        private const int MemoryPurgeStandbyList = 4;
        private const int MemoryPurgeLowPriorityStandbyList = 5;

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr process);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string? systemName, string privilegeName, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
            ref TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TokenPrivileges
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const int SePrivilegeEnabled = 0x00000002;

        /// <summary>
        /// Replaces the old ReduceMemory.vbs/.ps1 + EmptyStandbyList.exe combo:
        /// purges the modified, standby and priority-0 standby lists, then trims
        /// the working set of every running process.
        /// </summary>
        public static void ReduceMemory()
        {
            EnablePrivilege("SeProfileSingleProcessPrivilege");
            PurgeMemoryList(MemoryPurgeModificationList);
            PurgeMemoryList(MemoryPurgeStandbyList);
            PurgeMemoryList(MemoryPurgeLowPriorityStandbyList);
            TrimAllWorkingSets();
        }

        private static void PurgeMemoryList(int command)
        {
            try
            {
                int info = command;
                NtSetSystemInformation(SystemMemoryListInformation, ref info, sizeof(int));
            }
            catch
            {
                // Entry point missing on old builds; nothing to purge then.
            }
        }

        private static void TrimAllWorkingSets()
        {
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    using (process)
                    {
                        if (process.Id == Environment.ProcessId) continue;
                        EmptyWorkingSet(process.Handle);
                    }
                }
                catch
                {
                    // Protected or already-exited processes are skipped.
                }
            }
        }

        private static void EnablePrivilege(string name)
        {
            try
            {
                if (!LookupPrivilegeValue(null, name, out long luid)) return;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                        TokenAdjustPrivileges | TokenQuery, out IntPtr token)) return;
                try
                {
                    var tp = new TokenPrivileges
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SePrivilegeEnabled
                    };
                    AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    CloseHandle(token);
                }
            }
            catch
            {
                // Without the privilege the standby purge is a no-op, like the
                // old script running without elevation.
            }
        }

        // ----- Temporary file cleanup -----

        /// <summary>
        /// Replaces the old CleanupTemp.vbs/.ps1: clears the same user and
        /// system temp, error-report, dump, prefetch, icon-cache and
        /// recent-files locations.
        /// </summary>
        public static void CleanupTemp()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            ClearDirectoryContents(Path.GetTempPath());
            ClearDirectoryContents(Path.Combine(windows, "Temp"));
            ClearDirectoryContents(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Explorer"));
            ClearDirectoryContents(Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"));
            ClearDirectoryContents(Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"));
            ClearDirectoryContents(Path.Combine(windows, "Minidump"));
            ClearDirectoryContents(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Recent"));
            ClearDirectoryContents(Path.Combine(windows, "Prefetch"));
            TryDeleteFile(Path.Combine(windows, "MEMORY.DMP"));
        }

        private static void ClearDirectoryContents(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string entry in Directory.EnumerateFileSystemEntries(path))
                {
                    try
                    {
                        if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                        else File.Delete(entry);
                    }
                    catch
                    {
                        // Files in use are skipped, exactly like the old script.
                    }
                }
            }
            catch
            {
                // Location missing or unreadable; skip it.
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Locked dumps are skipped.
            }
        }
    }
}
