param([string]$RepositoryRoot=(Get-Location).Path,[string]$BuildDirectory=$PSScriptRoot,[switch]$Apply)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$build=[IO.Path]::GetFullPath($BuildDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $build 'manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
$version=[Version]::Parse($manifest.version)
if($version.Major -ne 1 -or $version.Minor -ne 5 -or $manifest.protocol -ne 2 -or -not $manifest.engineFiles){throw 'Choose a complete QA 1.5 build folder with protocol 2'}
$loader=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
$install=[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $loader) '../install.json'))
$config=Get-Content -LiteralPath $install -Raw -Encoding UTF8|ConvertFrom-Json
if([IO.Path]::GetFullPath($config.ContentRoot).TrimEnd('\') -ne $repo){throw 'Manager belongs to a different repository'}
$target=[IO.Path]::GetFullPath((Split-Path -Parent $config.HostExecutable)).TrimEnd('\')
if(-not $target.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $config.HostExecutable) -ne 'AICad.Host.exe'){throw 'Unexpected Host installation path'}
$payload=Join-Path $build 'engine/Host'
foreach($entry in $manifest.engineFiles){
 $inputFile=[IO.Path]::GetFullPath((Join-Path $payload $entry.name))
 $outputFile=[IO.Path]::GetFullPath((Join-Path $target $entry.name))
 if(-not $inputFile.StartsWith([IO.Path]::GetFullPath($payload)+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $outputFile.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe engine manifest path'}
 if((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.hash){throw ('Engine payload changed: '+$entry.name)}
}
if(-not $Apply){Write-Output ('Ready: '+$manifest.version+' -> '+$target);Write-Output 'Use -Apply to update this machine. AutoCAD is left running; Manager DLL and catalog are not changed.';return}
# Do not stop a drawing execution or leave an in-flight CAD dispatch behind.
$plans=Join-Path $config.DataRoot 'plans'
if(Test-Path -LiteralPath $plans){foreach($file in Get-ChildItem -LiteralPath $plans -Filter '*.json' -File){$plan=Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8|ConvertFrom-Json;if($plan.Status -eq 'DISPATCHED'){throw 'A CAD plan is running. Finish it before updating the background service.'}}}
$descriptor=Join-Path $config.DataRoot 'host.json'
$wasRunning=$false
if(Test-Path -LiteralPath $descriptor){
 $hostInfo=Get-Content -LiteralPath $descriptor -Raw|ConvertFrom-Json
 $process=Get-Process -Id $hostInfo.processId -ErrorAction SilentlyContinue
 if($process -and [IO.Path]::GetFullPath($process.Path) -eq [IO.Path]::GetFullPath($config.HostExecutable)){$wasRunning=$true;Stop-Process -Id $process.Id -Force;$process.WaitForExit()}
}
$backup=Join-Path $repo ('artifacts/qa-engine-backups/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $backup|Out-Null
$changed=[Collections.Generic.List[object]]::new()
function Start-HostEngine {
 $env:DOTNET_ROOT=Join-Path $repo '.tools/dotnet'
 Start-Process -FilePath $config.HostExecutable -WorkingDirectory $target -WindowStyle Hidden|Out-Null
}
try{
 foreach($entry in $manifest.engineFiles){
  $outputFile=Join-Path $target $entry.name;$copy=Join-Path $backup $entry.name;$exists=Test-Path -LiteralPath $outputFile
  if($exists){New-Item -ItemType Directory -Path (Split-Path -Parent $copy) -Force|Out-Null;Copy-Item -LiteralPath $outputFile -Destination $copy}
  $changed.Add([pscustomobject]@{name=$entry.name;existed=$exists})
  New-Item -ItemType Directory -Path (Split-Path -Parent $outputFile) -Force|Out-Null
  Copy-Item -LiteralPath (Join-Path $payload $entry.name) -Destination $outputFile -Force
  if((Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.hash){throw ('Installed engine hash mismatch: '+$entry.name)}
 }
 $changed|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $backup 'files.json') -Encoding UTF8
 Start-HostEngine
 Write-Output ('Updated QA background engine. Backup: '+$backup)
 Write-Output 'In CAD Tool Manager, select Rainmaker.Library > Cap nhat file and choose package/Rainmaker.Library.dll from this build.'
}catch{
 foreach($entry in $changed){$outputFile=Join-Path $target $entry.name;if($entry.existed){Copy-Item -LiteralPath (Join-Path $backup $entry.name) -Destination $outputFile -Force}else{if(Test-Path -LiteralPath $outputFile){Remove-Item -LiteralPath $outputFile -Force}}}
 if($wasRunning){Start-HostEngine}
 throw
}
