# Lists what .Net version per dll in a folder. To use,
# - start Powershell
# - cd into the desired folder
# - run .\Print-DllNetVersions.ps1
# - the output lists everything.

# Requires administrative privileges in some cases, but generally runs with standard user rights

Write-Host "Scanning current directory for .NET DLL versions..." -ForegroundColor Cyan

# Function to get .NET runtime version from a DLL file
function Get-DotNetVersionInfo {
    param (
        [string]$filePath
    )
    try {
        # Use AssemblyName.GetAssemblyName to get the assembly metadata
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($filePath)

        # Try to load the assembly to get ImageRuntimeVersion and attributes
        $assembly = [System.Reflection.Assembly]::LoadFile($filePath)
        $runtimeVersion = $assembly.ImageRuntimeVersion

        # Get the assembly version
        $assemblyVersion = $assemblyName.Version.ToString()

        # Get file version
        $fileVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($filePath)
        if ([string]::IsNullOrEmpty($fileVersionInfo.FileVersion)) {
            # Build from components if FileVersion string is empty
            $fileVersion = "$($fileVersionInfo.FileMajorPart).$($fileVersionInfo.FileMinorPart).$($fileVersionInfo.FileBuildPart).$($fileVersionInfo.FilePrivatePart)"
        } else {
            $fileVersion = $fileVersionInfo.FileVersion
        }

        # Try to get target framework attribute
        $targetFramework = "Unknown"
        try {
            $targetFrameworkAttr = $assembly.CustomAttributes | Where-Object {
                $_.AttributeType.Name -eq "TargetFrameworkAttribute"
            }
            if ($targetFrameworkAttr) {
                $tfValue = $targetFrameworkAttr.ConstructorArguments[0].Value
                # Extract just the version if it's a long string like ".NETCoreApp,Version=v8.0"
                if ($tfValue -match 'Version=v([\d.]+)') {
                    $targetFramework = "net$($matches[1])"
                } else {
                    $targetFramework = $tfValue
                }
            }
        }
        catch {
            # Target framework attribute not found
        }

        return [PSCustomObject]@{
            RuntimeVersion = $runtimeVersion
            TargetFramework = $targetFramework
            AssemblyVersion = $assemblyVersion
            FileVersion = $fileVersion
        }
    }
    catch {
        # Return N/A if the assembly cannot be loaded
        return [PSCustomObject]@{
            RuntimeVersion = "N/A"
            TargetFramework = "N/A"
            AssemblyVersion = "N/A"
            FileVersion = "N/A"
        }
    }
}

# Get all DLL files in the current directory
$dllFiles = Get-ChildItem -Path (Get-Location) -Filter *.dll -File

# Initialize an array to hold the results
$results = @()

# Process each DLL file
foreach ($file in $dllFiles) {
    $versionInfo = Get-DotNetVersionInfo -filePath $file.FullName
    $results += [PSCustomObject]@{
        FileName = $file.Name
        RuntimeVersion = $versionInfo.RuntimeVersion
        TargetFramework = $versionInfo.TargetFramework
        AssemblyVersion = $versionInfo.AssemblyVersion
        FileVersion = $versionInfo.FileVersion
    }
}

# Display the results as a table
if ($results.Count -gt 0) {
    $results | Format-Table -AutoSize
    Write-Host "`nScan complete. Total DLLs found: $($results.Count)" -ForegroundColor Green
} else {
    Write-Host "`nNo DLL files found in the current directory." -ForegroundColor Yellow
}