param([switch]$Build,[switch]$Check,[switch]$MigrateInstalled,[string]$RepositoryRoot='',[string]$ReleasePath='')
$ErrorActionPreference='Stop'
$repo=if($RepositoryRoot){[IO.Path]::GetFullPath($RepositoryRoot)}else{[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))}
$updatePaths=Join-Path $repo 'tools/Update-CadManagerPaths.ps1'
$buildScript=Join-Path $repo 'tools/Build.ps1'
if(!(Test-Path -LiteralPath $updatePaths)){throw ('RepositoryRoot khong dung hoac repo chua du tep: '+$updatePaths)}
if($Build -and !(Test-Path -LiteralPath $buildScript)){throw ('Thieu build script trong repo: '+$buildScript)}
if(!$Check -and (Get-Process -Name acad -ErrorAction SilentlyContinue)){throw 'Luu DWG va dong AutoCAD truoc khi cap nhat CAD Tool Manager.'}
$env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$sdk=Join-Path $repo '.tools/dotnet/dotnet.exe'
if(!(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
$buildRoot=Join-Path $repo 'artifacts'
if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){$buildRoot=Join-Path $repo 'storage/04-builds/artifacts'}
$release=Join-Path $buildRoot 'p0-p6-ribbon-manager'
if($ReleasePath){
    $source=[IO.Path]::GetFullPath($ReleasePath)
    if(!(Test-Path -LiteralPath (Join-Path $source 'AutoCAD/CAD.Bridge.AutoCAD.dll'))){throw 'ReleasePath khong chua CAD Manager.'}
    $manifest=Join-Path (Split-Path -Parent $source) 'runtime-manifest.json'
    if(Test-Path -LiteralPath $manifest){
        $bundle=Get-Content -LiteralPath $manifest -Raw -Encoding UTF8|ConvertFrom-Json
        foreach($file in $bundle.files){
            $path=[IO.Path]::GetFullPath((Join-Path $source $file.path))
            if(!$path.StartsWith($source.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Duong dan manifest khong hop le.'}
            if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256){throw ('Goi Manager chua dong bo du: '+$file.path)}
        }
    }
    if($Check){$release=$source}
    elseif($source -ne $release){New-Item -ItemType Directory -Path $release -Force|Out-Null;Copy-Item -Path (Join-Path $source '*') -Destination $release -Recurse -Force}
}
if($Build){& $buildScript -Component All -Release 'p0-p6-ribbon-manager' -RuntimeRelease 'p0-p6'}
$package=Join-Path $release 'LibraryPackage'
$dll=Join-Path $release 'AutoCAD/CAD.Bridge.AutoCAD.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Chua build CAD Manager. Chay lai voi -Build.'}
$product='SOFTWARE\Autodesk\AutoCAD\R24.0\ACAD-4101:409'
if(!(Test-Path -LiteralPath "HKLM:\$product")){throw 'Khong tim thay AutoCAD 2021 tren may.'}
if($Check){Write-Output "DLL: $dll";Write-Output 'Manager: AICADTOOLS / AICADRIBBON';exit 0}
# Bootstrap the runtime folder on a fresh clone before updating local paths.
$runtime=Join-Path $buildRoot 'p0-p6'
New-Item -ItemType Directory -Path $runtime -Force|Out-Null
if(!(Test-Path -LiteralPath (Join-Path $runtime 'install.json'))){Copy-Item -LiteralPath (Join-Path $release 'install.json') -Destination (Join-Path $runtime 'install.json')}
if(!(Test-Path -LiteralPath (Join-Path $runtime 'Host/AICad.Host.exe'))){Copy-Item -LiteralPath (Join-Path $release 'Host') -Destination (Join-Path $runtime 'Host') -Recurse}
# Library hand-offs belong in _Development; installing Manager must not create another Library ZIP.
if(Test-Path -LiteralPath (Join-Path $release 'Launcher/AICad.Launcher.dll')){
    New-Item -ItemType Directory -Path (Join-Path $buildRoot 'launcher') -Force|Out-Null
    Copy-Item -Path (Join-Path $release 'Launcher/*') -Destination (Join-Path $buildRoot 'launcher') -Recurse -Force
}
& $updatePaths
$legacyKey="$product\Applications\QALibraryManager"
$legacy=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($legacyKey)
if($legacy){
    $backupDir=Join-Path $buildRoot ('ribbon-manager/install-backups-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backupDir -Force|Out-Null
    $saved=@{key=$legacyKey;loader=$legacy.GetValue('LOADER');managed=$legacy.GetValue('MANAGED');loadCtrls=$legacy.GetValue('LOADCTRLS')}
    $legacy.Dispose()
    [IO.File]::WriteAllText((Join-Path $backupDir 'library-autoload.json'),($saved|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($legacyKey,$false)
    Write-Output 'Da go tu nap rieng cua Rainmaker.Library. Goi DLL van duoc giu de nhap bang Manager.'
}
$stagedHost=Join-Path $release 'Host'
if(Test-Path -LiteralPath (Join-Path $stagedHost 'AICad.Host.dll')){
    $config=Get-Content -LiteralPath (Join-Path $buildRoot 'p0-p6/install.json') -Raw|ConvertFrom-Json
    $targetHost=Join-Path $buildRoot 'p0-p6/Host'
    $backup=Join-Path $buildRoot ('ribbon-manager/host-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup|Out-Null
    Copy-Item -LiteralPath $targetHost -Destination $backup -Recurse
    $descriptor=Join-Path $config.DataRoot 'host.json'
    if(Test-Path -LiteralPath $descriptor){
        $info=Get-Content -LiteralPath $descriptor -Raw|ConvertFrom-Json
        $running=Get-Process -Id $info.processId -ErrorAction SilentlyContinue
        if($running){
            if($running.Path -ne (Join-Path $targetHost 'AICad.Host.exe')){throw 'Host PID khong khop duong dan du kien; khong dung tien trinh khac.'}
            Stop-Process -Id $running.Id
            $running.WaitForExit(10000)|Out-Null
        }
    }
    Copy-Item -Path (Join-Path $stagedHost '*') -Destination $targetHost -Recurse -Force
    & $sdk (Join-Path $buildRoot 'launcher/AICad.Launcher.dll') --no-browser
    if($LASTEXITCODE -ne 0){throw 'Host chua san sang; ban sao luu o '+$backup}
}
$stagedMcp=Join-Path $release 'MCP'
if(Test-Path -LiteralPath (Join-Path $stagedMcp 'AICad.Mcp.dll')){
    $targetMcp=Join-Path $buildRoot 'p0-p6/MCP'
    $mcpBackup=Join-Path $buildRoot ('ribbon-manager/mcp-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $mcpBackup -Force|Out-Null
    if(Test-Path -LiteralPath $targetMcp){Copy-Item -LiteralPath $targetMcp -Destination $mcpBackup -Recurse}
    New-Item -ItemType Directory -Path $targetMcp -Force|Out-Null
    Copy-Item -Path (Join-Path $stagedMcp '*') -Destination $targetMcp -Recurse -Force
}
if($MigrateInstalled){
    if(Get-Process -Name acad -ErrorAction SilentlyContinue){throw 'Luu DWG va dong AutoCAD truoc khi chuyen cau truc. May con lai cung phai dong CAD va cho dong bo xong.'}
    $storage=Join-Path $release 'StorageTool/Addin.Storage.dll'
    if(Test-Path -LiteralPath $storage){& $sdk $storage migrate --app-root $repo}
    else{& $sdk run --project (Join-Path $repo 'tools/Addin.Storage/Addin.Storage.csproj') -c Release -- migrate --app-root $repo}
    if($LASTEXITCODE -ne 0){throw 'Chuyen _Installed chua hoan tat. Doc thong bao va chay lai sau khi kho da dong bo.'}
}
Write-Output "Da cai: $dll"
Write-Output 'Luu DWG va mo lai AutoCAD. Go AICADTOOLS de nhap add-in; AICADRIBBON de quan ly tab.'
