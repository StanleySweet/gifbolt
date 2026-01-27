#!/usr/bin/env pwsh
# Deploy GifBolt native DLL to all sample app directories

$srcDll = "C:\Dev\Perso\gifbolt\build\src\GifBolt.Native\Release\GifBolt.Native.dll"
$searchDirs = @(
    "C:\Dev\Perso\gifbolt\samples"
)

Write-Host "Deploying GifBolt.Native.dll to all projects..." -ForegroundColor Cyan

# Check if source exists
if (-not (Test-Path $srcDll)) {
    Write-Host "ERROR: Source DLL not found: $srcDll" -ForegroundColor Red
    exit 1
}

$srcInfo = Get-Item $srcDll
$srcSize = $srcInfo.Length / 1KB
$srcTime = $srcInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
Write-Host "Source: $srcDll ($([math]::Round($srcSize, 2)) KB, $srcTime)" -ForegroundColor Cyan

# Find all bin directories in samples and src folders
$binDirs = @()
foreach ($dir in $searchDirs) {
    if (Test-Path $dir) {
        $binDirs += Get-ChildItem -Path $dir -Recurse -Directory -Filter "bin" |
            ForEach-Object { $_.FullName }
    }
}

if ($binDirs.Count -eq 0) {
    Write-Host "ERROR: No sample app bin directories found" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($binDirs.Count) bin directories" -ForegroundColor Cyan

$deployed = 0
$failed = 0

foreach ($binDir in $binDirs) {
    # Find all possible target framework subdirectories
    $tfmDirs = Get-ChildItem -Path $binDir -Directory | 
        Where-Object { $_.Name -match "^(Debug|Release)$" }
    
    foreach ($tfmDir in $tfmDirs) {
        # Find actual framework directories (net472, net6.0, etc.)
        $frameworks = Get-ChildItem -Path $tfmDir.FullName -Directory | 
            Where-Object { $_.Name -match "^net" }
        
        foreach ($fw in $frameworks) {
            $dstPath = Join-Path $fw.FullName "GifBolt.Native.dll"
            $dstDir = Split-Path -Parent $dstPath
            
            # Ensure directory exists
            if (-not (Test-Path $dstDir)) {
                New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
            }
            
            # Check if destination exists and is up to date
            if ((Test-Path $dstPath)) {
                $dstInfo = Get-Item $dstPath
                if ($dstInfo.LastWriteTime -eq $srcInfo.LastWriteTime -and $dstInfo.Length -eq $srcInfo.Length) {
                    Write-Host "ℹ $dstPath (already up to date)" -ForegroundColor Gray
                    $deployed++
                    continue
                }
            }
            
            try {
                Copy-Item $srcDll $dstPath -Force -ErrorAction Stop
                $dstSize = (Get-Item $dstPath).Length / 1KB
                Write-Host "✓ $dstPath ($([math]::Round($dstSize, 2)) KB)" -ForegroundColor Green
                $deployed++
            }
            catch {
                Write-Host "✗ Failed to copy to $dstPath" -ForegroundColor Red
                Write-Host "  Error: $_" -ForegroundColor Red
                $failed++
            }
        }
    }
}

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Deployed: $deployed" -ForegroundColor Green
if ($failed -gt 0) {
    Write-Host "  Failed: $failed" -ForegroundColor Red
}

if ($failed -eq 0) {
    Write-Host "`nAll deployments complete!" -ForegroundColor Green
} else {
    exit 1
}

