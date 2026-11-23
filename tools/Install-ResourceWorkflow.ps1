param(
 [Parameter(Mandatory=$true)][string]$QaBuild,
 [Parameter(Mandatory=$true)][string]$AxisBuild,
 [Parameter(Mandatory=$true)][string]$Runtime,
 [switch]$Apply,
 [switch]$ArchiveLegacy
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));Set-Location $repo
$warehouse=[IO.Path]::GetFullPath((Get-Content warehouse.local.json -Raw -Encoding UTF8|ConvertFrom-Json).path)
function VerifyBuild([string]$path,[string]$component,[string]$version){
 $path=[IO.Path]::GetFullPath($path)
 $allowed=[IO.Path]::GetFullPath((Join-Path $warehouse ('Tools/dotnet/_Development/'+$component+'/versions')))+'\'
 if(-not $path.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Choose an immutable development build'}
 $m=Get-Content (Join-Path $path 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
 if($m.component -ne $component -or $m.version -ne $version){throw 'Unexpected component/version'}
 $package=[IO.Path]::GetFullPath((Join-Path $path 'package'))
 if(@(Get-ChildItem $package -File).Count -ne @($m.files).Count){throw 'Package file count differs'}
 foreach($f in $m.files){$p=[IO.Path]::GetFullPath((Join-Path $package $f.name));if(-not $p.StartsWith($package+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash $p).Hash.ToLowerInvariant() -ne $f.hash){throw 'Package path/hash differs'}}
 return $path
}
$QaBuild=VerifyBuild $QaBuild 'Rainmaker.Library' '1.7.0'
$AxisBuild=VerifyBuild $AxisBuild 'Rainmaker.Axis' '1.8.0'
$Runtime=[IO.Path]::GetFullPath($Runtime)
$allowed=[IO.Path]::GetFullPath((Join-Path $warehouse 'Tools/dotnet/_Development/CAD.Manager/versions'))+'\'
if(-not $Runtime.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected runtime source'}
$runtimeManifest=Get-Content (Join-Path (Split-Path $Runtime) 'runtime-manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($f in $runtimeManifest.files){$p=[IO.Path]::GetFullPath((Join-Path $Runtime $f.path));if(-not $p.StartsWith($Runtime+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash $p).Hash.ToLowerInvariant() -ne $f.sha256){throw 'Runtime path/hash differs'}}
if(-not $Apply){Write-Output 'Verified QA 1.7.0, Axis 1.8.0 and runtime. Save drawings and close AutoCAD before -Apply.';return}
if(Get-Process acad -ErrorAction SilentlyContinue){throw 'Save drawings and close every AutoCAD window before updating'}
$config=Get-Content artifacts/p0-p6/install.json -Raw -Encoding UTF8|ConvertFrom-Json
$descriptor=Join-Path $config.DataRoot 'host.json'
if(Test-Path $descriptor){
 $state=Get-Content $descriptor -Raw|ConvertFrom-Json;$process=Get-Process -Id $state.processId -ErrorAction SilentlyContinue
 if($process){if($process.Path -ne $config.HostExecutable){throw 'Host PID/path mismatch; did not stop the process'};Stop-Process -Id $process.Id;$process.WaitForExit(10000)|Out-Null}
}
if($ArchiveLegacy){
 $python=$config.PythonExecutable
 if(-not(Test-Path -LiteralPath $python)){throw 'Configured Python unavailable; archival not started'}
 & $python (Join-Path $PSScriptRoot 'Archive-LegacyApp.py') --repo $repo --apply
 if($LASTEXITCODE -ne 0){throw 'Archival failed; inspect its rollback before continuing'}
}
& (Join-Path $PSScriptRoot 'Install-CadRibbonManager.ps1') -ReleasePath $Runtime
if($LASTEXITCODE -ne 0){throw 'Runtime installation failed'}
$loader=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
$manager=Split-Path $loader
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'Newtonsoft.Json.dll'))|Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'CAD.Contracts.dll'))|Out-Null
$catalog=[AICad.Contracts.CadToolCatalog]::new($repo)
foreach($entry in @(@('c6b4bffecd704cbcb56292579dcb6610',$QaBuild,'Rainmaker.Library.dll'),@('rainmaker-axis',$AxisBuild,'Rainmaker.Axis.dll'))){
 $index=$catalog.Read();$id=$entry[0];$old=$index.Items|Where-Object Id -eq $id
 if(-not $old){throw 'The existing add-in entry is missing'}
 $catalog.Demand($old)|Out-Null
 $before=[AICad.Contracts.Json]::Write(@{name=$old.Name;group=$old.Group;notes=$old.Notes;tab=$old.TabId;pins=$old.RibbonPins;enabled=$old.Enabled})
 $updated=$catalog.Import((Join-Path $entry[1] ('package/'+$entry[2])),$index.Revision,$id)
 $catalog.Demand($updated)|Out-Null
 $after=[AICad.Contracts.Json]::Write(@{name=$updated.Name;group=$updated.Group;notes=$updated.Notes;tab=$updated.TabId;pins=$updated.RibbonPins;enabled=$updated.Enabled})
 if($before -ne $after){throw 'Add-in metadata changed unexpectedly'}
 Write-Output ('Updated '+$updated.Name+': '+$updated.RelativePath)
}
Write-Output 'Installed resource workflow; catalog IDs, settings and Ribbon preserved. Reopen AutoCAD.'
