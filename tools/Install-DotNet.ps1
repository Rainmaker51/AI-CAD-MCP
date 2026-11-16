# Microsoft installer; installs only into this checkout, without changing PATH.
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=(Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw|ConvertFrom-Json).sdk.version
$installDir=Join-Path $repo '.tools/dotnet'
$installer=Join-Path $repo '.tools/dotnet-install.ps1'
[IO.Directory]::CreateDirectory((Join-Path $repo '.tools'))|Out-Null
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
& $installer -Version $version -Architecture x64 -InstallDir $installDir -NoPath
if(-not(Test-Path -LiteralPath (Join-Path $installDir 'dotnet.exe'))){throw 'Microsoft .NET SDK installation did not complete.'}
Write-Host "Local .NET SDK ready: $version"
