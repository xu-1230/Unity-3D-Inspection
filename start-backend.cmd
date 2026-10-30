@echo off
setlocal
chcp 65001 >nul
set "GOROOT=D:\ctf\go"
set "PATH=%GOROOT%\bin;%PATH%"

cd /d "%~dp0backend"

where go >nul 2>nul
if errorlevel 1 (
  echo [ERROR] go.exe not found. Expected at %GOROOT%\bin\go.exe
  echo         Install Go or adjust GOROOT at the top of this script.
  pause
  exit /b 1
)

if not exist "data" mkdir "data"

echo [backend] starting on http://localhost:8080 ...
echo [backend] data file: %CD%\data\issues.json
go run ./cmd/server
set "EXITCODE=%errorlevel%"

if not "%EXITCODE%"=="0" (
  echo.
  echo [ERROR] backend exited with code %EXITCODE%
  pause
)
exit /b %EXITCODE%
