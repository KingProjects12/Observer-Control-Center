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
    $roots = @([Environment]::GetFolderPath('ProgramFiles'), [Environment]::GetFolderPath('ProgramFilesX86'), $env:ProgramW6432, $env:LOCALAPPDATA + '\Programs') | Where-Object { $_ } | Select-Object -Unique
    $paths = @()
    foreach ($root in $roots) {
        foreach ($folder in @('Inno Setup 7','Inno Setup 6')) {
            $candidate = Join-Path (Join-Path $root $folder) 'ISCC.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $paths += $candidate }
        }
    }
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $paths += $found.Source }
    $candidates = foreach ($candidate in ($paths | Select-Object -Unique)) {
        $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($candidate)
        $versions = @($info.ProductVersion, $info.FileVersion) | ForEach-Object {
            if ($_ -match '(\d+\.\d+(?:\.\d+){0,2})') { [version]$Matches[1] }
        }
        $v = $versions | Sort-Object -Descending | Select-Object -First 1
        if (!$v) { $v = [version]'0.0' }
        Write-Host "[COMPILER] $candidate | product=$($info.ProductVersion) file=$($info.FileVersion)"
        [pscustomobject]@{ Path = $candidate; Version = $v }
    }
    # The compiler itself validates the WizardStyle directive. Do not reject
    # valid installations solely because ISCC's PE version metadata is older.
    return ($candidates | Sort-Object Version -Descending | Select-Object -First 1).Path
}
if ($InstallInno) {
    Write-Host '[INSTALL] Ensure the current Inno Setup package is present.'
    & choco upgrade innosetup -y --no-progress --limit-output
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup installation failed: $LASTEXITCODE" }
}
$iscc = Find-Iscc
if (!$iscc) { throw 'Inno Setup 6.6+ required for dark setup; install it from jrsoftware.org or enable -InstallInno on CI.' }
& "$PSScriptRoot\Generate-InstallerArt.ps1"
Write-Host "[BUILD] Inno Setup compiler: $iscc"
& $iscc "/Q" "/DMyAppVersion=$Version" 'installer\Observer.iss'
if ($LASTEXITCODE -ne 0) { throw "ISCC exited with code $LASTEXITCODE" }
$setup = Join-Path (Get-Location) "dist\Observer-Control-Center-Setup-$Version-win-x64.exe"
if (!(Test-Path $setup -PathType Leaf)) { throw "Installer missing: $setup" }
Write-Host "[SUCCESS] Installer built: $setup"
