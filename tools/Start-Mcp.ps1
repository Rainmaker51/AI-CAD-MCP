param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Release='p0-p6')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){Join-Path $repo 'storage/04-builds/artifacts'}else{Join-Path $repo 'artifacts'}
$runtime=Join-Path $repo '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $runtime)){$runtime=(Get-Command dotnet.exe -ErrorAction Stop).Source}
$env:DOTNET_ROOT=Split-Path $runtime -Parent
& $runtime (Join-Path $buildRoot "$Release/MCP/AICad.Mcp.dll")
exit $LASTEXITCODE
