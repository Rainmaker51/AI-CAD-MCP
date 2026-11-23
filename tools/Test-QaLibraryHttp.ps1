param([string]$Stage='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $Stage){$Stage=[IO.File]::ReadAllText((Join-Path $repo '.tools/qa-library-stage.txt')).Trim()}
$native=Get-ChildItem -LiteralPath (Join-Path $Stage 'validation') -Directory -Filter native-*|Sort-Object Name|Select-Object -Last 1
& (Join-Path $repo '.tools/dotnet/dotnet.exe') (Join-Path $repo 'tests/QaLibrary.Tests/bin/Debug/net10.0-windows/QaLibrary.Tests.dll') --verify-native (Join-Path $native.FullName 'library-data/cross-runtime.json')
if($LASTEXITCODE -ne 0){throw 'Native Framework / Host .NET cross-runtime verification failed'}
$output=& (Join-Path $repo '.tools/dotnet/dotnet.exe') (Join-Path $repo 'tests/QaLibrary.Tests/bin/Debug/net10.0-windows/QaLibrary.Tests.dll') --keep
if($LASTEXITCODE -ne 0){throw 'QA fixture test failed'}
$fixture=($output|Where-Object{$_ -like 'FIXTURE=*'}).Substring(8)
if(-not $fixture -or -not (Split-Path -Leaf $fixture).StartsWith('qa-library-tests-')){throw 'Fixture ownership marker missing'}
$test=Join-Path $Stage ('validation/http-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $test|Out-Null
Copy-Item -LiteralPath (Join-Path $Stage 'engine/Host') -Destination (Join-Path $test 'Host') -Recurse
$exe=Join-Path $test 'Host/AICad.Host.exe'
@{ContentRoot=$fixture;DataRoot=(Join-Path $fixture 'data');HostExecutable=$exe}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $test 'install.json') -Encoding UTF8
$env:DOTNET_ROOT=Join-Path $repo '.tools/dotnet'
$process=Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $test 'stderr.log')
try{
 $descriptor=Join-Path $fixture 'data/host.json'
 for($i=0;$i -lt 100 -and -not(Test-Path -LiteralPath $descriptor);$i++){Start-Sleep -Milliseconds 150;if($process.HasExited){throw 'Fixture Host exited early'}}
 $hostState=Get-Content -LiteralPath $descriptor -Raw|ConvertFrom-Json
 $headers=@{Authorization=('Bearer '+$hostState.token)}
 $results=@()
 foreach($kind in @('blocks','layers','hatches','texts','dims')){
  $view=Invoke-RestMethod -Uri ($hostState.url+'/api/qa-library/'+$kind) -Headers $headers
  if($view.protocol -ne 1 -or @($view.items).Count -ne 1){throw ('Invalid QA response: '+$kind)}
  $results+=@{kind=$kind;items=@($view.items).Count;sourceShown=([bool]$view.source)}
 }
 $authority=Invoke-RestMethod -Uri ($hostState.url+'/api/qa-library/authority') -Headers $headers
 if($authority.protocol -ne 2){throw 'Live CAD protocol not advertised'}
 $snapshots=@(Get-ChildItem -LiteralPath (Join-Path $fixture 'data/library-sync') -Directory|ForEach-Object{
  $file=Join-Path $_.FullName 'snapshot.json';if(Test-Path -LiteralPath $file){Get-Content -LiteralPath $file -Raw -Encoding UTF8|ConvertFrom-Json}
 })
 $live=$snapshots|Where-Object{$_.readMode -eq 'OPEN_DOCUMENT' -and @($_.items).Count -eq 5 -and $_.items.Kind -contains 'blocks'}|Select-Object -First 1
 $saved=$snapshots|Where-Object{$_.readMode -eq 'SAVED_FILE' -and $_.signature -eq $live.signature}|Select-Object -First 1
 if(-not $live -or -not $saved){throw 'Missing live HTTP fixtures'}
 $review=Invoke-RestMethod -Uri ($hostState.url+'/api/qa-library/live/register') -Headers $headers -Method Post -ContentType application/json -Body (@{id=$live.id}|ConvertTo-Json)
 foreach($kind in @('blocks','layers','hatches','texts','dims')){
  $view=Invoke-RestMethod -Uri ($hostState.url+'/api/qa-library/live/'+$live.id+'/'+$kind) -Headers $headers
  if(-not $view.live -or @($view.items).Count -ne 1){throw ('Invalid live CAD view: '+$kind)}
 }
 $prepared=Invoke-RestMethod -Uri ($hostState.url+'/api/qa-library/live/prepare') -Headers $headers -Method Post -ContentType application/json -Body (@{reviewId=$live.id;savedId=$saved.id}|ConvertTo-Json)
 if($prepared.id -ne $saved.id){throw 'Live saved approval did not prepare'}
 $blocked=$false
 try{Invoke-RestMethod -Uri ($hostState.url+'/api/library-sync/apply') -Headers $headers -Method Post -ContentType application/json -Body '{}'|Out-Null}
 catch{
  $response=$_.Exception.Response;$body=$_.ErrorDetails.Message
  if(-not $body){$reader=[IO.StreamReader]::new($response.GetResponseStream());try{$body=$reader.ReadToEnd()}finally{$reader.Dispose()}}
  if([int]$response.StatusCode -eq 409 -and $body -like '*QA_EDITOR_REQUIRED*'){$blocked=$true}else{throw ('Unexpected write rejection: '+$body)}
 }
 if(-not $blocked){throw 'Old web write endpoint bypassed QA authority'}
 $unauthorized=$false
 try{Invoke-RestMethod -Uri ($hostState.url+'/api/qa-library/blocks')|Out-Null}catch{if([int]$_.Exception.Response.StatusCode -eq 401){$unauthorized=$true}else{throw}}
 if(-not $unauthorized){throw 'QA endpoint accepted an unauthenticated request'}
 @{passed=$true;kinds=$results;originalLegacyCaptureReplayed=([bool]$env:QA_LEGACY_CAPTURE);crossRuntimeNativeSignature=$true;liveCadFiveKinds=$true;liveReview=$true;savedApprovalGuard=$true;legacyWritesBlocked=$blocked;authenticationRequired=$unauthorized}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $test 'result.json') -Encoding UTF8
 Write-Output ('PASS: HTTP five kinds, QA authority and authentication; '+$test)
}finally{
 if(-not $process.HasExited){Stop-Process -Id $process.Id -Force}
 # Keep the tiny fixture data as evidence; never copy tokens into the release package.
}
