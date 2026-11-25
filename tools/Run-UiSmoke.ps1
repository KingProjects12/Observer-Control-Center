$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$exe = (Resolve-Path 'dist/Observer.ControlCenter.exe').Path
$log = Join-Path ([IO.Path]::GetTempPath()) ("ObserverUiSmoke-" + [Guid]::NewGuid().ToString('N') + ".txt")
$process = Start-Process -FilePath $exe -ArgumentList @('--ui-smoke', ('"' + $log + '"')) -PassThru
try {
    if (!$process.WaitForExit(90000)) { $process.Kill(); throw 'UI smoke timed out' }
    if (Test-Path $log) { Get-Content $log }
    if ($process.ExitCode -ne 0 -or !(Test-Path $log) -or (Get-Content $log -Raw) -notlike 'PASS:*') { throw 'UI smoke failed' }
} finally { Remove-Item $log -Force -ErrorAction SilentlyContinue; $process.Dispose() }
