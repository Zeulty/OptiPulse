using System.Collections.Generic;
using ChlorideTweaks.Models;
using Microsoft.Win32;

namespace ChlorideTweaks.Data
{
    /// <summary>
    /// Registry modifications for the tweaks that formerly imported .reg files.
    /// They are applied directly with Microsoft.Win32.RegistryKey, so no .reg
    /// file is ever written to disk. String values may contain the {APPEXE}
    /// placeholder, which TweakRunner expands to the running executable path.
    /// </summary>
    internal static class TweakRegistry
    {
        private const RegistryHive Lm = RegistryHive.LocalMachine;
        private const RegistryHive Cu = RegistryHive.CurrentUser;
        private const RegistryHive Cr = RegistryHive.ClassesRoot;

        // ---------------- USB buffer sizes ----------------

        /// <summary>Sets the keyboard class driver data queue size (default: 100).</summary>
        internal static RegistryChange[] KeyboardBuffer(int packets) => new[]
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\services\kbdclass\Parameters",
                "KeyboardDataQueueSize", packets, RegistryValueKind.DWord)
        };

        /// <summary>Sets the mouse class driver data queue size (default: 100).</summary>
        internal static RegistryChange[] MouseBuffer(int packets) => new[]
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\services\mouclass\Parameters",
                "MouseDataQueueSize", packets, RegistryValueKind.DWord)
        };

        // ---------------- Game Booster ----------------

        internal static readonly RegistryChange[] GamesTaskBoost =
        {
            RegistryChange.Set(Lm,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "GPU Priority", 8, RegistryValueKind.DWord),
            RegistryChange.Set(Lm,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "Priority", 6, RegistryValueKind.DWord),
            RegistryChange.Set(Lm,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "Scheduling Category", "High", RegistryValueKind.String),
            RegistryChange.Set(Lm,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "SFIO Priority", "High", RegistryValueKind.String),
        };

        internal static readonly RegistryChange[] PrintPriorityIdle =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\Print",
                "PriorityClass", 1, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] DisableFsoGamebar =
        {
            RegistryChange.Set(Cu, @"System\GameConfigStore",
                "GameDVR_Enabled", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"System\GameConfigStore",
                "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"System\GameConfigStore",
                "GameDVR_HonorUserFSEBehaviorMode", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"System\GameConfigStore",
                "GameDVR_DXGIHonorFSEWindowsCompatible", 1, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"System\GameConfigStore",
                "GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Lm,
                @"SOFTWARE\Microsoft\PolicyManager\default\ApplicationManagement\AllowGameDVR",
                "value", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Lm, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR",
                "AllowGameDVR", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
                "AppCaptureEnabled", 0, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] DisableDriverSearch =
        {
            RegistryChange.Set(Lm, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching",
                "SearchOrderConfig", 0, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] DisableHibernation =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power",
                "HiberBootEnabled", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\Power",
                "HibernateEnabled", 0, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] DisablePrefetch =
        {
            RegistryChange.Set(Lm,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                "EnablePrefetcher", 0, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] DisableStartupDelay =
        {
            RegistryChange.Set(Cu, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                "StartupDelayInMSec", 0, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] HoverTime =
        {
            RegistryChange.Set(Cu, @"Control Panel\Mouse",
                "MouseHoverTime", "10", RegistryValueKind.String),
        };

        internal static readonly RegistryChange[] MenuDelay =
        {
            RegistryChange.Set(Cu, @"Control Panel\Desktop",
                "MenuShowDelay", "0", RegistryValueKind.String),
        };

        internal static readonly RegistryChange[] DisableGameDvr =
        {
            RegistryChange.Set(Cu, @"System\GameConfigStore",
                "GameDVR_Enabled", 0, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] PortThreadPriority =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\Print",
                "PortThreadPriority", 1, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] SystemResponsiveness =
        {
            RegistryChange.Set(Lm,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "SystemResponsiveness", 1, RegistryValueKind.DWord),
        };

        internal static readonly RegistryChange[] UnhideCoreParking =
        {
            RegistryChange.Set(Lm,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\943c8cb6-6f93-4227-ad87-e9a3feec08d1",
                "Attributes", "2", RegistryValueKind.String),
        };

        /// <summary>Sets the scheduler to short, fixed quantums with maximum
        /// foreground boost (the classic gaming value 26).</summary>
        internal static readonly RegistryChange[] ForegroundBoost =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                "Win32PrioritySeparation", 26, RegistryValueKind.DWord),
        };

        /// <summary>Windows default scheduling (2).</summary>
        internal static readonly RegistryChange[] ForegroundBoostDefault =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                "Win32PrioritySeparation", 2, RegistryValueKind.DWord),
        };

        /// <summary>Enables hardware-accelerated GPU scheduling.</summary>
        internal static readonly RegistryChange[] HagsOn =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                "HwSchMode", 2, RegistryValueKind.DWord),
        };

        /// <summary>HAGS disabled (Windows default).</summary>
        internal static readonly RegistryChange[] HagsDefault =
        {
            RegistryChange.Set(Lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                "HwSchMode", 1, RegistryValueKind.DWord),
        };

        // ---------------- Visual effects / input / background ----------------

        /// <summary>Disables Windows UI latency sources: window min/max
        /// animations, taskbar animations and transparency; marks the visual
        /// effects mode as custom.</summary>
        internal static readonly RegistryChange[] VisualEffectsOff =
        {
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                "VisualFXSetting", 3, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"Control Panel\Desktop\WindowMetrics",
                "MinAnimate", "0", RegistryValueKind.String),
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "TaskbarAnimations", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency", 0, RegistryValueKind.DWord),
        };

        /// <summary>Windows default visual effects.</summary>
        internal static readonly RegistryChange[] VisualEffectsDefault =
        {
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                "VisualFXSetting", 0, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"Control Panel\Desktop\WindowMetrics",
                "MinAnimate", "1", RegistryValueKind.String),
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "TaskbarAnimations", 1, RegistryValueKind.DWord),
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency", 1, RegistryValueKind.DWord),
        };

        /// <summary>Disables "Enhance pointer precision" (mouse acceleration):
        /// 1:1 movement no matter the speed.</summary>
        internal static readonly RegistryChange[] RawMouseInput =
        {
            RegistryChange.Set(Cu, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String),
            RegistryChange.Set(Cu, @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String),
            RegistryChange.Set(Cu, @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String),
        };

        /// <summary>Windows default pointer acceleration curve.</summary>
        internal static readonly RegistryChange[] PointerAccelerationDefault =
        {
            RegistryChange.Set(Cu, @"Control Panel\Mouse", "MouseSpeed", "1", RegistryValueKind.String),
            RegistryChange.Set(Cu, @"Control Panel\Mouse", "MouseThreshold1", "6", RegistryValueKind.String),
            RegistryChange.Set(Cu, @"Control Panel\Mouse", "MouseThreshold2", "10", RegistryValueKind.String),
        };

        /// <summary>Blocks UWP/Store apps from running in the background.</summary>
        internal static readonly RegistryChange[] BackgroundAppsOff =
        {
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                "GlobalUserDisabled", 1, RegistryValueKind.DWord),
        };

        /// <summary>Allows background apps again.</summary>
        internal static readonly RegistryChange[] BackgroundAppsDefault =
        {
            RegistryChange.Set(Cu, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                "GlobalUserDisabled", 0, RegistryValueKind.DWord),
        };

        /// <summary>Sets Delivery Optimization to HTTP-only (no P2P upload or
        /// download of updates to/from other PCs).</summary>
        internal static readonly RegistryChange[] UpdateDeliveryOff =
        {
            RegistryChange.Set(Lm, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                "DODownloadMode", 0, RegistryValueKind.DWord),
        };

        /// <summary>Removes the policy so Windows uses its default delivery
        /// optimization mode again.</summary>
        internal static readonly RegistryChange[] UpdateDeliveryDefault =
        {
            RegistryChange.RemoveValue(Lm, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                "DODownloadMode"),
        };

        // ---------------- Ghost Tools desktop menu ----------------

        /// <summary>The "God Settings" desktop right-click menu. The commands launch
        /// OptiPulse itself with hidden switches handled by App.OnStartup, so no
        /// helper scripts or binaries are deployed to the file system.</summary>
        internal static readonly RegistryChange[] GhostToolsMenu =
        {
            // Remove any previous installation first.
            RegistryChange.RemoveKey(Cr, @"DesktopBackground\Shell\GodSettings"),

            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings",
                "MUIVerb", "God Settings", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings",
                "Icon", "shell32.dll,-16762", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings",
                "Position", "Top", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings",
                "SubCommands", "", RegistryValueKind.String),

            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings\Shell\01_ReduceMemory",
                "MUIVerb", "Reduce Memory", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings\Shell\01_ReduceMemory",
                "Icon", "imageres.dll,-109", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings\Shell\01_ReduceMemory\command",
                null, "\"{APPEXE}\" --reduce-memory", RegistryValueKind.String),

            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings\Shell\02_CleanupTemp",
                "MUIVerb", "Cleanup Temporary Files", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings\Shell\02_CleanupTemp",
                "Icon", "shell32.dll,-31", RegistryValueKind.String),
            RegistryChange.Set(Cr, @"DesktopBackground\Shell\GodSettings\Shell\02_CleanupTemp\command",
                null, "\"{APPEXE}\" --cleanup-temp", RegistryValueKind.String),
        };
    }
}
