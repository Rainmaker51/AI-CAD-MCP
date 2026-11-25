param([Parameter(Mandatory=$true)][string]$ManagerDirectory,[Parameter(Mandatory=$true)][string]$AxisPackage,[string]$ReaderPackage='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$physical=Join-Path $repo ('.tools/test-runs/axis-reader-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $physical -Force|Out-Null
@{component='Rainmaker.Axis';purpose='Reader V1 real CAD resource regression';createdUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $physical 'test-run.json') -Encoding UTF8
[IO.File]::WriteAllText((Join-Path $repo '.tools/axis-reader-test-run.txt'),$physical)
$letter=@('Q','R','S','T','U','V','W','X','Y','Z')|Where-Object{-not(Test-Path ($_+':\'))}|Select-Object -First 1
if(-not $letter){throw 'No free drive alias'}
& subst ($letter+':') $physical
if($LASTEXITCODE -ne 0){throw 'Cannot create temporary alias'}
try{
 $root=$letter+':\'
 Copy-Item -LiteralPath $ManagerDirectory -Destination (Join-Path $root 'AutoCAD') -Recurse
 Copy-Item -LiteralPath $AxisPackage -Destination (Join-Path $root 'package') -Recurse
 if($ReaderPackage){Copy-Item -LiteralPath $ReaderPackage -Destination (Join-Path $root 'reader-package') -Recurse}
 $source=(Get-Content -LiteralPath (Join-Path $repo 'warehouse.local.json') -Raw -Encoding UTF8|ConvertFrom-Json).path
 $warehouse=Join-Path $root 'warehouse';New-Item -ItemType Directory -Path $warehouse|Out-Null
 Copy-Item -LiteralPath (Join-Path $source 'warehouse.json') -Destination $warehouse
 foreach($folder in @('Catalog/management')){
  $dest=Join-Path $warehouse $folder;New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force|Out-Null
  Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $dest -Recurse
 }
 foreach($file in @('Catalog/resource-codes.json','Catalog/sync/state.json')){
  $dest=Join-Path $warehouse $file;New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force|Out-Null;Copy-Item -LiteralPath (Join-Path $source $file) -Destination $dest
 }
 $hashes=New-Object 'Collections.Generic.HashSet[string]'
 foreach($folder in @('Versions/templates/ISO_QUANGANH','Styles/ISO_QUANGANH','Hatches/ISO_QUANGANH')){
  $from=Join-Path $source $folder;$to=Join-Path $warehouse $folder;New-Item -ItemType Directory -Path $to -Force|Out-Null
  $active=Get-Content -LiteralPath (Join-Path $from 'active.json') -Raw -Encoding UTF8|ConvertFrom-Json
  Copy-Item -LiteralPath (Join-Path $from 'active.json') -Destination $to
  Copy-Item -LiteralPath (Join-Path $from $active.version) -Destination $to -Recurse
  $file=if($folder -like '*templates*'){'content.json'}else{'catalog.json'}
  $profile=Get-Content -LiteralPath (Join-Path $to ($active.version+'/'+$file)) -Raw -Encoding UTF8|ConvertFrom-Json
  if($profile.sourceHash){$hashes.Add($profile.sourceHash)|Out-Null}
  foreach($block in $profile.Blocks){if($block.NativeSource.FileHash){$hashes.Add($block.NativeSource.FileHash)|Out-Null}}
  foreach($layer in $profile.Layers){if($layer.Appearance.SourceHash){$hashes.Add($layer.Appearance.SourceHash)|Out-Null}}
 }
 $assets=Join-Path $warehouse 'CAD/Assets';New-Item -ItemType Directory -Path $assets -Force|Out-Null
 foreach($hash in $hashes){Copy-Item -LiteralPath (Join-Path $source ('CAD/Assets/'+$hash+'.dwg')) -Destination $assets}
 @{path=$warehouse}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'warehouse.local.json') -Encoding UTF8
 @{ContentRoot=$root;DataRoot=(Join-Path $root 'data');HostExecutable=(Join-Path $repo 'artifacts/p0-p6/Host/AICad.Host.exe')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'install.json') -Encoding UTF8
 [IO.File]::WriteAllText((Join-Path $root '.aicad-tools-test'),'Isolated Reader V1 Axis test')
 $command=if($ReaderPackage){'AICADDETACHEDREADERTEST'}else{'AICADAXISUPGRADETEST'}
 $script=@('_NETLOAD',('"'+(Join-Path $root 'AutoCAD/CAD.Bridge.AutoCAD.dll')+'"'),$command,'_QUIT','_Y')-join "`r`n"
 [IO.File]::WriteAllText((Join-Path $root 'test.scr'),$script+"`r`n")
 $env:AICAD_TOOL_TEST_ROOT=$root
 $console=Join-Path $env:ProgramFiles 'Autodesk/AutoCAD 2021/accoreconsole.exe'
 & $console /i (Join-Path $repo 'content/launch/blank-mm.dwg') /s (Join-Path $root 'test.scr') /isolate AxisReaderV1 (Join-Path $root 'profile') *> (Join-Path $root 'core.log')
 $result=Join-Path $root 'axis-upgrade-results.json'
 if(-not(Test-Path -LiteralPath $result)){Get-Content -LiteralPath (Join-Path $root 'core.log') -Tail 30;throw 'No native test result'}
 $data=Get-Content -LiteralPath $result -Raw -Encoding UTF8;Write-Output $data
 if(-not($data|ConvertFrom-Json).passed){throw 'Axis native regression failed'}
 Write-Output ('Evidence: '+$physical)
}finally{Remove-Item Env:AICAD_TOOL_TEST_ROOT -ErrorAction SilentlyContinue;& subst ($letter+':') /D}
