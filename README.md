# LHM Exporter

`lhm_exporter` reads hardware sensor data from LibreHardwareMonitor and writes it to a `.prom` file for `windows_exporter`.

## Main features

- reads available hardware and sensors
- generates Prometheus textfile output
- refreshes the file periodically
- can run as a Windows Service

## Getting started

1. Run the application as Administrator.
2. Make sure `windows_exporter` is configured to read textfile collectors.
3. Keep the generated `.prom` file in the `textfile_inputs` folder.

## Setup options

There are two ways to use `lhm_exporter`:

### Build from source
Use this option if you want to compile and install it yourself.

Requirements:
- Windows
- .NET 8 SDK
- PowerShell 5.1 or later
- Administrator rights

Source code and setup guide: `scripts/README.md`

### Download a release
Use this option if you want the quickest installation path.

- Download the package from the GitHub Releases page
- Run the release package included with the download

## Important files

- `lhm_exporter/` - application source code
- `scripts/Install-LhmExporter.ps1` - setup script
- `scripts/Install-LhmExporter.cmd` - CMD wrapper for the setup script

## Default output

- folder: `C:\Program Files\windows_exporter\textfile_inputs`
- file: `lhm_exporter.prom`
- refresh interval: `10` seconds
