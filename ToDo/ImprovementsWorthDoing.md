# Improvements Worth Doing

Code quality issues identified by professional review (2026-06-25). These are recurring patterns across the codebase, not one-off oddities. Excludes test coverage (known gap, accepted).

---

## Priority 1 — Silent Exception Swallowing

**Files:** `FileBackup.cs` (lines 32-35, 54-56, 84-86), `QuickPaste.cs` (line 136-138), `ProcessExecution.cs` (lines 30-34)

**Pattern:** Empty `catch` blocks or bare `catch { return []; }` that swallow errors and return nulls/defaults. When file I/O or process startup fails, there is no trace of why.

**Fix:** At minimum, log the exception via `TracePrint` before returning. Where appropriate, surface the error to the caller instead of returning a silent default.

---

## Priority 2 — Unsafe Null / Bounds Dereferences

**Files:**
- `QuickPaste.cs:100` — `FindIndex` returns -1, used directly in `RemoveAt(index)` without a -1 check → `ArgumentOutOfRangeException`
- `BoundingBox.cs:48` — array indexed `coords[0..3]` after a `Split` without verifying enough elements were produced
- `FileBackup.cs:46` — `GetBackupFiles()` can return null (line 34 returns null on exception), but its result is used directly with `.MaxBy()` without a null check
- `SearchTerm.cs:36` — `List` initialised to `null`, then `[..List]` used at line 92 → `NullReferenceException` if not populated first

**Fix:** Add bounds/null guards before each of these accesses.

---

## Priority 3 — `async void` Outside Event Handlers

**Files:** `TimelapseMenuFile.cs`, `RecognitionSelector.xaml.cs`, `FileMetadataExportDataIntoFiles.xaml.cs`, ~7 others

**Pattern:** `async void` used in non-event-handler contexts. Exceptions thrown inside `async void` are unobservable and terminate the app without graceful shutdown.

**Fix:** Change to `async Task` wherever the method is not directly wired to a UI event. Callers that currently fire-and-forget should either `await` or explicitly discard with a logged continuation.

---

## Priority 4 — Public Fields + Naming Violations in CamtrapDP Types

**File:** `CamtrapDPDataPackage.cs` (lines 10–100)

**Pattern:** JSON serialization classes use lowercase class names (`resources`, `contributors`, `licenses`) and public fields instead of properties (`public List<resources> resources`). These were written this way to match JSON key names, but that is the wrong approach.

**Fix:** Rename classes to PascalCase (`Resources`, `Contributors`, etc.) and convert public fields to `{ get; set; }` properties. Add `[JsonPropertyName("resources")]` etc. attributes to preserve the JSON contract.

---

## Priority 5 — Code Duplication in DateTimeHandler / Dictionaries

**Files:** `DateTimeHandler.cs` (lines 23–74), `Dictionaries.cs` (lines 60–80)

**Pattern:**
- `DateTimeHandler` repeats near-identical "try this format, then try that format" chains across multiple methods (`TryParseDatabaseOrDisplayDateTime`, `TryParseDatabaseOrDisplayDate`, etc.)
- `Dictionaries` repeats the same null-check block for dict1/dict2 across methods

**Fix:** Extract the repeated logic into a private shared helper in each class.

---

## Lower Priority (worth noting, not urgent)

### Missing `ConfigureAwait(false)` in utility code
~348 `await` calls across 64 files; only ~120 have `ConfigureAwait`. For WPF this rarely deadlocks, but utility-layer methods that don't need the UI context should use `ConfigureAwait(false)` as a discipline. Not urgent given the WPF dispatcher model.

### Magic values
Hardcoded strings/numbers scattered in SQL building (e.g. `" AND "` in `ColumnTuplesWithWhere.cs:98`) and color constants used inline. Should be named constants.

### `Process` not disposed in `ProcessExecution.cs:78`
One code path creates a `Process` without a `using` statement, unlike the pattern used elsewhere in the same file. Minor resource leak.

### Long methods / high cyclomatic complexity
`QuickPaste.cs:152–232` (~80 lines, 4 nested loops) mixes validation, synchronization, and reconstruction. Worth splitting into smaller focused methods when touching that area.

---

## Implemented: AppLog — Persistent Error/Warning Log

**File:** `src\Timelapse\DebuggingSupport\AppLog.cs`  
**Constant added:** `Constant.File.LogFile = "Timelapse.log"`

### Usage
```csharp
AppLog.Warning("message");
AppLog.Warning("message", ex); // includes full exception chain
AppLog.Error("message");
AppLog.Error("message", ex);   // includes full exception chain
```
Caller file, method, and line number are captured automatically via `[CallerFilePath/MemberName/LineNumber]` — no extra arguments needed at call sites.

### Initialization
Called at the two database-open entry points:
- `DoLoadImages` in `TimelapseMenuCallbacks\TimelapseMenuFile.cs`
- `TemplateDoOpen` in `TemplateEditor\EditorCode\TemplateCode.cs`

Both call `AppLog.Initialize(Path.GetDirectoryName(templateFilePath))`.

Before any database is open, log calls are silent no-ops (accepted limitation).

### Log file location
`<root>\Backups\Timelapse.log` — appended to across sessions.

### Log format
```
=====================================================================
Session started 2026-06-25 14:23:00 | C:\MyImages\Survey2024
=====================================================================
2026-06-25 14:23:01.123 | WARNING | FileBackup.cs(46) TryCreateBackup | Backup folder missing
2026-06-25 14:23:01.124 | ERROR   | FileBackup.cs(89) TryCreateBackup | Failed to create backup
                              System.IO.IOException: Access to path denied.
                                 at System.IO.File.Copy(...)
```

### Key design points
- Session header is written **lazily** — only when the first warning/error of a session occurs. Sessions with no errors leave no trace in the file.
- Each `Initialize` call starts a new session (resets `_sessionHeaderWritten = false`).
- Root path appears in the session header, not repeated on every line.
- Thread-safe via `System.Threading.Lock`.
- `StreamWriter` opened and closed per write — no open handles to clean up on database close.
- Silent no-op if the Backups folder cannot be created.

### Open issue: log location unreachable on write failure

**Problem:** The log currently writes to `<root>\Backups\Timelapse.log`. If the write failure is caused by a file server or portable hard drive becoming temporarily unreachable, the log location is also unreachable — the error is lost at exactly the moment it's most needed.

**Secondary problem:** Even if the log were in a reliable location (e.g. `%LocalAppData%\Timelapse\Timelapse.log`), most users won't know how to find it when asked to email it for diagnosis.

**Approaches under consideration (decision pending):**

1. **"Open Log Folder" button in the shutdown dialog** — adds a button that opens File Explorer at the log folder via `Process.Start`. One click, no path knowledge needed. Users attach the file to an email.

2. **"Copy log to clipboard" button** — copies log file contents to the clipboard. Users paste into an email body. No file navigation required.

3. **Dual-location logging** — write to `%LocalAppData%\Timelapse\` as the primary (always reachable), and also attempt `<root>\Backups\` as a secondary. Show the Backups path to users first (familiar location); reference LocalAppData in the dialog Solution text as the fallback when the drive was unreachable.

4. **Embed log excerpt in the shutdown dialog** — show the last N lines directly in the dialog with a "Copy" button. No file navigation, no ambiguity.

**Recommended approach:** Option 3 (dual-location) + Option 1 ("Open Log Folder" button), with Option 2 (copy to clipboard) as a low-cost addition. This ensures the log is always captured, and gives users a one-click way to access it.
