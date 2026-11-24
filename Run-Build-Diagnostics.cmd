@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-Build-Diagnostics.ps1"
if errorlevel 1 (
  echo.
  echo BUILD FAILED - send the Observer_v2_0_3_Build_*.txt log from Desktop.
) else (
  echo.
  echo BUILD SUCCESS - dist\Observer.ControlCenter.exe
)
pause
