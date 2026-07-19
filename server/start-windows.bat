@echo off
REM ----------------------------------------------------------------------
REM  This file re-launches itself inside a window that CANNOT auto-close.
REM  Double-clicking a .bat normally runs it as "cmd /c", so the window
REM  disappears the instant the script ends OR crashes - the user never
REM  gets to read the error. Re-running with "cmd /k" keeps the window
REM  open no matter what happens inside (success, error, or crash).
REM ----------------------------------------------------------------------
if /i not "%~1"=="RUN" (
  start "Pump Yaqobi Server" cmd /k "%~f0" RUN
  exit /b
)

setlocal enabledelayedexpansion

REM ----------------------------------------------------------------------
REM  This file re-launches itself inside a window that CANNOT auto-close.
REM  Double-clicking a .bat normally runs it as "cmd /c", so the window
REM  disappears the instant the script ends OR crashes - the user never
REM  gets to read the error. Re-running with "cmd /k" keeps the window
REM  open no matter what happens inside (success, error, or crash).
REM
REM  NOTE: %~f0 and %~dp0 are captured into plain variables FIRST (outside
REM  any parenthesized block) and referenced later with !delayed! syntax.
REM  If the folder is placed somewhere like "New folder (2)\...", the
REM  parenthesis in the path breaks cmd's block parser when expanded
REM  directly inside a "( ... )" block - delayed expansion avoids that.
REM ----------------------------------------------------------------------
set "SELF=%~f0"
if /i not "%~1"=="RUN" (
  start "Pump Yaqobi Server" cmd /k "!SELF!" RUN
  exit /b
)

title Pump Yaqobi Server
cd /d "%~dp0"
set "HERE=%~dp0"
set "PF86=%ProgramFiles(x86)%"

echo ============================================
echo    Pump Yaqobi - Personal Server
echo ============================================
echo.
echo Looking for Node.js ...
echo.

set "NODE="

REM 1) node on PATH (most common)
call node --version >nul 2>nul && set "NODE=node"

REM 2) via where
if not defined NODE (
  for /f "delims=" %%p in ('where node 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM 3) standard install locations
if not defined NODE if exist "%ProgramFiles%\nodejs\node.exe" set "NODE=%ProgramFiles%\nodejs\node.exe"
if not defined NODE if exist "%PF86%\nodejs\node.exe" set "NODE=%PF86%\nodejs\node.exe"
if not defined NODE if exist "%LOCALAPPDATA%\Programs\nodejs\node.exe" set "NODE=%LOCALAPPDATA%\Programs\nodejs\node.exe"

REM 4) winget links
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "%LOCALAPPDATA%\Microsoft\WinGet\Links\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM 5) nvm-windows
if not defined NODE if exist "%APPDATA%\nvm\node.exe" set "NODE=%APPDATA%\nvm\node.exe"
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "%APPDATA%\nvm\v*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM 6) scoop
if not defined NODE if exist "%USERPROFILE%\scoop\apps\nodejs\current\node.exe" set "NODE=%USERPROFILE%\scoop\apps\nodejs\current\node.exe"
if not defined NODE if exist "%USERPROFILE%\scoop\apps\nodejs-lts\current\node.exe" set "NODE=%USERPROFILE%\scoop\apps\nodejs-lts\current\node.exe"

REM 7) chocolatey
if not defined NODE if exist "%ProgramData%\chocolatey\bin\node.exe" set "NODE=%ProgramData%\chocolatey\bin\node.exe"

REM 8) portable version this script downloaded before
if not defined NODE (
  for /f "delims=" %%p in ('dir /b /s "!HERE!node-portable\*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

REM 9) drive letter scan
if not defined NODE (
  for %%d in (C D E F G H) do (
    if not defined NODE if exist "%%d:\Program Files\nodejs\node.exe" set "NODE=%%d:\Program Files\nodejs\node.exe"
    if not defined NODE if exist "%%d:\nodejs\node.exe" set "NODE=%%d:\nodejs\node.exe"
  )
)

REM If Node was not found, auto-download a portable copy (no install, no admin)
if not defined NODE (
  echo Node.js was not found on this computer.
  echo No problem - downloading a portable copy automatically. No install needed.
  echo.
  call :download_node
  for /f "delims=" %%p in ('dir /b /s "!HERE!node-portable\*\node.exe" 2^>nul') do if not defined NODE set "NODE=%%p"
)

if not defined NODE (
  echo.
  echo [ERROR] Could not prepare Node.js automatically (maybe no internet).
  echo.
  echo Two options:
  echo   1^) Connect to the internet and run this file again.
  echo   2^) Or install Node.js LTS from nodejs.org and run this file again.
  echo.
  pause
  exit /b
)

echo.
echo Node is ready:
echo    %NODE%
echo.

REM Open port 8787 in Windows Firewall so the phone can connect.
REM Works only if run as Administrator; otherwise it is skipped silently.
netsh advfirewall firewall show rule name="Pump Yaqobi Server 8787" >nul 2>nul
if errorlevel 1 (
  netsh advfirewall firewall add rule name="Pump Yaqobi Server 8787" dir=in action=allow protocol=TCP localport=8787 >nul 2>nul
  if not errorlevel 1 echo [Firewall] Port 8787 opened for phone connections.
)

echo Starting the server ...
echo (To stop it, close this window or press Ctrl+C)
echo.
"%NODE%" server.js

echo.
echo Server stopped.
pause
exit /b

REM ==================================================================
REM  Subroutine: download a portable Node.js (no install)
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

echo Downloading Node.js (%NPKG%) ... this may take a few minutes.
curl -L --fail -o "%NZIP%" "%NURL%" 2>nul
if not exist "!NZIP!" (
  powershell -NoProfile -Command "try { Invoke-WebRequest -Uri '!NURL!' -OutFile '!NZIP!' } catch { exit 1 }"
)
if not exist "!NZIP!" (
  echo [ERROR] Download failed.
  goto :eof
)

echo Extracting ...
tar -xf "%NZIP%" -C "%NDIR%" 2>nul
if not exist "!NDIR!\%NPKG%\node.exe" (
  powershell -NoProfile -Command "try { Expand-Archive -Force '!NZIP!' '!NDIR!' } catch { exit 1 }"
)
del "%NZIP%" >nul 2>nul
echo Node.js download complete.
goto :eof
