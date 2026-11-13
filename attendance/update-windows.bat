@echo off
rem Heritage attendance - one-click update for Windows 10/11
rem Double-click this file. It downloads the latest code, installs, and deploys to Cloudflare.
setlocal
set "OWNER=kuh01041461581-ux"
set "REPO=ukukuk"
set "BRANCH=claude/intangible-heritage-attendance-system-oxdgwk"
set "WORK=%USERPROFILE%\attendance-update"

echo.
echo [1/4] Downloading the latest code...
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%WORK%" || goto :fail
curl.exe -L --fail -o "%WORK%\code.zip" "https://github.com/%OWNER%/%REPO%/archive/refs/heads/%BRANCH%.zip" || goto :fail

echo.
echo [2/4] Extracting...
tar.exe -xf "%WORK%\code.zip" -C "%WORK%" || goto :fail
set "APP="
for /d %%D in ("%WORK%\*") do if exist "%%D\attendance\wrangler.toml" set "APP=%%D\attendance"
if not defined APP goto :fail

echo.
echo [3/4] Installing (about 1 minute)...
cd /d "%APP%" || goto :fail
call npm install --no-audit --no-fund || goto :fail

echo.
echo [4/4] Deploying to Cloudflare...
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
echo  FAILED. Copy the red/error text above and send it.
echo ================================================
pause
exit /b 1
