# LHM Exporter Setup

This guide explains how to install, run, and remove `lhm_exporter` in a simple way.

## What it does

The application will:
- read sensor data from LibreHardwareMonitor
- write the results to a `.prom` file
- refresh the data automatically every 10 seconds
- store the output so `windows_exporter` can read it

## Quick use

Run Command Prompt or PowerShell as Administrator, then:

### Install
```powershell
.\scripts\Install-LhmExporter.ps1 -Action Install
```

### Check status
```powershell
.\scripts\Install-LhmExporter.ps1 -Action Status
```

### Restart
```powershell
.\scripts\Install-LhmExporter.ps1 -Action Restart
```

### Uninstall
```powershell
.\scripts\Install-LhmExporter.ps1 -Action Uninstall
```

## Configurable parameters

- `-ServiceName` service name, default `lhm_exporter`
- `-InstallDir` application install folder
- `-OutputDir` `.prom` output folder
- `-OutputFile` `.prom` file name
- `-IntervalSeconds` refresh interval, default `10`

## Example using the default settings

```powershell
.\scripts\Install-LhmExporter.ps1 -Action Install
```

## Example using custom paths

```powershell
.\scripts\Install-LhmExporter.ps1 -Action Install -ServiceName lhm_exporter -InstallDir 'C:\Program Files\lhm_exporter' -OutputDir 'C:\Program Files\windows_exporter\textfile_inputs' -IntervalSeconds 10
