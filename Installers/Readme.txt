This workflow produces three installation files
-----------------------------------------------
1. TimelapseBuildZip
   produces a zip file containing everything, including .Net 10

2. TimelapseInstaller-PerMachine
   An msi that creates a per machine version of Timelapse (excluding .Net 10)

3. TimelapseInstaller-PerUser
   An msi that creates a per user version of Timelapse (includes .Net 10):

Publishing workflow
---------------------
The process is fully automated without any manual pauses. Watch the progress in Visual Studio's Output window. You can also       
still run the individual publish profiles (RequiresDotNet10-win-x64 or SelfContained-win-x64) separately if you need to.

1. Select Release mode, x64
2. Select Top-level Solution 'Timelapse' | Rebuild (perhaps cleaning it first). 
- This looks for and builds TimelapseTemplatEditor.exe / View only as well, where it is included in the release bin.
3. Select Timelapse (not Solution) in Solution Explorer, 
4. Select Tools |Publish All Timelapse MSI and zip files.

  This will execute all 5 steps in sequence:

  [1/5] Publish RequiresDotNet10-win-x64...
  [2/5] Publish SelfContained-win-x64...
  [3/5] Build Timelapse Zip Distribution package
	  - Copies files from release and from zip installer folder
          - Creates zip file
  [4/5] Build PerMachine MSI Installer...
 	 Step 1: Updating version from executable..., 
                  where it is used to update the msi Product.wxs file to the right version number
	  Step 2: Generating file list from release folder, where list of required files are saved in Files.wxs...
                  Source: ..\..\Timelapse\bin\Publish\RequiresDotNet10-win-x64
	  Step 3: Build MSI installer using WiX...
  [5/5] Building PerUser MSI Installer...
	same as 4/5
  Cleanup temporary files


Output locations:
  - RequiresDotNet10 files: 	Timelapse\bin\Publish\RequiresDotNet10-win-x64\
  - SelfContained files:   	Timelapse\bin\Publish\SelfContained-win-x64\
  - Zip Package:     		Installers\bin\Release\Timelapse-Executables.zip
  - PerMachine MSI:  		Installers\bin\Release\TimelapseInstaller-PerMachine.msi
  - PerUser MSI:     		Installers\bin\Release\TimelapseInstaller-PerUser.msi


Optional: to create a particular .msi, 
--------------------------------------
- Timelapse|Publish, 
- select self-contained (used by per user) or requires-dotnet (used by per machine from dropdown, then Publish button
- in the TimelapseInstaller-PerMachine or PerUser, select BuildInstaller.bat to generate the particular .msi.

Optional: to create the zip only, 
- run VS026: Solution|Build Solution, to build a release version of all three executables
- run TimelapseBuildZip|BuildTimelapseZipFile.bat   Packages the following in a zip file:
   - files in the Timelapse bin/release folder (excludes unused language folders)
   - two .bat files for creating and removing shortcuts
   - README-Instructions.txt
   - a link to the Timelapse tutorial page

Explanation
-----------
The PowerShell script uses dotnet publish commands, which will automatically build the Release version as part of the publish process. You        
don't need to manually build the solution first - dotnet publish handles both building and publishing in one operation.


   
