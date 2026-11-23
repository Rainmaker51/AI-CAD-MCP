param([string]$Stage='')
$ErrorActionPreference='Stop';$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $Stage){$Stage=(Get-Content (Join-Path $repo '.tools/cad-manager-stage.txt') -Raw).Trim()}
$letter=@('Q','R','S','T','U','V','W','X','Y','Z')|Where-Object{-not(Test-Path ($_+':\'))}|Select-Object -First 1
if(-not $letter){throw 'No free drive alias'}
& subst ($letter+':') ([IO.Path]::GetFullPath($Stage))
if($LASTEXITCODE -ne 0){throw 'Cannot alias native test stage'}
try{
 $alias=$letter+':\';$test=Join-Path $alias ('validation/ribbon-'+(Get-Date -Format 'yyyyMMdd-HHmmss'));New-Item -ItemType Directory -Path $test|Out-Null
 Copy-Item -LiteralPath (Join-Path $alias 'package/AutoCAD') -Destination (Join-Path $test 'AutoCAD') -Recurse
 $loader=(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER
 $actual=Get-Content ([IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $loader) '../install.json'))) -Raw -Encoding UTF8|ConvertFrom-Json
 @{ContentRoot=$test;DataRoot=(Join-Path $test 'data');HostExecutable=$actual.HostExecutable}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $test 'install.json') -Encoding UTF8
 [IO.File]::WriteAllText((Join-Path $test '.manager-ribbon-test'),'Owned manager ribbon fixture')
 Copy-Item -LiteralPath (Join-Path $repo 'content/launch/blank-mm.dwg') -Destination (Join-Path $test 'blank.dwg')
 $script=@('_NETLOAD',('"'+(Join-Path $test 'AutoCAD/CAD.Bridge.AutoCAD.dll')+'"'),'AICADRIBBONEDITTEST','_QUIT','_Y')-join "`r`n"
 [IO.File]::WriteAllText((Join-Path $test 'test.scr'),$script+"`r`n")
 $env:AICAD_TOOL_TEST_ROOT=$test
 & (Join-Path $env:ProgramFiles 'Autodesk/AutoCAD 2021/accoreconsole.exe') /i (Join-Path $test 'blank.dwg') /s (Join-Path $test 'test.scr') /isolate CadManager090 (Join-Path $test 'profile') *> (Join-Path $test 'core.log')
 $result=Join-Path $test 'ribbon-edit-results.json'
 if(-not(Test-Path -LiteralPath $result)){Get-Content (Join-Path $test 'core.log') -Tail 25;throw 'No native ribbon result'}
 $value=Get-Content $result -Raw -Encoding UTF8;Write-Output $value
 if(-not($value|ConvertFrom-Json).passed){throw 'Native ribbon test failed'}
}finally{& subst ($letter+':') /D}
