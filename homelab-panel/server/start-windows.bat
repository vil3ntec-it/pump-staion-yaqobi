@echo off
REM Home server panel launcher (Windows)
setlocal
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo.
  echo   Node.js not found. Install Node.js 22 or newer from https://nodejs.org
  echo.
  pause
  exit /b 1
)

if not exist node_modules (
  echo.
  echo   Installing dependencies ^(first run only^)...
  echo.
  call npm install --omit=dev --no-audit --no-fund
  if errorlevel 1 (
    echo   Install failed. Check your internet connection.
    pause
    exit /b 1
  )
)

node --disable-warning=ExperimentalWarning src\index.js
pause
