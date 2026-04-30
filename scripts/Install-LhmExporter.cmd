@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-LhmExporter.ps1" %*
exit /b %errorlevel%
