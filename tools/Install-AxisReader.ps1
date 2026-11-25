param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$Apply)
$ErrorActionPreference='Stop'
$build=[IO.Path]::GetFullPath($BuildDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $build 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($manifest.component -ne 'Rainmaker.Axis' -or $manifest.version -ne '1.9.0' -or $manifest.readerProtocol -ne 1){throw 'Choose a verified Axis 1.9.0 Reader V1 build'}
$package=Join-Path $build 'package'
if(@(Get-ChildItem -LiteralPath $package -File).Count -ne @($manifest.files).Count){throw 'Unlisted package files'}
foreach($entry in $manifest.files){
 $file=[IO.Path]::GetFullPath((Join-Path $package $entry.name))
 if(-not $file.StartsWith($package+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -ne $entry.hash){throw 'Package path/hash mismatch'}
}
$loader=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
$manager=Split-Path -Parent $loader
if([version][Diagnostics.FileVersionInfo]::GetVersionInfo($loader).FileVersion -lt [version]'0.10.0.0'){throw 'Install Manager 0.10.0 before selecting Axis 1.9.0'}
$install=Get-Content -LiteralPath ([IO.Path]::GetFullPath((Join-Path $manager '../install.json'))) -Raw -Encoding UTF8|ConvertFrom-Json
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'Newtonsoft.Json.dll'))|Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $manager 'CAD.Contracts.dll'))|Out-Null
$components=[AICad.Contracts.CadSharedComponentStore]::new($install.DataRoot)
$selected=$components.Read();if(-not $selected){throw 'Install Reader in Manager > Shared components first'}
$reader=$components.Demand($selected)
if([Reflection.AssemblyName]::GetAssemblyName($reader).Version -ne [version]'1.0.0.0'){throw 'Reader V1 required'}
$catalog=[AICad.Contracts.CadToolCatalog]::new($install.ContentRoot)
$index=$catalog.Read();$id='rainmaker-axis';$tool=$index.Items|Where-Object Id -eq $id
if(-not $tool -or $tool.Kind -ne 'dotnet'){throw 'Existing Axis entry missing; select it in Manager'}
$catalog.Demand($tool)|Out-Null
if(-not $Apply){Write-Output ('Ready: '+$tool.RelativePath+' -> Axis 1.9.0; tool ID, settings, tabs and pins preserved');return}
if(Get-Process -Name acad -ErrorAction SilentlyContinue){throw 'Close AutoCAD before the coordinated Manager/Axis upgrade'}
$updated=$catalog.Import((Join-Path $package 'Rainmaker.Axis.dll'),$index.Revision,$id)
$catalog.Demand($updated)|Out-Null
if($updated.FileHash -ne ($manifest.files|Where-Object name -eq 'Rainmaker.Axis.dll').hash){throw 'Installed Axis hash differs'}
[pscustomobject]@{Version=$manifest.version;ToolId=$updated.Id;SelectedPath=$updated.RelativePath;FileHash=$updated.FileHash;RibbonPinsPreserved=($updated.RibbonPins.Count -eq $tool.RibbonPins.Count);ReaderProtocol=1}|ConvertTo-Json
