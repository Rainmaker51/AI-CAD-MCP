param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$warehouse=(Get-Content -LiteralPath (Join-Path $repo 'warehouse.local.json') -Raw -Encoding UTF8|ConvertFrom-Json).path
if(-not(Test-Path -LiteralPath (Join-Path $warehouse 'warehouse.json'))){throw 'Warehouse unavailable'}
$stage=Join-Path $warehouse ('Tools/dotnet/_Development/Rainmaker.Library.Reader/working/separate-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $stage -Force|Out-Null
@{component='Rainmaker.Library.Reader';purpose='Independent Reader deployment';createdUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $stage 'working.json') -Encoding UTF8
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home';$env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget/packages'
& (Join-Path $repo '.tools/dotnet/dotnet.exe') publish (Join-Path $repo 'src/Library.Reader') -c Release --no-restore -m:1 -nr:false -v:q -o (Join-Path $stage 'publish')
if($LASTEXITCODE -ne 0){throw 'Reader build failed'}
New-Item -ItemType Directory -Path (Join-Path $stage 'package')|Out-Null
foreach($name in @('Rainmaker.Library.Reader.dll','Rainmaker.Library.Reader.pdb')){Copy-Item -LiteralPath (Join-Path $stage ('publish/'+$name)) -Destination (Join-Path $stage 'package')}
[IO.File]::WriteAllText((Join-Path $repo '.tools/separate-reader-stage.txt'),$stage)
Write-Output $stage
