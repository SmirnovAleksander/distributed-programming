@echo off
chcp 65001 >nul
cd /d "%~dp0\.."

echo Запуск Valuator на портах 5001 и 5002...
start "Valuator5001" dotnet run --project Valuator --urls "http://0.0.0.0:5001"
start "Valuator5002" dotnet run --project Valuator --urls "http://0.0.0.0:5002"

echo Ожидание запуска Valuator (3 сек)...
timeout /t 3 /nobreak >nul

echo Запуск Nginx на порту 8080...
cd nginx
nginx -p "%cd%"
cd ..

echo.
echo Запущено: Valuator на 5001, 5002; Nginx на 8080.
echo Приложение доступно: http://localhost:8080/
pause
