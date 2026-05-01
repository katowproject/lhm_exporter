param(
    [string]$PublishDir = (Join-Path $PSScriptRoot 'publish'),
    [string]$OutputMsi = (Join-Path $PSScriptRoot 'lhm_exporter.msi')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$csproj = Get-ChildItem -Path (Join-Path $PSScriptRoot '..') -Recurse -Filter '*.csproj' | Select-Object -First 1
if ($null -eq $csproj) {
    throw 'Could not find the application project file.'
}

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

Write-Host 'Publishing application...'
& dotnet publish $csproj.FullName -c Release -r win-x64 --self-contained false -o $PublishDir | Out-Host

$wix = Get-Command candle.exe -ErrorAction SilentlyContinue
$light = Get-Command light.exe -ErrorAction SilentlyContinue
if ($null -eq $wix -or $null -eq $light) {
    throw 'WiX Toolset is required. Install WiX v3 and make sure candle.exe and light.exe are in PATH.'
}

$wxs = Join-Path $PSScriptRoot 'lhm_exporter.wxs'
$wixObj = Join-Path $PSScriptRoot 'lhm_exporter.wixobj'

Write-Host 'Building MSI...'
& $wix.Source $wxs -dPublishDir=$PublishDir -o $wixObj | Out-Host
& $light.Source $wixObj -o $OutputMsi | Out-Host

Write-Host "MSI created: $OutputMsi"
