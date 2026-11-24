# Observer Control Center

Windows network monitoring and diagnostics console for system administrators. Built with C#, WPF and .NET 10.

> **Current state: v2.0.4 preview.** The WPF application can display existing Observer CSV reports but is **not yet a live Keenetic collector**. The tested NAT / TOP TRAFFIC collector remains in Observer v1.9.1; migration is planned.

## Current preview

- Desktop Control Center with a mini-island window and animations
- TOP TRAFFIC from existing CSV data, charts, device search
- Import of existing network-connection CSV reports
- Read-only TCP 80/443 diagnostics for an explicitly entered IP
- GitHub release notifications (no silent downloads or auto-execution)

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
Official GitHub Actions release builds embed this source automatically.

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
