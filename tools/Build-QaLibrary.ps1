param([string]$Stage='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $repo
$warehouse=(Get-Content warehouse.local.json -Raw -Encoding UTF8|ConvertFrom-Json).path
$project=[xml](Get-Content src/Rainmaker.Library/Rainmaker.Library.csproj -Raw)
$version=$project.SelectSingleNode('//Version').InnerText
$working=[IO.Path]::GetFullPath((Join-Path $warehouse 'Tools/dotnet/_Development/Rainmaker.Library/working'))
if(-not $Stage){$Stage=Join-Path $working ('qa-'+$version.Replace('.','')+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Stage=[IO.Path]::GetFullPath($Stage)
if(-not $Stage.StartsWith($working+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Stage must be inside the Library working directory'}
New-Item -ItemType Directory -Path $Stage -Force|Out-Null
[IO.File]::WriteAllText((Join-Path $repo '.tools/qa-library-stage.txt'),$Stage)
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home';$env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget/packages'
$dotnet=Join-Path $repo '.tools/dotnet/dotnet.exe'
& $dotnet publish src/Rainmaker.Library -c Release --no-restore -m:1 -nr:false -o (Join-Path $Stage 'publish') -v:q
if($LASTEXITCODE -ne 0){throw 'QA build failed'}
$package=Join-Path $Stage 'package';New-Item -ItemType Directory -Path $package -Force|Out-Null
foreach($name in @('Rainmaker.Library.dll','Rainmaker.Library.pdb','Rainmaker.Library.Core.dll','Rainmaker.Library.Core.pdb','CAD.LibraryScanner.dll','CAD.LibraryScanner.pdb','Newtonsoft.Json.dll')){Copy-Item -LiteralPath (Join-Path $Stage ('publish/'+$name)) -Destination $package -Force}
& $dotnet publish src/Desktop.Host -c Release --no-restore -m:1 -nr:false -o (Join-Path $Stage 'engine/Host') -v:q
if($LASTEXITCODE -ne 0){throw 'Background engine build failed'}
& $dotnet publish src/CAD.LibraryScanner -c Release --no-restore -m:1 -nr:false -o (Join-Path $Stage 'engine/Host/LibraryScanner') -v:q
if($LASTEXITCODE -ne 0){throw 'Scanner build failed'}
Write-Output ('Stage: '+$Stage)
