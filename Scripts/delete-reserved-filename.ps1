# Script to delete files with reserved Windows names (nul, con, prn, aux, etc.)
# Usage: .\delete-reserved-filename.ps1 -FilePath "D:\@Timelapse\Timelapse\nul"

param(
    [Parameter(Mandatory=$true, HelpMessage="Full path to the file to delete")]
    [string]$FilePath
)

Write-Host "Attempting to delete: $FilePath"

try {
    # Use the \\?\ prefix to bypass normal Windows path parsing
    $uncPath = "\\?\$FilePath"
    Remove-Item -LiteralPath $uncPath -Force
    Write-Host "Successfully deleted: $FilePath" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "Failed to delete: $FilePath" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
