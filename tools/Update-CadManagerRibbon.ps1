param([string]$BuildDirectory=$PSScriptRoot,[switch]$Apply)
$ErrorActionPreference='Stop'
$build=[IO.Path]::GetFullPath($BuildDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $build 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($manifest.version -ne '0.9.0' -or $manifest.component -ne 'CAD.Manager'){throw 'Choose the verified Manager 0.9.0 build'}
$payload=[IO.Path]::GetFullPath((Join-Path $build 'package/AutoCAD'))
if(@(Get-ChildItem -LiteralPath $payload -File).Count -ne @($manifest.files).Count){throw 'Unlisted package files'}
foreach($entry in $manifest.files){$file=[IO.Path]::GetFullPath((Join-Path $payload $entry.name));if(-not $file.StartsWith($payload+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.hash){throw 'Package path/hash mismatch'}}
$key='HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager'
$loader=(Get-ItemProperty $key).LOADER
if(-not(Test-Path -LiteralPath $loader)){throw 'Existing Manager loader not found'}
$target=[IO.Path]::GetFullPath((Split-Path -Parent $loader))
if((Split-Path -Leaf $loader) -ne 'CAD.Bridge.AutoCAD.dll'){throw 'Unexpected Manager loader name'}
$config=Get-Content -LiteralPath ([IO.Path]::GetFullPath((Join-Path $target '../install.json'))) -Raw -Encoding UTF8|ConvertFrom-Json
$repo=[IO.Path]::GetFullPath($config.ContentRoot).TrimEnd('\')
if(-not $target.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected Manager installation directory'}
if(-not $Apply){Write-Output ('Ready: '+[Diagnostics.FileVersionInfo]::GetVersionInfo($loader).FileVersion+' -> 0.9.0; '+$target);Write-Output 'Save drawings and close AutoCAD before -Apply. Catalog, add-in packages and Host are preserved.';return}
if(Get-Process -Name acad -ErrorAction SilentlyContinue){throw 'Save drawings and close AutoCAD before updating its loaded Manager DLLs.'}
$backup=Join-Path $repo ('artifacts/manager-install-backups/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $backup|Out-Null
$marker=Join-Path $target 'plugin-version.txt';$hadMarker=Test-Path -LiteralPath $marker
if($hadMarker){Copy-Item -LiteralPath $marker -Destination (Join-Path $backup 'plugin-version.txt')}
$changed=[Collections.Generic.List[object]]::new()
try{
 foreach($entry in $manifest.files){
  $destination=Join-Path $target $entry.name;$exists=Test-Path -LiteralPath $destination
  if($exists){Copy-Item -LiteralPath $destination -Destination (Join-Path $backup $entry.name)}
  $changed.Add([pscustomobject]@{name=$entry.name;existed=$exists})
  Copy-Item -LiteralPath (Join-Path $payload $entry.name) -Destination $destination -Force
  if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.hash){throw 'Installed Manager hash differs'}
 }
 [IO.File]::WriteAllText((Join-Path $target 'plugin-version.txt'),'0.9.0.0')
 $changed|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $backup 'files.json') -Encoding UTF8
 Write-Output ('Installed Manager 0.9.0. Backup: '+$backup)
}catch{
 foreach($entry in $changed){$destination=Join-Path $target $entry.name;if($entry.existed){Copy-Item -LiteralPath (Join-Path $backup $entry.name) -Destination $destination -Force}else{if(Test-Path -LiteralPath $destination){Remove-Item -LiteralPath $destination -Force}}}
 if($hadMarker){Copy-Item -LiteralPath (Join-Path $backup 'plugin-version.txt') -Destination $marker -Force}elseif(Test-Path -LiteralPath $marker){Remove-Item -LiteralPath $marker -Force}
 throw
}
