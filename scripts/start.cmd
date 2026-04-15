@echo off
setlocal EnableExtensions
cd /d "%~dp0.."
set "ROOT=%cd%"

set "PIDDIR=%ROOT%\scripts\.pids"
set "RUNNERDIR=%ROOT%\scripts\.runners"

:: Подготовка окружения
for %%d in ("%PIDDIR%" "%RUNNERDIR%" "nginx\logs") do if not exist "%%~d" mkdir "%%~d"
del /q "%PIDDIR%\*.pid" "%RUNNERDIR%\*.cmd" >nul 2>&1

:: Проверка необходимости сборки
set "DO_BUILD=0"
if /I "%~1"=="rebuild" set "DO_BUILD=1"
if not exist "Valuator\bin\Debug\net8.0\Valuator.dll" set "DO_BUILD=1"

if "%DO_BUILD%"=="1" (
    echo [BUILD] Сборка компонентов системы...
    dotnet build Valuator\Valuator.csproj && dotnet build RankCalculator\RankCalculator.csproj || exit /b 1
)

:: Инфраструктура в Docker
echo [DOCKER] Перезапуск контейнеров (Redis, RabbitMQ, Nginx)...
docker rm -f my-redis my-rabbitmq my-nginx-lb >nul 2>&1
docker run -d --name my-redis -p 6379:6379 redis:7-alpine >nul
docker run -d --name my-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management >nul
docker run -d --name my-nginx-lb -p 8080:8080 -v "%ROOT%\nginx\conf\nginx.conf:/etc/nginx/nginx.conf:ro" -v "%ROOT%\nginx\logs:/logs" nginx:alpine >nul

echo Ожидание инициализации RabbitMQ...
timeout /t 15 /nobreak >nul

:: Запуск сервисов через вспомогательную функцию
call :Launch valuator-5001 "Valuator" "dotnet run --no-build --urls http://0.0.0.0:5001"
call :Launch valuator-5002 "Valuator" "dotnet run --no-build --urls http://0.0.0.0:5002"
call :Launch rank-1 "RankCalculator" "dotnet run --no-build"
call :Launch rank-2 "RankCalculator" "dotnet run --no-build"

echo.
echo === СИСТЕМА ЗАПУЩЕНА ===
echo Адрес: http://localhost:8080
pause
exit /b 0

:Launch
set "NAME=%~1"
set "FOLDER=%~2"
set "EXEC_CMD=%~3"
set "FILE=%RUNNERDIR%\%NAME%.cmd"

echo [+] Стартуем %NAME%...
(
    echo @echo off
    echo title %NAME%
    echo cd /d "%ROOT%\%FOLDER%"
    echo %EXEC_CMD%
) > "%FILE%"

for /f %%P in ('powershell -NoProfile -Command "$p = Start-Process cmd.exe -ArgumentList '/k', '\"%FILE%\"' -PassThru; $p.Id"') do (
    echo %%P > "%PIDDIR%\%NAME%.pid"
)
exit /b 0