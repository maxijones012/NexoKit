@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\build_android.ps1"
if errorlevel 1 (
  echo.
  echo ERROR AL COMPILAR ANDROID.
  pause
  exit /b 1
)
echo.
echo ANDROID LISTO.
pause
