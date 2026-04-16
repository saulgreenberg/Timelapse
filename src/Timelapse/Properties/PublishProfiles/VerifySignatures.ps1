# VerifySignatures.ps1
# Verifies Authenticode signatures on all first-party binaries and MSI installers.
# Called automatically at the end of PublishAll.ps1.

param(
    [Parameter(Mandatory)][string]$RequiresDotNet10Dir,
    [Parameter(Mandatory)][string]$SelfContainedDir,
    [Parameter(Mandatory)][string]$InstallersOutputDir,
    [string]$SignTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe'
)

$firstParty = @(
    'Timelapse.exe',
    'Timelapse.dll',
    'Timelapse-ViewOnly.exe',
    'TimelapseTemplateEditor.exe',
    'TimelapseWpf.Toolkit.dll',
    'DialogUpgradeFiles.dll'
)

$files = @()
foreach ($name in $firstParty) {
    $files += Join-Path $RequiresDotNet10Dir $name
    $files += Join-Path $SelfContainedDir $name
}
$files += Join-Path $InstallersOutputDir 'TimelapseInstaller-PerMachine.msi'
$files += Join-Path $InstallersOutputDir 'TimelapseInstaller-PerUser.msi'

$pass = 0
$fail = 0

foreach ($f in $files) {
    $name = (Split-Path $f -Parent | Split-Path -Leaf) + '\' + (Split-Path $f -Leaf)
    & $SignTool verify /pa $f 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  PASS  $name" -ForegroundColor Green
        $pass++
    } else {
        Write-Host "  FAIL  $name" -ForegroundColor Red
        $fail++
    }
}

Write-Host ""
if ($fail -eq 0) {
    Write-Host "  All $pass signatures verified successfully." -ForegroundColor Green
} else {
    Write-Host "  $pass passed, $fail FAILED." -ForegroundColor Red
    exit 1
}
