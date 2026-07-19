@echo off
chcp 65001 >nul
cd /d "%~dp0"

where node >/dev/null 2>nul
if errorlevel 1 (
  echo Node.js yaft nashod. Node 22+ ra az https://nodejs.org nasb konid.
  pause
  exit /b 1
)

echo Server WebVault Manager dar hale ejra...
node src\server.js
pause
