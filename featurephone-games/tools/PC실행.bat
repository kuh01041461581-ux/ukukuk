@echo off
chcp 65001 >nul
cd /d "%~dp0"
where python >nul 2>nul && (python pc_start.py & goto :eof)
where py >nul 2>nul && (py pc_start.py & goto :eof)
echo Python 이 필요합니다. https://www.python.org 에서 설치한 뒤 다시 실행하세요.
pause
