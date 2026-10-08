using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ChlorideTweaks.Services
{
    /// <summary>One startup entry: a Run-key value, a startup-folder file or
    /// a packaged (UWP) app's startup task, with its enabled state.</summary>
    public sealed class StartupEntry
    {
        /// <summary>Origin: "hkcu", "hklm", "wow", "folder-user",
        /// "folder-common" or "uwp".</summary>
        public string Location { get; set; } = "";
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public bool Enabled { get; set; }

        /// <summary>UWP only: the package family (owner of the task).</summary>
        public string? PackageFamily { get; set; }

        /// <summary>UWP only: the startup task name inside the package.</summary>
        public string? TaskName { get; set; }
    }

    /// <summary>
    /// Startup app manager for the "Startup Apps" page. Reads the same
    /// sources Task Manager shows:
    ///  - HKCU / HKLM Run keys (plus the WOW6432Node view),
    ///  - the per-user and all-users startup folders,
    ///  - packaged (UWP) app startup tasks registered under AppModel
    ///    SystemAppData (e.g. Claude, Discord, Telegram - apps Task Manager
    ///    lists that never appear in the Run keys).
    ///
    /// The enabled/disabled state lives in the Explorer StartupApproved
    /// binary values (first byte 02/06 = enabled, 03 = disabled), exactly
    /// like Task Manager toggles it - so disabling an entry here shows it
    /// disabled in Task Manager too, and vice versa. Nothing is ever
    /// Toggling flips the approval state without deleting any entries.
    /// </summary>
    public static class StartupService
    {
        private sealed class RegistrySource
        {
            public RegistryKey Hive = null!;
            public string RunPath = "";
            public string ApprovedPath = "";
            public string Origin = "";
        }

        private static readonly RegistrySource[] Sources =
        {
            new()
            {
                Hive = Registry.CurrentUser,
                RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
                ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                Origin = "hkcu"
            },
            new()
            {
                Hive = Registry.LocalMachine,
                RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
                ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                Origin = "hklm"
            },
            new()
            {
                Hive = Registry.LocalMachine,
                RunPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
                ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                Origin = "wow"
            }
        };

        private sealed class FolderSource
        {
            public string Folder = "";
            public RegistryKey Hive = null!;
            public string ApprovedPath = "";
            public string Origin = "";
        }

        private static FolderSource[] FolderSources() => new[]
        {
            new FolderSource
            {
                Folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Hive = Registry.CurrentUser,
                ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                Origin = "folder-user"
            },
            new FolderSource
            {
                Folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                Hive = Registry.LocalMachine,
                ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                Origin = "folder-common"
            }
        };

        private const string SystemAppDataPath =
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

        /// <summary>All startup entries (enabled and disabled), sorted by name.
        /// Read-only and safe.</summary>
        public static List<StartupEntry> GetEntries()
        {
            var list = new List<StartupEntry>();

            // Registry Run values, state from StartupApproved.
            foreach (RegistrySource source in Sources)
            {
                using var run = source.Hive.OpenSubKey(source.RunPath);
                if (run == null) continue;
                foreach (string valueName in run.GetValueNames())
                {
                    list.Add(new StartupEntry
                    {
                        Location = source.Origin,
                        Name = valueName,
                        Command = run.GetValue(valueName)?.ToString() ?? "",
                        Enabled = IsApprovedEnabled(source.Hive, source.ApprovedPath, valueName)
                    });
                }
            }

            // Startup folder files, state from StartupApproved.
            foreach (FolderSource source in FolderSources())
            {
                foreach (string file in SafeFiles(source.Folder))
                {
                    // desktop.ini is folder settings, not a startup app.
                    if (string.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string fileName = Path.GetFileName(file);
                    list.Add(new StartupEntry
                    {
                        Location = source.Origin,
                        Name = fileName,
                        Command = file,
                        Enabled = IsApprovedEnabled(source.Hive, source.ApprovedPath, fileName)
                    });
                }
            }

            // Packaged (UWP) app startup tasks - the Claude/Discord/Telegram
            // style entries Task Manager shows.
            list.AddRange(GetUwpEntries());

            return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Disables or re-enables an entry by flipping its approval
        /// state - exactly what Task Manager does, so both UIs stay in sync.
        /// Best effort: returns false when the state could not be written.</summary>
        public static bool SetEnabled(StartupEntry entry, bool enabled)
        {
            try
            {
                if (entry.Location == "uwp")
                    return SetUwpEnabled(entry, enabled);

                foreach (RegistrySource source in Sources)
                {
                    if (source.Origin != entry.Location) continue;
                    return WriteApproval(source.Hive, source.ApprovedPath, entry.Name, enabled);
                }

                foreach (FolderSource source in FolderSources())
                {
                    if (source.Origin != entry.Location) continue;
                    return WriteApproval(source.Hive, source.ApprovedPath, entry.Name, enabled);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ---------------- approval state ----------------

        /// <summary>Explorer writes a 12-byte binary value per entry:
        /// first byte 02/06 = enabled, 03 = disabled. Missing value = enabled
        /// (default for anything never toggled).</summary>
        private static bool IsApprovedEnabled(RegistryKey hive, string approvedPath, string name)
        {
            try
            {
                using var approved = hive.OpenSubKey(approvedPath);
                if (approved?.GetValue(name) is byte[] bytes && bytes.Length > 0)
                    return bytes[0] == 0x02 || bytes[0] == 0x06;
            }
            catch { }
            return true;
        }

        private static bool WriteApproval(RegistryKey hive, string approvedPath, string name, bool enabled)
        {
            using var approved = hive.CreateSubKey(approvedPath, writable: true);
            if (approved == null) return false;

            // Keep the 12-byte layout Windows writes: state byte + zeros.
            var data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            approved.SetValue(name, data, RegistryValueKind.Binary);
            return true;
        }

        // ---------------- UWP startup tasks ----------------

        private static IEnumerable<StartupEntry> GetUwpEntries()
        {
            var entries = new List<StartupEntry>();
            RegistryKey? root;
            try
            {
                root = Registry.CurrentUser.OpenSubKey(SystemAppDataPath);
            }
            catch
            {
                yield break;
            }
            if (root == null) yield break;

            using (root)
            {
                foreach (string family in root.GetSubKeyNames())
                {
                    // Package family names look like "Claude_pzs8sxrjxfjjc".
                    if (!family.Contains('_')) continue;

                    using var package = root.OpenSubKey(family);
                    if (package == null) continue;

                    foreach (string task in package.GetSubKeyNames())
                    {
                        using var taskKey = package.OpenSubKey(task);
                        if (taskKey?.GetValue("State") is not int state) continue;

                        entries.Add(new StartupEntry
                        {
                            Location = "uwp",
                            Name = UwpDisplayName(family),
                            Command = $"{family}!{task}",
                            Enabled = state == 2,   // StartupTaskState.Enabled
                            PackageFamily = family,
                            TaskName = task
                        });
                    }
                }
            }

            foreach (StartupEntry entry in entries)
                yield return entry;
        }

        private static bool SetUwpEnabled(StartupEntry entry, bool enabled)
        {
            if (entry.PackageFamily is not { } family || entry.TaskName is not { } task)
                return false;

            try
            {
                using var taskKey = Registry.CurrentUser.CreateSubKey(
                    SystemAppDataPath + @"\" + family + @"\" + task, writable: true);
                if (taskKey == null) return false;

                // StartupTaskState: 2 = Enabled; disabling as user action
                // uses DisabledByUser (4), like Task Manager does.
                taskKey.SetValue("State", enabled ? 2 : 4, RegistryValueKind.DWord);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Friendly name for a package family: the family prefix
        /// ("Claude_pzs8sxrjxfjjc" -> "Claude"). Fast (no shell-out); the
        /// prefix is already the product name for virtually every app that
        /// registers a startup task.</summary>
        private static string UwpDisplayName(string family)
        {
            int cut = family.LastIndexOf('_');
            return cut > 0 ? family[..cut] : family;
        }

        private static IEnumerable<string> SafeFiles(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                    return Directory.EnumerateFiles(folder);
            }
            catch { }
            return Array.Empty<string>();
        }
    }
}
