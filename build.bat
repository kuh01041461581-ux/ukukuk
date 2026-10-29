@echo off
chcp 65001 >nul
rem 윈도우에서 빌드: build.bat [출력.exe]
rem Roslyn csc 필요 (Visual Studio Build Tools 또는 .NET SDK). 예전 .NET Framework csc 4.0 은 최신 C# 문법을 못 읽어요.
cd /d "%~dp0"
if not exist Secrets.cs copy Secrets.example.cs Secrets.cs >nul
set OUT=%~1
if "%OUT%"=="" set OUT=out\OvertimeReminder.exe
if not exist out mkdir out
set CSC=
for /f "delims=" %%i in ('dir /b /s "%ProgramFiles%\Microsoft Visual Studio\csc.exe" 2^>nul') do set CSC=%%i
if "%CSC%"=="" for /f "delims=" %%i in ('dir /b /s "%ProgramFiles(x86)%\Microsoft Visual Studio\csc.exe" 2^>nul') do set CSC=%%i
if "%CSC%"=="" for /f "delims=" %%i in ('dir /b /s "%ProgramFiles%\dotnet\sdk\csc.dll" 2^>nul') do set CSCDLL=%%i
if "%CSC%"=="" if "%CSCDLL%"=="" echo Roslyn csc 를 찾지 못했어요. Build Tools 또는 .NET SDK 설치가 필요해요. & exit /b 1
set REF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set ARGS=-nologo -out:"%OUT%" -target:winexe -langversion:latest -optimize+ -unsafe -win32icon:app.ico -win32manifest:app.manifest -r:"%REF%\System.dll" -r:"%REF%\System.Core.dll" -r:"%REF%\System.Drawing.dll" -r:"%REF%\System.Windows.Forms.dll" -r:"%REF%\System.Xml.dll" @build.rsp
if not "%CSC%"=="" "%CSC%" %ARGS%
if "%CSC%"=="" dotnet "%CSCDLL%" %ARGS%
echo 완성: %OUT%
