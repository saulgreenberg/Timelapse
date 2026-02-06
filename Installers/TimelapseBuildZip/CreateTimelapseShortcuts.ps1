# CreateTimelapseShortcuts.ps1
# Creates shortcuts for Timelapse executables

Write-Host "=== Creating Timelapse Shortcuts ===" -ForegroundColor Green
Write-Host ""

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ParentDir = Split-Path -Parent $ScriptDir
$NetFolder = $ScriptDir

Write-Host "First cleaning up any existing shortcuts..." -ForegroundColor Yellow
$UninstallScript = Join-Path $ScriptDir "UninstallTimelapseShortcuts.ps1"
if (Test-Path $UninstallScript) {
    & $UninstallScript
}
Write-Host ""

# Define executables and their display names
$Executables = @(
    @{ File = "Timelapse.exe"; Name = "Timelapse" },
    @{ File = "Timelapse-ViewOnly.exe"; Name = "Timelapse-ViewOnly" },
    @{ File = "TimelapseTemplateEditor.exe"; Name = "TimelapseTemplateEditor" }
)

# Target locations
$Locations = @(
    @{ Path = $ParentDir; Name = "Current folder" },
    @{ Path = [Environment]::GetFolderPath("Desktop"); Name = "Desktop" }
)

$SuccessfulOperations = @()
$FailedOperations = @()
$CreatedShortcuts = @()

# Function to create a shortcut
function Create-Shortcut {
    param($TargetPath, $ShortcutPath, $WorkingDirectory)

    try {
        $WshShell = New-Object -comObject WScript.Shell
        $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
        $Shortcut.TargetPath = $TargetPath
        $Shortcut.WorkingDirectory = $WorkingDirectory
        $Shortcut.IconLocation = $TargetPath
        $Shortcut.Save()
        return $true
    } catch {
        return $false
    }
}

