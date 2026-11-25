<div align="center">

# 🖥️ Observer Control Center

**Network monitoring, diagnostics and traffic analysis for Windows**

A modern WPF control center with a Black Liquid Glass interface, local network-report analytics, a compact mini-island and GitHub-powered updates.

**Windows 10/11 x64 · C# · WPF · .NET 10**

[**Download latest release**](https://github.com/KingProjects12/Observer-Control-Center/releases/latest) · [**View source**](https://github.com/KingProjects12/Observer-Control-Center) · [**GitHub Actions**](https://github.com/KingProjects12/Observer-Control-Center/actions)

</div>

---

## ✨ What is Observer?

Observer Control Center brings network diagnostics and existing traffic reports into one desktop dashboard. It is designed for system administrators who want a clear view of devices, connections and traffic statistics without working through raw CSV files.

> **Current status — v2.0.9 (public test release).** This WPF application **imports and analyzes existing Observer reports**. It does **not yet collect live Keenetic NAT connections or TOP TRAFFIC data**. The working collector remains in Observer v1.9.1; its functionality is planned for migration.

## 🔍 Features

### 🪟 Black Liquid Glass interface

- Dark, translucent WPF dashboard with a cyan/lavender accent.
- Three glass-density settings, plus a fully opaque mode.
- Resizable main window, smooth transitions and a compact **mini-island**.
- Dark scrollbars and a color-coded event console: success, errors, warnings and system activity.
- Select log text with the mouse and press `Ctrl+C`, or use **КОПИРОВАТЬ ВСЁ** to copy the entire visible journal.

The glass effect uses window transparency, **not native Windows Acrylic or background blur**.

### 📊 Traffic and connection analysis

- Import existing `08_Top_Traffic.csv` reports.
- View top devices, traffic totals and charts.
- Search devices by name, IP or MAC address.
- Import `01_All_Connections.csv` for connection analysis.
- Open local report folders directly from the app.

Traffic figures reflect the data available in the imported reports; they are not a complete live view of every LAN or VPN flow.

### 🛠️ Diagnostics and updates

- Read-only TCP connection checks to **ports 80 and 443** of an IP address you explicitly enter.
- GitHub Release notifications for newer published versions.
- In the current update flow, download the official installer **after confirmation**, verify its file size and SHA-256, then start the normal Windows installer.
- No silent installation and no automatic replacement of a portable EXE.

A TCP port check does not authenticate to a router or change its configuration.

## 📦 Download and install

1. Open **[Latest GitHub Release](https://github.com/KingProjects12/Observer-Control-Center/releases/latest)**.
2. For a normal installation, download `Observer-Control-Center-Setup-<version>-win-x64.exe`.
3. Run the setup wizard and launch **Observer Control Center** from the Start menu or desktop shortcut.

Prefer a portable application? Download `Observer.ControlCenter.exe` from the same release instead. The Windows x64 builds are self-contained, so a separate .NET runtime installation is not needed.

**About SmartScreen:** current builds are unsigned. Windows may warn about an unknown publisher. Do not disable Microsoft Defender or SmartScreen. The published `.sha256` files can be used to verify download integrity, but a checksum is **not** a trusted publisher signature.

## 🆕 What's new in v2.0.9?

- Refined darker Black Liquid Glass surfaces; the matte glass mode is fully opaque.
- Update Center can download an official installer with progress and cancellation.
- Installer downloads are checked for size and SHA-256 before the setup wizard starts.
- The displayed app version now comes from the build metadata.

The log-copying features, mini-island and existing CSV tools from v2.0.8 remain available. **Live Keenetic collection is still planned, not shipped.**

[Read the v2.0.9 release notes](https://github.com/KingProjects12/Observer-Control-Center/releases/tag/v2.0.9).

## 🚀 Build from source

**Requirements:** Windows 10/11 x64, .NET 10 SDK or Visual Studio with the .NET desktop development workload.

```powershell
git clone https://github.com/KingProjects12/Observer-Control-Center.git
cd Observer-Control-Center

dotnet run --project Tests/Observer.UpdateTests.csproj -c Release
dotnet build Observer.ControlCenter.csproj -c Release
.\Publish-SingleExe.cmd
```

The self-contained Windows build is written to `dist\Observer.ControlCenter.exe`.

Official GitHub Actions builds embed `KingProjects12/Observer-Control-Center` as the update source. A successful **Actions artifact** is not the same as a published **GitHub Release** and does not itself trigger update notifications.

## 🧭 Roadmap

- [ ] Migrate tested Keenetic authentication, NAT snapshots and TOP TRAFFIC collection from v1.9.1.
- [ ] Restore full Network Analytics exports in CSV and XLSX.
- [ ] Add device and connection correlation, site management and local history.
- [ ] Extend validation on Windows 10/11, including transparency performance and mini-island transitions.
- [ ] Improve release signing and deployment confidence.

## 🔐 Privacy and security

Observer is intended for use on networks you administer or have permission to test. Do not publish real router passwords, private network inventories, raw NAT data or customer reports in GitHub issues. Do not expose router administration interfaces directly to the public internet.

Repository security checks help catch mistakes, but are not a substitute for reviewing source code, assets, dependencies and commit history.

## 💙 Project notes

Observer Control Center is an independently implemented WPF project with visual inspiration from [Atoll](https://github.com/Fuwni/dynamic-island-windows). If Atoll code is ever incorporated, its applicable MIT attribution and license notices must be retained.

**License status:** no project license has been selected yet. A public repository is not automatically an open-source license.

---

<div align="center">

**Observer Control Center** · Built for clear, practical network visibility.

[Releases](https://github.com/KingProjects12/Observer-Control-Center/releases) · [Report an issue](https://github.com/KingProjects12/Observer-Control-Center/issues)

</div>
