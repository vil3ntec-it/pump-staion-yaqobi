@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
title سرور پمپ یعقوبی
cd /d "%~dp0"

echo ============================================
echo    سرور شخصی پمپ یعقوبی
echo ============================================
echo.
echo در حال پیدا کردن Node.js ...
echo.

set "NODE="

REM --- 1) اگر node روی PATH باشد (رایج‌ترین حالت) ---
call node --version >nul 2>nul && set "NODE=node"

REM --- 2) با دستور where بگرد ---
if not defined NODE (
  for /f "delims=" %%p in ('where node 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM --- 3) مسیرهای نصب استاندارد (نصب‌کنندهٔ رسمی) ---
if not defined NODE if exist "%ProgramFiles%\nodejs\node.exe" set "NODE=%ProgramFiles%\nodejs\node.exe"
if not defined NODE if exist "%ProgramFiles(x86)%\nodejs\node.exe" set "NODE=%ProgramFiles(x86)%\nodejs\node.exe"
if not defined NODE if exist "%LOCALAPPDATA%\Programs\nodejs\node.exe" set "NODE=%LOCALAPPDATA%\Programs\nodejs\node.exe"

REM --- 4) نصب با winget (لینک‌های داخل WinGet) ---
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "%LOCALAPPDATA%\Microsoft\WinGet\Links\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM --- 5) nvm-windows ---
if not defined NODE if exist "%APPDATA%\nvm\node.exe" set "NODE=%APPDATA%\nvm\node.exe"
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "%APPDATA%\nvm\v*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM --- 6) scoop ---
if not defined NODE if exist "%USERPROFILE%\scoop\apps\nodejs\current\node.exe" set "NODE=%USERPROFILE%\scoop\apps\nodejs\current\node.exe"
if not defined NODE if exist "%USERPROFILE%\scoop\apps\nodejs-lts\current\node.exe" set "NODE=%USERPROFILE%\scoop\apps\nodejs-lts\current\node.exe"

REM --- 7) chocolatey ---
if not defined NODE if exist "%ProgramData%\chocolatey\bin\node.exe" set "NODE=%ProgramData%\chocolatey\bin\node.exe"

REM --- 8) fnm ---
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "%LOCALAPPDATA%\fnm_multishells\*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM --- 9) جست‌وجوی درایوها (مثل نسخهٔ قبلی، برای اطمینان) ---
if not defined NODE (
  for %%d in (C D E F G H) do (
    if not defined NODE if exist "%%d:\Program Files\nodejs\node.exe" set "NODE=%%d:\Program Files\nodejs\node.exe"
    if not defined NODE if exist "%%d:\nodejs\node.exe" set "NODE=%%d:\nodejs\node.exe"
  )
)

REM ------------------------------------------------------------------
REM  اگر Node پیدا نشد: پیشنهاد نصب خودکار با winget
REM ------------------------------------------------------------------
if not defined NODE (
  echo [خطا] Node.js پیدا نشد.
  echo.
  where winget >nul 2>nul
  if not errorlevel 1 (
    echo این کامپیوتر winget دارد و می‌تواند Node.js را خودکار نصب کند.
    set /p ANS="می‌خواهی همین حالا Node.js را خودکار نصب کنم؟ (y = بله) : "
    if /i "!ANS!"=="y" (
      echo.
      echo در حال نصب Node.js LTS ... ممکن است چند دقیقه طول بکشد.
      winget install -e --id OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements
      echo.
      echo نصب تمام شد. لطفا این پنجره را ببند و «start-windows.bat» را دوباره اجرا کن.
      echo (اگر باز هم پیدا نشد، کامپیوتر را یک‌بار ری‌استارت کن.)
      echo.
      pause
      exit /b
    )
  )
  echo.
  echo لطفا Node.js را از nodejs.org با «فایل نصب‌کننده (Installer)» نصب کن،
  echo نسخهٔ LTS را انتخاب کن، نصب پیش‌فرض را بزن و بعد کامپیوتر را یک‌بار ری‌استارت کن،
  echo سپس دوباره این فایل را اجرا کن.
  echo.
  echo اگر می‌دانی node.exe کجاست، به‌جای این فایل می‌توانی در cmd این را بزنی:
  echo    "مسیر-کامل-node.exe" server.js
  echo.
  pause
  exit /b
)

echo Node پیدا شد:
echo    %NODE%
echo در حال اجرای سرور ...
echo (برای توقف، این پنجره را ببند یا Ctrl+C بزن)
echo.
"%NODE%" server.js

echo.
echo سرور متوقف شد.
pause
