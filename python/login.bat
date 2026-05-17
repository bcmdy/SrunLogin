@echo off
chcp 65001 >nul
title 校园网登录
cd /d "%~dp0"

:: ==================== 修改这里 ====================
set "USERNAME=202600000000"
set "PASSWORD=abc123"
set "URL=http://1.1.1.1"
:: =================================================

echo 正在登录校园网...
py login.py login -u "%USERNAME%" -p "%PASSWORD%" --url "%URL%"
echo.
pause