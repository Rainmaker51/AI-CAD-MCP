param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$warehouse = (Get-Content (Join-Path $repo 'warehouse.local.json') -Raw -Encoding UTF8 | ConvertFrom-Json).path
$dotnet = [IO.Path]::GetFullPath((Join-Path $warehouse 'Tools/dotnet'))
$development = Join-Path $dotnet '_Development'
$catalogFile = Join-Path $warehouse 'Tools/catalog.json'
$beforeCatalog = (Get-FileHash -LiteralPath $catalogFile -Algorithm SHA256).Hash
$catalog = Get-Content -LiteralPath $catalogFile -Raw -Encoding UTF8 | ConvertFrom-Json
$protectedPaths = @($catalog.Items | Where-Object { $_.RelativePath } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path (Join-Path $warehouse 'Tools') $_.RelativePath)) })
$targets = [Collections.Generic.List[object]]::new()
function Add-Candidate([string]$Path, [string]$Reason) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return }
    $full = (Resolve-Path -LiteralPath $Path).Path
    if (-not $full.StartsWith($development + '\', [StringComparison]::OrdinalIgnoreCase) -or $full -notmatch '\\working\\') { throw "Outside a development working folder: $full" }
    if (@($protectedPaths | Where-Object { $_.StartsWith($full + '\', [StringComparison]::OrdinalIgnoreCase) }).Count) { throw "Catalog uses candidate: $full" }
    $cursor = Get-Item -LiteralPath $full
    while ($cursor -and $cursor.FullName.StartsWith($dotnet, [StringComparison]::OrdinalIgnoreCase)) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point: $($cursor.FullName)" }
        $cursor = $cursor.Parent
    }
    $entries = @(Get-ChildItem -LiteralPath $full -Recurse -Force)
    if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw "Reparse point below: $full" }
    $files = @($entries | Where-Object { -not $_.PSIsContainer })
    $bytes = ($files | Measure-Object Length -Sum).Sum
    $targets.Add([pscustomobject]@{ path=$full; bytes=[long]$bytes; files=$files.Count; reason=$Reason })
}
# Only proven duplicate package directories are eligible. The surrounding test scripts,
# diagnostics and any unmatched development binary remain available for investigation.
$axis = Join-Path $development 'Rainmaker.Axis'
$releaseHashes = @{}
Get-ChildItem -LiteralPath (Join-Path $axis 'versions') -Filter 'Rainmaker.Axis.dll' -Recurse -File | ForEach-Object { $releaseHashes[(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash] = $true }
Get-ChildItem -LiteralPath (Join-Path $axis 'working') -Directory -ErrorAction SilentlyContinue | Where-Object Name -Like 'library-preview-130-*' | ForEach-Object {
    $binary = Join-Path $_.FullName 'package/Rainmaker.Axis.dll'
    if ((Test-Path -LiteralPath $binary) -and $releaseHashes.ContainsKey((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash)) {
        Add-Candidate (Join-Path $_.FullName 'package') 'Temporary package duplicates an immutable delivered build'
        Add-Candidate (Join-Path $_.FullName 'validation-harness') 'Rebuildable test harness for a delivered package'
    }
}
# Disposable AutoCAD profiles from the earlier isolated Manager test. Never touch the
# real AutoCAD profile, _Installed, versions, CAD sources or application runtime cache.
$native = Join-Path $development 'CAD.Manager/working/install-875738e-fc9138/validation/native-axis'
foreach ($name in @('profile','profile-short')) { Add-Candidate (Join-Path $native $name) 'Generated isolated AutoCAD test profile' }
$report = [pscustomobject]@{ utc=[DateTime]::UtcNow.ToString('o'); applied=[bool]$Apply; bytes=[long](($targets | Measure-Object bytes -Sum).Sum); targets=@($targets) }
$audit = Join-Path $repo '.tools/addin-working-cleanup.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $audit -Encoding UTF8
if ($Apply) {
    foreach ($target in $targets.ToArray()) {
        # Repeat all containment/reparse checks immediately before deletion.
        $checkCount=$targets.Count
        Add-Candidate $target.path $target.reason
        if($targets.Count -ne $checkCount+1){throw 'Candidate disappeared before removal'}
        $targets.RemoveAt($targets.Count-1)
        Remove-Item -LiteralPath $target.path -Recurse -Force
    }
    if ((Get-FileHash -LiteralPath $catalogFile -Algorithm SHA256).Hash -ne $beforeCatalog) { throw 'Catalog changed during cleanup; inspect before proceeding' }
}
$report | Select-Object applied,bytes,@{n='folders';e={$_.targets.Count}}
Write-Output "Audit: $audit"
