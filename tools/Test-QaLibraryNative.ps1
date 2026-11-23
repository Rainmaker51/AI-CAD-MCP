param([string]$Stage='',[string]$ManagerDirectory='',[string]$LibraryPackage='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $Stage){$Stage=[IO.File]::ReadAllText((Join-Path $repo '.tools/qa-library-stage.txt')).Trim()}
$physicalStage=[IO.Path]::GetFullPath($Stage)
$fixtureOutput=& (Join-Path $repo '.tools/dotnet/dotnet.exe') (Join-Path $repo 'tests/QaLibrary.Tests/bin/Debug/net10.0-windows/QaLibrary.Tests.dll') --keep
if($LASTEXITCODE -ne 0){throw 'Standalone business fixture failed'}
$env:QALIB_STANDALONE_FIXTURE=($fixtureOutput|Where-Object{$_ -like 'FIXTURE=*'}).Substring(8)
if(-not $env:QALIB_STANDALONE_FIXTURE){throw 'Missing standalone fixture'}
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home';$env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget/packages'
& (Join-Path $repo '.tools/dotnet/dotnet.exe') build (Join-Path $repo 'tests/QaLibrary.NativeHarness') -c Release -m:1 -nr:false -v:q -o (Join-Path $physicalStage 'harness')
if($LASTEXITCODE -ne 0){throw 'Native harness build failed'}
# Core 2021 still has MAX_PATH limitations while cloning its isolated profile.
# A temporary drive alias keeps the files inside this add-in's working directory.
$letter=@('Q','R','S','T','U','V','W','X','Y','Z')|Where-Object{-not(Test-Path ($_+':\'))}|Select-Object -First 1
if(-not $letter){throw 'No free drive alias for the Core 2021 test'}
& subst ($letter+':') $physicalStage
if($LASTEXITCODE -ne 0){throw 'Cannot create temporary Core drive alias'}
$Stage=$letter+':\'
try{
$test=Join-Path $Stage ('validation/native-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $test|Out-Null
$manager=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
if($ManagerDirectory){$manager=Join-Path $ManagerDirectory 'CAD.Bridge.AutoCAD.dll'}
if(-not (Test-Path -LiteralPath $manager)){throw 'Configured AutoCAD 2021 Manager is missing'}
$autoCAD=Join-Path $test 'AutoCAD';New-Item -ItemType Directory -Path $autoCAD|Out-Null
Get-ChildItem -LiteralPath (Split-Path -Parent $manager) -File | Where-Object Extension -in @('.dll','.pdb') | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $autoCAD}
if(-not $LibraryPackage){$LibraryPackage=Join-Path $Stage 'package'}
Copy-Item -LiteralPath $LibraryPackage -Destination (Join-Path $test 'library') -Recurse
@{ContentRoot=$test;DataRoot=(Join-Path $test 'data');HostExecutable=(Join-Path $Stage 'engine/Host/AICad.Host.exe')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $test 'install.json') -Encoding UTF8
[IO.File]::WriteAllText((Join-Path $test '.aicad-tools-test'),'Isolated QA 1.4 Manager integration test')
Copy-Item -LiteralPath (Join-Path $repo 'content/launch/blank-mm.dwg') -Destination (Join-Path $test 'blank.dwg')
$script=@('_NETLOAD',('"'+(Join-Path $autoCAD 'CAD.Bridge.AutoCAD.dll')+'"'),'_NETLOAD',('"'+(Join-Path $Stage 'harness/QaLibrary.NativeHarness.dll')+'"'),'QANATIVEVERIFY','_QUIT','_Y')-join "`r`n"
[IO.File]::WriteAllText((Join-Path $test 'test.scr'),$script+"`r`n")
$env:AICAD_TOOL_TEST_ROOT=$test
$console=Join-Path $env:ProgramFiles 'Autodesk/AutoCAD 2021/accoreconsole.exe'
& $console /i (Join-Path $test 'blank.dwg') /s (Join-Path $test 'test.scr') /isolate QaLibrary140 (Join-Path $test 'profile') *> (Join-Path $test 'core.log')
$result=Join-Path $test 'ribbon-manager-results.json'
if(-not(Test-Path -LiteralPath $result)){Get-Content -LiteralPath (Join-Path $test 'core.log') -Tail 30;throw 'Native test did not produce results'}
Get-Content -LiteralPath $result -Raw
if(-not (Get-Content -LiteralPath $result -Raw|ConvertFrom-Json).passed){throw 'Native Manager test failed'}
Write-Output ('Native test: '+$test)
}finally{& subst ($letter+':') /D}
