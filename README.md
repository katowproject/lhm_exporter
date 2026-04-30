# LHM Exporter

`lhm_exporter` reads sensor data from LibreHardwareMonitor and writes it to a `.prom` file so `windows_exporter` can collect it.

## Main features

- reads available hardware and sensors
- generates Prometheus textfile output
- refreshes the file periodically
- can run as a Windows Service

## Quick start

1. Run the application as Administrator.
2. Make sure `windows_exporter` uses the textfile collector.
3. Let the `.prom` file be written to the `textfile_inputs` folder.

## Setup

The installation and service management guide is here:

- `scripts/README.md`

## Important files

- `lhm_exporter/` - application source code
- `scripts/Install-LhmExporter.ps1` - setup script
- `scripts/Install-LhmExporter.cmd` - CMD wrapper for the setup script

## Default output

- folder: `C:\Program Files\windows_exporter\textfile_inputs`
- file: `lhm_exporter.prom`
- refresh interval: `10` seconds
