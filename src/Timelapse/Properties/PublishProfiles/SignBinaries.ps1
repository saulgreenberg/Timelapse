# SignBinaries.ps1
# Signs all first-party Timelapse binaries in a given directory.
# Requires SimplySign Desktop to be running and logged in.
# Multiple files are passed in a single signtool call — one phone authorization covers all.

param(
    [Parameter(Mandatory)][string]$Directory,
    [string]$SignTool = (Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\signtool.exe' -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
        Select-Object -First 1 -ExpandProperty FullName),
    [string]$Thumbprint = 'B6FF9831D50B47E1500DD47A0612E01E371CABC4',
    [string]$TimestampUrl = 'http://timestamp.certum.pl'
)

if (-not $SignTool -or -not (Test-Path $SignTool)) {
    throw "signtool.exe not found under any installed Windows 10 SDK version. Install the Windows 10 SDK, or pass -SignTool <path> explicitly."
}

$firstParty = @(
    'Timelapse.exe',
    'Timelapse.dll',
    'Timelapse-ViewOnly.exe',
    'TimelapseTemplateEditor.exe',
    'TimelapseWpf.Toolkit.dll',
    'DialogUpgradeFiles.dll'
)

$toSign = $firstParty |
    ForEach-Object { Join-Path $Directory $_ } |
    Where-Object { Test-Path $_ }

if ($toSign.Count -eq 0) {
    Write-Host "  WARNING: No signable first-party files found in:" -ForegroundColor Yellow
    Write-Host "  $Directory" -ForegroundColor Yellow
    exit 0
}

Write-Host "  Signing $($toSign.Count) file(s):" -ForegroundColor Cyan
$toSign | ForEach-Object { Write-Host "    $_" }

& $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td sha256 /v $toSign

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: signtool failed (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "  Signing complete." -ForegroundColor Green
