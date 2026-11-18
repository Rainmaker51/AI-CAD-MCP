param([switch]$Build,[switch]$Check)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot=Join-Path $repo 'artifacts'
if(Test-Path -LiteralPath (Join-Path $repo 'storage/04-builds/artifacts')){$buildRoot=Join-Path $repo 'storage/04-builds/artifacts'}
$release=Join-Path $buildRoot 'p0-p6-ribbon-manager'
if($Build){& (Join-Path $PSScriptRoot 'Build.ps1') -Component All -Release 'p0-p6-ribbon-manager' -RuntimeRelease 'p0-p6'}
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
Copy-Item -LiteralPath (Join-Path $repo 'docs/CAD-RIBBON-MANAGER.md') -Destination (Join-Path $package 'HUONG-DAN.md') -Force
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $buildRoot 'Rainmaker.Library.AutoCAD2021.zip') -Force
& (Join-Path $PSScriptRoot 'Update-CadManagerPaths.ps1')
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
    & (Join-Path $repo '.tools/dotnet/dotnet.exe') (Join-Path $buildRoot 'launcher/AICad.Launcher.dll') --no-browser
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
Write-Output "Da cai: $dll"
Write-Output 'Luu DWG va mo lai AutoCAD. Go AICADTOOLS de nhap add-in; AICADRIBBON de quan ly tab.'
