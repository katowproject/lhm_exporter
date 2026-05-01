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

$exePath = Join-Path $PublishDir 'lhm_exporter.exe'
if (-not (Test-Path $exePath)) {
    throw "Expected published executable was not found: $exePath"
}

$heat = Get-Command heat.exe -ErrorAction SilentlyContinue
$candle = Get-Command candle.exe -ErrorAction SilentlyContinue
$light = Get-Command light.exe -ErrorAction SilentlyContinue
if ($null -eq $heat -or $null -eq $candle -or $null -eq $light) {
    throw 'WiX Toolset (heat.exe, candle.exe, light.exe) is required and must be in PATH.'
}

$wxs = Join-Path $PSScriptRoot 'lhm_exporter.wxs'
$harvestedWxs = Join-Path $PSScriptRoot 'harvested.wxs'
$mainWixObj = Join-Path $PSScriptRoot 'lhm_exporter.wixobj'
$harvestedWixObj = Join-Path $PSScriptRoot 'harvested.wixobj'
$tempMsi = Join-Path $PSScriptRoot ('lhm_exporter_' + [guid]::NewGuid().ToString('N') + '.msi')

Write-Host 'Harvesting publish folder with heat...'
$PublishDirFull = (Resolve-Path $PublishDir).Path
$excludePattern = 'lhm_exporter.exe'
& heat.exe dir $PublishDirFull -cg ApplicationComponents -dr INSTALLFOLDER -scom -sreg -sv -gg -srd -var var.PublishDir -out $harvestedWxs -x $excludePattern | Out-Host

if (Test-Path $harvestedWxs) {
    $content = Get-Content $harvestedWxs -Raw
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '<Component\b(?![^>]*\bWin64=)',
        '<Component Win64="yes"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
    Set-Content -Path $harvestedWxs -Value $content -Encoding UTF8
}

Write-Host 'Building MSI...'
& candle.exe $wxs "-dPublishDir=$PublishDirFull" -o $mainWixObj | Out-Host
& candle.exe $harvestedWxs "-dPublishDir=$PublishDirFull" -o $harvestedWixObj | Out-Host

& light.exe $mainWixObj $harvestedWixObj -o $tempMsi | Out-Host

if (Test-Path $OutputMsi) {
    try {
        Remove-Item $OutputMsi -Force -ErrorAction Stop
    }
    catch {
        Write-Error "Unable to remove existing MSI at $OutputMsi. Close any process using it and retry. Error: $_"
        exit 1
    }
}

Move-Item -Path $tempMsi -Destination $OutputMsi -Force

if (Test-Path $harvestedWxs) { Remove-Item $harvestedWxs -Force }
if (Test-Path $mainWixObj) { Remove-Item $mainWixObj -Force }
if (Test-Path $harvestedWixObj) { Remove-Item $harvestedWixObj -Force }

Write-Host "MSI created: $OutputMsi"