@echo off
chcp 65001 >nul
rem DeskCalendar.exe 빌드 (Windows, Python 3.10+ 필요). 결과: dist\DeskCalendar.exe
python -m pip install -r requirements.txt || goto :err
python -m PyInstaller --noconfirm --clean --onefile --windowed ^
  --collect-all pdfminer --hidden-import gongmun --hidden-import core --hidden-import holidays_kr --hidden-import cloudsync --hidden-import glass --hidden-import holidays_api --hidden-import PIL._tkinter_finder --hidden-import analyze --hidden-import quickadd --hidden-import ocr --hidden-import PIL.ImageGrab --hidden-import pystray --hidden-import pystray._win32 --collect-all korean_lunar_calendar --collect-all segno --name DeskCalendar --icon assets\app.ico desk_calendar.py || goto :err
echo.
echo 완료: dist\DeskCalendar.exe
pause
exit /b 0
:err
echo 빌드 실패
pause
exit /b 1
