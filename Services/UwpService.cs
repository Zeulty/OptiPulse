using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// UWP app and Windows bloatware remover for the "UWP Apps" page.
    /// Covers 5 core categories of Windows 10 & 11 bloatware:
    /// 1. Third-party partner & stub apps (TikTok, Spotify, Instagram, Netflix, etc.)
    /// 2. Microsoft internal consumer bloatware (OneDrive, Office Hub, Clipchamp, Teams, Xbox, etc.)
    /// 3. AI, Copilot, Edge & Web Experience (Widgets, Recall)
    /// 4. Telemetry and Data Collection Services (DiagTrack, dmwappushservice, WerSvc, CEIP)
    /// 5. Content Delivery Manager (Silent app installer, lockscreen ads, suggested apps)
    /// </summary>
    public static class UwpService
    {
        public sealed class UwpApp
        {
            public string Key { get; init; } = "";
            public string PackageName { get; init; } = "";
        }

        public static readonly UwpApp[] Catalog =
        {
            // --- AI, Web & Browser ---
            new UwpApp { Key = "copilot", PackageName = "Microsoft.Copilot" },
            new UwpApp { Key = "edge", PackageName = "Microsoft.MicrosoftEdge" },
            new UwpApp { Key = "webexperience", PackageName = "MicrosoftWindows.Client.WebExperience" },
            new UwpApp { Key = "recall", PackageName = "Microsoft.Windows.Recall" },

            // --- Third-Party Partner & Stub Apps ---
            new UwpApp { Key = "tiktok", PackageName = "ByteDance.TikTok" },
            new UwpApp { Key = "spotify", PackageName = "SpotifyAB.SpotifyMusic" },
            new UwpApp { Key = "instagram", PackageName = "Facebook.Instagram" },
            new UwpApp { Key = "whatsapp", PackageName = "5319275A.WhatsAppDesktop" },
            new UwpApp { Key = "disney", PackageName = "Disney.37853FC22B2CE" },
            new UwpApp { Key = "primevideo", PackageName = "AmazonVideo.PrimeVideo" },
            new UwpApp { Key = "netflix", PackageName = "4DF9E0F8.Netflix" },
            new UwpApp { Key = "linkedin", PackageName = "Microsoft.LinkedIn" },
            new UwpApp { Key = "candycrush", PackageName = "king.com.CandyCrushSaga" },
            new UwpApp { Key = "roblox", PackageName = "ROBLOXCORPORATION.ROBLOX" },
            new UwpApp { Key = "asphalt", PackageName = "Gameloft.Asphalt9Legends" },

            // --- Microsoft Consumer & Pre-Installed Apps ---
            new UwpApp { Key = "onedrive", PackageName = "Microsoft.OneDrive" },
            new UwpApp { Key = "teams", PackageName = "MicrosoftTeams" },
            new UwpApp { Key = "clipchamp", PackageName = "Clipchamp.Clipchamp" },
            new UwpApp { Key = "officehub", PackageName = "Microsoft.MicrosoftOfficeHub" },
            new UwpApp { Key = "powerautomate", PackageName = "Microsoft.PowerAutomateDesktop" },
            new UwpApp { Key = "quickassist", PackageName = "MicrosoftCorporationII.QuickAssist" },
            new UwpApp { Key = "3dbuilder", PackageName = "Microsoft.3DBuilder" },
            new UwpApp { Key = "3dviewer", PackageName = "Microsoft.Microsoft3DViewer" },
            new UwpApp { Key = "alarms", PackageName = "Microsoft.WindowsAlarms" },
            new UwpApp { Key = "camera", PackageName = "Microsoft.WindowsCamera" },
            new UwpApp { Key = "feedbackhub", PackageName = "Microsoft.WindowsFeedbackHub" },
            new UwpApp { Key = "gethelp", PackageName = "Microsoft.GetHelp" },
            new UwpApp { Key = "getstarted", PackageName = "Microsoft.GetStarted" },
            new UwpApp { Key = "groove", PackageName = "Microsoft.ZuneMusic" },
            new UwpApp { Key = "maps", PackageName = "Microsoft.WindowsMaps" },
            new UwpApp { Key = "wallet", PackageName = "Microsoft.MicrosoftWallet" },
            new UwpApp { Key = "solitaire", PackageName = "Microsoft.MicrosoftSolitaireCollection" },
            new UwpApp { Key = "mixedreality", PackageName = "Microsoft.MixedReality.Portal" },
            new UwpApp { Key = "money", PackageName = "Microsoft.BingFinance" },
            new UwpApp { Key = "movies", PackageName = "Microsoft.ZuneVideo" },
            new UwpApp { Key = "news", PackageName = "Microsoft.BingNews" },
            new UwpApp { Key = "onenote", PackageName = "Microsoft.Office.OneNote" },
            new UwpApp { Key = "paint3d", PackageName = "Microsoft.MSPaint" },
            new UwpApp { Key = "people", PackageName = "Microsoft.People" },
            new UwpApp { Key = "print3d", PackageName = "Microsoft.Print3D" },
            new UwpApp { Key = "skype", PackageName = "Microsoft.SkypeApp" },
            new UwpApp { Key = "sports", PackageName = "Microsoft.BingSports" },
            new UwpApp { Key = "voicerecorder", PackageName = "Microsoft.WindowsSoundRecorder" },
            new UwpApp { Key = "weather", PackageName = "Microsoft.BingWeather" },
            new UwpApp { Key = "yourphone", PackageName = "Microsoft.YourPhone" },

            // --- Xbox Suite ---
            new UwpApp { Key = "xboxapp", PackageName = "Microsoft.GamingApp" },
            new UwpApp { Key = "xboxcompanion", PackageName = "Microsoft.XboxApp" },
            new UwpApp { Key = "xboxgamebar", PackageName = "Microsoft.XboxGameOverlay" },
            new UwpApp { Key = "xboxspeech", PackageName = "Microsoft.XboxSpeechToTextOverlay" },
            new UwpApp { Key = "xboxtcui", PackageName = "Microsoft.Xbox.TCUI" },
            new UwpApp { Key = "xboxid", PackageName = "Microsoft.XboxIdentityProvider" },

            // --- Telemetry & Content Delivery Manager ---
            new UwpApp { Key = "telemetry", PackageName = "Microsoft.Windows.Telemetry" },
            new UwpApp { Key = "contentdelivery", PackageName = "Microsoft.Windows.ContentDeliveryManager" }
        };

        /// <summary>Names of the app packages installed on the system.</summary>
        public static HashSet<string> GetInstalledNames()
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string output = RunPowerShell(
                    "Get-AppxPackage | Select-Object -ExpandProperty Name",
                    TimeSpan.FromSeconds(60));
                foreach (string line in output.Split('\n'))
                {
                    string name = line.Trim();
                    if (name.Length > 0
                        && !name.Equals("Microsoft.Windows.ContentDeliveryManager", StringComparison.OrdinalIgnoreCase)
                        && !name.Equals("Microsoft.MicrosoftEdgeDevToolsClient", StringComparison.OrdinalIgnoreCase))
                    {
                        installed.Add(name);
                    }
                }
            }
            catch { /* listing is best-effort */ }

            // Helper to map wildcard/partial AppX names to official catalog PackageNames
            void CheckAndAdd(string partialName, string catalogPackageName)
            {
                if (installed.Any(n => n.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    installed.Add(catalogPackageName);
                }
            }

            CheckAndAdd("TikTok", "ByteDance.TikTok");
            CheckAndAdd("Spotify", "SpotifyAB.SpotifyMusic");
            CheckAndAdd("Instagram", "Facebook.Instagram");
            CheckAndAdd("WhatsApp", "5319275A.WhatsAppDesktop");
            CheckAndAdd("Disney", "Disney.37853FC22B2CE");
            CheckAndAdd("PrimeVideo", "AmazonVideo.PrimeVideo");
            CheckAndAdd("AmazonVideo", "AmazonVideo.PrimeVideo");
            CheckAndAdd("Netflix", "4DF9E0F8.Netflix");
            CheckAndAdd("LinkedIn", "Microsoft.LinkedIn");
            CheckAndAdd("CandyCrush", "king.com.CandyCrushSaga");
            CheckAndAdd("Roblox", "ROBLOXCORPORATION.ROBLOX");
            CheckAndAdd("Asphalt", "Gameloft.Asphalt9Legends");
            CheckAndAdd("Clipchamp", "Clipchamp.Clipchamp");
            CheckAndAdd("MicrosoftOfficeHub", "Microsoft.MicrosoftOfficeHub");
            CheckAndAdd("OfficeHub", "Microsoft.MicrosoftOfficeHub");
            CheckAndAdd("Teams", "MicrosoftTeams");
            CheckAndAdd("PowerAutomate", "Microsoft.PowerAutomateDesktop");
            CheckAndAdd("GamingApp", "Microsoft.GamingApp");
            CheckAndAdd("Xbox.TCUI", "Microsoft.Xbox.TCUI");
            CheckAndAdd("XboxIdentityProvider", "Microsoft.XboxIdentityProvider");
            CheckAndAdd("QuickAssist", "MicrosoftCorporationII.QuickAssist");
            CheckAndAdd("WebExperience", "MicrosoftWindows.Client.WebExperience");
            CheckAndAdd("Recall", "Microsoft.Windows.Recall");

            // Accurate Edge detection: Edge is installed only if msedge.exe exists on disk.
            // Avoid ghost Spartan / SystemApp stubs (e.g. EdgeDevToolsClient).
            installed.Remove("Microsoft.MicrosoftEdge");
            string edgeX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
            string edgeX64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe");
            string edgeUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe");

            bool isEdgeInstalled =
                File.Exists(edgeX86)
                || File.Exists(edgeX64)
                || File.Exists(edgeUser);

            if (isEdgeInstalled)
            {
                installed.Add("Microsoft.MicrosoftEdge");
            }

            // Enhanced Copilot detection: checks AppX and Windows Copilot policies
            bool isCopilotInstalled =
                installed.Any(n => n.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isCopilotInstalled)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot");
                    object? val = key?.GetValue("TurnOffWindowsCopilot");
                    if (val == null || (val is int intVal && intVal == 0))
                    {
                        using var advKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                        object? btnVal = advKey?.GetValue("ShowCopilotButton");
                        if (btnVal != null && (btnVal is int bVal && bVal == 1))
                        {
                            isCopilotInstalled = true;
                        }
                    }
                }
                catch { }
            }

            if (isCopilotInstalled)
            {
                installed.Add("Microsoft.Copilot");
            }

            // OneDrive detection
            string oneDriveUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe");
            string oneDriveX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft OneDrive\OneDrive.exe");
            string oneDriveX64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive\OneDrive.exe");

            bool isOneDriveInstalled =
                File.Exists(oneDriveUser)
                || File.Exists(oneDriveX86)
                || File.Exists(oneDriveX64)
                || installed.Any(n => n.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isOneDriveInstalled)
            {
                installed.Add("Microsoft.OneDrive");
            }

            // Telemetry services detection (DiagTrack service status)
            try
            {
                using var diagKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\DiagTrack");
                object? startVal = diagKey?.GetValue("Start");
                if (startVal is int s && s != 4) // 4 = Disabled
                {
                    installed.Add("Microsoft.Windows.Telemetry");
                }
            }
            catch { }

            // Content Delivery Manager detection (SilentInstalledApps & ConsumerFeatures)
            // Note: In Windows 10/11, Microsoft.Windows.ContentDeliveryManager is a NonRemovable SystemApp,
            // so its operational/bloat status is determined by whether silent installs/consumer ads are active.
            installed.Remove("Microsoft.Windows.ContentDeliveryManager");
            bool isCdmActive = true;
            try
            {
                using var cdmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
                object? silentVal = cdmKey?.GetValue("SilentInstalledAppsEnabled");
                if (silentVal is int s && s == 0)
                {
                    isCdmActive = false;
                }

                using var cloudKeyLm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent");
                object? disValLm = cloudKeyLm?.GetValue("DisableWindowsConsumerFeatures");
                if (disValLm is int dLm && dLm == 1)
                {
                    isCdmActive = false;
                }

                using var cloudKeyCu = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\CloudContent");
                object? disValCu = cloudKeyCu?.GetValue("DisableWindowsConsumerFeatures");
                if (disValCu is int dCu && dCu == 1)
                {
                    isCdmActive = false;
                }
            }
            catch { }

            if (isCdmActive)
            {
                installed.Add("Microsoft.Windows.ContentDeliveryManager");
            }

            return installed;
        }

        /// <summary>
        /// Removes the selected applications and components. Special-cases Edge,
        /// Copilot, OneDrive, Telemetry, ContentDeliveryManager, and cleans AppX
        /// packages including provisioned packages.
        /// </summary>
        public static void RemoveApps(IEnumerable<string> packageNames)
        {
            var names = packageNames
                .Where(n => !string.IsNullOrWhiteSpace(n) && !n.Contains('\''))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0) return;

            bool removeEdge = names.Any(n => n.Equals("Microsoft.MicrosoftEdge", StringComparison.OrdinalIgnoreCase));
            bool removeCopilot = names.Any(n => n.Equals("Microsoft.Copilot", StringComparison.OrdinalIgnoreCase));
            bool removeOneDrive = names.Any(n => n.Equals("Microsoft.OneDrive", StringComparison.OrdinalIgnoreCase));
            bool removeTelemetry = names.Any(n => n.Equals("Microsoft.Windows.Telemetry", StringComparison.OrdinalIgnoreCase));
            bool removeCdm = names.Any(n => n.Equals("Microsoft.Windows.ContentDeliveryManager", StringComparison.OrdinalIgnoreCase));

            var standardApps = names
                .Where(n => !n.Equals("Microsoft.MicrosoftEdge", StringComparison.OrdinalIgnoreCase)
                         && !n.Equals("Microsoft.Copilot", StringComparison.OrdinalIgnoreCase)
                         && !n.Equals("Microsoft.OneDrive", StringComparison.OrdinalIgnoreCase)
                         && !n.Equals("Microsoft.Windows.Telemetry", StringComparison.OrdinalIgnoreCase)
                         && !n.Equals("Microsoft.Windows.ContentDeliveryManager", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 1. Remove Microsoft Edge using the open-source Remove-MS-Edge tool
            if (removeEdge)
            {
                RemoveMicrosoftEdge();
            }

            // 2. Remove Copilot (AppX + Taskbar button policy + shell refresh)
            if (removeCopilot)
            {
                RemoveCopilot();
            }

            // 3. Remove OneDrive
            if (removeOneDrive)
            {
                RemoveOneDrive();
            }

            // 4. Disable Windows Telemetry & Diagnostic tracking
            if (removeTelemetry)
            {
                RemoveTelemetry();
            }

            // 5. Disable Content Delivery Manager (silent apps, spotlight ads, suggestions)
            if (removeCdm)
            {
                RemoveContentDeliveryManager();
            }

            // 6. Remove standard UWP apps (both user AppX and Provisioned package)
            if (standardApps.Count > 0)
            {
                // Build a PowerShell array literal so names are embedded directly
                // in the script. Using $args does NOT work because RunPowerShell
                // passes the entire string via  -Command "...", which treats it as
                // a single script block and leaves $args empty.
                string arrayLiteral = string.Join(",", standardApps.Select(n => $"'{n}'"));
                string script =
                    $"$names = @({arrayLiteral}); " +
                    "foreach ($n in $names) { " +
                    "  Get-AppxPackage -AllUsers | Where-Object { $_.Name -like ('*' + $n + '*') } | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                    "  Get-AppxPackage | Where-Object { $_.Name -like ('*' + $n + '*') } | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                    "  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like ('*' + $n + '*') } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue; " +
                    "};";
                try
                {
                    RunPowerShell(script, TimeSpan.FromMinutes(4));
                }
                catch { }
            }

            // Restart Explorer so taskbar, widgets and app registrations refresh cleanly
            try
            {
                RunPowerShell("Stop-Process -Name explorer -Force", TimeSpan.FromSeconds(15));
            }
            catch { }
        }

        private static void RemoveMicrosoftEdge()
        {
            try
            {
                string? batPath = FindEdgeRemoverScript();
                if (!string.IsNullOrEmpty(batPath) && File.Exists(batPath))
                {
                    string dir = Path.GetDirectoryName(batPath)!;
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"\"{batPath}\" -auto\"",
                        WorkingDirectory = dir,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds);
                }

                RunPowerShell("Stop-Process -Name msedge, MicrosoftEdgeUpdate -Force -ErrorAction SilentlyContinue", TimeSpan.FromSeconds(15));
                RunPowerShell("Get-AppxPackage -AllUsers *MicrosoftEdge* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue", TimeSpan.FromSeconds(30));

                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs");

                foreach (string dir in new[] { publicDesktop, userDesktop, startMenu })
                {
                    try
                    {
                        string lnk = Path.Combine(dir, "Microsoft Edge.lnk");
                        if (File.Exists(lnk)) File.Delete(lnk);
                    }
                    catch { }
                }

                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\EdgeUpdate", true);
                key?.SetValue("DoNotUpdateToEdgeWithChromium", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void RemoveCopilot()
        {
            try
            {
                RunPowerShell(
                    "Get-AppxPackage -AllUsers *Copilot* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                    "Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like '*Copilot*' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue",
                    TimeSpan.FromSeconds(45));

                using (var cu = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot", true))
                {
                    cu?.SetValue("TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                }
                using (var lm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", true))
                {
                    lm?.SetValue("TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                }
                using (var adv = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
                {
                    adv?.SetValue("ShowCopilotButton", 0, RegistryValueKind.DWord);
                }
                using (var edge = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
                {
                    edge?.SetValue("HubsSidebarEnabled", 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void RemoveOneDrive()
        {
            try
            {
                RunPowerShell("Stop-Process -Name OneDrive -Force -ErrorAction SilentlyContinue", TimeSpan.FromSeconds(15));
                RunPowerShell("Get-AppxPackage -AllUsers *OneDrive* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue", TimeSpan.FromSeconds(30));

                string sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string[] setupCandidates =
                {
                    Path.Combine(sysRoot, "SysWOW64", "OneDriveSetup.exe"),
                    Path.Combine(sysRoot, "System32", "OneDriveSetup.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\Update\OneDriveSetup.exe")
                };

                foreach (string candidate in setupCandidates)
                {
                    if (File.Exists(candidate))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = candidate,
                            Arguments = "/uninstall /silent",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit((int)TimeSpan.FromMinutes(2).TotalMilliseconds);
                        break;
                    }
                }

                // Clean registry autorun
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("OneDrive", false);

                // Delete shortcut
                string userLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OneDrive.lnk");
                if (File.Exists(userLnk)) File.Delete(userLnk);
            }
            catch { }
        }

        private static void RemoveTelemetry()
        {
            try
            {
                // Disable services
                string[] services = { "DiagTrack", "dmwappushservice", "WerSvc" };
                foreach (string s in services)
                {
                    RunProcess("sc.exe", $"stop {s}");
                    RunProcess("sc.exe", $"config {s} start= disabled");
                }

                // Disable telemetry scheduled tasks
                string[] tasks =
                {
                    @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                    @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                    @"\Microsoft\Windows\Device Information\Device",
                    @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                    @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"
                };

                foreach (string t in tasks)
                {
                    RunProcess("schtasks.exe", $"/change /tn \"{t}\" /disable");
                }

                // Set registry policy to 0
                using var lm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                lm?.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void RemoveContentDeliveryManager()
        {
            try
            {
                using (var cdm = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", true))
                {
                    if (cdm != null)
                    {
                        cdm.SetValue("SilentInstalledAppsEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("PreInstalledAppsEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("OemPreInstalledAppsEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("FeatureManagementEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SystemPaneSuggestionsEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SubscribedContent-338389Enabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SubscribedContent-353694Enabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SubscribedContent-353696Enabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SubscribedContent-353698Enabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("RotatingLockScreenEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("RotatingLockScreenOverlayEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("SoftLandingEnabled", 0, RegistryValueKind.DWord);
                        cdm.SetValue("ContentDeliveryAllowed", 0, RegistryValueKind.DWord);
                    }
                }

                using (var cloudLm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", true))
                {
                    cloudLm?.SetValue("DisableWindowsConsumerFeatures", 1, RegistryValueKind.DWord);
                    cloudLm?.SetValue("DisableSoftLanding", 1, RegistryValueKind.DWord);
                }

                using (var cloudCu = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\CloudContent", true))
                {
                    cloudCu?.SetValue("DisableWindowsConsumerFeatures", 1, RegistryValueKind.DWord);
                    cloudCu?.SetValue("DisableSoftLanding", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static string? FindEdgeRemoverScript()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Assets", "EdgeRemover", "Edge.bat"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "EdgeRemover", "Edge.bat"),
                Path.Combine(baseDir, "..", "..", "..", "Assets", "EdgeRemover", "Edge.bat")
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
            return null;
        }

        private static void RunProcess(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch { }
        }

        private static string RunPowerShell(string commandAndArgs, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"" + commandAndArgs + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("PowerShell could not be started.");
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit((int)timeout.TotalMilliseconds);
            return output;
        }
    }
}
