@echo off
setlocal EnableExtensions

cd /d "%~dp0.."
set "ROOT=%cd%"

set "VALUATOR_DIR=%ROOT%\Valuator"
set "RANK_DIR=%ROOT%\RankCalculator"
set "EVENTSLOGGER_DIR=%ROOT%\EventsLogger"

set "VALUATOR=%VALUATOR_DIR%\Valuator.csproj"
set "RANK=%RANK_DIR%\RankCalculator.csproj"
set "EVENTSLOGGER=%EVENTSLOGGER_DIR%\EventsLogger.csproj"

set "CONF=%ROOT%\nginx\conf\nginx.conf"
set "LOGS=%ROOT%\nginx\logs"

set "PIDDIR=%ROOT%\scripts\.pids"
set "RUNNERDIR=%ROOT%\scripts\.runners"

if not exist "%VALUATOR%" (
    echo ERROR: not found "%VALUATOR%"
    pause
    exit /b 1
)

if not exist "%RANK%" (
    echo ERROR: not found "%RANK%"
    pause
    exit /b 1
)

if not exist "%EVENTSLOGGER%" (
    echo ERROR: not found "%EVENTSLOGGER%"
    pause
    exit /b 1
)

if not exist "%CONF%" (
    echo ERROR: nginx.conf not found: "%CONF%"
    pause
    exit /b 1
)

if not exist "%LOGS%" mkdir "%LOGS%"
if not exist "%PIDDIR%" mkdir "%PIDDIR%"
if not exist "%RUNNERDIR%" mkdir "%RUNNERDIR%"

del /q "%PIDDIR%\*.pid" >nul 2>&1
del /q "%RUNNERDIR%\*.cmd" >nul 2>&1

if /I "%~1"=="rebuild" goto :build
if not exist "%VALUATOR_DIR%\bin\Debug\net8.0\Valuator.dll" goto :build
if not exist "%RANK_DIR%\bin\Debug\net8.0\RankCalculator.dll" goto :build
if not exist "%EVENTSLOGGER_DIR%\bin\Debug\net8.0\EventsLogger.exe" goto :build
goto :docker

:build
echo Building Valuator...
dotnet build "%VALUATOR%"
if errorlevel 1 (
    echo Valuator build failed
    pause
    exit /b 1
)

echo Building RankCalculator...
dotnet build "%RANK%"
if errorlevel 1 (
    echo RankCalculator build failed
    pause
    exit /b 1
)

echo Building EventsLogger...
dotnet build "%EVENTSLOGGER%"
if errorlevel 1 (
    echo EventsLogger build failed
    pause
    exit /b 1
)

:docker
echo Recreating Redis instances...
docker rm -f pa3-redis-main >nul 2>&1
docker run -d --name pa3-redis-main -p 6000:6379 redis:7-alpine >nul
if errorlevel 1 (
    echo Failed to start Redis-MAIN
    pause
    exit /b 1
)

docker rm -f pa3-redis-ru >nul 2>&1
docker run -d --name pa3-redis-ru -p 6001:6379 redis:7-alpine >nul
if errorlevel 1 (
    echo Failed to start Redis-RU
    pause
    exit /b 1
)

docker rm -f pa3-redis-eu >nul 2>&1
docker run -d --name pa3-redis-eu -p 6002:6379 redis:7-alpine >nul
if errorlevel 1 (
    echo Failed to start Redis-EU
    pause
    exit /b 1
)

docker rm -f pa3-redis-asia >nul 2>&1
docker run -d --name pa3-redis-asia -p 6003:6379 redis:7-alpine >nul
if errorlevel 1 (
    echo Failed to start Redis-ASIA
    pause
    exit /b 1
)

echo Recreating RabbitMQ...
docker rm -f pa3-rabbitmq >nul 2>&1
docker run -d --name pa3-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management >nul
if errorlevel 1 (
    echo Failed to start RabbitMQ
    pause
    exit /b 1
)

