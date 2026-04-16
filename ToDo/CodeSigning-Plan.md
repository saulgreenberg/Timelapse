# Code Signing Plan — Timelapse 2.5.x

## Context

Certum cloud certificate accessed via **SimplySign Desktop**.
- SHA1 thumbprint: `B6FF9831D50B47E1500DD47A0612E01E371CABC4`
- signtool: `C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe`
- Timestamp server: `http://timestamp.certum.pl`
- **Must use PowerShell, not Git Bash** (Git Bash mangles `/fd SHA256`)
- SimplySign Desktop must be running and logged in before any signing step
- Phone authorization (SimplySign mobile app) is required once per signtool invocation
- Multiple files can be passed to a single signtool call → one phone authorization for all

Full details: `D:\@Timelapse\CodeSigning(Certum)\SESSION-CONTEXT.md`
Full guide:   `D:\@Timelapse\CodeSigning(Certum)\SIGNING-GUIDE.md`

---

## Build Pipeline Summary

The master publish script is:
```
src\Timelapse\Properties\PublishProfiles\PublishAll.ps1
```
Triggered via VS: **Tools → Publish All Timelapse MSI and zip files**

It runs five steps in sequence:
1. `dotnet publish` → `bin\Publish\RequiresDotNet10-win-x64\`  (framework-dependent, for PerMachine MSI)
2. `dotnet publish` → `bin\Publish\SelfContained-win-x64\`     (self-contained with .NET 10, for PerUser MSI and ZIP)
3. `Installers\TimelapseBuildZip\BuildTimelapseZipFile.bat`    → `Installers\bin\Release\Timelapse-Executables.zip`
4. `Installers\TimelapseInstaller-PerMachine\BuildInstaller.bat` → `Installers\bin\Release\TimelapseInstaller-PerMachine.msi`
5. `Installers\TimelapseInstaller-PerUser\BuildInstaller.bat`    → `Installers\bin\Release\TimelapseInstaller-PerUser.msi`

The C++ wrappers (`TimelapseTemplateEditor.exe`, `Timelapse-ViewOnly.exe`) are built by the
`BuildCppWrappers` MSBuild target (`BeforeTargets="BeforeBuild"`) and land in the C# project's
output folder, from where `dotnet publish` copies them into the publish folders.

---

## What to Sign — Decision Table

### Sign these (first-party binaries)

| File | Why |
|------|-----|
| `Timelapse.exe` | Main app native host — what users launch |
| `Timelapse.dll` | Managed assembly — the actual C# code |
| `Timelapse-ViewOnly.exe` | C++ wrapper launching the view-only mode |
| `TimelapseTemplateEditor.exe` | C++ wrapper for template editor |
| `TimelapseWpf.Toolkit.dll` | Own project (`src\TimelapseWpf.Toolkit`) |
| `DialogUpgradeFiles.dll` | Own DLL from `Dependencies-Dlls\` |
| `TimelapseInstaller-PerMachine.msi` | The installer itself — users download and run this |
| `TimelapseInstaller-PerUser.msi` | The installer itself — users download and run this |

### Do NOT sign these (third-party binaries)

| File | Reason |
|------|--------|
| `AvalonDock.dll` | NuGet — Dirkster.AvalonDock, already signed by publisher |
| `CsvHelper.dll` | NuGet, already signed |
| `MetadataExtractor.dll` | NuGet, already signed |
| `Newtonsoft.Json.dll` | NuGet, already signed |
| `NReco.VideoConverter.dll` | NuGet |
| `SixLabors.Fonts.dll` / `SixLabors.ImageSharp.dll` / `SixLabors.ImageSharp.Drawing.dll` | NuGet |
| `System.Data.SQLite.dll` | NuGet |
| `XmpCore.dll` | NuGet |
| `Microsoft.WindowsAPICodePack*.dll` | NuGet |
| `e_sqlite3.dll` | Native SQLite — 3rd party |
| `exiftool(-k).exe` | Phil Harvey's ExifTool — 3rd party |
| `ffmpeg.exe` | FFmpeg — 3rd party |
| `Timelapse-Executables.zip` | Cannot Authenticode-sign a ZIP (not a PE/MSI) |

> Signing a third-party binary with your certificate would replace the vendor's signature with yours,
> which is both inaccurate and could break integrity checks.

---

## Recommended Integration Strategy

**Do NOT add signing to the `.csproj` MSBuild targets.**
Reason: `RemoveUnusedDlls` already runs on every Release build. Adding signing there means a phone
authorization on every `dotnet build -c Release`, which is too disruptive during development.
Signing belongs to the *release publishing* workflow only.

**Add signing steps to `PublishAll.ps1`**, the master script that already owns the full release pipeline.

### Where signing fits in the 5-step pipeline

```
[1/5] dotnet publish → RequiresDotNet10-win-x64
      ↓
[1b/5] Sign first-party binaries in RequiresDotNet10-win-x64\  ← one phone auth
      ↓
[2/5] dotnet publish → SelfContained-win-x64
      ↓
[2b/5] Sign first-party binaries in SelfContained-win-x64\     ← one phone auth
      ↓
[3/5] Build ZIP  (zips already-signed binaries — no signing needed)
      ↓
[4/5] Build PerMachine MSI
      ↓
[4b/5] Sign TimelapseInstaller-PerMachine.msi                  ← one phone auth
      ↓
[5/5] Build PerUser MSI
      ↓
[5b/5] Sign TimelapseInstaller-PerUser.msi                     ← one phone auth
      ↓
[6/5] Verify all 14 signatures
      ↓