# Function to register file associations
function Register-FileAssociation {
    param(
        [string]$Extension,
        [string]$ProgId,
        [string]$Description,
        [string]$ExecutablePath
    )

    try {
        # Register the file extension
        $ExtKey = "HKCU:\Software\Classes\$Extension"
        New-Item -Path $ExtKey -Force -ErrorAction Stop | Out-Null
        Set-ItemProperty -Path $ExtKey -Name "(Default)" -Value $ProgId -ErrorAction Stop

        # Register the ProgID
        $ProgIdKey = "HKCU:\Software\Classes\$ProgId"
        New-Item -Path $ProgIdKey -Force -ErrorAction Stop | Out-Null
        Set-ItemProperty -Path $ProgIdKey -Name "(Default)" -Value $Description -ErrorAction Stop

        # Set the default icon
        $IconKey = "$ProgIdKey\DefaultIcon"
        New-Item -Path $IconKey -Force -ErrorAction Stop | Out-Null
        Set-ItemProperty -Path $IconKey -Name "(Default)" -Value "`"$ExecutablePath`",0" -ErrorAction Stop

        # Set the open command
        $CommandKey = "$ProgIdKey\shell\open\command"
        New-Item -Path $CommandKey -Force -ErrorAction Stop | Out-Null
        Set-ItemProperty -Path $CommandKey -Name "(Default)" -Value "`"$ExecutablePath`" `"%1`"" -ErrorAction Stop

        return $true
    } catch [System.UnauthorizedAccessException] {
        Write-Host "    [FAIL] Access denied - registry permissions required" -ForegroundColor Red
        return $false
    } catch [System.Security.SecurityException] {
        Write-Host "    [FAIL] Security policy restriction prevented registration" -ForegroundColor Red
        return $false
    } catch {
        Write-Host "    [FAIL] $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to notify Windows Shell to refresh file associations
function Refresh-ShellIcons {
    try {
        $signature = @'
[DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
'@
        $type = Add-Type -MemberDefinition $signature -Name ShellChangeNotify -Namespace Win32 -PassThru -ErrorAction Stop
        $type::SHChangeNotify(0x08000000, 0x0000, [IntPtr]::Zero, [IntPtr]::Zero)
        return $true
    } catch {
        return $false
    }
}

foreach ($Executable in $Executables) {
    $ExePath = Join-Path $NetFolder $Executable.File

    if (-not (Test-Path $ExePath)) {
        $FailedOperations += "Executable not found: $($Executable.File)"
        continue
    }

    Write-Host "Creating shortcuts for $($Executable.Name)..." -ForegroundColor Cyan

    foreach ($Location in $Locations) {
        try {
            if (-not (Test-Path $Location.Path)) {
                New-Item -ItemType Directory -Path $Location.Path -Force | Out-Null
            }

            $ShortcutPath = Join-Path $Location.Path "$($Executable.Name).lnk"

            if (Create-Shortcut -TargetPath $ExePath -ShortcutPath $ShortcutPath -WorkingDirectory $NetFolder) {
                $SuccessfulOperations += "Created shortcut for $($Executable.Name) in $($Location.Name)"
                $CreatedShortcuts += $ShortcutPath
                Write-Host "  [OK] $($Location.Name)" -ForegroundColor Green
            } else {
                $FailedOperations += "Failed to create shortcut for $($Executable.Name) in $($Location.Name)"
                Write-Host "  [FAIL] $($Location.Name)" -ForegroundColor Red
                Write-Host "  [FAIL] likely because of restricted permissions to do so." -ForegroundColor Red
            }
        } catch {
            $FailedOperations += "Error accessing $($Location.Name): $($_.Exception.Message)"
            Write-Host "  [ERROR] $($Location.Name) - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    Write-Host ""
}

# Register file associations for Timelapse.exe
Write-Host "=== Registering File Associations ===" -ForegroundColor Green
Write-Host ""

$TimelapseExe = Join-Path $NetFolder "Timelapse.exe"

if (Test-Path $TimelapseExe) {
    Write-Host "Registering .tdb and .ddb file associations with Timelapse..." -ForegroundColor Cyan
    Write-Host "  Note: This modifies user registry settings (HKEY_CURRENT_USER)" -ForegroundColor Yellow
    Write-Host "  If this fails, you can manually associate files via 'Open With...'" -ForegroundColor Yellow
    Write-Host ""

    # Test if we can write to registry
    $CanWriteRegistry = $false
    try {
        $TestKey = "HKCU:\Software\Classes\TimelapseTest"
        New-Item -Path $TestKey -Force -ErrorAction Stop | Out-Null
        Remove-Item -Path $TestKey -Force -ErrorAction Stop
        $CanWriteRegistry = $true
    } catch {
        Write-Host "  [WARNING] Cannot write to registry - file associations will be skipped" -ForegroundColor Yellow
        Write-Host "  Reason: $($_.Exception.Message)" -ForegroundColor Yellow
        $FailedOperations += "Registry write test failed - file associations skipped"
    }

    if ($CanWriteRegistry) {
        # Register .tdb extension
        Write-Host "  Registering .tdb extension..." -ForegroundColor Cyan
        if (Register-FileAssociation -Extension ".tdb" -ProgId "Timelapse.Template" -Description "Timelapse Template File" -ExecutablePath $TimelapseExe) {
            $SuccessfulOperations += "Registered .tdb file association"
            Write-Host "    [OK] .tdb files will open with Timelapse" -ForegroundColor Green
        } else {
            $FailedOperations += "Failed to register .tdb file association"
        }

        # Register .ddb extension
        Write-Host "  Registering .ddb extension..." -ForegroundColor Cyan
        if (Register-FileAssociation -Extension ".ddb" -ProgId "Timelapse.Database" -Description "Timelapse Database File" -ExecutablePath $TimelapseExe) {
            $SuccessfulOperations += "Registered .ddb file association"
            Write-Host "    [OK] .ddb files will open with Timelapse" -ForegroundColor Green
        } else {
            $FailedOperations += "Failed to register .ddb file association"
        }

        # Refresh shell icons
        Write-Host "  Refreshing Windows Shell..." -ForegroundColor Cyan
        if (Refresh-ShellIcons) {
            Write-Host "    [OK] Shell refreshed - associations should be active immediately" -ForegroundColor Green
        } else {
            Write-Host "    [WARNING] Could not refresh shell - may need to log off/on for associations to take effect" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  [WARNING] Timelapse.exe not found - skipping file associations" -ForegroundColor Yellow
    $FailedOperations += "Timelapse.exe not found - file associations skipped"
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Green
Write-Host "Successful operations: $($SuccessfulOperations.Count)"
foreach ($Success in $SuccessfulOperations) {
    Write-Host "  [OK] $Success" -ForegroundColor Green
}

if ($FailedOperations.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed operations: $($FailedOperations.Count)"
    foreach ($Failure in $FailedOperations) {
        Write-Host "  [FAIL] $Failure" -ForegroundColor Red
    }
}

Write-Host ""
