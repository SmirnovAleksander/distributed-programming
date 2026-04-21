@echo off
setlocal EnableExtensions

cd /d "%~dp0.."
set "ROOT=%cd%"
set "PIDDIR=%ROOT%\scripts\.pids"

echo Closing saved process windows...
call :killPid "%PIDDIR%\valuator-5001.pid"
call :killPid "%PIDDIR%\valuator-5002.pid"
call :killPid "%PIDDIR%\rank-1.pid"
call :killPid "%PIDDIR%\rank-2.pid"

echo Fallback: stopping Valuator by ports...
call :killPort 5001
call :killPort 5002

echo Stopping Docker containers...
docker stop nginx-lb >nul 2>&1
docker stop pa3-rabbitmq >nul 2>&1
docker stop pa3-redis >nul 2>&1

echo Done.
pause
exit /b 0

:killPid
if not exist "%~1" exit /b 0
set /p PID=<"%~1"
if not "%PID%"=="" (
    taskkill /PID %PID% /T >nul 2>&1
    timeout /t 1 /nobreak >nul
    taskkill /PID %PID% /T /F >nul 2>&1
)
del /q "%~1" >nul 2>&1
exit /b 0

:killPort
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%~1 .*LISTENING"') do (
    taskkill /PID %%P /T >nul 2>&1
    timeout /t 1 /nobreak >nul
    taskkill /PID %%P /T /F >nul 2>&1
)
exit /b 0