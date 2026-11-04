@echo off
chcp 65001 >nul
echo [1/3] 필요한 패키지 설치...
python -m pip install --upgrade pip
python -m pip install pyinstaller lxml
echo [2/3] exe 빌드...
python -m PyInstaller -F -w --name "한글양식생성기" --add-data "templates;templates" --hidden-import lxml.etree app.py
echo [3/3] 완료: dist\한글양식생성기.exe
pause
