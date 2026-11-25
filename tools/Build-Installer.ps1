param(
    [Parameter(Mandatory=$true)][string]$Version,
    [switch]$InstallInno
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid application version: $Version"
}
$exe = Join-Path (Get-Location) 'dist\Observer.ControlCenter.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Publish Observer.ControlCenter.exe to dist before building the installer."
}
function Find-Iscc {
    $candidateDirectories = @(
        (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6'),
        (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 7'),
        (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Inno Setup 7')
    )
    foreach ($dir in $candidateDirectories) {
        $test = Join-Path $dir 'ISCC.exe'
        if (Test-Path $test -PathType Leaf) { return $test }
    }
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }
    return $null
}
$iscc = Find-Iscc
if ($iscc) {
    $compilerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($iscc)
    if ([version]::new($compilerVersion.FileMajorPart,$compilerVersion.FileMinorPart) -lt [version]"6.6") { $iscc = $null }
}
if (!$iscc -and $InstallInno) {
    Write-Host '[INSTALL] Fetching Inno Setup using Chocolatey on the GitHub Windows runner.'
    & choco upgrade innosetup -y --no-progress --limit-output
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup installation failed: $LASTEXITCODE" }
    $iscc = Find-Iscc
}
if (!$iscc) { throw 'Inno Setup 6.6+ required for dark setup; install it from jrsoftware.org or enable -InstallInno on CI.' }
$compilerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($iscc)
if ([version]::new($compilerVersion.FileMajorPart,$compilerVersion.FileMinorPart) -lt [version]'6.6') { throw 'Inno Setup 6.6+ required.' }
& "$PSScriptRoot\Generate-InstallerArt.ps1"
Write-Host "[BUILD] Inno Setup compiler: $iscc"
& $iscc "/Q" "/DMyAppVersion=$Version" 'installer\Observer.iss'
if ($LASTEXITCODE -ne 0) { throw "ISCC exited with code $LASTEXITCODE" }
$setup = Join-Path (Get-Location) "dist\Observer-Control-Center-Setup-$Version-win-x64.exe"
if (!(Test-Path $setup -PathType Leaf)) { throw "Installer missing: $setup" }
Write-Host "[SUCCESS] Installer built: $setup"
