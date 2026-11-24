@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-SingleExe.ps1"
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Send the error text to the developer.
) else (
  echo.
  echo READY: dist\Observer.ControlCenter.exe
)
pause
