$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){Join-Path $repo 'storage/04-builds/artifacts'}else{Join-Path $repo 'artifacts'}
$env:DOTNET_ROOT=[IO.Path]::GetFullPath((Join-Path $repo '.tools/dotnet'))
& (Join-Path $buildRoot 'app/MCP/AICad.Mcp.exe')
exit $LASTEXITCODE
