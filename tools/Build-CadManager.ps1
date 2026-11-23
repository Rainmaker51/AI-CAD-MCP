param([string]$Stage='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));Set-Location $repo
$warehouse=(Get-Content warehouse.local.json -Raw -Encoding UTF8|ConvertFrom-Json).path
$working=[IO.Path]::GetFullPath((Join-Path $warehouse 'Tools/dotnet/_Development/CAD.Manager/working'))
if(-not $Stage){$Stage=Join-Path $working ('manager-090-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Stage=[IO.Path]::GetFullPath($Stage)
if(-not $Stage.StartsWith($working+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Stage must be inside CAD.Manager working'}
New-Item -ItemType Directory -Path $Stage -Force|Out-Null
[IO.File]::WriteAllText((Join-Path $repo '.tools/cad-manager-stage.txt'),$Stage)
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home';$env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget/packages'
& (Join-Path $repo '.tools/dotnet/dotnet.exe') publish src/CAD.Bridge.AutoCAD -c Release --no-restore -m:1 -nr:false -v:q -o (Join-Path $Stage 'publish')
if($LASTEXITCODE -ne 0){throw 'Manager build failed'}
$package=Join-Path $Stage 'package/AutoCAD';New-Item -ItemType Directory -Path $package -Force|Out-Null
foreach($name in @('CAD.Bridge.AutoCAD.dll','CAD.Bridge.AutoCAD.pdb','CAD.Contracts.dll','CAD.Contracts.pdb','CAD.Geometry.dll','CAD.Geometry.pdb','Newtonsoft.Json.dll')){Copy-Item -LiteralPath (Join-Path $Stage ('publish/'+$name)) -Destination $package -Force}
Write-Output ('Stage: '+$Stage)
