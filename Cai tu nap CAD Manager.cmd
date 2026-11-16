@echo off
setlocal
set "AICAD_CHECK="
if /i "%~1"=="--check" set "AICAD_CHECK=-Check"
%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Update-CadManagerPaths.ps1" %AICAD_CHECK%
set "AICAD_RESULT=%ERRORLEVEL%"
if not defined AICAD_CHECK pause
exit /b %AICAD_RESULT%
