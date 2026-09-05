$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$envFile = Join-Path $Root '.tools\android-env.json'
if (!(Test-Path $envFile)) { & (Join-Path $Root 'Scripts\bootstrap_android.ps1') }
$e = Get-Content $envFile -Raw | ConvertFrom-Json
$adb = Join-Path $e.SDK_ROOT 'platform-tools\adb.exe'
$apk = Join-Path $Root 'Releases\Android\KitHerramientas_Android_0.9.0_R9.apk'
if (!(Test-Path $apk)) { & (Join-Path $Root 'Scripts\build_android.ps1') }
Write-Host 'Dispositivos ADB:' -ForegroundColor Cyan
& $adb devices
Write-Host 'Instalando APK...' -ForegroundColor Cyan
& $adb install -r $apk
if ($LASTEXITCODE -ne 0) { throw "ADB terminó con código $LASTEXITCODE. Revisá depuración USB y autorización del teléfono." }
Write-Host 'Aplicación instalada.' -ForegroundColor Green
