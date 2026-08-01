@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync-licenses.ps1"
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
  echo.
  echo sync-licenses failed with exit code %EXITCODE%.
  pause
  exit /b %EXITCODE%
)

echo.
pause
exit /b 0
