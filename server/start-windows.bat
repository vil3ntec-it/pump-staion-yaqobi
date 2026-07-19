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

REM --- 9) نسخهٔ «قابل‌حمل» که همین اسکریپت قبلاً دانلود کرده ---
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "%~dp0node-portable\*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM --- 10) جست‌وجوی درایوها (برای اطمینان) ---
if not defined NODE (
  for %%d in (C D E F G H) do (
    if not defined NODE if exist "%%d:\Program Files\nodejs\node.exe" set "NODE=%%d:\Program Files\nodejs\node.exe"
    if not defined NODE if exist "%%d:\nodejs\node.exe" set "NODE=%%d:\nodejs\node.exe"
  )
)

REM ------------------------------------------------------------------
REM  اگر Node پیدا نشد: نسخهٔ «قابل‌حمل» را خودکار دانلود کن
REM  (بدون نصب، بدون دسترسی مدیر — فقط یک‌بار دانلود می‌شود)
REM ------------------------------------------------------------------
if not defined NODE (
  echo Node.js روی این کامپیوتر پیدا نشد.
  echo نگران نباش — نسخهٔ «قابل‌حمل» را خودکار دانلود می‌کنم. نیازی به نصب نیست.
  echo.
  call :download_node
  REM بعد از دانلود، دوباره دنبال node.exe بگرد
  for /f "delims=" %%p in ('dir /b /s "%~dp0node-portable\*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

if not defined NODE (
  echo.
  echo [خطا] نتوانستم Node.js را خودکار آماده کنم (شاید اینترنت وصل نبود).
  echo.
  echo دو راه:
  echo   ۱) اینترنت را وصل کن و دوباره روی همین فایل دوبار کلیک کن.
  echo   ۲) یا Node.js را از nodejs.org نسخهٔ LTS نصب کن و دوباره اجرا کن.
  echo.
  pause
  exit /b
)

echo.
echo Node آماده است:
echo    %NODE%
echo.

REM --- باز کردن پورت 8787 در فایروال ویندوز (تا گوشی بتواند وصل شود) ---
REM اگر با دسترسی مدیر اجرا شده باشد، قانون فایروال ساخته می‌شود؛ وگرنه بی‌صدا رد می‌شود.
netsh advfirewall firewall show rule name="Pump Yaqobi Server 8787" >nul 2>nul
if errorlevel 1 (
  netsh advfirewall firewall add rule name="Pump Yaqobi Server 8787" dir=in action=allow protocol=TCP localport=8787 >nul 2>nul
  if not errorlevel 1 echo [فایروال] پورت 8787 برای اتصال از گوشی باز شد.
)

echo در حال اجرای سرور ...
echo (برای توقف، این پنجره را ببند یا Ctrl+C بزن)
echo.
"%NODE%" server.js

echo.
echo سرور متوقف شد.
pause
exit /b

REM ==================================================================
REM  زیربرنامه: دانلود نسخهٔ قابل‌حمل Node.js (بدون نصب)
REM ==================================================================
:download_node
set "NVER=v20.18.1"
set "NARCH=x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "NARCH=arm64"
if /i "%PROCESSOR_ARCHITECTURE%"=="x86" set "NARCH=x86"
set "NPKG=node-%NVER%-win-%NARCH%"
set "NURL=https://nodejs.org/dist/%NVER%/%NPKG%.zip"
set "NDIR=%~dp0node-portable"
set "NZIP=%NDIR%\node.zip"

if not exist "%NDIR%" mkdir "%NDIR%"

echo در حال دانلود Node.js (%NPKG%) ... این ممکن است چند دقیقه طول بکشد.
REM اول با curl (روی ویندوز ۱۰/۱۱ هست)
curl -L --fail -o "%NZIP%" "%NURL%" 2>nul
if not exist "%NZIP%" (
  REM اگر curl نبود/نشد، با PowerShell
  powershell -NoProfile -Command "try { Invoke-WebRequest -Uri '%NURL%' -OutFile '%NZIP%' } catch { exit 1 }"
)
if not exist "%NZIP%" (
  echo [خطا] دانلود ناموفق بود.
  goto :eof
)

echo در حال باز کردن فایل ...
REM اول با tar (روی ویندوز ۱۰/۱۱ هست)
tar -xf "%NZIP%" -C "%NDIR%" 2>nul
if not exist "%NDIR%\%NPKG%\node.exe" (
  REM اگر tar نشد، با PowerShell
  powershell -NoProfile -Command "try { Expand-Archive -Force '%NZIP%' '%NDIR%' } catch { exit 1 }"
)
del "%NZIP%" >nul 2>nul
echo دانلود Node.js تمام شد.
goto :eof
