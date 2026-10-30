@echo off
setlocal
set "ACTION=backend"
if not "%~1"=="" set "ACTION=%~1"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\start-inspection.ps1" %ACTION%
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo.
  echo Launcher exit code: %EXITCODE%
  pause
)
endlocal & exit /b %EXITCODE%
