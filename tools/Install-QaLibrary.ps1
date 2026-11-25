param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$Apply)
$ErrorActionPreference='Stop'
$build=[IO.Path]::GetFullPath($BuildDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $build 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($manifest.requiresHost -ne $false -or $manifest.version -notmatch '^1\.(?:[6789]|10|11)\.\d+$'){throw 'Choose a verified standalone QA 1.6-1.11 build'}
$package=[IO.Path]::GetFullPath((Join-Path $build 'package'))
if(@(Get-ChildItem -LiteralPath $package -File).Count -ne @($manifest.files).Count){throw 'Package contains unlisted files'}
foreach($entry in $manifest.files){
 $file=[IO.Path]::GetFullPath((Join-Path $package $entry.name))
 if(-not $file.StartsWith($package+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe manifest path'}
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.hash){throw ('Package changed: '+$entry.name)}
}
$loader=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
$manager=Split-Path -Parent $loader
$install=Get-Content -LiteralPath ([IO.Path]::GetFullPath((Join-Path $manager '../install.json'))) -Raw -Encoding UTF8|ConvertFrom-Json
# Use the exact installed Manager implementation; never write catalog or Installed by hand.
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'Newtonsoft.Json.dll'))|Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'CAD.Contracts.dll'))|Out-Null
$catalog=[AICad.Contracts.CadToolCatalog]::new($install.ContentRoot)
$index=$catalog.Read();$id='c6b4bffecd704cbcb56292579dcb6610'
$tool=$index.Items|Where-Object Id -eq $id
if(-not $tool -or $tool.Kind -ne 'dotnet'){throw 'Existing Rainmaker.Library entry was not found; use Manager to select the correct item'}
$catalog.Demand($tool)|Out-Null
$main=Join-Path $package 'Rainmaker.Library.dll'
if(-not $Apply){Write-Output ('Ready: '+$tool.RelativePath+' -> QA '+$manifest.version);Write-Output 'Use -Apply to run the installed Manager catalog update, preserving tool ID, tabs and pins.';return}
$updated=$catalog.Import($main,$index.Revision,$id)
$catalog.Demand($updated)|Out-Null
if($updated.FileHash -ne ($manifest.files|Where-Object name -eq 'Rainmaker.Library.dll').hash){throw 'Imported DLL hash differs'}
[pscustomobject]@{Version=$manifest.version;ToolId=$updated.Id;SelectedPath=$updated.RelativePath;FileHash=$updated.FileHash;RibbonPinsPreserved=($updated.RibbonPins.Count -eq $tool.RibbonPins.Count);RequiresHost=$false}|ConvertTo-Json
