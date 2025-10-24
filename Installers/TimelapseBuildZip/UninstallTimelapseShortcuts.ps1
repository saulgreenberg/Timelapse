# UninstallTimelapseShortcuts.ps1
# Removes Timelapse shortcuts created by CreateTimelapseShortcuts.ps1

Write-Host "=== Removing Timelapse Shortcuts ===" -ForegroundColor Green
Write-Host ""

Write-Host "Searching for Timelapse shortcuts to remove..."

# Define possible shortcut locations
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ParentDir = Split-Path -Parent $ScriptDir
$PossibleLocations = @(
    $ParentDir,
    [Environment]::GetFolderPath("Desktop")
)

$ShortcutNames = @("Timelapse.lnk", "Timelapse-ViewOnly.lnk", "TimelapseTemplateEditor.lnk")
$RemovedCount = 0

foreach ($Location in $PossibleLocations) {
    foreach ($ShortcutName in $ShortcutNames) {
        $ShortcutPath = Join-Path $Location $ShortcutName
        if (Test-Path $ShortcutPath) {
            try {
                Remove-Item $ShortcutPath -Force
                Write-Host "Removed: $ShortcutPath" -ForegroundColor Green
                $RemovedCount++
            } catch {
                Write-Host "Failed to remove: $ShortcutPath - $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

# Try to remove Timelapse folders if they are empty
$TimelapseFolder = "Timelapse"
$FoldersToCheck = @(
    (Join-Path $ParentDir $TimelapseFolder),
    (Join-Path ([Environment]::GetFolderPath("Desktop")) $TimelapseFolder)
)

foreach ($Folder in $FoldersToCheck) {
    if (Test-Path $Folder) {
        try {
            $Items = Get-ChildItem $Folder
            if ($Items.Count -eq 0) {
                Remove-Item $Folder -Force
                Write-Host "Removed empty folder: $Folder" -ForegroundColor Green
            }
        } catch {
            Write-Host "Could not remove folder: $Folder" -ForegroundColor Yellow
        }
    }
}

# Remove file associations
Write-Host ""
Write-Host "=== Removing File Associations ===" -ForegroundColor Green
Write-Host ""

$AssociationsRemoved = 0
$AssociationsFailed = 0

# Remove .tdb association
try {
    $TdbKey = "HKCU:\Software\Classes\.tdb"
    if (Test-Path $TdbKey) {
        $CurrentValue = (Get-ItemProperty -Path $TdbKey -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)"
        if ($CurrentValue -eq "Timelapse.Database") {
            Remove-Item -Path $TdbKey -Recurse -Force -ErrorAction Stop
            Write-Host "Removed .tdb file association" -ForegroundColor Green
            $AssociationsRemoved++
        } else {
            Write-Host "Skipped .tdb (associated with different program: $CurrentValue)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "No .tdb association found" -ForegroundColor Gray
    }
} catch {
    Write-Host "Failed to remove .tdb association: $($_.Exception.Message)" -ForegroundColor Red
    $AssociationsFailed++
}

# Remove .ddb association
try {
    $DdbKey = "HKCU:\Software\Classes\.ddb"
    if (Test-Path $DdbKey) {
        $CurrentValue = (Get-ItemProperty -Path $DdbKey -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)"
        if ($CurrentValue -eq "Timelapse.Database") {
            Remove-Item -Path $DdbKey -Recurse -Force -ErrorAction Stop
            Write-Host "Removed .ddb file association" -ForegroundColor Green
            $AssociationsRemoved++
        } else {
            Write-Host "Skipped .ddb (associated with different program: $CurrentValue)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "No .ddb association found" -ForegroundColor Gray
    }
} catch {
    Write-Host "Failed to remove .ddb association: $($_.Exception.Message)" -ForegroundColor Red
    $AssociationsFailed++
}

# Remove ProgID if no other extensions reference it
try {
    $ProgIdKey = "HKCU:\Software\Classes\Timelapse.Database"
    if (Test-Path $ProgIdKey) {
        # Check if any other extensions still reference this ProgID
        $OtherReferences = Get-ChildItem "HKCU:\Software\Classes" -ErrorAction SilentlyContinue |
            Where-Object { $_.PSChildName -match "^\." } |
            Where-Object {
                try {
                    $value = (Get-ItemProperty -Path $_.PSPath -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)"
                    $value -eq "Timelapse.Database"
                } catch {
                    $false
                }
            }

        if ($OtherReferences.Count -eq 0) {
            Remove-Item -Path $ProgIdKey -Recurse -Force -ErrorAction Stop
            Write-Host "Removed Timelapse.Database ProgID" -ForegroundColor Green
        } else {
            Write-Host "Kept Timelapse.Database ProgID (still referenced by other extensions)" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "Failed to remove Timelapse.Database ProgID: $($_.Exception.Message)" -ForegroundColor Red
    $AssociationsFailed++
}

# Refresh shell icons
try {
    $signature = @'
[DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
'@
    $type = Add-Type -MemberDefinition $signature -Name ShellChangeNotify -Namespace Win32 -PassThru -ErrorAction Stop
    $type::SHChangeNotify(0x08000000, 0x0000, [IntPtr]::Zero, [IntPtr]::Zero)
    Write-Host "Refreshed Windows Shell" -ForegroundColor Green
} catch {
    Write-Host "Could not refresh shell - changes will take effect after log off/on" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Green
Write-Host "Shortcuts removed: $RemovedCount"
Write-Host "File associations removed: $AssociationsRemoved"
if ($AssociationsFailed -gt 0) {
    Write-Host "File associations failed to remove: $AssociationsFailed" -ForegroundColor Red
}
Write-Host ""

