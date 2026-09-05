$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
& (Join-Path $Root 'Scripts\bootstrap_android.ps1')
$envFile = Join-Path $Root '.tools\android-env.json'
$e = Get-Content $envFile -Raw | ConvertFrom-Json
$env:JAVA_HOME = $e.JAVA_HOME
$env:ANDROID_HOME = $e.SDK_ROOT
$env:ANDROID_SDK_ROOT = $e.SDK_ROOT
if ($e.GRADLE_USER_HOME) { $env:GRADLE_USER_HOME = $e.GRADLE_USER_HOME }
if ($e.TEMP) { $env:TEMP = $e.TEMP; $env:TMP = $e.TEMP }

$android = Join-Path $Root 'Android'
$sdkEscaped = ($e.SDK_ROOT -replace '\\','\\\\')
"sdk.dir=$sdkEscaped" | Set-Content (Join-Path $android 'local.properties') -Encoding ASCII

Push-Location $android
try {
    Write-Host 'Compilando Android...' -ForegroundColor Cyan
    & $e.GRADLE --no-daemon :app:assembleDebug
    if ($LASTEXITCODE -ne 0) { throw "Gradle terminó con código $LASTEXITCODE" }
} finally { Pop-Location }

$apk = Join-Path $android 'app\build\outputs\apk\debug\app-debug.apk'
if (!(Test-Path $apk)) { throw 'No apareció el APK esperado.' }
$outDir = Join-Path $Root 'Releases\Android'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'KitHerramientas_Android_0.9.0_R9.apk'
Copy-Item $apk $out -Force
Write-Host "APK listo: $out" -ForegroundColor Green
