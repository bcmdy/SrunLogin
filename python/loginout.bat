@echo off
chcp 65001 >nul
title 校园网注销
cd /d "%~dp0"

:: ==================== 修改这里 ====================
set "USERNAME=202600000000"
set "URL=http://1.1.1.1"
:: =================================================

echo 正在注销校园网...
py login.py logout -u "%USERNAME%" --url "%URL%"
echo.
pause