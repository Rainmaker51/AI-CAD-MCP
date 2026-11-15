param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Release='app',[switch]$Phases)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot=if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){Join-Path $repo 'storage/04-builds/artifacts'}else{Join-Path $repo 'artifacts'}
$run=Join-Path $repo ('tests/fixtures/native-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($run)|Out-Null
[IO.File]::WriteAllText((Join-Path $run '.aicad-test-root'),'AI CAD isolated native test root')
$seed=Join-Path $repo 'content/launch/blank-mm.dwg'
Copy-Item -LiteralPath $seed -Destination (Join-Path $run 'seed.dwg')
$dll=(Join-Path $buildRoot "$Release/AutoCAD/CAD.Bridge.AutoCAD.dll").Replace('\','/')
$scr=Join-Path $run 'test.scr'
$commands="_NETLOAD`n`"$dll`"`nAICADNATIVETEST`n";if($Phases){$commands+="AICADPHASETEST`n"};$commands+="_QUIT`n_Y`n"
[IO.File]::WriteAllText($scr,$commands,[Text.Encoding]::ASCII)
$start=New-Object Diagnostics.ProcessStartInfo
$start.FileName='C:/Program Files/Autodesk/AutoCAD 2021/accoreconsole.exe'
$start.Arguments='/i "'+(Join-Path $run 'seed.dwg')+'" /s "'+$scr+'" /isolate AICadNative "'+(Join-Path $run 'profile')+'"'
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.WindowStyle='Hidden'
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$start.EnvironmentVariables['AICAD_TEST_ROOT']=$run
$process=[Diagnostics.Process]::Start($start)
$stdout=$process.StandardOutput.ReadToEndAsync()
$stderr=$process.StandardError.ReadToEndAsync()
$deadline=[DateTime]::UtcNow.AddMinutes(2)
while(-not $process.WaitForExit(1000)){if([DateTime]::UtcNow -gt $deadline){$process.Kill();throw "Native test timed out. Inspect $run"}}
[IO.File]::WriteAllText((Join-Path $run 'console.log'),$stdout.Result+$stderr.Result)
$result=Join-Path $run 'native-results.json'
if(-not(Test-Path -LiteralPath $result)){throw "No native result. Inspect $run/console.log"}
Get-Content -LiteralPath $result
if((Get-Content -LiteralPath $result -Raw|ConvertFrom-Json).failed -gt 0){throw 'Native fixture tests failed.'}
if($Phases){$phase=Join-Path $run 'phase-native-results.json';if(-not(Test-Path -LiteralPath $phase)){throw 'No phase native result'};Get-Content -LiteralPath $phase;if((Get-Content -LiteralPath $phase -Raw|ConvertFrom-Json).failed -gt 0){throw 'P0-P6 native tests failed.'}}
