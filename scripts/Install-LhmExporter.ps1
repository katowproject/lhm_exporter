param(
    [ValidateSet('Install', 'Uninstall', 'Restart', 'Status')]
    [string]$Action = 'Install',

    [string]$ServiceName = 'lhm_exporter',
    [string]$InstallDir = 'C:\Program Files\lhm_exporter',
    [string]$OutputDir = 'C:\Program Files\windows_exporter\textfile_inputs',
    [string]$OutputFile = 'lhm_exporter.prom',
    [int]$IntervalSeconds = 10,
    [string]$SourceDir = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-PublishSource {
    param([string]$BaseDir)

    $repoRoot = Resolve-Path (Join-Path $BaseDir '..')
    $csproj = Get-ChildItem -Path $repoRoot -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($null -ne $csproj) {
        $publishDir = Join-Path $csproj.DirectoryName 'bin\Release\net8.0\win-x64\publish'
        if (-not (Test-Path $publishDir)) {
            $tempPublishDir = Join-Path $env:TEMP ('lhm_exporter_publish_' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $tempPublishDir | Out-Null
            Write-Host 'Publishing application...'
            & dotnet publish $csproj.FullName -c Release -r win-x64 --self-contained false -o $tempPublishDir | Out-Host
            return $tempPublishDir
        }

        return $publishDir
    }

    $candidateDirs = @(
        $BaseDir,
        (Join-Path $BaseDir 'publish')
    )

    foreach ($candidate in $candidateDirs) {
        if (Test-Path (Join-Path $candidate 'lhm_exporter.exe')) {
            return $candidate
        }
    }

    throw 'Could not find lhm_exporter.exe or a .csproj file. Run this script from the repo folder or a publish output folder.'
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Stop-And-Remove-Service {
    param([string]$Name)

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -ne $service) {
        if ($service.Status -ne 'Stopped') {
            Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }

        & sc.exe delete $Name | Out-Null
        Start-Sleep -Seconds 2
    }
}

function Write-Status {
    param([string]$Name)

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        Write-Host "Service '$Name' is not installed."
    }
    else {
        Write-Host "Service '$Name' status: $($service.Status)"
    }
}

if (-not (Test-Administrator)) {
    throw 'This script must be run as Administrator.'
}

switch ($Action) {
    'Status' {
        Write-Status -Name $ServiceName
        return
    }

    'Uninstall' {
        Write-Host "Removing service '$ServiceName'..."
        Stop-And-Remove-Service -Name $ServiceName
        Write-Host 'Done.'
        return
    }

    'Restart' {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($null -eq $service) {
            throw "Service '$ServiceName' was not found. Run install first."
        }

        Restart-Service -Name $ServiceName -Force
        Write-Host "Service '$ServiceName' restarted."
        return
    }
}

$publishSource = Resolve-PublishSource -BaseDir $SourceDir
Ensure-Directory -Path $InstallDir
Ensure-Directory -Path $OutputDir

Write-Host "Copying application files to '$InstallDir'..."
Copy-Item -Path (Join-Path $publishSource '*') -Destination $InstallDir -Recurse -Force

Stop-And-Remove-Service -Name $ServiceName

$exePath = Join-Path $InstallDir 'lhm_exporter.exe'
if (-not (Test-Path $exePath)) {
    throw "File not found: $exePath"
}

$binaryPath = '"{0}" "{1}" "{2}" {3}' -f $exePath, $OutputDir, $OutputFile, $IntervalSeconds

Write-Host "Creating service '$ServiceName'..."
New-Service -Name $ServiceName -BinaryPathName $binaryPath -DisplayName 'LHM Exporter' -Description 'Exports LibreHardwareMonitor data to Prometheus textfile collector' -StartupType Automatic | Out-Null

Start-Service -Name $ServiceName
Write-Host ''
Write-Host 'Done.'
Write-Host "Service: $ServiceName"
Write-Host "Install: $InstallDir"
Write-Host "Output : $(Join-Path $OutputDir $OutputFile)"
Write-Host "Interval: $IntervalSeconds seconds"
Write-Host 'If windows_exporter is running, the metrics will be read from textfile_inputs.'
