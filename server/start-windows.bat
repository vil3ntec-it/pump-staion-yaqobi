@echo off
chcp 65001 >nul
title سرور پمپ یعقوبی
cd /d "%~dp0"

echo ============================================
echo    سرور شخصی پمپ یعقوبی
echo ============================================
echo.

REM --- پیدا کردن Node.js ---
set "NODE="
call node --version >nul 2>nul && set "NODE=node"
if not defined NODE (
  for %%d in (C D E F G H) do (
    if exist "%%d:\Program Files\nodejs\node.exe" set "NODE=%%d:\Program Files\nodejs\node.exe"
    if exist "%%d:\nodejs\node.exe" set "NODE=%%d:\nodejs\node.exe"
  )
)

if not defined NODE (
  echo [خطا] Node.js پیدا نشد.
  echo.
  echo لطفا Node.js را از nodejs.org با «فایل نصب‌کننده» نصب کنید و کامپیوتر را ری‌استارت کنید،
  echo سپس دوباره این فایل را اجرا کنید.
  echo.
  echo اگر می‌دانید node.exe کجاست، به‌جای این فایل می‌توانید در cmd این را بزنید:
  echo    "مسیر-کامل-node.exe" server.js
  echo.
  pause
  exit /b
)

echo Node پیدا شد. در حال اجرای سرور...
echo (برای توقف، این پنجره را ببندید یا Ctrl+C بزنید)
echo.
"%NODE%" server.js

echo.
echo سرور متوقف شد.
pause
