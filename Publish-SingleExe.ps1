# Run on Windows 10/11 with .NET 10 SDK (Visual Studio 2026).
# No elevated permissions; output is a self-contained portable win-x64 EXE.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Не найден dotnet SDK. Установи .NET 10/11 SDK и компоненты .NET Desktop Development."
}
Write-Host "[1/5] Checking available .NET SDK..." -ForegroundColor Cyan
& dotnet --version
if ($LASTEXITCODE -ne 0) { throw "dotnet --version failed" }

Write-Host "[2/5] Running release version/update tests..." -ForegroundColor Cyan
& dotnet run --project "Tests/Observer.UpdateTests.csproj" --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Update tests failed with exit code $LASTEXITCODE" }

Write-Host "[3/5] Building WPF project..." -ForegroundColor Cyan
& dotnet build "Observer.ControlCenter.csproj" --configuration Release
if ($LASTEXITCODE -ne 0) { throw "WPF build failed with exit code $LASTEXITCODE" }

Write-Host "[4/5] Publishing self-contained win-x64 single EXE..." -ForegroundColor Cyan
$repoFlag = @()
if ($env:OBSERVER_GITHUB_REPO) {
    if ($env:OBSERVER_GITHUB_REPO -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$') {
        throw "OBSERVER_GITHUB_REPO must have format owner/repo"
    }
    $repoFlag = @("-p:ObserverUpdateRepo=$($env:OBSERVER_GITHUB_REPO)")
}
$target = Join-Path $PSScriptRoot "dist"
& dotnet publish "Observer.ControlCenter.csproj" `
    --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false `
    -p:DebugType=None -p:DebugSymbols=false `
    @repoFlag --output $target
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }
$exe = Join-Path $target "Observer.ControlCenter.exe"
if (-not (Test-Path $exe)) { throw "Expected EXE was not created: $exe" }
Write-Host "[5/5] Done: $exe" -ForegroundColor Green
Get-Item $exe | Select-Object FullName, Length, LastWriteTime | Format-List
Write-Host "ВНИМАНИЕ: это UI PREVIEW с уведомлениями. Онлайн-сбор Keenetic API ещё не перенесён из v1.9.1." -ForegroundColor Yellow
