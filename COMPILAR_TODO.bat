@echo off
setlocal
cd /d "%~dp0"
echo ==============================================
echo       KIT HERRAMIENTAS - BUILD R3 FIX
echo ==============================================
echo.
echo [1/2] Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\build_desktop.ps1"
if errorlevel 1 goto error
echo.
echo [2/2] Android
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\build_android.ps1"
if errorlevel 1 goto error
echo.
echo ==============================================
echo TODO COMPILADO CORRECTAMENTE
echo Resultados: %~dp0Releases
echo ==============================================
pause
exit /b 0
:error
echo.
echo ==============================================
echo HUBO UN ERROR. MIRÁ EL MENSAJE DE ARRIBA.
echo ==============================================
pause
exit /b 1
