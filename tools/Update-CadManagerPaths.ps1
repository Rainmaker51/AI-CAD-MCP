param([switch]$Check)

$ErrorActionPreference = 'Stop'
$appRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot = Join-Path $appRoot 'storage/04-builds/artifacts'
if (-not (Test-Path -LiteralPath $buildRoot -PathType Container)) {
    $buildRoot = Join-Path $appRoot 'artifacts'
}
$hostConfig = Join-Path $buildRoot 'p0-p6/install.json'
$releases = foreach ($name in @('p0-p6-axis-addin', 'p0-p6-ribbon-center', 'p0-p6-rainmaker-toggle', 'p0-p6-rainmaker-tools', 'p0-p6-rainmaker', 'p0-p6-review-dark', 'p0-p6-addins', 'p0-p6')) {
    $release = Join-Path $buildRoot $name
    $dll = Join-Path $release 'AutoCAD/CAD.Bridge.AutoCAD.dll'
    if (Test-Path -LiteralPath $dll -PathType Leaf) {
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
        [pscustomobject]@{ Dll = $dll; Version = [version]$version; Config = Join-Path $release 'install.json' }
    }
}
$plugin = $releases | Where-Object Version -ge ([version]'0.7.6') | Sort-Object Version -Descending | Select-Object -First 1
if (-not $plugin) { throw 'Khong tim thay plugin AI CAD 0.7.6 tro len.' }
$product = 'SOFTWARE\Autodesk\AutoCAD\R24.0\ACAD-4101:409'
if (-not (Test-Path -LiteralPath "HKLM:\$product")) {
    throw 'Khong tim thay ban AutoCAD 2021 phu hop tren may nay.'
}
$hostExe = Join-Path $buildRoot 'p0-p6/Host/AICad.Host.exe'
if (-not (Test-Path -LiteralPath $hostExe -PathType Leaf)) {
    throw 'Khong tim thay AI CAD Host.'
}

$config = Get-Content -LiteralPath $hostConfig -Raw | ConvertFrom-Json
$previousRoot = $config.ContentRoot.TrimEnd('\', '/') + '\'

function Update-LocalPath([string]$Path) {
    if ($Path -and $Path.StartsWith($previousRoot, [StringComparison]::OrdinalIgnoreCase)) {
        return Join-Path $appRoot $Path.Substring($previousRoot.Length)
    }
    return $Path
}

$config.DataRoot = Update-LocalPath $config.DataRoot
if (-not $config.DataRoot -or -not (Test-Path -LiteralPath $config.DataRoot -PathType Container)) {
    $config.DataRoot = Join-Path $appRoot 'storage/02-app-data/data-p0-p6'
    if (-not (Test-Path -LiteralPath $config.DataRoot -PathType Container)) {
        $config.DataRoot = Join-Path $appRoot 'data-p0-p6'
    }
}
$config.ContentRoot = $appRoot
$config.HostExecutable = $hostExe
$config.PythonExecutable = Update-LocalPath $config.PythonExecutable
$config.CodexExecutable = Update-LocalPath $config.CodexExecutable

if ($Check) {
    Write-Output "App: $appRoot"
    Write-Output "Plugin: $($plugin.Dll)"
    Write-Output "Host: $hostExe"
    Write-Output "Data: $($config.DataRoot)"
    exit 0
}

$json = $config | ConvertTo-Json -Depth 10
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($path in (@($hostConfig, $plugin.Config) | Select-Object -Unique)) {
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    if ($exists -and ((Get-Content -LiteralPath $path -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 10) -ceq $json)) {
        continue
    }
    $temp = $path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temp, $json, $utf8)
        if ($exists) {
            [IO.File]::Replace($temp, $path, [System.Management.Automation.Language.NullString]::Value)
        } else {
            [IO.File]::Move($temp, $path)
        }
    } finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
    }
}

$key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("$product\Applications\AICadToolManager")
try {
    $key.SetValue('DESCRIPTION', 'AI CAD Tool Manager', [Microsoft.Win32.RegistryValueKind]::String)
    $key.SetValue('LOADER', $plugin.Dll, [Microsoft.Win32.RegistryValueKind]::String)
    $key.SetValue('MANAGED', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
    $key.SetValue('LOADCTRLS', 2, [Microsoft.Win32.RegistryValueKind]::DWord)
} finally {
    $key.Dispose()
}

Write-Host 'Da cap nhat duong dan app va cai tu nap CAD Tool Manager.'
Write-Host 'Luu ban ve, dong het AutoCAD, mo lai DWG roi go AICADADDINS.'