echo Recreating nginx-lb...
docker rm -f nginx-lb >nul 2>&1
docker run -d --name nginx-lb -p 8080:8080 ^
  -v "%CONF%:/etc/nginx/nginx.conf:ro" ^
  -v "%LOGS%:/logs" ^
  nginx:alpine >nul
if errorlevel 1 (
    echo Failed to start nginx-lb
    pause
    exit /b 1
)

echo Waiting for RabbitMQ...
timeout /t 20 /nobreak >nul

echo Creating runner files...

> "%RUNNERDIR%\valuator-5001.cmd" (
    echo @echo off
    echo title Valuator-5001
    echo set DB_MAIN=localhost:6000
    echo set DB_RU=localhost:6001
    echo set DB_EU=localhost:6002
    echo set DB_ASIA=localhost:6003
    echo cd /d "%VALUATOR_DIR%"
    echo dotnet run --no-build --urls http://0.0.0.0:5001
)

> "%RUNNERDIR%\valuator-5002.cmd" (
    echo @echo off
    echo title Valuator-5002
    echo set DB_MAIN=localhost:6000
    echo set DB_RU=localhost:6001
    echo set DB_EU=localhost:6002
    echo set DB_ASIA=localhost:6003
    echo cd /d "%VALUATOR_DIR%"
    echo dotnet run --no-build --urls http://0.0.0.0:5002
)

> "%RUNNERDIR%\rank-1.cmd" (
    echo @echo off
    echo title RankCalculator-1
    echo set DB_MAIN=localhost:6000
    echo set DB_RU=localhost:6001
    echo set DB_EU=localhost:6002
    echo set DB_ASIA=localhost:6003
    echo cd /d "%RANK_DIR%"
    echo dotnet run --no-build
)

> "%RUNNERDIR%\rank-2.cmd" (
    echo @echo off
    echo title RankCalculator-2
    echo set DB_MAIN=localhost:6000
    echo set DB_RU=localhost:6001
    echo set DB_EU=localhost:6002
    echo set DB_ASIA=localhost:6003
    echo cd /d "%RANK_DIR%"
    echo dotnet run --no-build
)

> "%RUNNERDIR%\eventslogger-1.cmd" (
    echo @echo off
    echo title EventsLogger-1
    echo cd /d "%EVENTSLOGGER_DIR%"
    echo dotnet run --no-build
)

> "%RUNNERDIR%\eventslogger-2.cmd" (
    echo @echo off
    echo title EventsLogger-2
    echo cd /d "%EVENTSLOGGER_DIR%"
    echo dotnet run --no-build
)

echo Starting Valuator-5001...
for /f %%P in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '""%RUNNERDIR%\valuator-5001.cmd""' -PassThru; $p.Id"') do set "PID=%%P"
> "%PIDDIR%\valuator-5001.pid" echo %PID%

echo Starting Valuator-5002...
for /f %%P in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '""%RUNNERDIR%\valuator-5002.cmd""' -PassThru; $p.Id"') do set "PID=%%P"
> "%PIDDIR%\valuator-5002.pid" echo %PID%

echo Starting RankCalculator-1...
for /f %%P in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '""%RUNNERDIR%\rank-1.cmd""' -PassThru; $p.Id"') do set "PID=%%P"
> "%PIDDIR%\rank-1.pid" echo %PID%

echo Starting RankCalculator-2...
for /f %%P in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '""%RUNNERDIR%\rank-2.cmd""' -PassThru; $p.Id"') do set "PID=%%P"
> "%PIDDIR%\rank-2.pid" echo %PID%

echo Starting EventsLogger-1...
for /f %%P in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '""%RUNNERDIR%\eventslogger-1.cmd""' -PassThru; $p.Id"') do set "PID=%%P"
> "%PIDDIR%\eventslogger-1.pid" echo %PID%

echo Starting EventsLogger-2...
for /f %%P in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '""%RUNNERDIR%\eventslogger-2.cmd""' -PassThru; $p.Id"') do set "PID=%%P"
> "%PIDDIR%\eventslogger-2.pid" echo %PID%

echo.
echo Done.
echo Open: http://localhost:8080
pause
exit /b 0