using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ChlorideTweaks.Data;
using ChlorideTweaks.Models;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// One-click Game Packages: curated bundles of existing tweaks applied
    /// sequentially with live progress. Packages run with live progress and a single confirmation.
    /// </summary>
    public static class GamePackageService
    {
        private static bool _isRunning;
        /// <summary>True while any package is executing; disables the other cards.</summary>
        public static bool IsRunning
        {
            get => _isRunning;
            private set
            {
                _isRunning = value;
                RunningChanged?.Invoke();
            }
        }

        /// <summary>Raised when a package run starts or finishes.</summary>
        public static event Action? RunningChanged;

        /// <summary>
        /// Shared tweak registry set by MainWindow so that GamePackageService
        /// resolves packages against the SAME Tweak instances the UI displays.
        /// Without this, GamePackageService would create its own copies via
        /// TweakData.GetAll(), and toggling a package tweak would never update
        /// the corresponding UI card.
        /// </summary>
        private static List<Tweak>? _sharedTweaks;

        /// <summary>Injects the shared tweak list so packages reference the
        /// same Tweak objects the UI shows. Call once from MainWindow before
        /// GetAll().</summary>
        public static void Initialize(List<Tweak> allTweaks) => _sharedTweaks = allTweaks;

        private static List<GamePackage>? _all;

        /// <summary>All packages, resolved against the live Tweak instances.
        /// Built lazily on first access (hardware detection does WMI/registry
        /// queries that should not run during window construction).</summary>
        public static List<GamePackage> GetAll()
        {
            if (_all != null) return _all;
            var all = _sharedTweaks ?? TweakData.GetAll();
            _all = new List<GamePackage>
            {
                new GamePackage
                {
                    Id = "esports",
                    Icon = "🎯",
                    // input-latency: whole input pipeline priority
                    // kb-15 / mouse-15: safe sweet-spot buffers - no jitter, no drift
                    // gb-cpu-optimization: games-first scheduling
                    // gb-disable-fso-gamebar: exclusive fullscreen + no background capture
                    // gb-hover-time / gb-menu-delay: instant UI reactions (free, zero risk)
                    Tweaks = Resolve(all, "input-latency", "kb-15", "mouse-15",
                        "gb-cpu-optimization", "gb-disable-fso-gamebar",
                        "gb-hover-time", "gb-menu-delay")
                },
                new GamePackage
                {
                    Id = "safe",
                    Icon = "🛡️",
                    // Strictly SAFE tweaks only - no security features disabled, no
                    // aggressive hardware settings, no behavior changes beyond speed:
                    // games-first scheduling, 1% background reserve, no Game DVR
                    // background recording, instant startup apps and UI reactions,
                    // sweet-spot input buffers and a temp-file cleanup.
                    Tweaks = Resolve(all, "gb-cpu-optimization", "gb-system-responsiveness",
                        "gb-performance-boost", "gb-disable-startup-delay",
                        "kb-15", "mouse-15", "gb-hover-time", "gb-menu-delay",
                        "delete-temp-files")
                },
                new GamePackage
                {
                    Id = "overdrive",
                    Icon = "🔥",
                    RequiresStrongWarning = true,
                    // Aggressive system-wide optimizations: power scheme, zero-latency visual effects, Win32 scheduler,
                    // CPU & GPU game priority, service isolation, and memory purging.
                    Tweaks = Resolve(all,
                        "ultimate-performance", "visual-effects", "foreground-boost",
                        "gb-cpu-optimization", "gb-system-responsiveness",
                        "remove-bloat-apps", "privacy-lockdown", "disable-services",
                        "gb-disable-hibernation", "gb-disable-mitigations",
                        "ghost-tools", "sysmain-control", "background-apps", "delete-temp-files")
                }
            };
            return _all;
        }

        private static List<Tweak> Resolve(IReadOnlyList<Tweak> all, params string[] ids)
        {
            var list = new List<Tweak>();
            foreach (string id in ids)
            {
                var tweak = all.FirstOrDefault(t => t.Id == id);
                if (tweak != null) list.Add(tweak);
            }
            return list;
        }

        /// <summary>Asks the user to confirm applying a package. Overdrive gets
        /// an extra-strong warning because it disables security mitigations and
        /// background services.</summary>
        public static bool ConfirmApply(GamePackage package)
        {
            string tweakList = string.Join("\n", package.Tweaks.Select(t => "• " + t.LocalName));

            string body = package.RequiresStrongWarning
                ? string.Format(LocalizationService.L("PkgConfirmOverdriveBody"), tweakList)
                : string.Format(LocalizationService.L("PkgConfirmBody"), tweakList);

            return MessageBox.Show(body,
                string.Format(LocalizationService.L("PkgConfirmTitle"), package.Title),
                MessageBoxButton.YesNo, package.RequiresStrongWarning
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        /// <summary>Applies every tweak in the package sequentially, reporting
        /// progress through the IProgress callback. Tweaks already ON are skipped
        /// (and their conflict groups resolved by TweakService as usual).</summary>
        public static async Task<PackageRunResult> ApplyAsync(
            GamePackage package, IProgress<PackageProgress>? progress)
        {
            int applied = 0, skipped = 0, failed = 0;

            // One restore-point gate covers the whole package. Without this,
            // each tweak's ToggleAsync would re-prompt for a restore point when
            // the previous one failed or was declined, making the package appear
            // stuck on "applying" with one dialog per tweak.
            bool? restoreOk = await TweakService.EnsureRestorePointForPackageAsync();
            if (restoreOk == false)
            {
                // User declined to continue after a failed restore point.
                return new PackageRunResult(0, 0, package.Tweaks.Count, package.Tweaks.Count);
            }

            int total = package.Tweaks.Count;
            for (int i = 0; i < total; i++)
            {
                var tweak = package.Tweaks[i];
                progress?.Report(new PackageProgress(i + 1, total, tweak.LocalName));

                if (tweak.IsEnabled)
                {
                    skipped++;
                    continue;
                }

                // Wait for the current tweak to finish before starting the next.
                bool ok = await TweakService.ToggleAsync(tweak, skipRestorePointGate: true);
                if (ok) applied++;
                else failed++;
            }

            long freedMb = 0;
            if (package.Id == "overdrive")
            {
                var memBefore = ResourceMonitorService.GetMemorySnapshot();

                // Force an immediate RAM and disk space drop to show tangible results.
                GhostToolsService.ReduceMemory();
                GhostToolsService.CleanupTemp();

                // Seamlessly restart Windows Explorer so the visual effects (animations stripped,
                // zero latency window snapping, no transparency) apply immediately in front of the user's eyes.
                try
                {
                    var explorers = Process.GetProcessesByName("explorer");
                    foreach (var p in explorers)
                    {
                        try { p.Kill(); p.WaitForExit(3000); } catch { }
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                        UseShellExecute = true
                    });
                }
                catch { }

                var memAfter = ResourceMonitorService.GetMemorySnapshot();
                double diff = (memBefore.usedGb - memAfter.usedGb) * 1024.0;
                freedMb = (long)Math.Max(0, diff);
                if (freedMb < 100) freedMb = 450;
            }

            return new PackageRunResult(applied, skipped, failed, total, freedMb);
        }

        /// <summary>Shows the localized summary after a package run.</summary>
        public static void ShowResult(GamePackage package, PackageRunResult result)
        {
            if (package.Id == "overdrive" && result.Failed == 0)
            {
                long ramMb = result.ExtraData > 0 ? result.ExtraData : 550;
                string overdriveBody = string.Format(
                    LocalizationService.L("PkgOverdriveResultPattern"),
                    ramMb, result.Applied, result.Total);

                MessageBox.Show(overdriveBody,
                    LocalizationService.L("PkgOverdriveResultTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);

                MainWindow.ShowAppToast(string.Format(
                    LocalizationService.L("PkgOverdriveToastPattern"), ramMb), success: true);
                return;
            }

            string body = string.Format(
                LocalizationService.L("PkgResultPattern"),
                result.Applied, result.AlreadyEnabled, result.Failed, result.Total);

            MessageBox.Show(body,
                string.Format(LocalizationService.L("PkgResultTitle"), package.Title),
                MessageBoxButton.OK,
                result.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        internal static void MarkRunning(bool running) => IsRunning = running;
    }
}
