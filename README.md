<div align="center">

# Observer Control Center

### Network monitoring · Traffic analytics · Diagnostics

A Windows network administration console with Black Liquid Glass, local traffic reports, a mini island and GitHub updates.

**Windows 10/11 x64 · C# · WPF · .NET 10**

**English** · [Русский](README_RU.md)

[Download](https://github.com/KingProjects12/Observer-Control-Center/releases/latest) · [Releases](https://github.com/KingProjects12/Observer-Control-Center/releases) · [Builds](https://github.com/KingProjects12/Observer-Control-Center/actions)

</div>

## Overview

Observer is a desktop console for authorized network administrators. It imports existing CSV reports and helps explore devices, connections and traffic in one place.

**2.0.12 public test:** the original v1.9.1 collector is integrated into WPF, including NAT, VPN REMOTE, TOP Traffic, central-flow import and CSV/XLSX reports. Actual router and GUI verification is still needed.

## Features

- Black Liquid Glass: three density modes, near-black panels, cyan or lavender accents, fully opaque matte mode. WPF transparency, not Acrylic blur.
- Animated mini island, resizable main window, dark scrollbars.
- Colored event log; select and copy text with Ctrl+C or copy all displayed lines.
- Import `08_Top_Traffic.csv` and `01_All_Connections.csv`; device ranking, totals, charts and filters for names, IP and MAC addresses.
- Local report folders and read-only TCP checks on ports 80 and 443 of an explicitly supplied address. No router configuration changes or API authentication.
- Live read-only Keenetic x-ndw2 authentication and NAT snapshots, local office /24 filtering, VPN REMOTE (existing authorized route), TOP Traffic (four windows), central-flow CSV import, raw evidence, CSV/XLSX reports. No credentials persisted. x-ndw4 unsupported.
- GitHub release notifications. Self-update adds verified EXE replacement, backup and restart without a setup wizard; 2.0.9 still launches setup.
- English and Russian interface. Original collector logs and reports can retain Russian strings. Installer language is saved per user and retained by self-update. Standalone launch defaults to English when no language has been set.

## Installation

1. Open [the latest release](https://github.com/KingProjects12/Observer-Control-Center/releases/latest).
2. Under Assets, download `Observer-Control-Center-Setup-<version>-win-x64.exe` and run setup.
3. Choose English or Russian in the installer; Observer uses that choice.
4. Start Observer from the Start menu or shortcut.

For portable launch, download `Observer.ControlCenter.exe`. Both distributions are self-contained. Preview builds are available from [GitHub Actions](https://github.com/KingProjects12/Observer-Control-Center/actions).

**Windows security:** binaries are not yet signed with a trusted Authenticode certificate. SmartScreen may warn. Do not disable Windows protection. SHA-256 verifies integrity, not publisher identity.

## Build from source

Requirements: Windows 10/11 x64, .NET 10 SDK or Visual Studio with .NET desktop development.

```powershell
git clone https://github.com/KingProjects12/Observer-Control-Center.git
cd Observer-Control-Center
dotnet run --project Tests/Observer.UpdateTests.csproj -c Release
dotnet run --project LegacyCollectorTests/LegacyCollectorTests.csproj -c Release
dotnet build Observer.ControlCenter.csproj -c Release
.\Publish-SingleExe.cmd
```

Output: `dist\Observer.ControlCenter.exe`. Official builds lock the GitHub update repository. An Actions artifact is not a GitHub Release.

## Roadmap

- Verify the integrated Keenetic collector on authorized routers and finish English collector diagnostics.
- Network Analytics CSV and XLSX reports, site registry and observation history.
- Windows 10/11 UI, performance, language and self-update testing.
- Trusted code signing.

## Security and license

Use only on networks you own or administer with permission. Do not publish router credentials, internal addresses, raw NAT dumps or customer reports. Do not expose router administration to the internet. Automated source checks do not replace manual review.

The interface is independently developed with visual inspiration from [Atoll](https://github.com/Fuwni/dynamic-island-windows). If Atoll code is incorporated, its MIT notices must be retained. Observer's license has not yet been selected; a public repository does not itself grant redistribution rights.

[Report an issue](https://github.com/KingProjects12/Observer-Control-Center/issues) · [Русская документация](README_RU.md)
