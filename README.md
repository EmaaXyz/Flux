<div align="center">

# ⚡ FLUX — System Monitor & Optimizer

**A sleek Windows PC tweaking and hardware monitoring app**  
Built with C# · WPF · .NET 8 · MVVM · Glassmorphism UI

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?style=flat-square&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)
![Version](https://img.shields.io/badge/version-1.0.0-silver?style=flat-square)

</div>

---

## 📸 Preview

> Dark glassmorphism UI with sidebar navigation, live hardware stats, and premium tweak cards.

---

## ⚠️ Important notice before purchasing

> **We strongly advise against purchasing a Premium license right away.**
>
> Flux is currently under active development and some features may **not work correctly** on all systems.  
> Before buying, we recommend:
> - Downloading the free version and testing the free tweaks first
> - Verifying compatibility with your hardware and Windows version
> - Waiting for a more stable release if you experience any issues
>
> Any purchase is made **at your own discretion and risk**. Refunds are not guaranteed.

---

## ✨ Features

### Free (no account required)
| Tweak | Description |
|---|---|
| ⚡ Gaming Mode | Kills background processes and sets High Performance power plan |
| 🔋 Power Plan | Switches to High Performance profile |
| 🧠 RAM Flush | Clears standby list and forces GC |
| 🎨 Disable Visual FX | Turns off animations and transparency |
| 🗑️ Clean Temp Files | Wipes `%TEMP%`, `C:\Windows\Temp`, Prefetch |
| ⚙️ Disable SysMain | Stops Superfetch service |
| 🌐 Flush DNS Cache | Runs `ipconfig /flushdns` |
| 💤 Disable Hibernation | Removes `hiberfil.sys` and frees disk space |

### Premium (unlock with license key)
18 advanced tweaks including:
- DNS over HTTPS, TCP/IP Optimizer, GPU Shader Cache Cleaner
- Disable Windows Telemetry, HPET Disable, CPU Core Parking Disabler
- Xbox Game Bar removal, SSD Optimizer, Audio Latency Reducer
- Registry Cleaner, Boot Time Optimizer, DirectX Shader Optimizer
- and more...

### Monitor
- **Free:** CPU Usage, RAM Usage, GPU Temperature (live)
- **Premium:** Per-Core CPU, GPU VRAM, Network I/O, Disk I/O, Temperature History, Process Tracker, Fan Speed

---

## 🚀 Installation

### Option 1 — Download the executable (recommended)

1. Go to the [**Releases**](../../releases/latest) page
2. Download `Flux.exe`
3. Right-click → **Run as Administrator** (required for system tweaks)
4. That's it — no installation needed

> ⚠️ **Windows SmartScreen** may warn you on first launch since the binary is not code-signed.  
> Click **"More info" → "Run anyway"** to proceed.

### Option 2 — Build from source

```bash
git clone https://github.com/YOUR_USERNAME/flux.git
cd flux
dotnet build -c Release
```

Run the output at `bin/Release/net8.0-windows/Flux.exe` as Administrator.

**Requirements:**
- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (included in self-contained build)
- Administrator privileges

---

## 🔑 Premium License

Premium tweaks are unlocked with a one-time license key.

1. Open Flux → click the **gear icon** (bottom-left)
2. Go to the **LICENSE** section
3. Enter your key in the format `FLUX-XXXX-XXXX-XXXX-XXXX`
4. Click **ACTIVATE**

Keys are single-use and validated offline.  
To purchase a key → **[PayPal](https://paypal.me/EMAA104)** — €5 one-time, no subscription.

---

## 🌍 Languages

Flux supports **Italian**, **English**, and **French**.  
Change language anytime from Settings (gear icon).

---

## 🏗️ Tech Stack

| Component | Technology |
|---|---|
| Language | C# 12 |
| Framework | .NET 8 WPF |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Hardware monitoring | LibreHardwareMonitorLib |
| UI style | Glassmorphism, dark theme |
| DI | Microsoft.Extensions.DependencyInjection |

---

## 📁 Project Structure

```
Flux/
├── Core/
│   ├── Engine/          # HardwareEngine — polls sensors every 1s
│   ├── Models/          # HardwareModels
│   ├── Services/        # GamingModeManager, HardwareSensorService
│   ├── Localizer.cs     # IT / EN / FR string tables
│   └── ValidKeys.cs     # SHA-256 hashes of premium license keys
├── UI/
│   ├── ViewModels/      # MainViewModel (MVVM)
│   └── Views/           # MainWindow, LoginWindow, SettingsWindow, PremiumWindow
├── Assets/              # FluxLogo.jpg / .ico
└── App.xaml             # Entry point → LoginWindow
```

---

## ⚠️ Disclaimer

Some tweaks modify Windows registry keys and system services.  
While all changes are standard and reversible, use at your own risk.  
Always create a System Restore point before applying aggressive tweaks.

---

## 📄 License

MIT © 2026 — Emanuele

---

<div align="center">
  <sub>Built with ♥ using C# WPF · .NET 8</sub>
</div>