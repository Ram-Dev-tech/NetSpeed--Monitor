# NetSpeed Monitor (Windows 11 Utility)

A lightweight, portable, and gorgeous Windows 11 utility that displays real-time internet download and upload speeds. It embeds directly inside the taskbar as a topmost capsule and features a macOS-style translucent flyout panel containing comprehensive statistics and options.

---

## 🌟 Features

- **Taskbar-Embedded Speed Pill**: Shows dynamic speeds (`↓ 76K  ↑ 75K`) directly next to the system tray icons, vertically centered and blending natively with the Windows 11 taskbar.
- **Mac-Style Acrylic Flyout**: Toggled by clicking the system tray icon. Shows:
  - **Dynamic IP Routing**: Active network adapter description and local IPv4 address (e.g. `en0 • 10.104.45.100`).
  - **Real-Time Data Rates**: High-contrast readouts in Green (`DOWNLOAD`) and Orange (`UPLOAD`).
  - **Session Accumulators**: Cumulative transferred data sizes since startup (e.g., `Total: 13.69 GB` / `Total: 221.0 MB`).
- **Ghost/Interactive Mode**: Toggle between **Lock Position (Ghost)** (click-through overlay) and **Unlock Position (Drag)** to position the pill anywhere on your taskbar.
- **Unit Conversions**: Dynamically switch between **Bytes/s** (`KB/s`, `MB/s`) and **Bits/s** (`kbps`, `Mbps`).
- **Adjustable Refresh Rates**: Tweak update polling frequency (`0.5s`, `1.0s`, `2.0s`, `5.0s`).
- **No Asset Overhead**: System tray icon is generated dynamically in memory on launch (leak-free GDI cleanup), making it a **single self-contained `.exe` binary** with zero setup dependencies.

---

## 🛠️ Compilation

Since the utility is written in C# 5.0 compatible WPF, it compiles natively using the default .NET Framework tools built into Windows (no external SDK required).

Open **PowerShell** and run:
```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe c:\Users\Ramde\Downloads\NetSpeedOverlay\NetSpeedOverlay.csproj /p:Configuration=Release
```

The compiled binary will be placed at:
`c:\Users\Ramde\Downloads\NetSpeedOverlay\bin\Release\NetSpeedOverlay.exe`

---

## 🚀 Usage

1. Run the `NetSpeedOverlay.exe` process.
2. The speed pill will appear in the taskbar next to your system tray icons.
3. **Tray Interactions**:
   - **Click the tray icon** to toggle the details flyout.
   - Click anywhere outside the flyout to dismiss it automatically.
4. **Relocating the Pill**:
   - Open the flyout, click **Overlay Position**, and choose **Unlock Position (Drag)**.
   - Click and drag the speed pill on your taskbar to center it depending on which system icons are visible.
   - Select **Lock Position (Ghost)** in the menu to lock it and restore click-through behavior.
   - Select **Snap: Taskbar (Default)** to return it to the default tray coordinates.
