@echo off
setlocal
set "SUB=%~1"
if "%SUB%"=="" set "SUB=up"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\start-inspection.ps1" docker %SUB%
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo.
  echo Launcher exit code: %EXITCODE%
  pause
)
endlocal & exit /b %EXITCODE%
