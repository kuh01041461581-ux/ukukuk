@echo off
rem Heritage attendance - one-click update for Windows 10/11
rem Double-click this file. It downloads the latest code, installs, and deploys to Cloudflare.
setlocal
set "OWNER=kuh01041461581-ux"
set "REPO=ukukuk"
set "BRANCH=claude/intangible-heritage-attendance-system-oxdgwk"
set "WORK=%USERPROFILE%\attendance-update"
set "STEP=start"

echo.
echo [0/4] Checking tools...
set "STEP=0 checking tools (Node.js / npm / curl / tar)"
where node >nul 2>nul || goto :fail
where npm >nul 2>nul || goto :fail
where curl.exe >nul 2>nul || goto :fail
where tar.exe >nul 2>nul || goto :fail
node -v

echo.
echo [1/4] Downloading the latest code...
set "STEP=1 downloading code from GitHub"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%WORK%" || goto :fail
curl.exe -L --fail -o "%WORK%\code.zip" "https://github.com/%OWNER%/%REPO%/archive/refs/heads/%BRANCH%.zip" || goto :fail

echo.
echo [2/4] Extracting...
set "STEP=2 extracting the zip file"
tar.exe -xf "%WORK%\code.zip" -C "%WORK%" || goto :fail
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
