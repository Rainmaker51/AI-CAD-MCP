$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sources=@(
 @{Name='AutoLispExt';Url='https://github.com/Autodesk-AutoCAD/AutoLispExt.git';Commit='74f59ee220a3689b6b13d26c7be8107c985b93dd'},
 @{Name='CadAddinManager';Url='https://github.com/chuongmep/CadAddinManager.git';Commit='cea835872e94c53f5f61f0d22436d6a476c1e64b'}
)
$base=Join-Path $repo 'third_party/reference'
New-Item -ItemType Directory -Path $base -Force | Out-Null
foreach($source in $sources){
 $destination=Join-Path $base $source.Name
 if(Test-Path -LiteralPath $destination){Write-Host "$($source.Name) already exists; preserving local changes.";continue}
 & git clone --no-checkout --filter=blob:none $source.Url $destination
 if($LASTEXITCODE -ne 0){throw "Download failed: $($source.Name)"}
 & git -C $destination checkout --detach $source.Commit
 if($LASTEXITCODE -ne 0){throw "Checkout failed: $($source.Name)"}
}
