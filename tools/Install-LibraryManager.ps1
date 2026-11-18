param([switch]$Build,[switch]$Check)
# Library is installed through CAD Manager; never register a second standalone autoloader.
& (Join-Path $PSScriptRoot 'Install-CadRibbonManager.ps1') -Build:$Build -Check:$Check
