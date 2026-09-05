@echo off
setlocal
cd /d "%~dp0"
:menu
cls
echo ==========================================
echo         NEXOKIT - R9
echo ==========================================
echo.
echo [1] Abrir Desktop compilado
echo [2] Compilar Desktop
echo [3] Compilar Android
echo [4] Compilar todo
echo [5] Instalar APK por USB
echo [6] Abrir Releases
echo [7] Abrir carpeta del proyecto
echo [0] Salir
echo.
set /p op=Elegir opcion: 
if "%op%"=="1" goto run
if "%op%"=="2" call COMPILAR_DESKTOP.bat & goto menu
if "%op%"=="3" call COMPILAR_ANDROID.bat & goto menu
if "%op%"=="4" call COMPILAR_TODO.bat & goto menu
if "%op%"=="5" call INSTALAR_ANDROID_USB.bat & goto menu
if "%op%"=="6" start "" "%~dp0Releases" & goto menu
if "%op%"=="7" start "" "%~dp0" & goto menu
if "%op%"=="0" exit /b 0
goto menu
:run
if exist "%~dp0Releases\Windows\KitHerramientas.exe" (
  start "" "%~dp0Releases\Windows\KitHerramientas.exe"
) else (
  echo Desktop todavia no esta compilado.
  pause
)
goto menu
