@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\build_desktop.ps1"
if errorlevel 1 (
  echo.
  echo ERROR AL COMPILAR DESKTOP.
  pause
  exit /b 1
)
echo.
echo DESKTOP LISTO.
pause
