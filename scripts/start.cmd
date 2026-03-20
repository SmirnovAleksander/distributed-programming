@echo off
chcp 65001 >nul
cd /d "%~dp0\.."

echo Запуск Valuator на портах 5001 и 5002...
echo Запуск RankCalculator (2 экземпляра)...
start "RankCalculator1" dotnet run --project RankCalculator
start "RankCalculator2" dotnet run --project RankCalculator

echo Ожидание запуска RankCalculator (5 сек)...
timeout /t 5 /nobreak >nul

start "Valuator5001" dotnet run --project Valuator --urls "http://0.0.0.0:5001"
start "Valuator5002" dotnet run --project Valuator --urls "http://0.0.0.0:5002"

echo Ожидание запуска Valuator (5 сек)...
timeout /t 5 /nobreak >nul

echo Удаление старого контейнера Nginx...
docker rm -f my-nginx 2>nul

echo Запуск Nginx в Docker...
docker run --name my-nginx ^
  -p 8080:8080 ^
  -v "%cd%\nginx\conf\nginx.conf:/etc/nginx/nginx.conf:ro" ^
  -v "%cd%\nginx\logs:/logs" ^
  -d nginx

echo.
echo Запущено: Valuator на 5001, 5002; Nginx (Docker) на 8080.
echo Приложение доступно: http://localhost:8080/
pause