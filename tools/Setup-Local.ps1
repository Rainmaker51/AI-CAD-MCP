# Build the two release paths expected by Desktop.Launcher on this machine.
# Close this project's AutoCAD and AI CAD Host before rebuilding loaded DLLs.
param()
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') -Release p0-p6
& (Join-Path $PSScriptRoot 'Build.ps1') -Component AutoCAD -Release p0-p6-axis-test -RuntimeRelease p0-p6
Write-Host 'Local setup ready. Open Mo AI CAD.cmd.'
