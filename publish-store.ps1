# Empaqueta Iderma Capilar App (WinUI 3) para Microsoft Store.
# Sube el .msix sin firmar. No uses un .msixupload armado a mano.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
$desktop = [Environment]::GetFolderPath('Desktop')
$msixDir = Join-Path $root 'msix'

if (Test-Path $msixDir) {
  Remove-Item -LiteralPath $msixDir -Recurse -Force
}

dotnet publish "IdermaFichas.csproj" `
  -c Release `
  -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 `
  -p:WindowsPackageType=MSIX `
  -p:WindowsAppSDKSelfContained=false `
  -p:GenerateAppxPackageOnBuild=true `
  -p:GenerateAppxUploadPackageOnBuild=false `
  -p:UapAppxPackageBuildMode=SideloadOnly `
  -p:AppxPackageSigningEnabled=false `
  -p:GenerateTemporaryStoreCertificate=false `
  -p:AppxBundle=Never

if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish falló con código $LASTEXITCODE"
}

$msix = Get-ChildItem -Path $msixDir -Recurse -Filter '*.msix' |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

if (-not $msix) {
  throw 'No se encontró el archivo .msix generado.'
}

$zipCheck = [System.IO.Compression.ZipFile]::OpenRead($msix.FullName)
try {
  $names = $zipCheck.Entries | ForEach-Object { $_.FullName }
  if ($names -contains 'AppxSignature.p7x') {
    throw 'El MSIX tiene firma local (AppxSignature.p7x). Desactiva la firma y vuelve a generar.'
  }
  if ($names -notcontains 'AppxManifest.xml') {
    throw 'El MSIX no incluye AppxManifest.xml.'
  }
} finally {
  $zipCheck.Dispose()
}

$tempDir = 'C:\Temp\IdermaStore'
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
$destMsix = Join-Path $tempDir 'IdermaCapilarApp_1.0.0.0_x64.msix'
Copy-Item -LiteralPath $msix.FullName -Destination $destMsix -Force
Remove-Item -LiteralPath (Join-Path $desktop 'IdermaCapilarApp.msixupload') -Force -ErrorAction SilentlyContinue

Write-Host "MSIX: $($msix.FullName) ($($msix.Length) bytes)"
Write-Host "SUBIR ESTE ARCHIVO (no uses el Escritorio / OneDrive):"
Write-Host $destMsix
