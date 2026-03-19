@echo off
chcp 65001 >nul
cd /d "%~dp0\.."

echo Остановка Nginx в Docker...
docker rm -f my-nginx 2>nul

echo Остановка Valuator (порты 5001, 5002)...
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":5001 :5002" ^| findstr "LISTENING"') do (
    taskkill /F /PID %%a 2>nul
)

echo Готово.
pause