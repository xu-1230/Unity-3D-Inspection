@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul

set "PROJECT=%~dp0unity"
set "HUB=%ProgramFiles%\Unity Hub\Unity Hub.exe"

if not exist "%PROJECT%\ProjectSettings\ProjectVersion.txt" (
  echo [ERROR] Unity project files missing: %PROJECT%\ProjectSettings\ProjectVersion.txt
  pause
  exit /b 1
)

set "EDITOR="
if exist "%ProgramFiles%\Unity\Hub\Editor" (
  for /f "delims=" %%v in ('dir /b /ad /o-n "%ProgramFiles%\Unity\Hub\Editor" 2^>nul') do (
    if not defined EDITOR (
      if exist "%ProgramFiles%\Unity\Hub\Editor\%%v\Editor\Unity.exe" (
        set "EDITOR=%ProgramFiles%\Unity\Hub\Editor\%%v\Editor\Unity.exe"
      )
    )
  )
)

if defined EDITOR (
  echo [unity] opening project with !EDITOR!
  start "" "!EDITOR!" -projectPath "%PROJECT%"
  exit /b 0
)

if exist "%HUB%" (
  echo [unity] no Unity Editor found under %ProgramFiles%\Unity\Hub\Editor
  echo [unity] opening Unity Hub, then add project manually: %PROJECT%
  start "" "%HUB%"
  exit /b 0
)

echo [ERROR] Neither Unity Editor nor Unity Hub was found.
echo         Install Unity Hub and a Unity LTS editor, then re-run this script.
echo         Project path: %PROJECT%
pause
exit /b 1
