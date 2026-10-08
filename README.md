<div align="center">

<img src="https://img.shields.io/badge/-%E2%9A%A1%20OptiPulse-181818?style=for-the-badge&labelColor=181818&color=9B59F5&logoColor=white" alt="OptiPulse" height="50"/>

# ⚡ OptiPulse — Windows Gaming & Performance Optimizer

### *Squeeze every last frame out of your PC. 100% Free & Open Source.*

[![Version](https://img.shields.io/badge/Version-1.0.5-9B59F5?style=for-the-badge)](https://github.com/Krafein/OptiPulse/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(64--bit)-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/Krafein/OptiPulse)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-00C851?style=for-the-badge)](LICENSE)
[![Languages](https://img.shields.io/badge/Languages-EN%20%7C%20TR%20%7C%20ES%20%7C%20ZH-F39C12?style=for-the-badge)](https://github.com/Krafein/OptiPulse)
[![Admin](https://img.shields.io/badge/Requires-Administrator-E74C3C?style=for-the-badge&logo=windows)](https://github.com/Krafein/OptiPulse)

<br/>

> **OptiPulse** is a lightweight, hardware-aware Windows optimization suite designed for competitive gamers, enthusiasts, and power users. Every tweak, preset bundle, and optimization tool is fully accessible, transparent, and completely free forever.

<br/>

---

</div>

## 💡 Overview

OptiPulse targets every bottleneck between your hardware and high-performance gaming: **input latency, CPU scheduling, RAM management, network bufferbloat, background telemetry overhead, and system bloatware**.

* 🟢 **No Paywalls, No Accounts:** No registration, no keys, no ads, and no locked features.
* 🛡️ **Safety First:** Built-in Windows Restore Point prompt before applying modifications.
* 🔄 **100% Reversible:** Every tweak stores its reversal commands and original settings for instant rollback.
* ⚡ **Native Performance:** Built in C# with .NET 8 WPF and direct Win32 / P/Invoke calls. Zero heavy dependencies.

---

## 🚀 Features Breakdown

### 🎯 1-Click Curated Game Packages
Pre-configured optimization bundles that apply tested combinations with one click:
* **Esports & Competitive:** Lowers input latency, optimizes CPU thread priorities, and strips unnecessary background scheduling.
* **Safe Gaming:** Only applies non-invasive, zero-risk tweaks that improve smoothness without altering security baselines.
* **Maximum Overdrive:** Aggressive system-wide profile disabling background services, maximizing Win32 thread priorities, and flushing memory caches.

### 🕹️ Smart Game Launcher
* Add your favorite executables to the launcher.
* Automatically pauses background desktop shell activity and flushes memory standby lists while gaming.
* Restores normal Windows state seamlessly once you exit.

### ⚙️ 40+ Core System & Hardware Tweaks
* **CPU Scheduling:** Win32PrioritySeparation (0x26), game foreground priority, MMCSS tuning.
* **GPU & Fullscreen Optimization:** Disables Fullscreen Optimizations (FSO), GameDVR background recording, and DWM latency overhead.
* **Network & Ping:** TCP/IP stack optimizations, MTU tuning, network throttling removal, and bufferbloat reduction.
* **RAM Management:** Real-time RAM profiling (from 4 GB up to 64 GB), service svchost splitting, and page-combining toggles.
* **USB Packet Buffering:** Direct configuration of keyboard and mouse class driver packet queue depths for near-instant input polling.

### 🧹 System Maintenance Suite
* **UWP Bloatware Remover:** One-click removal of pre-installed stub apps, consumer bloat, telemetry services, and third-party partner packages.
* **Startup Applications Manager:** Enable or disable startup applications cleanly using native Windows `StartupApproved` registry keys.
* **System Junk & Cache Cleaner:** Deep scan and cleanup of temporary folders, crash dumps, thumbnail caches, and browser residue.

### 📊 Live Resource Monitor & Modern UI
* Real-time monitoring of CPU utilization, RAM usage, storage metrics, and motherboard info.
* Fully customizable neon accent theming with dark glass UI styling.
* Hardware & security inspection (TPM 2.0 status, UEFI/Legacy Boot, Secure Boot detection).

---

## 🛠️ Building & Running

### Requirements
* **OS:** Windows 10 (64-bit) or Windows 11 (64-bit)
* **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
* **Privileges:** Administrator rights (required for Windows system and service modifications)

### 1. Clone & Build
```powershell
# Clone the repository
git clone https://github.com/Krafein/OptiPulse.git
cd OptiPulse

# Build Debug configuration
dotnet build

# Run the app
dotnet run
```

### 2. Publishing an Executable (.exe)

#### Standard Release Output:
```powershell
dotnet publish -c Release -o ./publish
```

#### Standalone Single-File Executable:
If you want a portable `.exe` that runs on PCs even without .NET 8 installed:
```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish-single
```

---

## 🌐 Supported Languages

OptiPulse features complete native localization across 4 languages:

| Language | Status |
| :--- | :---: |
| 🇬🇧 **English** | Complete |
| 🇹🇷 **Türkçe** | Complete |
| 🇪🇸 **Español** | Complete |
| 🇨🇳 **简体中文** | Complete |

Switch languages dynamically from the sidebar without needing to restart the application.

---

## 🛡️ Safety & Disclaimer

OptiPulse modifies Windows registry entries, kernel parameters, and system services to maximize responsiveness and eliminate gaming latency. While every tweak has been thoroughly tested and is completely reversible, always ensure you create a System Restore Point before making significant changes to your system.

Use at your own discretion.

---

## 📜 License

Distributed under the **MIT License**. See `LICENSE` for more information.

<br/>

<div align="center">

**⚡ OptiPulse — Built for Gamers, Open for Everyone.**

</div>
