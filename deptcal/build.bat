@echo off
rem 부서 일정 달력 exe 빌드 (윈도우 기본 포함 csc 사용, 설치할 것 없음)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set SMA=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Management.Automation\v4.0_3.0.0.0__31bf3856ad364e35\System.Management.Automation.dll
"%CSC%" /nologo /target:winexe /out:DeptCalendar.exe /reference:"%SMA%" /reference:System.Windows.Forms.dll /resource:DeptCalendarApp.ps1,app.ps1 Launcher.cs
if errorlevel 1 (echo 빌드 실패 & exit /b 1)
echo DeptCalendar.exe 만들었습니다.
