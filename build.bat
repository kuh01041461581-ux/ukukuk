@echo off
chcp 65001 >nul
rem DeskCalendar.exe 빌드 (Windows, Python 3.10+ 필요). 결과: dist\DeskCalendar.exe
python -m pip install -r requirements.txt || goto :err
python -m PyInstaller --noconfirm --clean --onefile --windowed ^
  --collect-all pdfminer --hidden-import gongmun --name DeskCalendar --icon assets\app.ico desk_calendar.py || goto :err
echo.
echo 완료: dist\DeskCalendar.exe
pause
exit /b 0
:err
echo 빌드 실패
pause
exit /b 1
