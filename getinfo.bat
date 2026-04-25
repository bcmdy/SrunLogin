@echo off
chcp 65001 >nul
title 校园网状态查询
cd /d "%~dp0"

set "USERNAME=202344111119"
set "URL=http://1.1.1.1"

py login.py info -u "%USERNAME%" --url "%URL%"
echo.
pause