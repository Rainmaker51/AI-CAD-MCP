param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$Apply)
$ErrorActionPreference='Stop'
$build=[IO.Path]::GetFullPath($BuildDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $build 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($manifest.component -ne 'Rainmaker.Library.Reader' -or $manifest.readerProtocol -ne 1){throw 'Choose a verified Reader V1 package'}
$package=Join-Path $build 'package'
foreach($entry in $manifest.files){$file=[IO.Path]::GetFullPath((Join-Path $package $entry.name));if(-not $file.StartsWith($package+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -ne $entry.hash){throw 'Package path/hash mismatch'}}
$loader=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
$manager=Split-Path -Parent $loader
$config=Get-Content -LiteralPath ([IO.Path]::GetFullPath((Join-Path $manager '../install.json'))) -Raw -Encoding UTF8|ConvertFrom-Json
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'Newtonsoft.Json.dll'))|Out-Null
$contracts=[Reflection.Assembly]::LoadFrom((Join-Path $manager 'CAD.Contracts.dll'))
if(-not $contracts.GetType('AICad.Contracts.CadSharedComponentStore')){throw 'Install the separate-Reader Manager 0.10.0 build first'}
$store=[AICad.Contracts.CadSharedComponentStore]::new($config.DataRoot)
$current=$store.Read();$revision=if($current){$current.Revision}else{0}
if(-not $Apply){Write-Output ('Ready: install Reader '+$manifest.version+' separately at '+$store.Root);return}
$installed=$store.Install((Join-Path $package 'Rainmaker.Library.Reader.dll'),$revision)
Write-Output ('Installed Reader '+$installed.Version+' at '+$store.Demand($installed))
Write-Output 'Restart AutoCAD if Reader was already loaded. Manager, Axis and catalog were not changed.'
