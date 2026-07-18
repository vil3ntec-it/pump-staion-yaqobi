@echo off
chcp 65001 >nul
title سرور پمپ یعقوبی
cd /d "%~dp0"

echo ============================================
echo    سرور شخصی پمپ یعقوبی
echo ============================================
echo.

REM اگر وابستگی‌ها نصب نشده‌اند، نصب کن
if not exist "node_modules\ws" (
  echo نصب وابستگی‌ها برای اولین بار...
  call npm install
  echo.
)

REM اگر فایل .env نیست، از روی نمونه بساز و هشدار بده
if not exist ".env" (
  echo [!] فایل .env پیدا نشد. از روی .env.example کپی می‌شود...
  copy ".env.example" ".env" >nul
  echo [!] لطفا فایل .env را باز کنید و AUTH_TOKEN و DATABASE_URL را درست کنید،
  echo [!] سپس این فایل را دوباره اجرا کنید.
  pause
  exit /b
)

echo در حال اجرای سرور... برای توقف، این پنجره را ببندید یا Ctrl+C بزنید.
echo.
node server.js

echo.
echo سرور متوقف شد.
pause
