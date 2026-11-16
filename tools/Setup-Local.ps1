# Recreate machine-local paths and build the current application from source.
param([string]$WarehousePath,[switch]$InstallSdk,[switch]$SkipOcr,[switch]$CheckOnly,[switch]$Test,[switch]$DownloadReferences,[switch]$StopHost)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$pointer=Join-Path $repo 'warehouse.local.json'
if(-not $WarehousePath -and (Test-Path -LiteralPath $pointer)){$WarehousePath=(Get-Content -LiteralPath $pointer -Raw|ConvertFrom-Json).path}
if(-not $WarehousePath){throw 'Provide -WarehousePath pointing to the complete synchronized KHO-AI-CAD folder.'}
if(-not [IO.Path]::IsPathRooted($WarehousePath) -or $WarehousePath.StartsWith('http')){throw 'WarehousePath must be a local synchronized folder, not a web link.'}
$WarehousePath=[IO.Path]::GetFullPath($WarehousePath)
$marker=Join-Path $WarehousePath 'warehouse.json'
if(-not(Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw|ConvertFrom-Json).schemaVersion -ne 1){throw 'Warehouse marker missing or invalid. Synchronize the entire warehouse first.'}
foreach($folder in @('Versions','Templates','Skills','Catalog','CAD/Assets','CAD/Originals','Hatches','Styles')){if(-not(Test-Path -LiteralPath (Join-Path $WarehousePath $folder) -PathType Container)){throw "Warehouse folder missing: $folder"}}
if(-not(Test-Path -LiteralPath (Join-Path $WarehousePath 'CAD/Originals/ISO_QuangAnh_ThuVien.dwg'))){throw 'The base library ISO_QuangAnh_ThuVien.dwg is missing from CAD/Originals.'}
foreach($file in @('acad.exe','accoreconsole.exe','acmgd.dll','acdbmgd.dll','accoremgd.dll')){if(-not(Test-Path -LiteralPath (Join-Path 'C:/Program Files/Autodesk/AutoCAD 2021' $file))){throw "AutoCAD 2021 prerequisite missing: $file"}}
$sdk=Join-Path $repo '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){$command=Get-Command dotnet.exe -ErrorAction SilentlyContinue;$sdk=if($command){$command.Source}else{$null}}
Push-Location $repo
try{
 $sdkReady=$false
 if($sdk){try{$installed=& $sdk --version 2>$null;$sdkReady=$LASTEXITCODE -eq 0 -and $installed -match '^10\.0\.'}catch{$sdkReady=$false}}
 if(-not $sdkReady){
  if(-not $InstallSdk -or $CheckOnly){throw 'SDK required by global.json is unavailable. Rerun with -InstallSdk, or install the matching .NET SDK.'}
  & (Join-Path $PSScriptRoot 'Install-DotNet.ps1')
 }
 if(-not $SkipOcr){& (Join-Path $PSScriptRoot 'Setup-Ocr.ps1') -CheckOnly:$CheckOnly}
 Write-Host 'Prerequisites OK: AutoCAD 2021, .NET SDK, warehouse and OCR selection.'
 if($CheckOnly){Write-Host 'Read-only checks passed. No warehouse pointer or build output changed.';return}
 $buildRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){Join-Path $repo 'storage/04-builds/artifacts'}else{Join-Path $repo 'artifacts'}
 $dataRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/02-app-data/data-p0-p6')){Join-Path $repo 'storage/02-app-data/data-p0-p6'}else{Join-Path $repo 'data-p0-p6'}
 $hostFile=Join-Path $dataRoot 'host.json'
 if(Test-Path -LiteralPath $hostFile){
  $descriptor=Get-Content -LiteralPath $hostFile -Raw|ConvertFrom-Json
  $running=Get-Process -Id $descriptor.processId -ErrorAction SilentlyContinue
  if($running -and $running.Path -eq (Join-Path $buildRoot 'p0-p6/Host/AICad.Host.exe')){
   if(-not $StopHost){throw 'AI CAD Host is running. Rerun with -StopHost after finishing active tasks.'}
   Stop-Process -Id $running.Id
  }
 }
 $native=Join-Path $buildRoot 'p0-p6/AutoCAD/CAD.Bridge.AutoCAD.dll'
 if(Test-Path -LiteralPath $native){try{$probe=[IO.File]::Open($native,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$probe.Dispose()}catch{throw 'Plugin DLL is loaded. Save and close the AutoCAD session using this build before updating.'}}
 @{path=$WarehousePath}|ConvertTo-Json|Set-Content -LiteralPath $pointer -Encoding utf8
 & (Join-Path $PSScriptRoot 'Build.ps1') -Component All -Release p0-p6 -RuntimeRelease p0-p6 -Test:$Test
 if(-not(Test-Path -LiteralPath (Join-Path $buildRoot 'p0-p6/AutoCAD/plugin-version.txt'))){throw 'Native version marker missing after build.'}
 if($DownloadReferences){& (Join-Path $PSScriptRoot 'Get-ToolManagerReferences.ps1')}
 Write-Host 'Setup complete. Open Mo AI CAD.cmd; log in to AI separately on this machine.'
}finally{Pop-Location}
