# UpdateVersion.ps1
# Extracts version from Timelapse.exe and updates Product.wxs

param(
    [string]$ExePath = "..\..\src\Timelapse\bin\Publish\RequiresDotNet8-win-x64\Timelapse.exe",
    [string]$WxsPath = "Product.wxs"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Updating Version in Product.wxs" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if executable exists
if (-not (Test-Path $ExePath)) {
    Write-Host "ERROR: Timelapse.exe not found at: $ExePath" -ForegroundColor Red
    Write-Host "Please build Timelapse in Release mode first." -ForegroundColor Yellow
    exit 1
}

# Check if Product.wxs exists
if (-not (Test-Path $WxsPath)) {
    Write-Host "ERROR: Product.wxs not found at: $WxsPath" -ForegroundColor Red
    exit 1
}

# Extract version from executable
try {
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
    $version = $versionInfo.FileVersion

    if ([string]::IsNullOrWhiteSpace($version)) {
        Write-Host "ERROR: Could not extract version from $ExePath" -ForegroundColor Red
        exit 1
    }

    Write-Host "Extracted version from executable: $version" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "ERROR: Failed to read version from executable: $_" -ForegroundColor Red
    exit 1
}

# Read Product.wxs content
try {
    $wxsContent = Get-Content $WxsPath -Raw -Encoding UTF8
}
catch {
    Write-Host "ERROR: Failed to read $WxsPath : $_" -ForegroundColor Red
    exit 1
}

# Update version in Product.wxs (find Version attribute in Package element only)
# Use a more specific pattern that matches the Package element's Version attribute
$pattern = '(<Package[^>]*?\s+Version\s*=\s*")[^"]*(")'
$replacement = "`${1}$version`${2}"

if ($wxsContent -match $pattern) {
    $newContent = $wxsContent -replace $pattern, $replacement

    # Write updated content back to file
    try {
        Set-Content $WxsPath -Value $newContent -Encoding UTF8 -NoNewline
        Write-Host "Updated Product.wxs with version: $version" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "ERROR: Failed to write to $WxsPath : $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "ERROR: Could not find Version attribute in $WxsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Version update completed successfully!" -ForegroundColor Green
Write-Host ""
exit 0
