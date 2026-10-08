using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Raw, language-neutral snapshot of the user's hardware and security
    /// state. Everything the UI shows is formatted from this through
    /// FormatDetails(), so switching the app language instantly re-renders
    /// every detail string (TPM chip text, uptime, slot counts...) without
    /// querying WMI again.
    /// </summary>
    public sealed class PcInfo
    {
        public string CpuName { get; set; } = "";
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public double CpuBaseGhz { get; set; }

        public string GpuName { get; set; } = "";
        public ulong GpuVramBytes { get; set; }

        public ulong RamTotalBytes { get; set; }
        public int RamModules { get; set; }
        public int RamSlots { get; set; }
        public string RamType { get; set; } = "";     // "DDR4", "DDR5"...

        public string Motherboard { get; set; } = "";
        public string BoardMaker { get; set; } = "";

        public ulong StorageTotalBytes { get; set; }
        public ulong StorageFreeBytes { get; set; }
        public int StorageDrives { get; set; }

        public string DisplayResolution { get; set; } = "";   // "1920 × 1080"
        public int DisplayRefreshHz { get; set; }

        public bool Uefi { get; set; }
        public bool? SecureBoot { get; set; }          // null = unknown/unsupported
        public bool? Tpm { get; set; }                 // null = not detected
        public string TpmSpec { get; set; } = "";      // "2.0"
        public bool TpmActive { get; set; }
        public DateTime BootTimeUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>The localized, display-ready strings for the Home screen.</summary>
    public sealed class PcInfoView
    {
        public string CpuName { get; set; } = "";
        public string CpuDetail { get; set; } = "";
        public string GpuName { get; set; } = "";
        public string GpuDetail { get; set; } = "";
        public string RamTotal { get; set; } = "";
        public string RamDetail { get; set; } = "";
        public string Board { get; set; } = "";
        public string BoardDetail { get; set; } = "";
        public string StorageTotal { get; set; } = "";
        public string StorageDetail { get; set; } = "";
        public string Display { get; set; } = "";
        public string DisplayDetail { get; set; } = "";
        public string SecureBootDetail { get; set; } = "";
        public string TpmDetail { get; set; } = "";
        public string UptimeDetail { get; set; } = "";
    }

    /// <summary>
    /// Simple system info reader for the Home screen.
    /// </summary>
    public static class SystemInfoService
    {
        [DllImport("kernel32.dll")]
        private static extern bool GetFirmwareType(ref uint firmwareType);

        /// <summary>E.g.: "Windows 11 23H2" or "Windows 10 22H2"</summary>
        public static string GetWindowsVersionLabel()
        {
            try
            {
                int build = Environment.OSVersion.Version.Build;
                string? displayVersion = null;

                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    if (key != null)
                    {
                        displayVersion = key.GetValue("DisplayVersion") as string;
                        if (string.IsNullOrWhiteSpace(displayVersion))
                        {
                            displayVersion = key.GetValue("ReleaseId") as string;
                        }

                        if (int.TryParse(key.GetValue("CurrentBuild")?.ToString(), out int regBuild) && regBuild > 0)
                        {
                            build = Math.Max(build, regBuild);
                        }
                    }
                }
                catch { }

                string name = build >= 22000 ? "Windows 11" : "Windows 10";

                if (string.IsNullOrWhiteSpace(displayVersion))
                {
                    displayVersion = build switch
                    {
                        >= 26100 => "24H2",
                        >= 22631 => "23H2",
                        >= 22621 => "22H2",
                        >= 22000 => "21H2",
                        >= 19045 => "22H2",
                        >= 19044 => "21H2",
                        >= 19043 => "21H1",
                        >= 19042 => "20H2",
                        >= 19041 => "2004",
                        _ => null
                    };
                }

                return !string.IsNullOrWhiteSpace(displayVersion)
                    ? $"{name} {displayVersion}"
                    : name;
            }
            catch
            {
                return "Windows";
            }
        }

        /// <summary>The PC's registered logged-on user name (e.g. "Pigeon")</summary>
        public static string GetOwnerDisplayName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var raw = mo["UserName"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        // "COMPUTER\User" -> "User"
                        int idx = raw.IndexOf('\\');
                        return idx >= 0 ? raw[(idx + 1)..] : raw;
                    }
                }
            }
            catch
            {
                // If WMI is unavailable, silently fall back to the environment variable
            }
            return Environment.UserName;
        }

        public static PcInfo Collect()
        {
            var pc = new PcInfo();

            try
            {
                var (name, cores, threads, ghz) = GetCpu();
                pc.CpuName = name; pc.CpuCores = cores; pc.CpuThreads = threads; pc.CpuBaseGhz = ghz;
            }
            catch { }
            try
            {
                var (name, vram) = GetGpu();
                pc.GpuName = name; pc.GpuVramBytes = vram;
            }
            catch { }
            try
            {
                var (totalBytes, modules, slots, type) = GetRam();
                pc.RamTotalBytes = totalBytes; pc.RamModules = modules; pc.RamSlots = slots; pc.RamType = type;
            }
            catch { }
            try
            {
                var (board, maker) = GetMotherboard();
                pc.Motherboard = board; pc.BoardMaker = maker;
            }
            catch { }
            try
            {
                var (total, free, drives) = GetStorage();
                pc.StorageTotalBytes = total; pc.StorageFreeBytes = free; pc.StorageDrives = drives;
            }
            catch { }
            try
            {
                var (res, hz) = GetDisplay();
                pc.DisplayResolution = res; pc.DisplayRefreshHz = hz;
            }
            catch { }
            try { pc.Uefi = GetFirmware(); } catch { }
            try { pc.SecureBoot = GetSecureBoot(); } catch { }
            try
            {
                var (state, spec, active) = GetTpm();
                pc.Tpm = state; pc.TpmSpec = spec; pc.TpmActive = active;
            }
            catch { }

            pc.BootTimeUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
            return pc;
        }

        /// <summary>Formats the raw snapshot into display strings in the
        /// CURRENT app language. Re-run after a language switch.</summary>
        public static PcInfoView FormatDetails(PcInfo pc)
        {
            string L(string key) => LocalizationService.L(key);
            string unknown = L("SysUnknown");
            var view = new PcInfoView();

            view.CpuName = pc.CpuName.Length > 0 ? pc.CpuName : unknown;
            var cpuParts = new List<string>();
            if (pc.CpuCores > 0 && pc.CpuThreads > 0)
                cpuParts.Add(string.Format(L("SysCoresPattern"), pc.CpuCores, pc.CpuThreads));
            if (pc.CpuBaseGhz > 0)
                cpuParts.Add(string.Format(L("SysGhzBasePattern"), pc.CpuBaseGhz.ToString("0.0#")));
            view.CpuDetail = string.Join(" · ", cpuParts);

            view.GpuName = pc.GpuName.Length > 0 ? pc.GpuName : unknown;
            view.GpuDetail = pc.GpuVramBytes > 0 ? $"{FormatBytes(pc.GpuVramBytes)} VRAM" : "";

            view.RamTotal = pc.RamTotalBytes > 0 ? FormatBytes(pc.RamTotalBytes) : unknown;
            var ramParts = new List<string>();
            if (pc.RamModules > 0)
            {
                if (pc.RamSlots > 0)
                    ramParts.Add(string.Format(L("SysSlotsPattern"), pc.RamModules, pc.RamSlots));
                else
                    ramParts.Add(string.Format(L(pc.RamModules == 1 ? "SysModuleOne" : "SysModulesPattern"), pc.RamModules));
            }
            if (pc.RamType.Length > 0) ramParts.Add(pc.RamType);
            view.RamDetail = string.Join(" · ", ramParts);

            view.Board = pc.Motherboard.Length > 0 ? pc.Motherboard : unknown;
            view.BoardDetail = pc.BoardMaker.Length > 0 && pc.BoardMaker != view.Board ? pc.BoardMaker : "";

            view.StorageTotal = pc.StorageTotalBytes > 0
                ? string.Format(L("SysTotalPattern"), FormatBytes(pc.StorageTotalBytes))
                : unknown;
            if (pc.StorageDrives > 0)
            {
                var storageParts = new List<string> { string.Format(L("SysFreePattern"), FormatBytes(pc.StorageFreeBytes)) };
                storageParts.Add(string.Format(L(pc.StorageDrives == 1 ? "SysDriveOne" : "SysDrivesPattern"), pc.StorageDrives));
                view.StorageDetail = string.Join(" · ", storageParts);
            }

            view.Display = pc.DisplayResolution.Length > 0 ? pc.DisplayResolution : unknown;
            view.DisplayDetail = pc.DisplayRefreshHz > 0
                ? string.Format(L("SysHzPattern"), pc.DisplayRefreshHz)
                : "";

            view.SecureBootDetail = pc.Uefi ? L("SysUefiFirmware") : L("SysLegacyFirmware");

            view.TpmDetail = pc.Tpm switch
            {
                null => L("SysNoTpm"),
                _ => (pc.TpmSpec.Length > 0
                        ? string.Format(L("SysTpmChipPattern"), pc.TpmSpec)
                        : L("SysTpmChip"))
                     + (pc.TpmActive ? "" : L("SysTpmInactiveSuffix"))
            };

            DateTime bootLocal = DateTime.Now - (DateTime.UtcNow - pc.BootTimeUtc);
            // Month names follow the app language (tr-TR -> "Eyl", not the OS
            // culture's "Sep"); the date pattern is localized too so Chinese
            // can put the month first.
            view.UptimeDetail = bootLocal.Date == DateTime.Today
                ? string.Format(L("SysUptimeSincePattern"), bootLocal.ToString("HH:mm"))
                : string.Format(L("SysUptimeSincePattern"),
                    bootLocal.ToString(L("SysUptimeDatePattern"), LocalizationService.CurrentCulture));

            return view;
        }

        /// <summary>Localized uptime: e.g. "1g 2sa 3dk" in Turkish.</summary>
        public static string FormatUptime(TimeSpan t) =>
            t.TotalDays >= 1
                ? string.Format(LocalizationService.L("SysUptimeDHM"), (int)t.TotalDays, t.Hours, t.Minutes)
            : t.TotalHours >= 1
                ? string.Format(LocalizationService.L("SysUptimeHM"), (int)t.TotalHours, t.Minutes)
                : string.Format(LocalizationService.L("SysUptimeM"), (int)t.TotalMinutes);

        private static (string name, int cores, int threads, double ghz) GetCpu()
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject mo in searcher.Get())
            {
                string name = CleanName(mo["Name"]?.ToString() ?? "");
                if (name.Length == 0) continue;
                return (name,
                    ToInt(mo["NumberOfCores"]) ?? 0,
                    ToInt(mo["NumberOfLogicalProcessors"]) ?? 0,
                    (ToInt(mo["MaxClockSpeed"]) ?? 0) / 1000.0);
            }
            return ("", 0, 0, 0);
        }

        private static (string name, ulong vram) GetGpu()
        {
            // Registry first: each adapter subkey pairs DriverDesc with the true 64-bit
            // VRAM size, so the name and capacity always belong to the same card.
            // (WMI's AdapterRAM wraps at 4 GB, and shared-memory iGPUs inflate totals.)
            var adapters = GetRegistryAdapters();
            if (adapters.Count > 0)
            {
                var best = adapters.OrderByDescending(a => GpuRank(a.name))
                                    .ThenByDescending(a => a.vram)
                                    .First();
                if (best.vram > 0) return best;

                // Registry knew the card but not its size -> try WMI for that name
                var (wmiName, wmiRam) = GetGpuFromWmi();
                if (wmiName == best.name && wmiRam > 0) return (best.name, wmiRam);
                return best;
            }

            return GetGpuFromWmi();
        }

        private static (string name, ulong vram) GetGpuFromWmi()
        {
            string? bestName = null;
            ulong bestRam = 0;
            int bestRank = -1;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject mo in searcher.Get())
            {
                string name = CleanName(mo["Name"]?.ToString() ?? "");
                if (name.Length == 0) continue;

                int rank = GpuRank(name);
                ulong ram = ToULong(mo["AdapterRAM"]);
                if (rank > bestRank || (rank == bestRank && ram > bestRam))
                {
                    bestName = name;
                    bestRam = ram;
                    bestRank = rank;
                }
            }
            return (bestName ?? "", bestRam);
        }

        // Enumerates display adapter subkeys, pairing each adapter name with its
        // own VRAM size. Subkeys are "0000", "0001", ...; the ACL-protected
        // "Properties" key is skipped by the 4-digit filter.
        private static List<(string name, ulong vram)> GetRegistryAdapters()
        {
            var list = new List<(string name, ulong vram)>();
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (key == null) return list;

                foreach (string sub in key.GetSubKeyNames())
                {
                    if (sub.Length != 4 || !uint.TryParse(sub, out _)) continue;

                    using var k = key.OpenSubKey(sub);
                    if (k?.GetValue("DriverDesc") is not string desc || desc.Trim().Length == 0)
                        continue;

                    string name = CleanName(desc);
                    ulong vram = GetVramFromKey(k);

                    // The same GPU can appear in several subkeys; keep the largest size
                    int idx = list.FindIndex(a => a.name == name);
                    if (idx < 0) list.Add((name, vram));
                    else if (vram > list[idx].vram) list[idx] = (name, vram);
                }
            }
            catch { }
            return list;
        }

        private static ulong GetVramFromKey(RegistryKey k)
        {
            // qwMemorySize is the true 64-bit size; older drivers only expose the
            // 32-bit MemorySize. Values may be REG_QWORD, REG_DWORD, or REG_BINARY.
            foreach (string valueName in new[]
                     {
                         "HardwareInformation.qwMemorySize",
                         "HardwareInformation.MemorySize"
                     })
            {
                var v = k.GetValue(valueName);
                if (v == null) continue;

                if (v is byte[] bytes)
                {
                    if (bytes.Length >= 8) return BitConverter.ToUInt64(bytes, 0);
                    if (bytes.Length >= 4) return BitConverter.ToUInt32(bytes, 0);
                }
                if (v is long l) return (ulong)l;
                if (v is int i) return (ulong)i;
            }
            return 0;
        }

        // 2 = dedicated GPU, 1 = unknown/other, 0 = integrated graphics.
        // Names are pre-cleaned of "(R)"/"(TM)" noise by CleanName.
        private static int GpuRank(string name)
        {
            if (name.Contains("GeForce", StringComparison.OrdinalIgnoreCase)
                || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
                || name.Contains("GTX", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Quadro", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Radeon Pro", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Arc A", StringComparison.OrdinalIgnoreCase))
                return 2;

            if (name.Contains("UHD", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Iris", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Radeon Graphics", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Arc Graphics", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Vega", StringComparison.OrdinalIgnoreCase))
                return 0;

            return 1;
        }

        private static (ulong totalBytes, int modules, int slots, string type) GetRam()
        {
            ulong totalBytes = 0;
            int modules = 0;
            int bestType = 0;

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, SMBIOSMemoryType FROM Win32_PhysicalMemory"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    totalBytes += ToULong(mo["Capacity"]);
                    modules++;
                    bestType = Math.Max(bestType, ToInt(mo["SMBIOSMemoryType"]) ?? 0);
                }
            }

            if (totalBytes == 0) return (0, 0, 0, "");

            int maxSlots = 0;
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                foreach (ManagementObject mo in searcher.Get())
                    maxSlots = Math.Max(maxSlots, ToInt(mo["MemoryDevices"]) ?? 0);
            }
            catch { }

            string type = bestType switch { 24 => "DDR3", 26 => "DDR4", 34 => "DDR5", _ => "" };
            return (totalBytes, modules, maxSlots, type);
        }

        private static (string board, string maker) GetMotherboard()
        {
            static string Clean(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                string t = s.Trim();
                string lower = t.ToLowerInvariant();
                if (lower.Contains("to be filled") || lower.Contains("default string") || lower.Contains("unknown") || lower == "none" || lower == "null")
                    return "";
                return t;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string maker = Clean(mo["Manufacturer"]?.ToString());
                    string product = Clean(mo["Product"]?.ToString());

                    string board = product.Length > 0 ? product : maker;
                    if (board.Length == 0) continue;
                    return (board, maker.Length > 0 && !board.Equals(maker, StringComparison.OrdinalIgnoreCase) ? maker : "");
                }
            }
            catch { }

            // Fallback: If BaseBoard didn't provide a board/model name, query ComputerSystem (useful for OEMs, laptops, etc.)
            try
            {
                using var sysSearcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                foreach (ManagementObject mo in sysSearcher.Get())
                {
                    string maker = Clean(mo["Manufacturer"]?.ToString());
                    string model = Clean(mo["Model"]?.ToString());

                    string board = model.Length > 0 ? model : maker;
                    if (board.Length == 0) continue;
                    return (board, maker.Length > 0 && !board.Equals(maker, StringComparison.OrdinalIgnoreCase) ? maker : "");
                }
            }
            catch { }

            return ("", "");
        }

        private static (ulong total, ulong free, int drives) GetStorage()
        {
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.DriveType == System.IO.DriveType.Fixed && d.IsReady)
                .ToArray();
            if (drives.Length == 0) return (0, 0, 0);

            ulong total = 0, free = 0;
            foreach (var d in drives)
            {
                total += (ulong)d.TotalSize;
                free += (ulong)d.AvailableFreeSpace;
            }
            return (total, free, drives.Length);
        }

        private static (string resolution, int hz) GetDisplay()
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController");
            foreach (ManagementObject mo in searcher.Get())
            {
                int w = ToInt(mo["CurrentHorizontalResolution"]) ?? 0;
                int h = ToInt(mo["CurrentVerticalResolution"]) ?? 0;
                int hz = ToInt(mo["CurrentRefreshRate"]) ?? 0;
                if (w > 0 && h > 0)
                    return ($"{w} × {h}", hz);
            }
            return ("", 0);
        }

        // 1 = legacy BIOS, 2 = UEFI
        private static bool GetFirmware()
        {
            try
            {
                uint type = 0;
                if (GetFirmwareType(ref type))
                    return type == 2;
            }
            catch { }
            return false;
        }

        private static bool? GetSecureBoot()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                if (key?.GetValue("UEFISecureBootEnabled") is int i) return i == 1;
            }
            catch { }
            return null;   // Key missing -> legacy BIOS or unsupported
        }

        private static (bool? state, string spec, bool active) GetTpm()
        {
            try
            {
                var scope = new ManagementScope(@"root\cimv2\security\microsofttpm");
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_Tpm"));
                foreach (ManagementObject mo in searcher.Get())
                {
                    bool enabled = ToBool(mo["IsEnabled_InitialValue"]) ?? false;
                    bool activated = ToBool(mo["IsActivated_InitialValue"]) ?? false;
                    string spec = (mo["SpecVersion"]?.ToString() ?? "").Split(',')[0].Trim();
                    return (enabled && activated, spec, enabled && activated);
                }
            }
            catch { }
            return (null, "", false);
        }

        // Strips "(R)"/"(TM)" noise and collapses duplicate spaces in WMI names
        private static string CleanName(string s) =>
            string.Join(" ", s.Replace("(R)", "").Replace("(TM)", "").Replace("(C)", "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

        // Binary units (GiB/TiB), matching how Windows reports sizes
        private static string FormatBytes(ulong bytes)
        {
            double gb = bytes / 1024.0 / 1024 / 1024;
            if (gb >= 1024) return $"{gb / 1024.0:0.#} TB";
            if (gb >= 10) return $"{gb:0} GB";
            return $"{gb:0.#} GB";
        }

        private static int? ToInt(object? v) =>
            v != null && int.TryParse(v.ToString(), out int i) ? i : null;

        private static ulong ToULong(object? v) =>
            v != null && ulong.TryParse(v.ToString(), out ulong u) ? u : 0;

        private static bool? ToBool(object? v) =>
            v != null && bool.TryParse(v.ToString(), out bool b) ? b : null;
    }
}
