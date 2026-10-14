param([string]$Compiler = 'makensis.exe')
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
$files = Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Portable') -File
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Portable\DockerImageExplorer.exe'))) { throw 'Publish the application into Portable before building the installer.' }
$files | ForEach-Object { 'Delete "$INSTDIR\{0}"' -f $_.Name } | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'UninstallFiles.nsh')
$size = [int](($files | Measure-Object Length -Sum).Sum / 1024)
('WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "EstimatedSize" {0}' -f $size) | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'InstallSize.nsh')
& $Compiler /V2 (Join-Path $PSScriptRoot 'DockerImageExplorer.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
