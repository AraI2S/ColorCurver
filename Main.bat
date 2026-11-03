@echo off
setlocal DisableDelayedExpansion
pushd "%~dp0" || (
  echo Cannot open the application folder.
  pause
  exit /b 1
)
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Main.ps1" %*
set "ExitCode=%ERRORLEVEL%"
popd
echo.
echo Press any key to close...
pause >nul
exit /b %ExitCode%
