@echo off
chcp 65001 >nul
title سرور پمپ یعقوبی
cd /d "%~dp0"

echo ============================================
echo    سرور شخصی پمپ یعقوبی
echo ============================================
echo.

REM --- پیدا کردن Node.js حتی اگر در PATH نباشد (بدون نیاز به ری‌استارت) ---
if exist "%ProgramFiles%\nodejs\node.exe" set "PATH=%ProgramFiles%\nodejs;%PATH%"
if exist "%ProgramW6432%\nodejs\node.exe" set "PATH=%ProgramW6432%\nodejs;%PATH%"
if exist "%LocalAppData%\Programs\nodejs\node.exe" set "PATH=%LocalAppData%\Programs\nodejs;%PATH%"
if exist "C:\Program Files\nodejs\node.exe" set "PATH=C:\Program Files\nodejs;%PATH%"

REM --- بررسی: آیا Node پیدا شد؟ ---
node --version >nul 2>nul
if errorlevel 1 (
  echo [خطا] Node.js پیدا نشد.
  echo.
  echo لطفا مطمئن شوید Node.js را از nodejs.org نصب کرده‌اید.
  echo اگر تازه نصب کرده‌اید، یک بار کامپیوتر را ری‌استارت کنید و دوباره این فایل را اجرا کنید.
  echo.
  pause
  exit /b
)

for /f "delims=" %%v in ('node --version') do echo Node.js پیدا شد: %%v
echo.

REM --- نصب وابستگی‌ها (فقط بار اول) ---
if not exist "node_modules\ws" (
  echo نصب وابستگی‌ها برای اولین بار... (کمی صبر کنید، به اینترنت نیاز دارد)
  call npm install
  echo.
)

REM --- ساخت فایل تنظیمات در صورت نبود ---
if not exist ".env" (
  echo [!] فایل .env پیدا نشد. از روی نمونه ساخته شد.
  copy ".env.example" ".env" >nul
  echo.
  echo ============================================
  echo  لطفا فایل  .env  را با Notepad باز کنید و دو خط زیر را پر کنید:
  echo     AUTH_TOKEN=یک رمز دلخواه و طولانی
  echo     DB_PASSWORD=رمز postgres که موقع نصب گذاشتید
  echo  سپس همین start-windows را دوباره اجرا کنید.
  echo ============================================
  echo.
  pause
  exit /b
)

echo در حال اجرای سرور... برای توقف، این پنجره را ببندید یا Ctrl+C بزنید.
echo.
node server.js

echo.
echo سرور متوقف شد.
pause
