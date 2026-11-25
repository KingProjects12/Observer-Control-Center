# Observer Control Center

Windows network monitoring and diagnostics console for system administrators. Built with C#, WPF and .NET 10.

> **Current state: v2.0.8 preview.** The WPF application can display existing Observer CSV reports but is **not yet a live Keenetic collector**. The tested NAT / TOP TRAFFIC collector remains in Observer v1.9.1; migration is planned.

## Current preview

- Desktop Control Center with a mini-island window and animations
- TOP TRAFFIC from existing CSV data, charts, device search
- Import of existing network-connection CSV reports
- Read-only TCP 80/443 diagnostics for an explicitly entered IP
- GitHub release notifications (temporary 1-minute polling while validating updates; automatic 30-minute backoff on GitHub HTTP 403/429; no silent downloads or auto-execution)

## Building on Windows

Install the .NET 10 SDK or Visual Studio with .NET Desktop Development.

```powershell
dotnet run --project Tests/Observer.UpdateTests.csproj -c Release
dotnet build Observer.ControlCenter.csproj -c Release
.\Publish-SingleExe.cmd
```

Self-contained Windows x64 executable: `dist\Observer.ControlCenter.exe`.

For a local preview, configure the update source in the app as:
`KingProjects12/Observer-Control-Center`.
Official GitHub Actions preview and release builds embed this source automatically. For test versions, a successful CI run uploads a Windows EXE artifact; CI artifacts are not GitHub Releases and do not trigger update notices.

## Privacy and security

Do **not** publish real router credentials, internal IP addresses, raw traffic JSON, client inventories, or organization-specific reports. No public WAN management exposure is needed or recommended. CI safety scanning is a supplementary check, not a substitute for manual review.

## Project roadmap

1. Verify mini-island animations and window states on Windows.
2. Migrate the tested Keenetic API authorization, NAT snapshots and TOP TRAFFIC from v1.9.1.
3. Produce matching Network Analytics reports in CSV and XLSX.
4. Add history, site management, device/connection correlation and safe diagnostics.
5. Create a tested stable public release and verify notification delivery.

## License and design provenance

The repository does not yet have a project license; public visibility alone does not grant redistribution or modification rights. A license should be selected before describing the project as open source. The interface was independently implemented with inspiration from [Atoll](https://github.com/Fuwni/dynamic-island-windows). If Atoll source code is reused in future, its MIT copyright and license notice must be retained as applicable.

## Installer (v2.0.8 preview)

Pull request Windows builds now contain a portable `Observer.ControlCenter.exe` **and** `Observer-Control-Center-Setup-<version>-win-x64.exe` generated with Inno Setup. The installer runs per user, installs desktop/start menu shortcuts, and provides an uninstaller. Settings and existing reports outside the installation folder are preserved.

**SmartScreen warning notice:** an installer alone does not remove Windows Defender SmartScreen reputation checks. These preview binaries are currently unsigned. Before broad public releases, sign the executable and installer with a trusted code-signing identity and build reputation; do not instruct users to disable Defender. For now verify SHA-256 against the build artifact and use only official GitHub Actions links.

**UI:** Black Liquid Glass is a dark translucent-inspired WPF theme (not native Windows Acrylic); the dark scrollbar and color-coded event log replace the stock Windows scrollbars and monochrome log lines. Status: success green, failure red, warning amber, system/download blue.

## 2.0.8 preview: copyable console and real transparent glass

- **Copy all logs:** the `КОПИРОВАТЬ ВСЁ` button copies the currently displayed journal lines and timestamps in order.
- **Copy any fragment:** the colored console is now a read-only WPF RichTextBox. Select text with your mouse and use Ctrl+C.
- **Genuine window transparency:** WPF layered window with a partially transparent black shell and cards; see-through areas reveal windows behind the app.
- **Three density modes:** use `Стекло: ПРОЗРАЧНО` in the left sidebar to cycle between default, maximum transparency, and opaque high-contrast mode.
- **Resizing:** while not maximized, resize the borderless window using the bottom-right diagonal grip.

The effect is per-pixel alpha translucency, **not** system-wide background blur/acrylic. Layered windows can consume more CPU/GPU on some Windows 10 systems; test performance while scrolling the table. Before a stable Release verify the existing mini-island and maximize transitions continue to work.