[7/5] Generate SHA-256 checksums → checksums-SHA256.txt + console output
```

**Total phone authorizations per full release: 4** (SimplySign session caching means in practice only 1 prompt)

The ZIP does not need to be signed because its contained EXEs/DLLs are already signed before the
ZIP is assembled. Authenticode signatures on ZIP contents survive the zip/unzip. The SHA-256
checksum serves as the integrity guarantee for the ZIP container itself.

---

## The Signing Helper Script

Create `src\Timelapse\Properties\PublishProfiles\SignBinaries.ps1`:

```powershell
# SignBinaries.ps1
# Signs all first-party Timelapse binaries in a given directory.
# Requires SimplySign Desktop to be running and logged in.

param(
    [Parameter(Mandatory)][string]$Directory,
    [string]$SignTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe',
    [string]$Thumbprint = 'B6FF9831D50B47E1500DD47A0612E01E371CABC4',
    [string]$TimestampUrl = 'http://timestamp.certum.pl'
)

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
    Write-Host "  No signable files found in $Directory" -ForegroundColor Yellow
    return
}

Write-Host "  Signing $($toSign.Count) file(s) in $Directory ..." -ForegroundColor Cyan
& $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td sha256 /v $toSign

if ($LASTEXITCODE -ne 0) {
    throw "signtool failed (exit code $LASTEXITCODE)"
}
Write-Host "  Signing complete." -ForegroundColor Green
```

---

## Changes to PublishAll.ps1

After step 1 publish, add:
```powershell
Write-Host "[1b/5] Signing RequiresDotNet10 binaries..." -ForegroundColor Yellow
& "$PSScriptRoot\SignBinaries.ps1" -Directory "$projectDir\bin\Publish\RequiresDotNet10-win-x64"
if ($LASTEXITCODE -ne 0) { throw "Signing RequiresDotNet10 binaries failed" }
```

After step 2 publish, add:
```powershell
Write-Host "[2b/5] Signing SelfContained binaries..." -ForegroundColor Yellow
& "$PSScriptRoot\SignBinaries.ps1" -Directory "$projectDir\bin\Publish\SelfContained-win-x64"
if ($LASTEXITCODE -ne 0) { throw "Signing SelfContained binaries failed" }
```

After step 4 MSI build, add:
```powershell
Write-Host "[4b/5] Signing PerMachine MSI..." -ForegroundColor Yellow
& $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td sha256 /v `
    "$InstallersDir\bin\Release\TimelapseInstaller-PerMachine.msi"
if ($LASTEXITCODE -ne 0) { throw "Signing PerMachine MSI failed" }
```

After step 5 MSI build, add:
```powershell
Write-Host "[5b/5] Signing PerUser MSI..." -ForegroundColor Yellow
& $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td sha256 /v `
    "$InstallersDir\bin\Release\TimelapseInstaller-PerUser.msi"
if ($LASTEXITCODE -ne 0) { throw "Signing PerUser MSI failed" }
```

---

## Open Questions / Decisions Needed

1. **DialogUpgradeFiles.dll** — this DLL lives in `Dependencies-Dlls\` and is a pre-built binary
   checked into the repo. If it is your own code and you have the source, sign it once at source
   and re-check it in. If it is third-party, remove it from the sign list.

2. **TimelapseWpf.Toolkit.dll** — built from `src\TimelapseWpf.Toolkit\`. Because it is a project
   reference, it lands in the publish folder automatically. Signing it during the publish-step
   signing pass is correct. No separate action needed.

3. **Signing on regular Release builds (non-publish)** — currently not recommended (too disruptive).
   If you ever want it, add an `AuthenticodeSigning` MSBuild target to `Timelapse.csproj` with
   `AfterTargets="RemoveUnusedDlls"` and `Condition="'$(Configuration)'=='Release'"`, signing
   `$(TargetPath)` and the DLLs listed above. Discussed in `SIGNING-GUIDE.md`.

4. **SmartScreen reputation** — even after signing, Windows SmartScreen will warn users for a while
   because the certificate is brand new (issued 2026-04-15). This is normal. Reputation builds as
   more users run the signed binaries. Nothing to fix — just communicate to users.

5. **ZIP signing** — resolved. The ZIP cannot be Authenticode-signed (not a PE/MSI format). SHA-256
   checksums are now auto-generated by `PublishAll.ps1` step 7, written to `checksums-SHA256.txt`,
   and printed to the console at the end of every build for copy-paste to the website.

---

## Implementation Steps (ordered)

- [x] 1. Create `src\Timelapse\Properties\PublishProfiles\SignBinaries.ps1`
- [x] 2. Modify `PublishAll.ps1` to call SignBinaries.ps1 after each publish and sign each MSI
- [x] 3. Confirmed: `DialogUpgradeFiles.dll` is first-party — included in sign list
- [x] 4. Full test run completed successfully 2026-04-15 — all 14 files signed, 0 errors
- [x] 5. All 14 signed files verified successfully 2026-04-15 (14 passed, 0 failed)
- [x] 6. SHA-256 checksum generation added to PublishAll.ps1 step 7 — writes checksums-SHA256.txt and prints to console

---

## Quick-reference: signtool commands

**Sign a file:**
```powershell
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe' sign `
  /sha1 B6FF9831D50B47E1500DD47A0612E01E371CABC4 `
  /fd SHA256 /tr http://timestamp.certum.pl /td sha256 /v `
  'C:\path\to\file.exe'
```

**Verify a file:**
```powershell
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe' verify /pa /v 'C:\path\to\file.exe'
```

**Verify an MSI:**
```powershell
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe' verify /pa /v 'C:\path\to\installer.msi'
```
