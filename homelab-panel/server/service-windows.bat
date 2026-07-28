@echo off
REM ---------------------------------------------------------------------------
REM  پنل را طوری نصب می‌کند که همیشه در پس‌زمینه بالا باشد:
REM  با بستنِ ترمینال خاموش نمی‌شود و با روشن شدنِ کامپیوتر خودش بالا می‌آید.
REM
REM      service-windows.bat install     نصب + اجرای همین حالا
REM      service-windows.bat start       فقط اجرا
REM      service-windows.bat stop        توقف
REM      service-windows.bat status      وضعیت
REM      service-windows.bat uninstall   حذف از راه‌اندازی خودکار
REM
REM  به دسترسی مدیر نیازی ندارد.
REM ---------------------------------------------------------------------------
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "TASK=PumpYaqobiPanel"
set "HERE=%~dp0"
if "%HERE:~-1%"=="\" set "HERE=%HERE:~0,-1%"
set "PIDFILE=%HERE%\data\panel.pid"

set "ACTION=%~1"
if "%ACTION%"=="" set "ACTION=install"

if /i "%ACTION%"=="install"   goto :install
if /i "%ACTION%"=="start"     goto :start
if /i "%ACTION%"=="stop"      goto :stop
if /i "%ACTION%"=="status"    goto :status
if /i "%ACTION%"=="uninstall" goto :uninstall

echo.
echo   Usage: service-windows.bat [install^|start^|stop^|status^|uninstall]
echo.
exit /b 1

:install
echo.
echo   Registering the panel to start automatically at logon...
schtasks /create /tn "%TASK%" /tr "wscript.exe \"%HERE%\run-hidden.vbs\"" /sc onlogon /f >nul
if errorlevel 1 (
  echo   [X] Could not register the scheduled task.
  echo       You can still start it manually with: service-windows.bat start
  pause
  exit /b 1
)
echo   [OK] Registered. It will come up by itself every time you log in.
goto :start

:start
call :isrunning
if "%RUNNING%"=="1" (
  echo   Already running ^(PID !PID!^).
  goto :done
)
echo   Starting the panel in the background...
start "" wscript.exe "%HERE%\run-hidden.vbs"
REM چند ثانیه فرصت بده تا بالا بیاید و فایل PID را بنویسد
for /l %%i in (1,1,20) do (
  timeout /t 1 /nobreak >nul
  if exist "%PIDFILE%" goto :started
)
:started
call :isrunning
if "%RUNNING%"=="1" (
  echo   [OK] Running ^(PID !PID!^). You can close this window - it keeps running.
) else (
  echo   [!] Did not come up yet. Check data\panel.log
)
goto :done

:stop
call :isrunning
if not "%RUNNING%"=="1" (
  echo   Not running.
  goto :done
)
echo   Stopping PID !PID! ...
taskkill /pid !PID! /t /f >nul 2>nul
del /q "%PIDFILE%" 2>nul
echo   [OK] Stopped.
goto :done

:status
call :isrunning
if "%RUNNING%"=="1" (echo   Running ^(PID !PID!^)) else (echo   Not running)
schtasks /query /tn "%TASK%" >nul 2>nul
if errorlevel 1 (echo   Autostart: not registered) else (echo   Autostart: registered)
goto :done

:uninstall
call :stop
schtasks /delete /tn "%TASK%" /f >nul 2>nul
echo   [OK] Removed from autostart.
goto :done

REM --------------------------- کمکی: آیا بالاست؟ -----------------------------
:isrunning
set "RUNNING=0"
set "PID="
if not exist "%PIDFILE%" exit /b 0
set /p PID=<"%PIDFILE%"
if "!PID!"=="" exit /b 0
tasklist /fi "PID eq !PID!" 2>nul | find "!PID!" >nul
if not errorlevel 1 set "RUNNING=1"
exit /b 0

:done
echo.
if /i not "%ACTION%"=="status" pause
exit /b 0
