using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ChlorideTweaks.Data;
using ChlorideTweaks.Models;
using Microsoft.Win32;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Orchestrates toggling tweaks: the one-time system restore point gate,
    /// running the apply/revert actions (in-memory commands and direct
    /// registry modifications), remembering applied tweaks across restarts
    /// and keeping conflicting tweaks exclusive.
    /// </summary>
    public static class TweakService
    {
        private static List<Tweak> _tweaks = new();

        /// <summary>Feeds in the loaded tweaks so conflicts resolve on the live UI objects.</summary>
        public static void Initialize(IEnumerable<Tweak> tweaks) => _tweaks = tweaks.ToList();

        public static async Task<bool> ToggleAsync(Tweak tweak, bool skipRestorePointGate = false)
        {
            if (tweak.IsBusy) return false;

            bool target = !tweak.IsEnabled;   // ON = apply, OFF = revert

            // Switching OFF a persistent tweak that has no revert commands
            // (the 'Windows default' buffer sizes): the default value is what
            // every other buffer tweak's revert restores, so there is nothing
            // to run - only the saved state is cleared.
            if (!target && !tweak.CanRevert && !tweak.OneShot)
            {
                tweak.IsEnabled = false;
                AppStateService.SetTweakEnabled(tweak.Id, false);
                return true;
            }

            bool hasAction = target
                ? (tweak.ApplyCommands?.Count ?? 0) + (tweak.ApplyRegistry?.Count ?? 0) > 0
                : tweak.CanRevert;
            if (!hasAction) return false;

            tweak.IsBusy = true;
            tweak.StatusText = target ? LocalizationService.L("StatusApplying") : LocalizationService.L("StatusReverting");
            try
            {
                // Safety gate: ask for a restore point before the first change.
                // Package apply handles this once upfront and passes
                // skipRestorePointGate=true to avoid re-prompting for every tweak.
                if (!skipRestorePointGate && !await EnsureRestorePointAsync())
                    return false;

                // If this is the Disk Compression (CompactOS) tweak, either open
                // the interactive drive selector + live progress modal (ON) or
                // revert the previously compressed drives (OFF).
                IReadOnlyList<string>? selectedDrives = null;
                TweakRunner.RunResult? preRunResult = null;
                if (tweak.Id == "disk-compression")
                {
                    if (target)
                    {
                        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                    ?? Application.Current?.MainWindow;
                        var dialog = new DiskSelectWindow();
                        if (owner != null && owner != dialog)
                            dialog.Owner = owner;

                        if (dialog.ShowDialog() != true || dialog.SelectedDrives.Count == 0)
                            return false;

                        selectedDrives = dialog.SelectedDrives;
                        preRunResult = dialog.CompressionRunResult;
                    }
                    else
                    {
                        var saved = AppStateService.GetCompressedDrives();
                        if (saved.Count == 0)
                            saved.Add(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\");
                        selectedDrives = saved;
                    }
                }

                TweakRunner.RunResult result = preRunResult
                    ?? ((tweak.Id == "disk-compression" && selectedDrives != null)
                        ? await RunDiskCompressionAsync(selectedDrives, target)
                        : await TweakRunner.RunAsync(tweak, target));

                if (!result.Ok)
                {
                    // Ambient failure toast plus the detailed error dialog.
                    MainWindow.ShowAppToast(
                        string.Format(LocalizationService.L("ToastFailed"), tweak.LocalName),
                        success: false);
                    MessageBox.Show(
                        string.Format(LocalizationService.L("TweakFailedBody"), tweak.LocalName, result.Output),
                        LocalizationService.L("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (tweak.OneShot)
                {
                    // One-shot actions (cleanups, launchers) run but keep no ON state.
                    tweak.IsEnabled = false;
                    AppStateService.SetTweakEnabled(tweak.Id, false);
                    if (!tweak.Interactive)
                    {
                        // Interactive pickers print their own result in the
                        // console window; everything else gets a toast.
                        MainWindow.ShowAppToast(
                            string.Format(LocalizationService.L("ToastOneShotDone"), tweak.LocalName),
                            success: true);
                    }
                    return true;
                }

                if (tweak.Id == "disk-compression" && selectedDrives != null)
                {
                    AppStateService.SetCompressedDrives(target ? selectedDrives : Array.Empty<string>());
                }

                // Remember the state for the next time the app opens
                tweak.IsEnabled = target;
                AppStateService.SetTweakEnabled(tweak.Id, target);

                string displayLabel = tweak.LocalName;
                if (tweak.Id == "disk-compression" && selectedDrives != null && selectedDrives.Count > 0)
                {
                    string drivesList = string.Join(", ", selectedDrives.Select(d => d.TrimEnd('\\')));
                    displayLabel = $"{tweak.LocalName} ({drivesList})";
                }

                // Completion feedback once the process run has finished.
                MainWindow.ShowAppToast(
                    string.Format(
                        LocalizationService.L(target ? "ToastApplied" : "ToastReverted"),
                        displayLabel),
                    success: true);

                // Only one tweak per conflict group can be active (RAM/GPU/CPU
                // profiles, keyboard/mouse buffer sizes...): the newly applied
                // one overwrites the shared values, so switch the others off
                // in the UI and in the saved state.
                if (target && tweak.ConflictGroup != null)
                {
                    foreach (var other in _tweaks.Where(t =>
                                 t != tweak
                                 && t.ConflictGroup == tweak.ConflictGroup
                                 && t.IsEnabled))
                    {
                        other.IsEnabled = false;
                        AppStateService.SetTweakEnabled(other.Id, false);
                        other.ResyncToggle();
                    }
                }
                return true;
            }
            finally
            {
                tweak.IsBusy = false;
                tweak.StatusText = "";
                tweak.ResyncToggle();
            }
        }

        internal static async Task<TweakRunner.RunResult> RunDiskCompressionAsync(IReadOnlyList<string> drives, bool apply)
        {
            // Stop any existing background compact.exe worker before starting a new apply/revert pass.
            try
            {
                foreach (var proc in Process.GetProcessesByName("compact"))
                {
                    try { proc.Kill(); } catch { }
                }
            }
            catch { }

            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            var commands = new List<string>();

            if (apply)
            {
                commands.Add(@"fsutil behavior set DisableCompression 0 >nul 2>&1 & reg add ""HKLM\SYSTEM\CurrentControlSet\Control\FileSystem"" /v ""NtfsDisableCompression"" /t REG_DWORD /d ""0"" /f >nul 2>&1 & reg add ""HKCU\Software\OptiPulse"" /v ""CompactOsEnabled"" /t REG_DWORD /d ""1"" /f >nul 2>&1 & exit /b 0");
                foreach (string rawDrive in drives)
                {
                    string root = rawDrive.EndsWith("\\") ? rawDrive : rawDrive + "\\";
                    commands.Add($@"compact.exe /c ""{root}*"" /i /q /exe:xpress8k >nul 2>&1 & exit /b 0");
                }
            }
            else
            {
                commands.Add(@"reg delete ""HKCU\Software\OptiPulse"" /v ""CompactOsEnabled"" /f >nul 2>&1 & exit /b 0");
                foreach (string rawDrive in drives)
                {
                    string root = rawDrive.EndsWith("\\") ? rawDrive : rawDrive + "\\";
                    commands.Add($@"compact.exe /u ""{root}*"" /i /q /exe >nul 2>&1 & exit /b 0");
                }
            }

            var result = await TweakRunner.RunCommandsAsync(commands, interactive: false);
            if (!result.Ok)
                return result;

            // Launch the deep CompactOS / WOF XPRESS8K pass in the background
            // without pipe redirection so the UI remains immediately responsive.
            foreach (string rawDrive in drives)
            {
                string root = rawDrive.EndsWith("\\") ? rawDrive : rawDrive + "\\";
                bool isSystem = string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);

                try
                {
                    string args = isSystem
                        ? (apply ? "/CompactOS:always" : "/CompactOS:never")
                        : (apply
                            ? $"/c /s:\"{root}\" /i /q /exe:xpress8k *.exe *.dll *.pak *.bin"
                            : $"/u /s:\"{root}\" /i /q /exe *.exe *.dll *.pak *.bin");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "compact.exe",
                        Arguments = args,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                }
                catch
                {
                }
            }

            return result;
        }

        /// <summary>
        /// If OptiPulse has never created a restore point on this machine,
        /// offer to create one (user may accept or refuse). Once a point has
        /// been created successfully, this never asks again.
        /// </summary>
        private static async Task<bool> EnsureRestorePointAsync()
        {
            if (AppStateService.State.RestorePointCreated)
                return true;

            var answer = MessageBox.Show(
                LocalizationService.L("RestoreAskBody"),
                LocalizationService.L("RestoreTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return true;   // user refused - continue without a backup

            var (ok, error) = await RestorePointService.CreateRestorePointAsync();
            if (ok)
            {
                AppStateService.MarkRestorePointCreated();
                MessageBox.Show(
                    LocalizationService.L("RestoreOkBody"),
                    LocalizationService.L("RestoreTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }

            var cont = MessageBox.Show(
                string.Format(LocalizationService.L("RestoreFailBody"), error),
                LocalizationService.L("RestoreTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return cont == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Restore point gate used by GamePackageService.ApplyAsync before
        /// running any tweaks. Returns null when no prompt is needed
        /// (restore point already created), true when the user accepted/
        /// continued, false when the user declined after a failure and the
        /// package should abort.
        /// </summary>
        internal static async Task<bool?> EnsureRestorePointForPackageAsync()
        {
            if (AppStateService.State.RestorePointCreated)
                return null;   // no prompt needed

            var answer = MessageBox.Show(
                LocalizationService.L("RestoreAskBody"),
                LocalizationService.L("RestoreTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return true;   // user refused - continue without a backup

            var (ok, error) = await RestorePointService.CreateRestorePointAsync();
            if (ok)
            {
                AppStateService.MarkRestorePointCreated();
                MessageBox.Show(
                    LocalizationService.L("RestoreOkBody"),
                    LocalizationService.L("RestoreTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }

            // Restore point failed: ask whether to continue without one.
            var cont = MessageBox.Show(
                string.Format(LocalizationService.L("RestoreFailBody"), error),
                LocalizationService.L("RestoreTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            // Mark as created so subsequent tweaks and packages don't re-prompt
            // about the restore point - the user already decided to proceed.
            AppStateService.MarkRestorePointCreated();
            return cont == MessageBoxResult.Yes;
        }

        /// <summary>RAM sizes (GB) the profile slider offers.</summary>
        public static IReadOnlyList<int> RamSizes { get; } = new[] { 4, 8, 10, 12, 16, 20, 24, 32, 64 };

        /// <summary>The RAM profile currently marked as applied, if any.</summary>
        public static int? CurrentRamProfile()
        {
            foreach (int gb in RamSizes)
                if (AppStateService.IsTweakEnabled(RamProfileId(gb)))
                    return gb;
            return null;
        }

        /// <summary>Applies (or reverts) the RAM profile selected on the RAM
        /// page slider: restore-point gate, in-memory command run,
        /// persisted state and a completion toast.</summary>
        public static async Task RunRamProfileAsync(int gb, bool apply)
        {
            if (!await EnsureRestorePointAsync())
                return;

            var commands = apply ? TweakScripts.RamProfile(gb) : TweakScripts.RamRevert;
            var result = await TweakRunner.RunCommandsAsync(commands, interactive: false);
            var profileName = string.Format(LocalizationService.L("RamProfileName"), gb);

            if (!result.Ok)
            {
                MainWindow.ShowAppToast(
                    string.Format(LocalizationService.L("ToastFailed"), profileName),
                    success: false);
                MessageBox.Show(
                    string.Format(LocalizationService.L("TweakFailedBody"), profileName, result.Output),
                    LocalizationService.L("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Persist: only one profile can be active; applying replaces the
            // previous one, reverting clears the state entirely.
            foreach (int size in RamSizes)
                AppStateService.SetTweakEnabled(RamProfileId(size), false);
            if (apply)
                AppStateService.SetTweakEnabled(RamProfileId(gb), true);

            MainWindow.ShowAppToast(
                string.Format(
                    LocalizationService.L(apply ? "ToastApplied" : "ToastReverted"), profileName),
                success: true);
        }

        private static string RamProfileId(int gb) => $"ram-{gb}gb";

        // ---------------- USB buffer sliders ----------------

        /// <summary>Buffer sizes (packets) the USB sliders offer. The Windows
        /// default (100) is the slider's Revert target, not a step.</summary>
        public static IReadOnlyList<int> UsbBufferSizes { get; } = new[] { 5, 10, 15, 20, 25, 50 };

        /// <summary>The keyboard data queue size currently in the registry;
        /// null = the Windows default (100) is in use.</summary>
        public static int? CurrentKeyboardBuffer()
        {
            try
            {
                if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\kbdclass\Parameters",
                        "KeyboardDataQueueSize", null) is int i)
                    return i;
            }
            catch { }
            return null;
        }

        /// <summary>The mouse data queue size currently in the registry;
        /// null = the Windows default (100) is in use.</summary>
        public static int? CurrentMouseBuffer()
        {
            try
            {
                if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\mouclass\Parameters",
                        "MouseDataQueueSize", null) is int i)
                    return i;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Applies the selected buffer size to the keyboard or mouse class
        /// driver, or reverts to the Windows default (packets = null).
        /// Reads the true state back from the registry, so it also reflects
        /// changes made by Game Packages or the old per-size tweak cards.
        /// </summary>
        public static async Task RunUsbBufferAsync(bool keyboard, int? packets)
        {
            if (!await EnsureRestorePointAsync())
                return;

            RegistryChange[] changes =
            {
                packets is { } size
                    ? RegistryChange.Set(RegistryHive.LocalMachine,
                        keyboard
                            ? @"SYSTEM\CurrentControlSet\services\kbdclass\Parameters"
                            : @"SYSTEM\CurrentControlSet\services\mouclass\Parameters",
                        keyboard ? "KeyboardDataQueueSize" : "MouseDataQueueSize",
                        size, RegistryValueKind.DWord)
                    : RegistryChange.RemoveValue(RegistryHive.LocalMachine,
                        keyboard
                            ? @"SYSTEM\CurrentControlSet\services\kbdclass\Parameters"
                            : @"SYSTEM\CurrentControlSet\services\mouclass\Parameters",
                        keyboard ? "KeyboardDataQueueSize" : "MouseDataQueueSize")
            };

            var result = await Task.Run(() => TweakRunner.RunRegistryAsync(changes));
            string name = packets is { } size2
                ? $"{LocalizationService.L(keyboard ? "UsbKeyboardName" : "UsbMouseName")} " +
                  $"({string.Format(LocalizationService.L("UsbPacketsPattern"), size2)})"
                : LocalizationService.L(keyboard ? "UsbKeyboardName" : "UsbMouseName");

            if (!result.Ok)
            {
                MainWindow.ShowAppToast(
                    string.Format(LocalizationService.L("ToastFailed"), name),
                    success: false);
                MessageBox.Show(
                    string.Format(LocalizationService.L("TweakFailedBody"), name, result.Output),
                    LocalizationService.L("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Clear the legacy per-size tweak states (kb-5..kb-50, kb-default-100
            // and the mouse equivalents); the sliders now own this area.
            string prefix = keyboard ? "kb-" : "mouse-";
            foreach (string id in LegacyUsbIds(prefix))
                AppStateService.SetTweakEnabled(id, false);

            MainWindow.ShowAppToast(
                string.Format(LocalizationService.L(
                    packets is null ? "ToastReverted" : "ToastApplied"), name),
                success: true);
        }

        private static IEnumerable<string> LegacyUsbIds(string prefix)
        {
            foreach (int size in UsbBufferSizes)
                yield return $"{prefix}{size}";
            yield return $"{prefix}default-100";
        }
    }
}
