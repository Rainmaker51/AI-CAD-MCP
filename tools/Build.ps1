param([switch]$Test,[ValidateSet('All','AutoCAD','Host','MCP')][string]$Component='All',[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Release='app',[ValidatePattern('^[a-zA-Z0-9_-]*$')][string]$RuntimeRelease='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdk=Join-Path $repo '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
$env:MSBUILDDISABLENODEREUSE='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:NUGET_PACKAGES=[IO.Path]::GetFullPath((Join-Path $repo '.tools/nuget/packages'))
$buildRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){Join-Path $repo 'storage/04-builds/artifacts'}else{Join-Path $repo 'artifacts'}
Push-Location $repo
try {
 $projects=if($Component -eq 'AutoCAD'){@('CAD.Bridge.AutoCAD')}elseif($Component -eq 'Host'){@('Desktop.Host')}elseif($Component -eq 'MCP'){@('MCP.Server')}else{@('CAD.Bridge.AutoCAD','Desktop.Host','MCP.Server')}
 foreach($name in $projects) {
  & $sdk restore "src/$name/$name.csproj" --ignore-failed-sources
  if($LASTEXITCODE -ne 0){throw "Restore failed: $name"}
  $dest=if($name -eq 'CAD.Bridge.AutoCAD'){'AutoCAD'}elseif($name -eq 'Desktop.Host'){'Host'}else{'MCP'}
  & $sdk publish "src/$name/$name.csproj" --no-restore -c Release -o "$buildRoot/$Release/$dest"
  if($LASTEXITCODE -ne 0){throw "Build failed: $name"}
 }
 if($Component -eq 'All' -or $Component -eq 'Host'){
  & $sdk publish 'src/CAD.HashCompat/CAD.HashCompat.csproj' -c Release -o "$buildRoot/$Release/Host/HashCompat"
  if($LASTEXITCODE -ne 0){throw 'Framework digest helper build failed'}
  & $sdk publish 'src/Desktop.Launcher/Desktop.Launcher.csproj' -c Release -o "$buildRoot/launcher"
  if($LASTEXITCODE -ne 0){throw 'Desktop launcher build failed'}
  & $sdk publish 'src/CAD.LibraryScanner/CAD.LibraryScanner.csproj' -c Release -o "$buildRoot/$Release/Host/LibraryScanner"
  if($LASTEXITCODE -ne 0){throw 'Native library scanner build failed'}


 }
 if(-not $RuntimeRelease){$RuntimeRelease=$Release}
 $data=if($RuntimeRelease -eq 'app'){'data'}else{'data-'+$RuntimeRelease}
 $python=Get-ChildItem 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.*/dependencies/python/python.exe' -ErrorAction SilentlyContinue|Sort-Object FullName -Descending|Select-Object -First 1 -ExpandProperty FullName
 $poppler=(Get-Command pdftoppm -ErrorAction SilentlyContinue).Source
 if(-not $python -and $poppler){$python=Join-Path ([IO.Path]::GetFullPath((Join-Path (Split-Path $poppler -Parent) '../../../../'))) 'python/python.exe'}
 $codex=(Get-Command codex -ErrorAction SilentlyContinue).Source
 $physicalData=Join-Path $repo "storage/02-app-data/$data"
 $dataRoot=if(Test-Path -LiteralPath $physicalData -PathType Container){$physicalData}else{Join-Path $repo $data}
 $config=[ordered]@{DataRoot=$dataRoot;ContentRoot=$repo;HostExecutable=(Join-Path $buildRoot "$RuntimeRelease/Host/AICad.Host.exe");PythonExecutable=$python;CodexExecutable=$codex}
 [IO.Directory]::CreateDirectory((Join-Path $repo $data))|Out-Null
 $config|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $buildRoot "$Release/install.json") -Encoding utf8
 if($Test){& $sdk run --project tests/AICad.Tests/AICad.Tests.csproj -c Release;if($LASTEXITCODE -ne 0){throw 'Tests failed'}}
 Write-Host 'Build ready. Open Mo AI CAD.cmd.'
}finally{Pop-Location}
