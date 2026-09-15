# OptiPulse

**A Windows performance & gaming optimization tool.**

OptiPulse is a lightweight WPF desktop app for Windows that applies safe, tested system tweaks to reduce input latency, boost FPS, and clean up background clutter — all from a single, polished interface.

## Features

### ⚙️ CPU Optimization
- Intel & AMD CPU latency profiles (timer distribution, TSX, power throttling)
- Ultimate Performance power plan activation
- Foreground process priority boost (Win32PrioritySeparation)

### 🎮 GPU Optimization
- NVIDIA, AMD & Intel GPU latency profiles
- TDR, preemption, power gating and interrupt mode tuning
- Hardware-Accelerated GPU Scheduling (HAGS) toggle

### 🧠 RAM Optimization
- Adjustable RAM buffer profile
- One-click memory purge (standby list + working set trim via native APIs)

### 🔌 USB / Input
- Keyboard & mouse HID buffer size tuning (5 → 100 packets)
- Input delay tweaks with safe defaults and full revert support

### 🌐 Network
- Risk-labeled network latency tweaks

### 🚀 Game Booster
- Instant memory reduction and temp/cache cleanup, right from the desktop context menu

### 📦 Game Packages *(VIP)*
- Curated one-click tweak bundles per game or use case, with live per-tweak progress

### 🧹 UWP App Removal *(VIP)*
- Remove pre-installed Windows bloatware — only apps that are safe to remove (Store, Calculator and system dependencies are excluded)

### ▶️ Startup App Manager *(VIP)*
- View and toggle startup apps, covering Run keys, startup folders, and UWP/packaged startup tasks

### 🔍 System Scan & Clean
- **Scan (free):** temp files, prefetch, Windows Update cache, thumbnail/icon cache, crash dumps, recycle bin, browser caches (Chrome, Edge, Firefox, Brave), MUI cache, orphaned uninstallers, dead startup entries, broken shortcuts
- **Fix (VIP):** one-click cleanup for everything the scan finds

## 🛡️ Safety First

- System restore point created automatically before your first tweak
- Every tweak carries a **risk label** (Safe / Moderate / High) with a description of its effects
- All tweaks are fully reversible
- Conflict groups prevent incompatible tweaks from being applied together

## 🌍 Localization

Available in **English · Turkish · Spanish · Chinese (Simplified)**

## 🔑 License

OptiPulse uses a **Free / VIP** model:

| Feature                                  |  Free  |  VIP  |
|------------------------------------------|:------:|:-----:|
| 🌐 Network Latency Optimization           |   ✅   |  ✅   |
| 🧹 Built-in App Remover (UWP)             |   ✅   |  ✅   |
| ▶️ Startup App Manager                    |   ✅   |  ✅   |
| 🧠 RAM Service Split Profiles (4–16 GB)   |   ✅   |  ✅   |
| ⚙️ Foreground CPU Boost                   |   ✅   |  ✅   |
| 🎮 NVIDIA GPU Optimization                |   ❌   |  ✅   |
| 🎮 AMD GPU Optimization                   |   ❌   |  ✅   |
| 🔋 Ultimate Performance Power Plan        |   ❌   |  ✅   |
| ⌨️ Input Latency Removal                  |   ❌   |  ✅   |
| 🧠 RAM Profiles up to 64 GB               |   ❌   |  ✅   |
| 🔌 Extreme 5/10-Packet USB Buffers        |   ❌   |  ✅   |
| 🔒 Privacy Lockdown (400+ settings)       |   ❌   |  ✅   |
| 🧹 Bloat App & Telemetry Task Removal     |   ❌   |  ✅   |
| 🔍 System Scan & Clean                    |   ❌   |  ✅   |
| 📦 One-Click Game Packages                |   ❌   |  ✅   |

## 💻 Requirements

- Windows 10 / 11 (x64)
- .NET 8 Runtime *(bundled in the installer)*
- Administrator privileges *(required for registry and driver-level tweaks)*

## 📥 Installation

1. Download the latest installer from the [Releases](../../releases) page.
2. Run `OptiPulse_Setup.exe` and follow the setup wizard.
   > ⚠️ Windows Defender SmartScreen may warn on first run — this is expected for new, unsigned executables. Click **"More info → Run anyway"** to proceed.
3. Launch OptiPulse and activate with your license key (or continue on the Free tier).

## Usage

1. Open OptiPulse.
2. Pick a game package for instant optimization, or browse individual tweaks by category.
3. Review each tweak's risk level before applying.
4. Apply — a restore point is created automatically before your first change.
5. Toggle any tweak off at any time to revert it.

## Disclaimer

Some tweaks modify system-level settings. Read each tweak's description and risk label before applying, and keep the automatic restore point in case you want to roll back.

## License

Licensed software — a valid license key unlocks VIP features. See [LICENSE](LICENSE) for details.
