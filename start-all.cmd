@echo off
setlocal
chcp 65001 >nul

echo [all] checking ports ...
netstat -ano | findstr ":8080 " | findstr LISTENING >nul
if errorlevel 1 (
  start "unity-backend" cmd /k "%~dp0start-backend.cmd"
) else (
  echo [all] port 8080 already in use, skip backend
)

timeout /t 3 /nobreak >nul

netstat -ano | findstr ":5173 " | findstr LISTENING >nul
if errorlevel 1 (
  start "unity-frontend" cmd /k "%~dp0start-frontend.cmd"
) else (
  echo [all] port 5173 already in use, skip frontend
)

timeout /t 5 /nobreak >nul
start "" "http://localhost:5173"

echo [all] backend  http://localhost:8080/api/health
echo [all] frontend http://localhost:5173
echo [all] close the spawned windows to stop the services
exit /b 0
