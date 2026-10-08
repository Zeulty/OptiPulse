using System.Collections.Generic;
using ChlorideTweaks.Models;

namespace ChlorideTweaks.Data
{
    public static class TweakData
    {
        // Sidebar order. "Home" and "Game Packages" are handled specially.
        public static readonly List<string> CategoryOrder = new()
        {
            "Home", "Game Launcher", "Game Packages", "CPU", "GPU", "RAM", "Disk", "USB", "Input Delay", "Network",
            "Game Booster", "UWP Apps", "Startup Apps", "Privacy", "Scan"
        };

        public static List<Tweak> GetAll() => new()
        {
            // ---------------- CPU (profiles conflict with each other) ----------------
            new Tweak {
                Id = "cpu-intel", Category = "CPU", Name = "Intel CPU optimizations",
                ApplyCommands = TweakScripts.CpuIntel, RevertCommands = TweakScripts.CpuIntelRevert,
                ConflictGroup = "cpu-profile", Risk = RiskLevel.Moderate,
                Description = "Intel-specific latency tweaks: distributes kernel timers across cores (DistributeTimers), keeps TSX enabled, disables power throttling, timer coalescing and energy estimation, and turns off VBS memory accounting boot options.",
                WhoFor = "Users with an Intel CPU who want lower and steadier input/frame timing - competitive gamers and enthusiasts benefit most. Laptop owners should think twice.",
                Purpose = "Stops Windows from slowing your processor down to save power, so games and your actions respond faster and more evenly.",
                SideEffect = "Power throttling stays off, so laptops run hotter and drain battery noticeably faster. VBS-related memory accounting is reduced, which slightly weakens some virtualization-based isolation. Takes full effect after a reboot."
            },
            new Tweak {
                Id = "cpu-amd", Category = "CPU", Name = "AMD CPU optimizations",
                ApplyCommands = TweakScripts.CpuAmd, RevertCommands = TweakScripts.CpuAmdRevert,
                ConflictGroup = "cpu-profile", Risk = RiskLevel.Moderate,
                Description = "AMD-specific tweaks: distributes kernel timers across cores (DistributeTimers) and disables TSX, which is buggy on many AMD CPUs.",
                WhoFor = "Users with an AMD CPU chasing lower latency and more consistent frame pacing - mainly gamers.",
                Purpose = "Spreads the processor's internal timing work across all cores, so games run more smoothly and frame timing stays steady.",
                SideEffect = "Software that relies on Intel TSX instructions (rare - certain databases and security libraries) will not use them. Takes full effect after a reboot."
            },
            new Tweak {
                Id = "ultimate-performance", Category = "CPU", Name = "Ultimate Performance power plan",
                ApplyCommands = TweakScripts.UltimatePerformance, RevertCommands = TweakScripts.UltimatePerformanceRevert,
                Risk = RiskLevel.Moderate,
                Description = "Activates Windows' hidden Ultimate Performance power scheme, which is more aggressive than High Performance: it prevents CPU cores from parking and downclocking and keeps latency-critical components at full power.",
                WhoFor = "Desktop gamers who want maximum sustained performance and do not run the PC on battery.",
                Purpose = "Removes the last power-saving delays between your input and the game reacting, so frame times stay consistent.",
                SideEffect = "Power consumption and heat rise noticeably; on laptops the battery drains much faster. Reverting switches back to the Balanced plan and removes the duplicated scheme. No reboot required."
            },
            new Tweak {
                Id = "foreground-boost", Category = "CPU", Name = "Foreground process boost (CPU share)",
                ApplyRegistry = TweakRegistry.ForegroundBoost, RevertRegistry = TweakRegistry.ForegroundBoostDefault,
                Risk = RiskLevel.Safe,
                Description = "Sets the Windows scheduler (Win32PrioritySeparation = 26) to short, fixed CPU time slices with maximum foreground boost, so the window you are focused on - your game - gets a much larger share of CPU time.",
                WhoFor = "Players who keep background apps open (browser, Discord, launchers) while gaming, especially on CPUs with 4-6 cores.",
                Purpose = "The game window gets its CPU work done first; background programs wait their turn instead of stealing cores mid-fight.",
                SideEffect = "Background apps (downloads, encodes, music players) may run slightly slower while a game is in the foreground. Applies to newly started processes; a reboot applies it everywhere."
            },

            // ---------------- GPU (profiles conflict with each other) ----------------
            new Tweak {
                Id = "gpu-nvidia", Category = "GPU", Name = "NVIDIA GPU optimizations",
                ApplyCommands = TweakScripts.GpuNvidia, RevertCommands = TweakScripts.GpuNvidiaRevert,
                ConflictGroup = "gpu-profile", Risk = RiskLevel.High,
                Description = "Full NVIDIA latency profile: MSI interrupt mode on the GPU, driver preemption disabled, low-latency power/tolerance values, VR direct-flip tuning, write combining off and NVIDIA telemetry tasks disabled.",
                WhoFor = "NVIDIA GPU owners with a rock-stable system who accept real risk for the last milliseconds of latency - competitive gamers on personal machines. Not for work PCs or daily drivers that must never crash.",
                Purpose = "Removes the last bits of waiting time between your graphics card and the screen, so games react as fast as possible.",
                SideEffect = "TDR (GPU driver crash recovery) is DISABLED - a hung driver freezes the whole PC instead of recovering with a black-screen flicker. NVIDIA telemetry, crash reporting and GeForce Experience self-update stop running, so driver update notifications disappear. Requires a reboot."
            },
            new Tweak {
                Id = "gpu-amd", Category = "GPU", Name = "AMD GPU optimizations",
                ApplyCommands = TweakScripts.GpuAmd, RevertCommands = TweakScripts.GpuAmdRevert,
                ConflictGroup = "gpu-profile", Risk = RiskLevel.High,
                Description = "AMD driver latency profile: display post-processing raised to High priority, GPU preemption and power gating disabled, ULPS and ASPM off, TDR off, thermal throttling and stutter mode off, logging disabled.",
                WhoFor = "AMD GPU owners on stable drivers who want minimum display latency and accept the risks. Not for systems where a freeze would be costly.",
                Purpose = "Puts screen work first in line and stops the graphics card from pausing to save power, so what you see reacts faster.",
                SideEffect = "TDR (driver crash recovery) is DISABLED - a hung driver can freeze the PC instead of recovering. Power gating, ULPS and thermal throttling features are off, so the card runs hotter and draws more power at idle. AMD's logging service is disabled. Requires a reboot."
            },
            new Tweak {
                Id = "gpu-intel", Category = "GPU", Name = "Intel GPU optimizations",
                ApplyCommands = TweakScripts.GpuIntel, RevertCommands = TweakScripts.GpuIntelRevert,
                ConflictGroup = "gpu-profile", Risk = RiskLevel.Moderate,
                Description = "Intel iGPU profile: display post-processing priority, preemption and power settings tuned, TDR off, overlay quality enhancement disabled and the dedicated iGPU memory segment raised to 1 GB.",
                WhoFor = "Users gaming on an Intel integrated GPU (or Intel Arc) in a laptop or compact desktop - especially e-sports titles at lower settings.",
                Purpose = "Gives the built-in graphics top priority and 1 GB of its own memory, so games run smoother on PCs without a separate graphics card.",
                SideEffect = "1 GB of RAM is permanently reserved for the iGPU, so systems with 8 GB or less have less usable memory. TDR (driver crash recovery) is disabled - a hung driver can freeze the PC. Requires a reboot."
            },
            new Tweak {
                Id = "hags", Category = "GPU", Name = "Hardware-accelerated GPU scheduling (HAGS)",
                ApplyRegistry = TweakRegistry.HagsOn, RevertRegistry = TweakRegistry.HagsDefault,
                Risk = RiskLevel.Moderate,
                Description = "Turns on hardware-accelerated GPU scheduling, letting the GPU manage its own work scheduling instead of the CPU - reduces latency and can improve performance on supporting hardware (NVIDIA GTX 10-series or newer, AMD RDNA, Windows 10 2004+).",
                WhoFor = "Owners of recent NVIDIA or AMD GPUs who want the modern low-latency rendering path, especially with Reflex-capable setups.",
                Purpose = "Frees the CPU from GPU scheduling work and shortens the render queue, so frames reach the screen sooner.",
                SideEffect = "On older GPU drivers HAGS caused occasional instability - update your driver first. Unsupported hardware simply ignores the value. Requires a reboot to take effect."
            },

            // ---------------- RAM ----------------
            // The RAM section is a single slider card on its own page (see
            // MainWindow's RamPanel + TweakService.RunRamProfile); there are no
            // individual RAM tweak entries here anymore.

            // ---------------- USB: hidden buffer internals ----------------
            // The USB page shows two slider cards (keyboard + mouse buffers)
            // instead of these tweak cards, so they never render. They stay
            // here because Game Packages resolve "kb-15"/"mouse-15" through
            // them, and old saved states restore cleanly.
            new Tweak {
                Id = "kb-5", Category = "USB", Name = "Keyboard buffer - 5 packets",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(5), RevertRegistry = TweakRegistry.KeyboardBuffer(100),
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Moderate,
                Description = "Sets the keyboard class driver data queue size to 5 packets (Windows default: 100).",
                WhoFor = "Competitive players who barely type while gaming and want the absolute lowest key-to-screen latency. Not for writers or office work.",
                Purpose = "Your keyboard normally waits in a queue of up to 100 before telling the game; with 5, key presses show up on screen a tiny bit sooner.",
                SideEffect = "A very small buffer can DROP keystrokes on slow or heavily loaded systems. If keys ever feel missed, revert to a larger value. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "kb-10", Category = "USB", Name = "Keyboard buffer - 10 packets",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(10), RevertRegistry = TweakRegistry.KeyboardBuffer(100),
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Moderate,
                Description = "Sets the keyboard class driver data queue size to 10 packets (Windows default: 100).",
                WhoFor = "Gamers who want very low keyboard latency without the most aggressive 5-packet setting.",
                Purpose = "Your keyboard normally waits in a queue of up to 100 before telling the game; with 10, key presses show up sooner with less risk of lost keys than 5.",
                SideEffect = "On slow or heavily loaded systems a few keystrokes can still be dropped - if that happens, go back to a larger value. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "kb-15", Category = "USB", Name = "Keyboard buffer - 15 packets",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(15), RevertRegistry = TweakRegistry.KeyboardBuffer(100),
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the keyboard class driver data queue size to 15 packets (Windows default: 100).",
                WhoFor = "Gamers who also type regularly and want lower latency with low drop risk.",
                Purpose = "Your keyboard normally waits in a queue of up to 100 before telling the game; with 15, key presses show up sooner and every key still gets through.",
                SideEffect = "Practically none - dropped input is rare at this size. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "kb-20", Category = "USB", Name = "Keyboard buffer - 20 packets",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(20), RevertRegistry = TweakRegistry.KeyboardBuffer(100),
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the keyboard class driver data queue size to 20 packets (Windows default: 100).",
                WhoFor = "Everyone who games and types - the commonly recommended balance.",
                Purpose = "Your keyboard normally waits in a queue of up to 100 before telling the game; with 20, key presses show up sooner with basically no risk of lost keys.",
                SideEffect = "Practically none. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "kb-25", Category = "USB", Name = "Keyboard buffer - 25 packets",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(25), RevertRegistry = TweakRegistry.KeyboardBuffer(100),
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the keyboard class driver data queue size to 25 packets (Windows default: 100).",
                WhoFor = "Everyone who games and types - the commonly recommended balance.",
                Purpose = "Your keyboard normally waits in a queue of up to 100 before telling the game; with 25, key presses show up sooner with basically no risk of lost keys.",
                SideEffect = "Practically none. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "kb-50", Category = "USB", Name = "Keyboard buffer - 50 packets",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(50), RevertRegistry = TweakRegistry.KeyboardBuffer(100),
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the keyboard class driver data queue size to 50 packets - half the Windows default.",
                WhoFor = "Cautious users who want a measurable improvement without approaching the aggressive low values.",
                Purpose = "Halves the keyboard's waiting queue, giving a small speed-up with basically no risk.",
                SideEffect = "Practically none. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "kb-default-100", Category = "USB", Name = "Keyboard buffer - 100 (Windows default)",
                ApplyRegistry = TweakRegistry.KeyboardBuffer(100), RevertRegistry = null,
                ConflictGroup = "kb-buffer", Risk = RiskLevel.Safe,
                Description = "Restores the Windows default keyboard buffer size of 100 packets.",
                WhoFor = "Anyone who currently has a smaller buffer set and wants to go back to stock behavior.",
                Purpose = "Puts the keyboard back to the exact settings Windows came with.",
                SideEffect = "None - this is the stock value, so there is nothing to revert. Toggling it off only clears its state."
            },

            // ---------------- USB: mouse buffers (one active value) ----------------
            new Tweak {
                Id = "mouse-5", Category = "USB", Name = "Mouse buffer - 5 packets",
                ApplyRegistry = TweakRegistry.MouseBuffer(5), RevertRegistry = TweakRegistry.MouseBuffer(100),
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Moderate,
                Description = "Sets the mouse class driver data queue size to 5 packets (Windows default: 100).",
                WhoFor = "Competitive players chasing the absolute lowest mouse latency, on systems fast enough to keep up.",
                Purpose = "Your mouse normally waits in a queue of up to 100 before telling the game; with 5, movements show up on screen a tiny bit sooner.",
                SideEffect = "A very small buffer can drop movement packets on slow or heavily loaded systems - the cursor may feel like it 'skips'. If that happens, revert to a larger value. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "mouse-10", Category = "USB", Name = "Mouse buffer - 10 packets",
                ApplyRegistry = TweakRegistry.MouseBuffer(10), RevertRegistry = TweakRegistry.MouseBuffer(100),
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Moderate,
                Description = "Sets the mouse class driver data queue size to 10 packets (Windows default: 100).",
                WhoFor = "Gamers who want very low mouse latency without the most aggressive 5-packet setting.",
                Purpose = "Your mouse normally waits in a queue of up to 100 before telling the game; with 10, movements show up sooner with less skip risk than 5.",
                SideEffect = "On slower systems some movement packets can still be dropped (cursor 'skips'). Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "mouse-15", Category = "USB", Name = "Mouse buffer - 15 packets",
                ApplyRegistry = TweakRegistry.MouseBuffer(15), RevertRegistry = TweakRegistry.MouseBuffer(100),
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the mouse class driver data queue size to 15 packets (Windows default: 100).",
                WhoFor = "Gamers who want lower mouse latency with low skip risk.",
                Purpose = "Your mouse normally waits in a queue of up to 100 before telling the game; with 15, movements show up sooner and stay reliable.",
                SideEffect = "Practically none at this size. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "mouse-20", Category = "USB", Name = "Mouse buffer - 20 packets",
                ApplyRegistry = TweakRegistry.MouseBuffer(20), RevertRegistry = TweakRegistry.MouseBuffer(100),
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the mouse class driver data queue size to 20 packets (Windows default: 100).",
                WhoFor = "Everyone who games - the commonly recommended balance.",
                Purpose = "Your mouse normally waits in a queue of up to 100 before telling the game; with 20, movements show up sooner with basically no risk.",
                SideEffect = "Practically none. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "mouse-25", Category = "USB", Name = "Mouse buffer - 25 packets",
                ApplyRegistry = TweakRegistry.MouseBuffer(25), RevertRegistry = TweakRegistry.MouseBuffer(100),
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the mouse class driver data queue size to 25 packets (Windows default: 100).",
                WhoFor = "Everyone who games - the commonly recommended balance.",
                Purpose = "Your mouse normally waits in a queue of up to 100 before telling the game; with 25, movements show up sooner with basically no risk.",
                SideEffect = "Practically none. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "mouse-50", Category = "USB", Name = "Mouse buffer - 50 packets",
                ApplyRegistry = TweakRegistry.MouseBuffer(50), RevertRegistry = TweakRegistry.MouseBuffer(100),
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Safe,
                Description = "Sets the mouse class driver data queue size to 50 packets - half the Windows default.",
                WhoFor = "Cautious users who want a measurable improvement without the aggressive low values.",
                Purpose = "Halves the mouse's waiting queue, giving a small speed-up with basically no risk.",
                SideEffect = "Practically none. Takes effect after a reboot or replug. Enabling another buffer size automatically replaces this one."
            },
            new Tweak {
                Id = "mouse-default-100", Category = "USB", Name = "Mouse buffer - 100 (Windows default)",
                ApplyRegistry = TweakRegistry.MouseBuffer(100), RevertRegistry = null,
                ConflictGroup = "mouse-buffer", Risk = RiskLevel.Safe,
                Description = "Restores the Windows default mouse buffer size of 100 packets.",
                WhoFor = "Anyone who currently has a smaller buffer set and wants to go back to stock behavior.",
                Purpose = "Puts the mouse back to the exact settings Windows came with.",
                SideEffect = "None - this is the stock value, so there is nothing to revert. Toggling it off only clears its state."
            },

            // ---------------- Input Delay ----------------
            new Tweak {
                Id = "input-latency", Category = "Input Delay", Name = "Input latency removal",
                ApplyCommands = TweakScripts.InputLatency, RevertCommands = TweakScripts.InputLatencyRevert,
                Risk = RiskLevel.Moderate,
                Description = "Raises thread priority across the whole input pipeline - USB host/controllers, keyboard and mouse class drivers, NDIS and the DirectX kernel - gives csrss real-time CPU/IO priority and applies NVIDIA low-latency driver values.",
                WhoFor = "Gamers who want the whole input chain to be processed before anything else; works with any mouse and keyboard.",
                Purpose = "Everything your mouse and keyboard signals pass through gets moved to the front of the line, so your actions happen on screen before other computer work.",
                SideEffect = "Real-time and high-priority system threads can occasionally cause audio crackling or slightly slower background apps under full load. NVIDIA-specific driver values are also written on non-NVIDIA systems (ignored, harmless). Requires a reboot."
            },
            new Tweak {
                Id = "keyboard-pack", Category = "Input Delay", Name = "Keyboard response pack",
                ApplyCommands = TweakScripts.KeyboardPack, RevertCommands = TweakScripts.KeyboardPackRevert,
                Risk = RiskLevel.Safe,
                Description = "Complete low-latency keyboard setup in one go: filter/toggle/sticky/mouse keys turned off, keyboard repeat delay 0 with maximum repeat rate, keyboard and mouse class drivers at thread priority 31 and a 21-packet input buffer.",
                WhoFor = "Anyone who games or types and does NOT rely on Windows accessibility keyboard features.",
                Purpose = "Makes the keyboard react as fast as it can: no built-in delays and instant key repeat when you hold a key down.",
                SideEffect = "The accessibility features Sticky Keys, Filter Keys, Toggle Keys and Mouse Keys STOP WORKING (their shortcuts too) - users who depend on them should not enable this. Cursor behavior is unaffected."
            },
            new Tweak {
                Id = "mouse-pack", Category = "Input Delay", Name = "Mouse precision pack",
                ApplyCommands = TweakScripts.MousePack, RevertCommands = TweakScripts.MousePackRevert,
                Risk = RiskLevel.Safe,
                Description = "Disables mouse acceleration and pointer smoothing for 1:1 raw mouse movement, removes beep and hover delays, sets the default double-click/hover sizes and raises the mouse class thread priority.",
                WhoFor = "Gamers who want raw, unaccelerated mouse movement (the '1:1' feel). Users who like Windows' pointer acceleration should skip it.",
                Purpose = "Makes the cursor move exactly with your hand - no Windows tricks that speed up or smooth out the movement. This helps you aim the same way every time in games.",
                SideEffect = "The cursor FEELS noticeably different: no acceleration means slower physical movements move the cursor less. Everyday users often dislike it at first. Sound beeps on errors are silenced for the mouse."
            },

            // ---------------- Network ----------------
            new Tweak {
                Id = "network-latency", Category = "Network", Name = "Network latency optimization",
                ApplyCommands = TweakScripts.NetworkLatency, RevertCommands = TweakScripts.NetworkLatencyRevert,
                Risk = RiskLevel.Safe,
                Description = "Disables Nagle's algorithm (TCPNoDelay and TcpAckFrequency = 1) on every network interface and turns off the multimedia network throttling index, so small game packets are sent immediately instead of being batched.",
                WhoFor = "Competitive players of any online game where every millisecond of round-trip time matters.",
                Purpose = "Your actions reach the game server a packet earlier: no 200 ms Nagle batching delay and no 10-packets-per-ms throttle cap during multimedia playback.",
                SideEffect = "Tiny packets are sent slightly more often, which adds a small amount of overhead traffic. Some VPN or corporate networks may behave differently. Takes effect on new connections; a reboot guarantees it."
            },
            new Tweak {
                Id = "nic-power", Category = "Network", Name = "Network adapter power saving off",
                ApplyCommands = TweakScripts.NicPowerOff, RevertCommands = TweakScripts.NicPowerRevert,
                Risk = RiskLevel.Safe,
                Description = "Turns off 'Allow the computer to turn off this device to save power' for every physical network adapter, so the network card never enters power-save between packets.",
                WhoFor = "Anyone who gets micro-stutters or ping spikes every few seconds while gaming - the classic symptom of a sleeping network card.",
                Purpose = "Keeps the network adapter at full readiness, removing the wake-up delay that causes periodic latency spikes.",
                SideEffect = "The adapter uses a little more power (roughly 1 W); on laptops this slightly reduces battery life. Unplugging and replugging the network cable (or a reboot) reapplies the setting."
            },
            new Tweak {
                Id = "update-delivery", Category = "Network", Name = "P2P update delivery off",
                ApplyRegistry = TweakRegistry.UpdateDeliveryOff, RevertRegistry = TweakRegistry.UpdateDeliveryDefault,
                Risk = RiskLevel.Safe,
                Description = "Sets Windows Update Delivery Optimization to HTTP-only, stopping Windows from uploading and downloading updates to and from other PCs on the internet and local network.",
                WhoFor = "Anyone on a metered or shared connection who wants zero background upload traffic while gaming.",
                Purpose = "Frees the upload bandwidth Windows would otherwise spend seeding updates to strangers, keeping your ping stable during downloads.",
                SideEffect = "Updates may download slightly slower on some connections because peer assistance is gone. Windows Update itself keeps working normally."
            },

            // ---------------- Game Booster & Disk ----------------
            new Tweak {
                Id = "sysmain-control", Category = "Disk", Name = "SysMain (Superfetch) off",
                ApplyCommands = TweakScripts.SysMainDisable, RevertCommands = TweakScripts.SysMainRevert,
                Risk = RiskLevel.Moderate,
                Description = "Stops and disables the SysMain service (formerly Superfetch), which pre-loads frequently used apps into RAM and generates constant background disk reads.",
                WhoFor = "Gamers on SSDs - especially with most RAM in use - who see disk activity spikes during matches.",
                Purpose = "Stops Windows from filling your RAM with prefetch data and from competing with the game for disk access.",
                SideEffect = "Programs you open every day launch a little slower after a reboot (no prefetch cache); on hard-disk (HDD) systems this can be noticeable. Windows may re-enable the service after big feature updates."
            },
            new Tweak {
                Id = "visual-effects", Category = "Game Booster", Name = "Visual effects & animations off",
                ApplyRegistry = TweakRegistry.VisualEffectsOff, RevertRegistry = TweakRegistry.VisualEffectsDefault,
                Risk = RiskLevel.Safe,
                Description = "Disables Windows UI latency sources: window minimize/maximize animations, taskbar animations and transparency effects, and switches the visual effects mode to custom.",
                WhoFor = "Players who alt-tab frequently and want instant window and menu response.",
                Purpose = "Removes the animation frames between minimizing/maximizing the game and seeing your desktop, making alt-tabbing feel instant.",
                SideEffect = "The desktop looks flatter (no transparency, no window animations). Sign out and back in - or restart Explorer - for every change to apply."
            },
            new Tweak {
                Id = "background-apps", Category = "Game Booster", Name = "Background apps off",
                ApplyRegistry = TweakRegistry.BackgroundAppsOff, RevertRegistry = TweakRegistry.BackgroundAppsDefault,
                Risk = RiskLevel.Safe,
                Description = "Blocks Windows Store/UWP apps from running in the background, so they cannot burn CPU, network and RAM while you are in a game.",
                WhoFor = "Anyone who sees Store apps (mail, weather, chat clients) in Task Manager while gaming.",
                Purpose = "Stops UWP apps from waking up mid-game, reserving RAM and generating background network traffic.",
                SideEffect = "Store app notifications (mail, messages) arrive only while the app is open. Desktop (non-Store) apps are unaffected. On Windows 11 some apps can still be managed individually in Settings."
            },
            new Tweak {
                Id = "gb-cpu-optimization", Category = "Game Booster", Name = "Games task priority boost",
                ApplyRegistry = TweakRegistry.GamesTaskBoost, RevertCommands = TweakScripts.GbCpuOptimizationRevert,
                Risk = RiskLevel.Safe,
                Description = "Raises the priority of the multimedia 'Games' task profile - CPU priority 6, GPU priority 8, High scheduling and High SFIO - so games get preference over background work.",
                WhoFor = "Anyone who games on this PC - the single most universal Game Booster tweak.",
                Purpose = "While a game is running, Windows puts the game first in line for the computer's power, ahead of other programs.",
                SideEffect = "Background programs (downloads, backups, scans) get slightly less CPU during gameplay."
            },
            new Tweak {
                Id = "gb-print-priority", Category = "Game Booster", Name = "Print spooler to idle priority",
                ApplyRegistry = TweakRegistry.PrintPriorityIdle, RevertCommands = TweakScripts.GbPrintPriorityRevert,
                Risk = RiskLevel.Safe,
                Description = "Drops the print spooler's priority class to idle.",
                WhoFor = "Gamers who also print sometimes and want printing to never steal CPU time mid-game.",
                Purpose = "Printing runs on leftover power only, so it never slows down your game.",
                SideEffect = "Printing slows down noticeably - jobs may pause while a game is running. Printing still completes."
            },
            new Tweak {
                Id = "gb-disable-fso-gamebar", Category = "Game Booster", Name = "Disable fullscreen optimizations & Game Bar",
                ApplyRegistry = TweakRegistry.DisableFsoGamebar, RevertCommands = TweakScripts.GbFsoGamebarRevert,
                Risk = RiskLevel.Moderate,
                Description = "Turns off Game DVR / Xbox Game Bar background recording and disables Windows 'fullscreen optimizations', giving games true exclusive fullscreen.",
                WhoFor = "Fullscreen gamers who never use Game Bar recording and want exclusive fullscreen access. Not for streamers who rely on Game Bar capture.",
                Purpose = "Games take over the whole screen directly, and Windows stops the hidden recording that runs 'just in case' - games feel quicker and run a bit smoother.",
                SideEffect = "Xbox Game Bar recording and the Win+Alt+R / Win+Alt+PrtScn capture shortcuts STOP WORKING. Some overlays (Discord, FPS counters) and alt-tab transitions behave differently or flicker in a few games."
            },
            new Tweak {
                Id = "gb-disable-driver-search", Category = "Game Booster", Name = "Stop driver downloads via Windows Update",
                ApplyRegistry = TweakRegistry.DisableDriverSearch, RevertCommands = TweakScripts.GbDriverSearchRevert,
                Risk = RiskLevel.Moderate,
                Description = "Windows Update will no longer search for and download driver updates (SearchOrderConfig=0).",
                WhoFor = "Users who manage drivers manually (vendor installers, DDU clean installs) - especially GPU owners tired of driver swaps mid-session.",
                Purpose = "Stops Windows from swapping out the drivers you installed, so your settings stay exactly as you made them.",
                SideEffect = "Driver updates - including SECURITY fixes for network, storage and graphics drivers - stop arriving through Windows Update. You must update drivers yourself from now on."
            },
            new Tweak {
                Id = "gb-disable-hibernation", Category = "Disk", Name = "Disable hibernation & fast startup",
                ApplyRegistry = TweakRegistry.DisableHibernation, RevertCommands = TweakScripts.GbHibernationRevert,
                Risk = RiskLevel.Moderate,
                Description = "Turns off hibernation and Fast Startup and frees disk space equal to a large part of your RAM.",
                WhoFor = "Desktop users and anyone who never uses hibernate; also anyone troubleshooting Fast Startup boot issues.",
                Purpose = "Deletes the big hibernation file and skips the 'half asleep' shutdown Windows uses, which also avoids some startup glitches.",
                SideEffect = "Hibernation and Fast Startup STOP WORKING: 'Hibernate' disappears from the power menu and shutdowns become full shutdowns, so startup takes a bit longer. Laptops lose hibernate-on-low-battery."
            },
            new Tweak {
                Id = "gb-disable-prefetch", Category = "Disk", Name = "Disable prefetcher",
                ApplyRegistry = TweakRegistry.DisablePrefetch, RevertCommands = TweakScripts.GbPrefetchRevert,
                Risk = RiskLevel.Moderate,
                Description = "Disables the Windows prefetcher.",
                WhoFor = "SSD-only systems; HDD users should keep the prefetcher.",
                Purpose = "Stops Windows from doing background guessing about which apps you'll open - something a fast SSD drive doesn't need.",
                SideEffect = "On HDD systems apps launch SLOWER over time because Windows stops learning launch patterns. The Superfetch/SysMain benefit is lost."
            },
            new Tweak {
                Id = "gb-disable-startup-delay", Category = "Game Booster", Name = "Remove startup app delay",
                ApplyRegistry = TweakRegistry.DisableStartupDelay, RevertCommands = TweakScripts.GbStartupDelayRevert,
                Risk = RiskLevel.Safe,
                Description = "Startup apps launch immediately without Windows' built-in stagger delay.",
                WhoFor = "Everyone, most visible with several startup apps.",
                Purpose = "Windows normally opens startup apps slowly, one by one; this gets them all up right away when you log in.",
                SideEffect = "All startup apps launch at once, causing a short CPU/disk spike right after login."
            },
            new Tweak {
                Id = "gb-hover-time", Category = "Game Booster", Name = "Mouse hover time - 10 ms",
                ApplyRegistry = TweakRegistry.HoverTime, RevertCommands = TweakScripts.GbHoverTimeRevert,
                Risk = RiskLevel.Safe,
                Description = "Tooltips and hover highlights react in 10 ms instead of the default 400 ms.",
                WhoFor = "Everyone - pure interface responsiveness.",
                Purpose = "Little help boxes and highlights appear almost the moment you hover over something.",
                SideEffect = "Tooltips popping up fast can feel busy until you get used to it."
            },
            new Tweak {
                Id = "gb-menu-delay", Category = "Game Booster", Name = "Menu show delay - 0 ms",
                ApplyRegistry = TweakRegistry.MenuDelay, RevertCommands = TweakScripts.GbMenuDelayRevert,
                Risk = RiskLevel.Safe,
                Description = "Context menus appear instantly instead of after the default 400 ms animation delay.",
                WhoFor = "Everyone - pure interface responsiveness.",
                Purpose = "Right-click menus open the instant you click.",
                SideEffect = "None worth mentioning."
            },
            new Tweak {
                Id = "gb-performance-boost", Category = "Game Booster", Name = "Disable Game DVR",
                ApplyRegistry = TweakRegistry.DisableGameDvr, RevertCommands = TweakScripts.GbPerformanceBoostRevert,
                Risk = RiskLevel.Safe,
                Description = "Turns off Game DVR background recording.",
                WhoFor = "Anyone; especially gamers who never record with Game Bar.",
                Purpose = "Stops Windows from silently recording your screen 'just in case' - that recording uses power your game could use instead.",
                SideEffect = "Background recording with Win+Alt+R is unavailable while this is on."
            },
            new Tweak {
                Id = "gb-port-thread-priority", Category = "Game Booster", Name = "Print port thread to idle",
                ApplyRegistry = TweakRegistry.PortThreadPriority, RevertCommands = TweakScripts.GbPortThreadPriorityRevert,
                Risk = RiskLevel.Safe,
                Description = "Lowers the print port thread priority to idle.",
                WhoFor = "Gamers who print occasionally.",
                Purpose = "Printing-related background work runs on leftover power only, so it never interrupts your game.",
                SideEffect = "Printing becomes slower while other work is happening."
            },
            new Tweak {
                Id = "gb-system-responsiveness", Category = "Game Booster", Name = "System responsiveness - 1%",
                ApplyRegistry = TweakRegistry.SystemResponsiveness, RevertCommands = TweakScripts.GbSystemResponsivenessRevert,
                Risk = RiskLevel.Safe,
                Description = "Reserves only 1% of CPU for background tasks (Windows default: 20%), giving multimedia and games more headroom.",
                WhoFor = "Gaming and multimedia PCs.",
                Purpose = "By default Windows keeps a fifth of the computer's power in reserve; this hands almost all of it to games instead.",
                SideEffect = "Heavy background jobs (rendering, antivirus scans, updates) get less CPU and take a little longer."
            },
            new Tweak {
                Id = "gb-wake-up-cores", Category = "Game Booster", Name = "Unhide core parking control",
                ApplyRegistry = TweakRegistry.UnhideCoreParking, RevertCommands = TweakScripts.GbWakeUpCoresRevert,
                Risk = RiskLevel.Safe,
                Description = "Makes the hidden 'Processor performance core parking' setting visible in Power Options.",
                WhoFor = "Users who want to stop Windows from parking CPU cores; needs manual setup afterwards.",
                Purpose = "Only un-hides a hidden setting - after enabling, open Power Options > Processor power management and set core parking to 100% so Windows stops putting parts of the processor to sleep.",
                SideEffect = "None by itself; keeping all cores unparked (done manually in Power Options) slightly raises idle power draw."
            },
            new Tweak {
                Id = "gb-bcdedit-tweaks", Category = "Game Booster", Name = "BCD timer tweaks",
                ApplyCommands = TweakScripts.GbBcdedit, RevertCommands = TweakScripts.GbBcdeditRevert,
                Risk = RiskLevel.Moderate,
                Description = "Disables the Windows dynamic tick and forces the platform tick (useplatformtick) for more consistent frame and input timing.",
                WhoFor = "Advanced users chasing the most consistent frame pacing; the classic companion to timer-resolution tweaks.",
                Purpose = "Keeps the computer's internal clock ticking at one fixed, steady speed, so game frames and your actions land at an even rhythm.",
                SideEffect = "Boot configuration is modified. On some hardware combos this causes stutter, unstable audio or boot quirks - if anything feels off, revert and reboot. Idle power draw rises slightly. Requires a reboot."
            },
            new Tweak {
                Id = "gb-gamebar-presence-writer", Category = "Game Booster", Name = "Disable Game Bar Presence Writer",
                ApplyCommands = TweakScripts.GbGamebarPresence, RevertCommands = TweakScripts.GbGamebarPresenceRevert,
                Risk = RiskLevel.Safe,
                Description = "Stops the Xbox Game Bar's PresenceWriter background process from relaunching itself.",
                WhoFor = "Anyone annoyed by Game Bar processes starting with games.",
                Purpose = "Stops a small hidden Xbox program from constantly restarting itself while you play.",
                SideEffect = "Xbox Game Bar presence/online status features stop updating."
            },
            new Tweak {
                Id = "gb-disable-memory-compression", Category = "Game Booster", Name = "Disable memory compression",
                ApplyCommands = TweakScripts.GbMemoryCompression, RevertCommands = TweakScripts.GbMemoryCompressionRevert,
                Risk = RiskLevel.Moderate,
                Description = "Turns off Windows RAM compression.",
                WhoFor = "Systems with 16 GB or more RAM. Low-RAM users should keep compression on.",
                Purpose = "Stops Windows from squeezing your memory to fit more in, which saves processor work when you have plenty of memory anyway.",
                SideEffect = "Memory compression STOPS WORKING: RAM fills faster, and on 8-12 GB systems (or with many browser tabs) apps slow down from disk paging. Windows updates sometimes re-enable it."
            },
            new Tweak {
                Id = "gb-disable-mitigations", Category = "Game Booster", Name = "Disable CPU security mitigations",
                ApplyCommands = TweakScripts.GbMitigations, RevertCommands = TweakScripts.GbMitigationsRevert,
                Risk = RiskLevel.High,
                Description = "Disables all Spectre/Meltdown-style CPU mitigations for extra performance.",
                WhoFor = "Isolated personal gaming PCs that hold NO sensitive data and are never used for banking or work. Everyone else should stay away from this tweak.",
                Purpose = "Gets back the small amount of speed the processor lost from security fixes - a bit faster, but genuinely less protected.",
                SideEffect = "Known CPU side-channel attacks become possible again - this is a genuine security reduction, not a cosmetic one. Malware using Spectre-class attacks could read memory it should not see. The revert re-enables the standard protections but is best-effort."
            },
            new Tweak {
                Id = "gb-game-optimizer", Category = "Game Booster", Name = "Per-game optimizer (interactive)",
                ApplyCommands = TweakScripts.GameOptimizer, RevertCommands = TweakScripts.GameOptimizer,
                Interactive = true, Risk = RiskLevel.Safe,
                Description = "Opens a file picker in a console window: pick a game .exe and it applies GPU high-performance preference, disables fullscreen optimizations and raises CPU priority - for that one game only.",
                WhoFor = "Anyone who wants certain games optimized without touching global settings.",
                Purpose = "Gives one game you pick three speed-ups: top graphics power, full-screen control and more of the computer's power - only that game, nothing else.",
                SideEffect = "Make sure you pick the game's real .exe (launchers often point elsewhere). To undo, toggle this off and pick the same game again."
            },
            new Tweak {
                Id = "ghost-tools", Category = "Game Booster", Name = "Ghost Tools desktop menu (God Settings)",
                ApplyRegistry = TweakRegistry.GhostToolsMenu, ApplyCommands = TweakScripts.GhostToolsLegacyCleanup, RevertCommands = TweakScripts.GhostToolsUninstall,
                Risk = RiskLevel.Moderate,
                Description = "Installs the GhostTools toolbox to C:\\GhostTools and adds a 'God Settings' entry to the desktop right-click menu with two commands: Reduce Memory (EmptyStandbyList clears working sets, modified page list, standby and priority-0 standby lists) and Cleanup Temporary Files (temp folders, error reports, crash dumps, prefetch, icon cache and recent files).",
                WhoFor = "Users who want one-click memory and junk-file cleanup from the desktop without opening OptiPulse each time.",
                Purpose = "Puts two maintenance shortcuts on the desktop right-click menu, so you can free up memory and disk space anytime with a single click.",
                SideEffect = "Cleanup permanently deletes temp files, error reports, crash dumps, prefetch data, the icon cache (Windows rebuilds it afterwards) and the recent files list. Reduce Memory briefly spikes CPU while the memory lists are emptied. The toolbox is deployed to C:\\GhostTools; reverting removes the menu and deletes that folder."
            },

            // ---------------- Former Debloat section (now Game Booster / Disk) ----------------
            new Tweak {
                Id = "search-indexing", Category = "Disk", Name = "Windows Search indexing off",
                ApplyCommands = TweakScripts.SearchIndexingDisable, RevertCommands = TweakScripts.SearchIndexingRevert,
                Risk = RiskLevel.Safe,
                Description = "Stops and disables the Windows Search (WSearch) indexing service, which constantly scans files and builds the search database in the background.",
                WhoFor = "Anyone who notices disk and CPU spikes from SearchIndexer.exe during games or work.",
                Purpose = "Removes the background file scanning that competes with games for disk time and CPU cycles.",
                SideEffect = "Start-menu and Explorer file searches become slow and may only find file names, not contents. Revert re-enables the service (delayed automatic start)."
            },
            new Tweak {
                Id = "remove-bloat-apps", Category = "Privacy", Name = "Remove bloat apps & telemetry tasks",
                ApplyCommands = TweakScripts.RemoveBloatApps, RevertCommands = TweakScripts.RemoveBloatAppsRevert,
                Risk = RiskLevel.Moderate,
                Description = "Uninstalls common Windows store bloat apps and disables ~70 telemetry, maintenance and tracking scheduled tasks.",
                WhoFor = "Anyone who wants a cleaner Windows and is willing to reinstall the occasional app from the Store.",
                Purpose = "Removes the apps Windows ships with that you never use, and stops hidden background tasks that watch and report what you do.",
                SideEffect = "Uninstalled store apps must be reinstalled MANUALLY from the Microsoft Store. While active, scheduled maintenance stops running: Disk Defragmenter schedules, Storage Sense cleanup, Windows Update cleanup and recovery-environment checks no longer trigger automatically."
            },
            new Tweak {
                Id = "privacy-lockdown", Category = "Privacy", Name = "Privacy & permissions lock-down",
                ApplyCommands = TweakScripts.PrivacyLockdown, RevertCommands = TweakScripts.PrivacyLockdownRevert,
                Risk = RiskLevel.High,
                Description = "Denies apps access to diagnostics, calendar, email, contacts, calls and chat; disables Office telemetry and message sync; disables SmartScreen plus seven background services.",
                WhoFor = "Privacy-focused users on personal PCs who understand they are trading away security and comfort features. Not for work machines or family PCs used for printing.",
                Purpose = "Blocks apps from reading your calendar, contacts, email, calls and diagnostic data, and stops Office from sending usage info.",
                SideEffect = "Microsoft Defender SmartScreen is DISABLED - a real security feature that warns about malicious downloads; you lose that protection. The print spooler stops, so PRINTING stops working. Maps, geolocation, W3SVC (IIS web server) and Device Management services stop. Message sync across devices no longer works."
            },
            new Tweak {
                Id = "disable-services", Category = "Game Booster", Name = "Disable background services",
                ApplyCommands = TweakScripts.DisableServices, RevertCommands = TweakScripts.DisableServicesRevert,
                Risk = RiskLevel.Moderate,
                Description = "Disables 19 non-essential services: BITS, Bluetooth, Geolocation, DiagTrack telemetry, Hyper-V, Phone, Print Spooler, QWAVE, SysMain and Windows Search.",
                WhoFor = "Gaming PCs where Bluetooth, printing, Hyper-V/WSL2 and Windows Search are genuinely unused. If you use ANY of those, skip this.",
                Purpose = "Switches off 19 background helpers that most gamers never notice - your games get more of the computer's power.",
                SideEffect = "While active these features STOP WORKING: printing, Bluetooth devices, geolocation in apps, Hyper-V and WSL2 (Docker!), phone connectivity, high-priority audio/video streaming (QWAVE), the SysMain prefetcher and Windows Search results. Background Windows Update transfers (BITS) also pause. Everything comes back on revert."
            },
            new Tweak {
                Id = "delete-temp-files", Category = "Disk", Name = "Delete temporary files",
                ApplyCommands = TweakScripts.DeleteTempFiles, RevertCommands = null,
                OneShot = true, Risk = RiskLevel.Safe,
                Description = "Clears the user and Windows temp folders to free disk space.",
                WhoFor = "Everyone; run it whenever disk space runs low.",
                Purpose = "Cleans out leftover files that programs forgot to delete, freeing up disk space.",
                SideEffect = "Deletion is permanent. If an installer is mid-run when you run this, it must be started again."
            },
            new Tweak {
                Id = "disk-compression", Category = "Disk", Name = "Disk compression (CompactOS)",
                ApplyCommands = TweakScripts.DiskCompression, RevertCommands = TweakScripts.DiskCompressionRevert,
                Risk = RiskLevel.Safe,
                Description = "Applies Windows CompactOS & WOF (XPRESS8K) transparent lossless compression to the selected drive(s), shrinking OS binaries, applications and game files on disk without modifying or deleting any data.",
                WhoFor = "SSD, NVMe and HDD users who want to reclaim gigabytes of free storage space and reduce sequential disk read volume.",
                Purpose = "Compresses read-mostly system and application binaries using Windows' native kernel-level XPRESS8K algorithm so files take up significantly less space on the selected disk while reading transparently in real time.",
                SideEffect = "Compressed files are decompressed on the fly in RAM using a negligible amount of CPU on modern processors. During initial compression or revert, background disk activity increases briefly. Reverting decompresses the target drive(s) back to standard uncompressed state."
            },
            new Tweak {
                Id = "auto-pagefile", Category = "Disk", Name = "Windows automatic pagefile management",
                ApplyCommands = TweakScripts.AutoPageFile, RevertCommands = TweakScripts.AutoPageFileRevert,
                Risk = RiskLevel.Safe,
                Description = "Enables the 'Automatically manage paging file size for all drives' option in Windows Virtual Memory settings (PagingFiles = ?:\\pagefile.sys) and disables pagefile clearing on shutdown.",
                WhoFor = "Gamers and everyday users who had custom or disabled pagefiles causing out-of-memory crashes in modern games, or anyone who wants Windows to dynamically scale virtual memory.",
                Purpose = "Lets Windows automatically decide and resize pagefile.sys based on real-time RAM pressure, preventing game crashes (especially in heavy DX12/Unreal Engine titles) and faster shutdowns.",
                SideEffect = "Windows reserves dynamic disk space on the system drive for pagefile.sys. Reverting unchecks automatic management across all drives and sets a manual system-managed entry on the OS drive. A reboot applies changes fully."
            },
            new Tweak {
                Id = "dns-mtu-tuner", Category = "Network", Name = "Network MTU & DNS Tuner",
                ApplyCommands = TweakScripts.DnsMtuTuner, RevertCommands = TweakScripts.DnsMtuTunerRevert,
                Risk = RiskLevel.Moderate,
                Description = "Optimizes packet transmission by setting MTU to 1500 and changes DNS to Cloudflare (1.1.1.1) for lowest latency.",
                WhoFor = "Gamers experiencing high ping or DNS resolution lag.",
                Purpose = "Forces all network adapters to use the fastest gaming DNS and optimal packet size.",
                SideEffect = "If your ISP strictly requires custom DNS, it might block Cloudflare. Reverting restores DHCP DNS."
            },
            new Tweak {
                Id = "privacy-shield", Category = "Privacy", Name = "Telemetry & Privacy Shield",
                ApplyCommands = TweakScripts.PrivacyShield, RevertCommands = TweakScripts.PrivacyShieldRevert,
                Risk = RiskLevel.Safe,
                Description = "Blocks Windows 10/11 telemetry, Cortana, Edge background pre-launch, and advertising ID tracking.",
                WhoFor = "Everyone who wants less background tracking and more CPU headroom.",
                Purpose = "Stops Windows from constantly sending diagnostic data and pre-loading Edge in the background.",
                SideEffect = "Cortana search features will be disabled. Edge will take slightly longer to launch the first time."
            },
            new Tweak {
                Id = "tcp-ip-advanced", Category = "Network", Name = "Advanced TCP/IP Network Optimizer",
                ApplyCommands = TweakScripts.TcpIpOptimizer, RevertCommands = TweakScripts.TcpIpOptimizerRevert,
                Risk = RiskLevel.High,
                Description = "Rebuilds the TCP stack: Restricted Auto-Tuning, enabled ECN Capability, disabled TCP Chimney Offload, enabled DCA/NetDMA, and custom initial congestion window (ICW=10).",
                WhoFor = "Competitive multiplayer gamers on stable fiber/ethernet connections.",
                Purpose = "Forces Windows to process network packets immediately without batching, severely reducing bufferbloat and hit-reg delays.",
                SideEffect = "May decrease peak download speeds on extremely fast gigabit lines. If you experience packet loss, revert immediately."
            },
            new Tweak {
                Id = "win32-priority-sep", Category = "CPU", Name = "Aggressive Win32 Priority Separation",
                ApplyCommands = TweakScripts.Win32PrioritySep, RevertCommands = TweakScripts.Win32PrioritySepRevert,
                Risk = RiskLevel.Moderate,
                Description = "Overrides Windows Thread Scheduler to use a hex value of 0x26 (38).",
                WhoFor = "Users struggling with micro-stutters and 1% low frame drops.",
                Purpose = "Forces equal length, fixed intervals for foreground and background processes, ensuring background services don't stall the game's render thread.",
                SideEffect = "Alt-tabbing may feel slightly slower or 'heavier' while a game is running."
            },
            new Tweak {
                Id = "defender-disable", Category = "Privacy", Name = "Disable Windows Defender & Security Center",
                ApplyCommands = TweakScripts.DefenderDisable, RevertCommands = TweakScripts.DefenderDisableRevert,
                Risk = RiskLevel.High,
                Description = "Forcefully disables Windows Defender Real-Time Protection and AntiSpyware via group policies, and kills the WinDefend service.",
                WhoFor = "Enthusiasts playing on trusted offline machines or running a lightweight third-party antivirus.",
                Purpose = "Completely removes the massive CPU/Disk IO overhead caused by Defender's constant background scanning of game assets.",
                SideEffect = "Your PC will be unprotected against malware. Windows Security Center will display warnings."
            },
        };
    }
}
