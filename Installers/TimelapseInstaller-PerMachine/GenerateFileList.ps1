# PowerShell script to generate WiX file components from release folder
# Simplified version - all files in single component group
param(
    [string]$SourceDir = "..\..\src\Timelapse\bin\Publish\RequiresDotNet8-win-x64",
    [string]$OutputFile = "Files.wxs"
)

Write-Host "Generating file list from: $SourceDir"
Write-Host "Output file: $OutputFile"

# Check if source directory exists
if (!(Test-Path $SourceDir)) {
    Write-Error "Source directory not found: $SourceDir"
    exit 1
}

# Start building the WXS content
$wxsContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <!-- Directory structure for subdirectories -->
    <DirectoryRef Id="INSTALLFOLDER">

"@

# Get all subdirectories
$sourceDirFullPath = (Resolve-Path $SourceDir).Path
$subdirs = Get-ChildItem -Path $sourceDirFullPath -Directory -Recurse | Sort-Object FullName

# Create directory map and track hierarchy
$dirMap = @{}
$dirMap[""] = "INSTALLFOLDER"
$dirStructure = @{}  # Maps parent path to list of child directories

# Build directory hierarchy
foreach ($subdir in $subdirs) {
    $relativePath = $subdir.FullName.Substring($sourceDirFullPath.Length + 1)
    $dirId = "Dir_" + ($relativePath -replace '[\\\/\-\(\) \.]', '_')
    $dirMap[$relativePath] = $dirId

    $parentRelativePath = Split-Path $relativePath -Parent
    if (-not $parentRelativePath) {
        $parentRelativePath = ""
    }

    # Track children for each parent
    if (-not $dirStructure.ContainsKey($parentRelativePath)) {
        $dirStructure[$parentRelativePath] = @()
    }
    $dirStructure[$parentRelativePath] += @{
        Path = $relativePath
        Name = $subdir.Name
        Id = $dirId
    }
}

# Function to recursively build nested directory XML
function Build-DirectoryXml {
    param(
        [string]$parentPath,
        [int]$indentLevel
    )

    $indent = "  " * $indentLevel
    $result = ""

    if ($dirStructure.ContainsKey($parentPath)) {
        foreach ($dir in $dirStructure[$parentPath]) {
            # Check if this directory has children
            $hasChildren = $dirStructure.ContainsKey($dir.Path)

            if ($hasChildren) {
                # Open tag with children
                $result += "$indent<Directory Id=`"$($dir.Id)`" Name=`"$($dir.Name)`">`n"
                $result += Build-DirectoryXml -parentPath $dir.Path -indentLevel ($indentLevel + 1)
                $result += "$indent</Directory>`n"
            } else {
                # Self-closing tag for leaf directories
                $result += "$indent<Directory Id=`"$($dir.Id)`" Name=`"$($dir.Name)`" />`n"
            }
        }
    }

    return $result
}

# Generate nested directory structure starting from root
$wxsContent += Build-DirectoryXml -parentPath "" -indentLevel 3

$wxsContent += @"
    </DirectoryRef>

    <!-- Main Component Group with all files -->
    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">

"@

# Get all files recursively
$files = Get-ChildItem -Path $sourceDirFullPath -File -Recurse | Sort-Object FullName
$componentId = 1

# Skip these specific files as they're handled in MainExecutables
$skipFiles = @("Timelapse.exe", "Timelapse-ViewOnly.exe", "TimelapseTemplateEditor.exe")

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($sourceDirFullPath.Length + 1)
    $fileName = $file.Name

    # Skip files that are handled in MainExecutables
    if ($skipFiles -contains $fileName) {
        continue
    }

    # Determine the directory for this component
    $relativeDir = Split-Path $relativePath -Parent
    if (-not $relativeDir) {
        $relativeDir = ""
    }
    $dirId = $dirMap[$relativeDir]

    # Generate a unique component ID
    $compId = "Comp_$componentId"
    $componentId++

    # Generate a GUID for this component
    $guid = [guid]::NewGuid().ToString().ToUpper()

    $wxsContent += @"
      <Component Id="$compId" Guid="$guid" Directory="$dirId">
        <File Source="`$(var.SourceDir)\$relativePath" />
      </Component>

"@
}

# Close ProductComponents group and add MainExecutables
$wxsContent += @"
    </ComponentGroup>

    <ComponentGroup Id="MainExecutables" Directory="INSTALLFOLDER">
      <!-- Main executables -->
      <Component Id="MainExecutable" Guid="12345678-90AB-CDEF-0123-456789ABCD01">
        <File Source="`$(var.SourceDir)\Timelapse.exe" KeyPath="yes" />
      </Component>

      <Component Id="ViewOnlyExecutable" Guid="23456789-ABCD-EF01-2345-6789ABCDEF02">
        <File Source="`$(var.SourceDir)\Timelapse-ViewOnly.exe" KeyPath="yes" />
      </Component>

      <Component Id="TemplateEditorExecutable" Guid="34567890-BCDE-F012-3456-789ABCDEF012">
        <File Source="`$(var.SourceDir)\TimelapseTemplateEditor.exe" KeyPath="yes" />
      </Component>
    </ComponentGroup>

  </Fragment>
</Wix>
"@

# Write to file
$wxsContent | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "Generated $componentId components with $($dirMap.Count-1) subdirectories"
Write-Host "File list saved to: $OutputFile"
