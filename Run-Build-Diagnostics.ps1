# One-click diagnostic build for Windows PowerShell 5.1 and Visual Studio 2026.
$ErrorActionPreference = "Stop"
$desktop = [Environment]::GetFolderPath("Desktop")
if (-not $desktop -or -not (Test-Path $desktop)) { $desktop = $PSScriptRoot }
$log = Join-Path $desktop ("Observer_v2_0_4_Build_" + (Get-Date -Format "yyyy-MM-dd_HH-mm-ss") + ".txt")
$exitStatus = 0
Start-Transcript -Path $log -Force | Out-Null
try {
    Set-Location $PSScriptRoot
    Write-Host "OBSERVER v2.0.4 - BUILD DIAGNOSTICS" -ForegroundColor Cyan
    Write-Host "SDK versions:" -ForegroundColor Yellow
    & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) { throw "dotnet --list-sdks exited with code $LASTEXITCODE" }
    Write-Host "Framework runtimes:" -ForegroundColor Yellow
    & dotnet --list-runtimes
    if ($LASTEXITCODE -ne 0) { throw "dotnet --list-runtimes exited with code $LASTEXITCODE" }
    Write-Host "Testing, building and publishing..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "Publish-SingleExe.ps1")
    Write-Host "SUCCESS: $PSScriptRoot\dist\Observer.ControlCenter.exe" -ForegroundColor Green
}
catch {
    $exitStatus = 1
    Write-Host "BUILD ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Write-Host "LOG: $log" -ForegroundColor Cyan
    try { Stop-Transcript | Out-Null } catch { }
}
exit $exitStatus
