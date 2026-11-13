@echo off
rem 창이 멈추거나 응답이 없을 때: 브라우저로 실행합니다 (이 PC 안에서만 동작)
start "" "%~dp0ExpenseManager.exe" --browser
