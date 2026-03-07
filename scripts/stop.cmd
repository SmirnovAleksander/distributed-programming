@echo off
chcp 65001 >nul
cd /d "%~dp0\.."

echo Остановка Nginx...
cd nginx
nginx -s quit -p "%cd%" 2>nul
nginx -s stop -p "%cd%" 2>nul
cd ..

echo Остановка Valuator (порты 5001, 5002)...
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":5001 :5002" ^| findstr "LISTENING"') do (
    taskkill /F /PID %%a 2>nul
)

echo Готово.
pause
