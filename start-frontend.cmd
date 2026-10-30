@echo off
setlocal
chcp 65001 >nul

cd /d "%~dp0frontend"

where node >nul 2>nul
if errorlevel 1 (
  echo [ERROR] node.exe not found. Install Node.js 20 or newer first.
  pause
  exit /b 1
)

if not exist "node_modules" (
  echo [frontend] node_modules missing, running npm install ...
  call npm install --no-audit --no-fund
  if errorlevel 1 (
    echo [ERROR] npm install failed
    pause
    exit /b 1
  )
)

echo [frontend] starting on http://localhost:5173 ...
echo [frontend] /api requests are proxied to http://localhost:8080
call npm run dev
set "EXITCODE=%errorlevel%"

if not "%EXITCODE%"=="0" (
  echo.
  echo [ERROR] frontend exited with code %EXITCODE%
  pause
)
exit /b %EXITCODE%
