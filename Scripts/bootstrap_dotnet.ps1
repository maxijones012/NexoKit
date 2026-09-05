$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tools = Join-Path $Root '.tools'
$Dotnet = Join-Path $Tools 'dotnet'
$PortableTemp = Join-Path $Tools 'temp'
$DotnetHome = Join-Path $Tools 'dotnet-home'
$NugetPackages = Join-Path $Tools 'nuget-packages'
$Downloads = Join-Path $Tools 'downloads'
$SdkVersion = '10.0.400'
$SdkZip = Join-Path $Downloads "dotnet-sdk-$SdkVersion-win-x64.zip"
$SdkUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/$SdkVersion/dotnet-sdk-$SdkVersion-win-x64.zip"

New-Item -ItemType Directory -Force -Path $Tools,$Dotnet,$PortableTemp,$DotnetHome,$NugetPackages,$Downloads | Out-Null

# Todo el bootstrap queda en la unidad donde está el proyecto.
$env:TEMP = $PortableTemp
$env:TMP = $PortableTemp
$env:DOTNET_CLI_HOME = $DotnetHome
$env:NUGET_PACKAGES = $NugetPackages
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Get-ProjectDriveInfo {
    $rootPath = [IO.Path]::GetPathRoot($Root)
    return [System.IO.DriveInfo]::new($rootPath)
}

try {
    $drive = Get-ProjectDriveInfo
    $freeGb = [Math]::Round($drive.AvailableFreeSpace / 1GB, 2)
    Write-Host "Espacio libre en $($drive.Name): $freeGb GB" -ForegroundColor DarkGray
    if ($drive.AvailableFreeSpace -lt 2GB) {
        throw "Hay menos de 2 GB libres en $($drive.Name). Liberá espacio en esa unidad antes de compilar Desktop."
    }
} catch {
    if ($_.Exception.Message -like 'Hay menos de*') { throw }
    Write-Host 'No se pudo consultar el espacio libre; continúo igualmente.' -ForegroundColor DarkYellow
}

$exe = Join-Path $Dotnet 'dotnet.exe'
if (!(Test-Path $exe)) {
    # Si quedó una extracción incompleta de un intento anterior, se limpia sólo .tools\dotnet.
    if (Test-Path $Dotnet) { Remove-Item $Dotnet -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Force -Path $Dotnet | Out-Null

    if (!(Test-Path $SdkZip)) {
        $partial = "$SdkZip.partial"
        Remove-Item $partial -Force -ErrorAction SilentlyContinue
        Write-Host "Descargando .NET SDK $SdkVersion directamente al proyecto..." -ForegroundColor Cyan
        Write-Host "Destino: $SdkZip" -ForegroundColor DarkGray
        try {
            Invoke-WebRequest -Uri $SdkUrl -OutFile $partial -UseBasicParsing
            Move-Item $partial $SdkZip -Force
        } catch {
            Remove-Item $partial -Force -ErrorAction SilentlyContinue
            throw "Falló la descarga de .NET SDK $SdkVersion. $($_.Exception.Message)"
        }
    }

    Write-Host "TEMP portable: $PortableTemp" -ForegroundColor DarkGray
    Write-Host 'Extrayendo .NET 10 LTS portable...' -ForegroundColor Cyan
    try {
        Expand-Archive -Path $SdkZip -DestinationPath $Dotnet -Force
    } catch {
        Remove-Item $Dotnet -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item $SdkZip -Force -ErrorAction SilentlyContinue
        throw 'El ZIP de .NET quedó incompleto o dañado. Se eliminó para que el próximo intento lo descargue de nuevo.'
    }
}

if (!(Test-Path $exe)) { throw "No apareció dotnet.exe en $Dotnet" }
$ver = & $exe --version
if ($LASTEXITCODE -ne 0) { throw 'dotnet.exe existe pero no pudo ejecutarse.' }
Write-Host "Entorno .NET portable listo. SDK $ver" -ForegroundColor Green
