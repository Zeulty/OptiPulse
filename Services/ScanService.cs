using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace ChlorideTweaks.Services
{
    /// <summary>How a finding's detail line is worded.</summary>
    public enum ScanDetailKind
    {
        Files,
        Entries,
        Items
    }

    /// <summary>One scan area result. Raw and language-neutral; the UI
    /// formats the detail line so a language switch re-renders it.</summary>
    public sealed class ScanFinding
    {
        public string Id { get; init; } = "";
        public long Bytes { get; init; }
        public int Count { get; init; }
        public ScanDetailKind Kind { get; init; }

        /// <summary>True when something was found and a fix exists.</summary>
        public bool HasFix => Count > 0;
    }

    /// <summary>
    /// System scanner for the Scan page. Analyzes junk files and provides
    /// one-click cleaning. Fixes always re-analyze their area right before
    /// cleaning, so nothing is deleted based on stale scan data.
    ///
    /// Areas: temporary files, prefetch, Windows Update cache, thumbnail and
    /// icon caches, crash dumps and error reports, recycle bin, browser
    /// caches (Chrome/Edge/Firefox/Brave), leftover MUI Cache entries, RAS
    /// tracing keys, orphaned uninstallers, dead startup entries and broken
    /// shortcuts.
    /// </summary>
    public static class ScanService
    {
        // ---------------- public API ----------------

        /// <summary>Runs the full scan. Read-only. progress receives each
        /// area id before it is scanned (called from a worker thread).</summary>
        public static List<ScanFinding> Scan(Action<string>? progress = null)
        {
            var findings = new List<ScanFinding>();

            void Area(string id, Func<ScanFinding> scan)
            {
                progress?.Invoke(id);
                try { findings.Add(scan()); }
                catch { findings.Add(new ScanFinding { Id = id }); }
            }

            Area("temp", ScanTemp);
            Area("prefetch", ScanPrefetch);
            Area("wu-cache", ScanWuCache);
            Area("thumbcache", ScanThumbcache);
            Area("dumps", ScanDumps);
            Area("recycle", ScanRecycle);
            Area("browser", ScanBrowserCache);
            Area("muicache", () => new ScanFinding
            {
                Id = "muicache",
                Kind = ScanDetailKind.Entries,
                Count = AnalyzeMuiCache().Count
            });
            Area("tracing", () => new ScanFinding
            {
                Id = "tracing",
                Kind = ScanDetailKind.Entries,
                Count = AnalyzeTracing().Count
            });
            Area("uninstall", () => new ScanFinding
            {
                Id = "uninstall",
                Kind = ScanDetailKind.Entries,
                Count = AnalyzeUninstall().Count
            });
            Area("startup", () => new ScanFinding
            {
                Id = "startup",
                Kind = ScanDetailKind.Entries,
                Count = AnalyzeStartup().Count
            });
            Area("shortcuts", () => new ScanFinding
            {
                Id = "shortcuts",
                Kind = ScanDetailKind.Items,
                Count = FindBrokenShortcuts().Count
            });

            return findings;
        }

        /// <summary>
        /// Cleans one area (re-analyzing it first). Never throws: locked or
        /// in-use items are skipped like every other cleaner does.
        /// </summary>
        public static void Fix(string id)
        {
            try
            {
                switch (id)
                {
                    case "temp": FixTemp(); break;
                    case "prefetch": FixPrefetch(); break;
                    case "wu-cache": FixWuCache(); break;
                    case "thumbcache": FixThumbcache(); break;
                    case "dumps": FixDumps(); break;
                    case "recycle": FixRecycle(); break;
                    case "browser": FixBrowserCache(); break;
                    case "muicache": FixMuiCache(); break;
                    case "tracing": FixTracing(); break;
                    case "uninstall": FixUninstall(); break;
                    case "startup": FixStartup(); break;
                    case "shortcuts": FixShortcuts(); break;
                }
            }
            catch
            {
                // Cleaning is best-effort; locked files simply stay.
            }
        }

        // ---------------- file areas: scanning ----------------

        private static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        private static string LocalAppData =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string ProgramData =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        private static ScanFinding ScanTemp()
        {
            var (bytes, files) = MeasureDirectory(Path.GetTempPath());
            var (b2, f2) = MeasureDirectory(Path.Combine(WinDir, "Temp"));
            return new ScanFinding
            {
                Id = "temp", Kind = ScanDetailKind.Files, Bytes = bytes + b2, Count = files + f2
            };
        }

        private static ScanFinding ScanPrefetch()
        {
            var (bytes, files) = MeasureDirectory(
                Path.Combine(WinDir, "Prefetch"), "*.pf", SearchOption.TopDirectoryOnly);
            return new ScanFinding
            {
                Id = "prefetch", Kind = ScanDetailKind.Files, Bytes = bytes, Count = files
            };
        }

        private static ScanFinding ScanWuCache()
        {
            var (bytes, files) = MeasureDirectory(
                Path.Combine(WinDir, "SoftwareDistribution", "Download"));
            return new ScanFinding
            {
                Id = "wu-cache", Kind = ScanDetailKind.Files, Bytes = bytes, Count = files
            };
        }

        private static ScanFinding ScanThumbcache()
        {
            var (bytes, files) = MeasureDirectory(
                Path.Combine(LocalAppData, "Microsoft", "Windows", "Explorer"),
                "*cache*.db", SearchOption.TopDirectoryOnly);
            return new ScanFinding
            {
                Id = "thumbcache", Kind = ScanDetailKind.Files, Bytes = bytes, Count = files
            };
        }

        private static ScanFinding ScanDumps()
        {
            long bytes = 0;
            int files = 0;

            var (b, f) = MeasureDirectory(Path.Combine(WinDir, "Minidump"));
            bytes += b; files += f;
            (b, f) = MeasureDirectory(Path.Combine(LocalAppData, "CrashDumps"));
            bytes += b; files += f;
            (b, f) = MeasureDirectory(Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"));
            bytes += b; files += f;
            (b, f) = MeasureDirectory(Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportQueue"));
            bytes += b; files += f;

            string memoryDmp = Path.Combine(WinDir, "MEMORY.DMP");
            try
            {
                if (File.Exists(memoryDmp))
                {
                    bytes += new FileInfo(memoryDmp).Length;
                    files++;
                }
            }
            catch { }

            return new ScanFinding
            {
                Id = "dumps", Kind = ScanDetailKind.Files, Bytes = bytes, Count = files
            };
        }

        private static ScanFinding ScanRecycle()
        {
            long bytes = 0;
            long items = 0;
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                try
                {
                    var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
                    if (SHQueryRecycleBin(drive.Name, ref info) == 0)
                    {
                        bytes += (long)info.i64Size;
                        items += (long)info.i64NumItems;
                    }
                }
                catch { }
            }
            return new ScanFinding
            {
                Id = "recycle", Kind = ScanDetailKind.Items, Bytes = bytes, Count = (int)Math.Min(items, int.MaxValue)
            };
        }

        private static ScanFinding ScanBrowserCache()
        {
            long bytes = 0;
            int files = 0;

            // Chromium family: one cache set per profile folder.
            foreach (string root in new[]
                     {
                         Path.Combine(LocalAppData, "Google", "Chrome", "User Data"),
                         Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"),
                         Path.Combine(LocalAppData, "BraveSoftware", "Brave-Browser", "User Data")
                     })
            {
                foreach (string profile in SafeSubDirectories(root))
                {
                    foreach (string cacheName in new[] { "Cache", "Code Cache", "GPUCache" })
                    {
                        var (b, f) = MeasureDirectory(Path.Combine(profile, cacheName));
                        bytes += b; files += f;
                    }
                }
            }

            // Firefox: cache2 inside each profile.
            string firefoxProfiles = Path.Combine(LocalAppData, "Mozilla", "Firefox", "Profiles");
            foreach (string profile in SafeSubDirectories(firefoxProfiles))
            {
                var (b, f) = MeasureDirectory(Path.Combine(profile, "cache2"));
                bytes += b; files += f;
            }

            return new ScanFinding
            {
                Id = "browser", Kind = ScanDetailKind.Files, Bytes = bytes, Count = files
            };
        }

        // ---------------- file areas: fixing ----------------

        private static void FixTemp()
        {
            CleanDirectoryContents(Path.GetTempPath());
            CleanDirectoryContents(Path.Combine(WinDir, "Temp"));
        }

        private static void FixPrefetch()
        {
            CleanMatchingFiles(Path.Combine(WinDir, "Prefetch"), "*.pf");
        }

        private static void FixWuCache()
        {
            // The update services briefly lock parts of the cache; stopping
            // them lets the cleaner remove everything, then they restart.
            RunCmd("net stop wuauserv >nul 2>&1 & net stop bits >nul 2>&1");
            try
            {
                CleanDirectoryContents(Path.Combine(WinDir, "SoftwareDistribution", "Download"));
            }
            finally
            {
                RunCmd("net start bits >nul 2>&1 & net start wuauserv >nul 2>&1");
            }
        }

        private static void FixThumbcache()
        {
            CleanMatchingFiles(
                Path.Combine(LocalAppData, "Microsoft", "Windows", "Explorer"), "*cache*.db");
        }

        private static void FixDumps()
        {
            CleanDirectoryContents(Path.Combine(WinDir, "Minidump"));
            CleanDirectoryContents(Path.Combine(LocalAppData, "CrashDumps"));
            CleanDirectoryContents(Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"));
            CleanDirectoryContents(Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportQueue"));
            TryDeleteFile(Path.Combine(WinDir, "MEMORY.DMP"));
        }

        private static void FixRecycle()
        {
            const uint SHERB_NOCONFIRMATION = 0x1;
            const uint SHERB_NOPROGRESSUI = 0x2;
            const uint SHERB_NOSOUND = 0x4;
            try
            {
                // null root = every drive.
                SHEmptyRecycleBin(IntPtr.Zero, null!,
                    SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            }
            catch { }
        }

        private static void FixBrowserCache()
        {
            foreach (string root in new[]
                     {
                         Path.Combine(LocalAppData, "Google", "Chrome", "User Data"),
                         Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"),
                         Path.Combine(LocalAppData, "BraveSoftware", "Brave-Browser", "User Data")
                     })
            {
                foreach (string profile in SafeSubDirectories(root))
                {
                    foreach (string cacheName in new[] { "Cache", "Code Cache", "GPUCache" })
                        CleanDirectoryContents(Path.Combine(profile, cacheName));
                }
            }

            string firefoxProfiles = Path.Combine(LocalAppData, "Mozilla", "Firefox", "Profiles");
            foreach (string profile in SafeSubDirectories(firefoxProfiles))
                CleanDirectoryContents(Path.Combine(profile, "cache2"));
        }

        // ---------------- registry areas ----------------

        private const string MuiCachePath =
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        private const string TracingPath = @"Software\Microsoft\Tracing";
        private const string CurrentVersionRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string CurrentVersionRunOnce = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

        /// <summary>MUI Cache values whose program path no longer exists.</summary>
        private static List<string> AnalyzeMuiCache()
        {
            var dead = new List<string>();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(MuiCachePath);
                if (key == null) return dead;

                foreach (string name in key.GetValueNames())
                {
                    string path = StripMuiSuffix(name);
                    if (!IsPlausibleFullPath(path)) continue;
                    if (!File.Exists(path)) dead.Add(name);
                }
            }
            catch { }
            return dead;
        }

        private static void FixMuiCache()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(MuiCachePath, writable: true);
                if (key == null) return;
                foreach (string name in AnalyzeMuiCache())
                {
                    try { key.DeleteValue(name, throwOnMissingValue: false); } catch { }
                }
            }
            catch { }
        }

        /// <summary>RAS tracing keys (one per app that ever dialed).</summary>
        private static List<string> AnalyzeTracing()
        {
            var names = new List<string>();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(TracingPath);
                if (key == null) return names;
                names.AddRange(key.GetSubKeyNames());
            }
            catch { }
            return names;
        }

        private static void FixTracing()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(TracingPath, writable: true);
                if (key == null) return;
                foreach (string name in AnalyzeTracing())
                {
                    try { key.DeleteSubKeyTree(name, throwOnMissingSubKey: false); } catch { }
                }
            }
            catch { }
        }

        /// <summary>Add/Remove entries whose uninstaller executable is gone.</summary>
        private static List<(RegistryKey root, string path, string name)> AnalyzeUninstall()
        {
            var dead = new List<(RegistryKey, string, string)>();
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string path in roots)
            {
                foreach (RegistryKey hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    using var parent = hive.OpenSubKey(path);
                    if (parent == null) continue;

                    foreach (string name in parent.GetSubKeyNames())
                    {
                        using var entry = parent.OpenSubKey(name);
                        if (entry == null) continue;

                        string? uninstaller = entry.GetValue("UninstallString") as string
                                              ?? entry.GetValue("QuietUninstallString") as string;
                        if (string.IsNullOrWhiteSpace(uninstaller)) continue;

                        string? exe = ExtractExePath(uninstaller);
                        if (exe == null || !Path.IsPathRooted(exe)) continue;   // MSI/rundll: skip
                        if (File.Exists(exe)) continue;

                        dead.Add((hive, path, name));
                    }
                }
            }
            return dead;
        }

        private static void FixUninstall()
        {
            foreach (var (hive, path, name) in AnalyzeUninstall())
            {
                try
                {
                    using var parent = hive.OpenSubKey(path, writable: true);
                    parent?.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
                }
                catch { }
            }
        }

        /// <summary>Startup (Run/RunOnce) entries whose program is gone.</summary>
        private static List<(RegistryKey hive, string path, string value)> AnalyzeStartup()
        {
            var dead = new List<(RegistryKey, string, string)>();
            string[] paths =
            {
                CurrentVersionRun,
                CurrentVersionRunOnce,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
            };

            foreach (string path in paths)
            {
                foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                {
                    using var key = hive.OpenSubKey(path);
                    if (key == null) continue;

                    foreach (string value in key.GetValueNames())
                    {
                        string? command = key.GetValue(value) as string;
                        if (string.IsNullOrWhiteSpace(command)) continue;

                        string? exe = ExtractExePath(command);
                        if (exe == null || !Path.IsPathRooted(exe)) continue;
                        if (File.Exists(exe)) continue;

                        dead.Add((hive, path, value));
                    }
                }
            }
            return dead;
        }

        private static void FixStartup()
        {
            foreach (var (hive, path, value) in AnalyzeStartup())
            {
                try
                {
                    using var key = hive.OpenSubKey(path, writable: true);
                    key?.DeleteValue(value, throwOnMissingValue: false);
                }
                catch { }
            }
        }

        // ---------------- shortcuts ----------------

        /// <summary>.lnk files on the desktops and start menus whose target
        /// is gone. WScript.Shell needs an STA thread.</summary>
        private static List<string> FindBrokenShortcuts()
        {
            var broken = new List<string>();

            var thread = new Thread(() =>
            {
                try
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType == null) return;
                    dynamic shell = Activator.CreateInstance(shellType)!;

                    string[] folders =
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
                    };

                    foreach (string folder in folders)
                    {
                        if (folder.Length == 0) continue;
                        foreach (string link in SafeFiles(folder, "*.lnk"))
                        {
                            try
                            {
                                dynamic sc = shell.CreateShortcut(link);
                                string target = (string)sc.TargetPath;
                                if (target.Length > 0 && Path.IsPathRooted(target) && !File.Exists(target))
                                    broken.Add(link);
                            }
                            catch { /* unreadable shortcut: skip */ }
                        }
                    }
                }
                catch { /* WSH unavailable: no shortcut findings */ }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return broken;
        }

        private static void FixShortcuts()
        {
            foreach (string link in FindBrokenShortcuts())
                TryDeleteFile(link);
        }

        // ---------------- helpers ----------------

        internal static string StripMuiSuffix(string valueName)
        {
            foreach (string suffix in new[] { ".FriendlyAppName", ".ApplicationCompany" })
            {
                if (valueName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return valueName[..^suffix.Length];
            }
            return valueName;
        }

        /// <summary>Extracts the executable path from a command line
        /// ("C:\a b\app.exe" /arg  or  C:\a b\app.exe /arg  or  %env%\app.exe).</summary>
        internal static string? ExtractExePath(string command)
        {
            command = Environment.ExpandEnvironmentVariables(command.Trim());
            if (command.Length == 0) return null;

            if (command[0] == '"')
            {
                int end = command.IndexOf('"', 1);
                return end > 1 ? command[1..end] : command.Trim('"');
            }

            // Unquoted: the path may still contain spaces (C:\Program
            // Files\app.exe -arg), so cut at an ".exe" boundary instead of
            // the first space.
            int search = 0;
            while (true)
            {
                int exeIdx = command.IndexOf(".exe", search, StringComparison.OrdinalIgnoreCase);
                if (exeIdx < 0) break;
                int after = exeIdx + 4;
                if (after >= command.Length || command[after] == ' ' || command[after] == '"')
                    return command[..after];
                search = exeIdx + 1;
            }

            int space = command.IndexOf(' ');
            return space < 0 ? command : command[..space];
        }

        internal static bool IsPlausibleFullPath(string path)
        {
            if (path.Length < 3) return false;
            bool rooted = (char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
                          || path.StartsWith(@"\\", StringComparison.Ordinal);
            if (!rooted) return false;
            return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                   || Path.HasExtension(path);
        }

        private static IEnumerable<string> SafeSubDirectories(string root)
        {
            try
            {
                if (Directory.Exists(root)) return Directory.EnumerateDirectories(root);
            }
            catch { }
            return Array.Empty<string>();
        }

        private static IEnumerable<string> SafeFiles(string root, string pattern)
        {
            try
            {
                if (Directory.Exists(root))
                    return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
            }
            catch { }
            return Array.Empty<string>();
        }

        /// <summary>Recursively measures a directory; unreadable parts are
        /// skipped instead of aborting the count.</summary>
        private static (long bytes, int files) MeasureDirectory(
            string path, string pattern = "*", SearchOption option = SearchOption.AllDirectories)
        {
            long bytes = 0;
            int files = 0;

            if (option == SearchOption.TopDirectoryOnly)
            {
                foreach (string file in SafeFiles(path, pattern))
                {
                    long? size = TryFileSize(file);
                    if (size is { } s) { bytes += s; files++; }
                }
                return (bytes, files);
            }

            // Manual walk: an EnumerateFiles(*) call would abort on the first
            // access-denied subfolder.
            var queue = new Queue<string>();
            if (!Directory.Exists(path)) return (0, 0);
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                string dir = queue.Dequeue();
                string[] entries = Array.Empty<string>();
                try { entries = Directory.GetFileSystemEntries(dir); }
                catch { continue; }

                foreach (string entry in entries)
                {
                    try
                    {
                        if (Directory.Exists(entry))
                        {
                            queue.Enqueue(entry);
                        }
                        else
                        {
                            if (TryFileSize(entry) is { } size)
                            {
                                bytes += size;
                                files++;
                            }
                        }
                    }
                    catch { }
                }
            }
            return (bytes, files);
        }

        private static long? TryFileSize(string file)
        {
            try { return new FileInfo(file).Length; }
            catch { return null; }
        }

        private static void CleanDirectoryContents(string path)
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
                    catch { /* in use: keep */ }
                }
            }
            catch { }
        }

        private static void CleanMatchingFiles(string path, string pattern)
        {
            foreach (string file in SafeFiles(path, pattern))
                TryDeleteFile(file);
        }

        private static void TryDeleteFile(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch { /* in use: keep */ }
        }

        private static void RunCmd(string command)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + command,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(20_000);
            }
            catch { }
        }

        // ---------------- shell / recycle bin interop ----------------

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public ulong i64Size;
            public ulong i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
    }
}
