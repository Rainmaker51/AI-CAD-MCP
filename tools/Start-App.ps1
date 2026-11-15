param([switch]$NoBrowser,[switch]$OpenAutoCAD,[switch]$Demo,[switch]$Phases,[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Release='app',[ValidateSet('chat','account')][string]$Page='chat',[ValidatePattern('^[a-zA-Z0-9_-]*$')][string]$PluginRelease='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){Join-Path $repo 'storage/04-builds/artifacts'}else{Join-Path $repo 'artifacts'}
$env:DOTNET_ROOT=[IO.Path]::GetFullPath((Join-Path $repo '.tools/dotnet'))
$hostExe=Join-Path $buildRoot "$Release/Host/AICad.Host.exe"
if(-not(Test-Path -LiteralPath $hostExe)){throw 'Run tools/Build.ps1 first.'}
$config=Get-Content -LiteralPath (Join-Path $buildRoot "$Release/install.json") -Raw|ConvertFrom-Json
$descriptor=Join-Path $config.DataRoot 'host.json'
$running=$false
if(Test-Path -LiteralPath $descriptor){try{$info=Get-Content -LiteralPath $descriptor -Raw|ConvertFrom-Json;$process=Get-Process -Id $info.processId -ErrorAction Stop;$running=$process.Path -eq $hostExe}catch{}}
if(-not $running){Start-Process -FilePath $hostExe -WorkingDirectory (Split-Path $hostExe) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $config.DataRoot 'host.stdout.log') -RedirectStandardError (Join-Path $config.DataRoot 'host.stderr.log');for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 200;if(Test-Path -LiteralPath $descriptor){try{$info=Get-Content -LiteralPath $descriptor -Raw|ConvertFrom-Json;$candidate=Get-Process -Id $info.processId -ErrorAction Stop;if($candidate.Path -eq $hostExe){$running=$true;break}}catch{}}}}
if(-not $running){throw 'Host did not start. Read data/host.stderr.log.'}
if(-not $NoBrowser){Start-Process ($info.url+'/?page='+$Page+'#token='+$info.token)}
if($OpenAutoCAD){
 $cad='C:/Program Files/Autodesk/AutoCAD 2021/acad.exe'
 if(-not(Test-Path -LiteralPath $cad)){throw 'AutoCAD 2021 was not found at the configured path.'}
 if(-not $PluginRelease){$PluginRelease=$Release}
 $dll=(Join-Path $buildRoot "$PluginRelease/AutoCAD/CAD.Bridge.AutoCAD.dll").Replace('\','/')
 $script=Join-Path $buildRoot ("load-plugin-"+$Release+".scr")
 $commands="_NETLOAD`n`"$dll`"`nAICAD`n"
 if($Phases){$commands+="AICADP0P6DEMO`n"}elseif($Demo){$commands+="AICADDEMO`n"}
 [IO.File]::WriteAllText($script,$commands,[Text.Encoding]::ASCII)
 $launchRoot=Join-Path $config.DataRoot ('drawings/launch-'+[Guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($launchRoot)|Out-Null
 $drawing=Join-Path $launchRoot 'AI-CAD-START.dwg'
 Copy-Item -LiteralPath (Join-Path $repo 'content/launch/blank-mm.dwg') -Destination $drawing
 # Graphical AutoCAD uses /b for scripts; /s is specific to Core Console.
 $cadProcess=Start-Process -FilePath $cad -ArgumentList @(('"'+$drawing+'"'),'/nologo','/b',('"'+$script+'"')) -WindowStyle Normal -PassThru
 Write-Host ('Started AutoCAD 2021 process '+$cadProcess.Id+' with a new isolated test drawing.')
}
Write-Host ('AI CAD ready: '+$info.url+(' (LIVE). Use NETLOAD in AutoCAD to load artifacts/'+$Release+'/AutoCAD/CAD.Bridge.AutoCAD.dll.'))
