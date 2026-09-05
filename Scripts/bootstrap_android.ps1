$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tools = Join-Path $Root '.tools'
$Downloads = Join-Path $Tools 'downloads'
$PortableTemp = Join-Path $Tools 'temp'
$GradleUserHome = Join-Path $Tools 'gradle-home'
$JdkRoot = Join-Path $Tools 'jdk17'
$SdkRoot = Join-Path $Tools 'android-sdk'
$GradleRoot = Join-Path $Tools 'gradle-9.3.1'
New-Item -ItemType Directory -Force -Path $Tools,$Downloads,$PortableTemp,$GradleUserHome,$SdkRoot | Out-Null

# Mantener descargas, temporales y cachés en la misma unidad del proyecto.
$env:TEMP = $PortableTemp
$env:TMP = $PortableTemp
$env:GRADLE_USER_HOME = $GradleUserHome

function Show-FreeSpace {
    try {
        $rootPath = [IO.Path]::GetPathRoot($Root)
        $drive = [System.IO.DriveInfo]::new($rootPath)
        $freeGb = [Math]::Round($drive.AvailableFreeSpace / 1GB, 2)
        Write-Host "Espacio libre en $rootPath $freeGb GB" -ForegroundColor DarkGray
        if ($drive.AvailableFreeSpace -lt 7GB) {
            throw "Hay menos de 7 GB libres en $rootPath. El primer build Android necesita espacio para JDK + SDK + Gradle."
        }
    } catch {
        if ($_.Exception.Message -like 'Hay menos de*') { throw }
        Write-Host 'No se pudo consultar el espacio libre; continúo igualmente.' -ForegroundColor DarkYellow
    }
}

function Get-File([string]$Url,[string]$Path) {
    if (!(Test-Path $Path)) {
        Write-Host "Descargando $([IO.Path]::GetFileName($Path))..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $Url -OutFile $Path -UseBasicParsing
    }
}

Show-FreeSpace
Write-Host "TEMP portable: $PortableTemp" -ForegroundColor DarkGray

if (!(Test-Path (Join-Path $JdkRoot 'bin\java.exe'))) {
    $zip = Join-Path $Downloads 'jdk17.zip'
    Get-File 'https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jdk/hotspot/normal/eclipse?project=jdk' $zip
    $tmp = Join-Path $Tools '_jdk_extract'
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $inner = Get-ChildItem $tmp -Directory | Select-Object -First 1
    if (!$inner) { throw 'No se pudo extraer JDK 17.' }
    Remove-Item $JdkRoot -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item $inner.FullName $JdkRoot
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

$SdkManager = Join-Path $SdkRoot 'cmdline-tools\latest\bin\sdkmanager.bat'
if (!(Test-Path $SdkManager)) {
    $zip = Join-Path $Downloads 'commandlinetools-win.zip'
    Get-File 'https://dl.google.com/android/repository/commandlinetools-win-15859902_latest.zip' $zip
    $tmp = Join-Path $Tools '_sdk_extract'
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $latest = Join-Path $SdkRoot 'cmdline-tools\latest'
    New-Item -ItemType Directory -Force -Path (Split-Path $latest) | Out-Null
    Remove-Item $latest -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item (Join-Path $tmp 'cmdline-tools') $latest
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

if (!(Test-Path (Join-Path $GradleRoot 'bin\gradle.bat'))) {
    $zip = Join-Path $Downloads 'gradle-9.3.1-bin.zip'
    Get-File 'https://services.gradle.org/distributions/gradle-9.3.1-bin.zip' $zip
    Expand-Archive -Path $zip -DestinationPath $Tools -Force
}

$env:JAVA_HOME = $JdkRoot
$env:ANDROID_HOME = $SdkRoot
$env:ANDROID_SDK_ROOT = $SdkRoot

Write-Host 'Aceptando licencias Android SDK...' -ForegroundColor DarkCyan
$yes = (1..40 | ForEach-Object { 'y' }) -join "`n"
$yes | & $SdkManager --sdk_root=$SdkRoot --licenses | Out-Null

Write-Host 'Preparando Android SDK API 37...' -ForegroundColor DarkCyan
& $SdkManager --sdk_root=$SdkRoot 'platform-tools' 'platforms;android-37' 'build-tools;36.0.0'
if ($LASTEXITCODE -ne 0) { throw "sdkmanager terminó con código $LASTEXITCODE" }

@{
    JAVA_HOME = $JdkRoot
    SDK_ROOT = $SdkRoot
    GRADLE = (Join-Path $GradleRoot 'bin\gradle.bat')
    GRADLE_USER_HOME = $GradleUserHome
    TEMP = $PortableTemp
} | ConvertTo-Json | Set-Content (Join-Path $Tools 'android-env.json') -Encoding UTF8

Write-Host 'Entorno Android portable listo.' -ForegroundColor Green
