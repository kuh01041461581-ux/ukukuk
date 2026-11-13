@echo off
rem Heritage attendance - one-click update for Windows 10/11
rem 1) Download the ZIP in your browser (link is in the instructions)  2) Double-click this file.
rem It finds the newest ukukuk-*.zip in your Downloads folder, installs, and deploys to Cloudflare.
setlocal
set "WORK=%USERPROFILE%\attendance-update"
set "STEP=start"

echo.
echo [0/4] Checking tools...
set "STEP=0 checking tools (Node.js / npm / tar)"
where node >nul 2>nul || goto :fail
where npm >nul 2>nul || goto :fail
where tar.exe >nul 2>nul || goto :fail
node -v

echo.
echo [1/4] Looking for the downloaded zip in Downloads...
set "STEP=1 finding the zip (download it in your browser first)"
set "ZIP="
for /f "delims=" %%F in ('dir /b /o-d "%USERPROFILE%\Downloads\ukukuk-*.zip" 2^>nul') do if not defined ZIP set "ZIP=%USERPROFILE%\Downloads\%%F"
if not defined ZIP goto :fail
echo Using: %ZIP%

echo.
echo [2/4] Extracting...
set "STEP=2 extracting the zip file"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%WORK%" || goto :fail
tar.exe -xf "%ZIP%" -C "%WORK%" || goto :fail
set "APP="
for /d %%D in ("%WORK%\*") do if exist "%%D\attendance\wrangler.toml" set "APP=%%D\attendance"
if not defined APP goto :fail
echo Folder: %APP%

echo.
echo [3/4] Installing (about 1 minute)...
set "STEP=3 npm install"
cd /d "%APP%" || goto :fail
call npm install --no-audit --no-fund || goto :fail

echo.
echo [4/4] Deploying to Cloudflare...
set "STEP=4 wrangler deploy (login may be needed)"
call npx wrangler deploy || goto :fail

echo.
echo ================================================
echo  DONE. Refresh the admin page (Ctrl+F5).
echo ================================================
pause
exit /b 0

:fail
echo.
echo ================================================
echo  FAILED at step: %STEP%
echo  Please send this window's text (or a screenshot).
echo ================================================
pause
exit /b 1
