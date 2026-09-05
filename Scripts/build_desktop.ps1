$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
& (Join-Path $Root 'Scripts\bootstrap_dotnet.ps1')
$dotnet = Join-Path $Root '.tools\dotnet\dotnet.exe'
$project = Join-Path $Root 'Desktop\KitHerramientas.Desktop.csproj'
$outDir = Join-Path $Root 'Releases\Windows'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Write-Host 'Compilando Desktop self-contained...' -ForegroundColor Cyan
& $dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish termino con codigo $LASTEXITCODE" }
$exe = Join-Path $outDir 'KitHerramientas.exe'
if (!(Test-Path $exe)) { throw 'No aparecio KitHerramientas.exe.' }
Write-Host "EXE listo: $exe" -ForegroundColor Green
