# Potential Bugs — diff 30259f9 → HEAD (develop)

> **Scope:** Logic bugs and regressions only. Pure ReSharper-style transforms (pattern matching,
> `Any()` / `Count()`, `ToList()` → `[..]`, `static` promotions, null-conditional operators,
> expression-body refactors) are excluded unless the transform itself introduced a logic error.
>
> **Severity key:** High = likely to cause a crash, data loss, or silent data corruption in normal
> use. Medium = incorrect behaviour under reachable conditions. Low = theoretical, cosmetic impact,
> or very narrow edge case.

---

## High Severity

### Bug-1 · `CsvReaderWriter.ExportToCsv` — `ConfigureAwait(true)` removed from `Task.Run`

**File:** `src/Timelapse/Database/CsvReaderWriter.cs`

**What changed:**
```diff
-    }, token).ConfigureAwait(true);
+    }, token);
```

**Problem:** `ConfigureAwait(true)` forced the continuation after the `Task.Run` to resume on the
original (UI) thread. Without it the code after the `await` runs on a thread-pool thread.
That post-`await` code performs UI operations (cursor reset, partial-file cleanup) and checks
WPF-owned state. This will throw `InvalidOperationException` ("The calling thread cannot access
this object because a different thread owns it") as soon as the export finishes or is cancelled.

---

### Bug-2 · `CsvReaderWriter.ExportMetadataToCsv` — inner `try/catch` removed from lambda

**File:** `src/Timelapse/Database/CsvReaderWriter.cs`

**What changed:** The `try { ... } catch { return false; }` block that wrapped the body of the
`Task.Run` lambda was removed. Any `IOException`, `UnauthorizedAccessException`, or other file I/O
exception thrown inside the lambda will now propagate out of `Task.Run` as a faulted task and
become an unobserved exception crash (or manifest as a confusing exception at the `await` site in
the caller).

---

### Bug-5 · `SQLiteWrapper.ExecuteNonQueryWithRollbackCore` — double `Thread.Sleep` on BUSY

**File:** `src/Timelapse/Database/SQLiteWrapper.cs`

**What changed:** The BUSY-retry loop now contains two `Thread.Sleep` calls per iteration:
one inside the `catch (SQLiteException busy)` block and a second unconditional one at the bottom
of the `for` loop body. On the first retry the thread sleeps `1×250 ms` inside the catch then
immediately sleeps another `1×250 ms`, totalling 500 ms instead of 250 ms. Worst-case (fourth
retry) is ~5 s instead of ~2.5 s. This doubles all BUSY-wait times and makes the UI appear frozen
for twice as long as intended during concurrent write contention.

---

## Medium Severity

### Bug-3 · `CsvReaderWriter.VerifyDataInColumns` — return value uses pre-populated list

**File:** `src/Timelapse/Database/CsvReaderWriter.cs`

**What changed:**
```diff
-    return !abort;         // abort was a local bool reset to false at the top of this method
+    return importErrors.Count == 0;
```

**Problem:** `importErrors` is the same `List<string>` passed in by the caller. If the caller
already appended header-validation errors before invoking `VerifyDataInColumns`, the list is
non-empty on entry. The method will return `false` (data errors found) even when it found zero
column-data problems, causing a valid CSV import to be aborted.

---

### Bug-4 · `CsvReaderWriter.VerifyDataInColumns` — cancellation treated as success

**File:** `src/Timelapse/Database/CsvReaderWriter.cs`

**What changed:** When `token.IsCancellationRequested` is detected mid-column the method does:
```csharp
return true;   // "no errors found"
```

**Problem:** Returning `true` (no errors) on cancellation causes the caller to proceed to the
database-write phase with whatever partial data has already been validated. A user who clicks
Cancel mid-import may have partially-validated (corrupted) data written to the database. The
correct behaviour is to return `false` (abort) or propagate `OperationCanceledException`.

---

### Bug-6 · `FileDatabaseUpdate.UpdateFilesCore` — `BusyCancelIndicator` not reset on write failure

**File:** `src/Timelapse/Database/FileDatabaseUpdate.cs`

**What changed:** `bci?.Reset(false)` was moved to after a success check; the failure-path now
`return`s before reaching it. When a write fails the BCI stays in the "busy" state indefinitely,
blocking the progress bar and any subsequent UI operations that check `BusyCancelIndicator.IsBusy`.

---

### Bug-7 · `ThumbnailGridVirtualized.OnMouseLeftButtonDown` — `cellHeight` zeroed before double-click fires

**File:** `src/Timelapse/Controls/ThumbnailGridVirtualized.xaml.cs`

**What changed:** `cellHeight = 0` is set immediately before raising the `OnDoubleClick` event.
`IsGridActive` (used in several guard conditions) returns `cellHeight > 0`, so during the
double-click handler the grid reports itself as inactive. Any handler that calls `IsGridActive`,
`GetSelected()`, or attempts navigation while handling the double-click will see an inconsistent
state.

---

### Bug-9 · `TimelapseImageSetLoading` — window title shows full file path

**File:** `src/Timelapse/TimelapsePartialClasses/TimelapseImageSetLoading.cs`

**What changed:**
```diff
-    Title = Defaults.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) + ")";
+    Title = Defaults.MainWindowBaseTitle + " (" + fileDatabase.FilePath + ")";
```

**Problem:** On any path longer than ~50 characters the title bar overflows or is truncated by
the OS. Any automated test or external tool that checks the window title by expected substring
will break.

---

### Bug-10 · `FileDatabase.RepairClassificationCategoriesIfNeeded` — requires SQLite ≥ 3.25

**File:** `src/Timelapse/Database/FileDatabase.cs`

**What changed:** The new repair query uses the window function `ROW_NUMBER() OVER (ORDER BY rowid)`,
introduced in SQLite 3.25.0 (2018-09-15). If the bundled `SQLite.Interop.dll` ships an older
version, every database open that contains recognitions will throw a syntax exception, triggering
`ExceptionShutdownDialog` and forcing Timelapse to close.

---

### Bug-11 · `FileDatabase.AddFiles` — atomicity changed to all-or-nothing

**File:** `src/Timelapse/Database/FileDatabase.cs`

**What changed:** Previously INSERTs were batched and flushed per `RowsPerInsert` (partial
progress preserved on failure). Now ALL insert statements are accumulated first and executed in a
single transaction. If any INSERT fails (disk full, lock, malformed row) **zero** files are
inserted. For a first-time load of a large folder, a mid-write failure loses all progress and the
user must restart the entire import.

---

### Bug-12 · `CommonDatabase.MetadataDeleteLevelFromDatabase` — trailing semicolons in individual SQL statements

**File:** `src/Timelapse/Database/CommonDatabase.cs`

**What changed:**
```csharp
$"DELETE FROM {DBTables.MetadataInfo} WHERE {Control.Level} = {level}; "
```

Each statement string (including the trailing `"; "`) is executed individually via
`command.CommandText = stmt; command.ExecuteNonQuery()`. SQLite rejects statement strings that
contain a trailing semicolon when executed through the single-statement API (`sqlite3_prepare_v2`),
returning `SQLITE_ERROR`. Level deletion in metadata databases will silently fail or throw.

---

### Bug-13 · `TimelapseFileShow` — navigation "already there" check compares viewport index to cache row

**File:** `src/Timelapse/TimelapsePartialClasses/TimelapseFileShow.cs`

**What changed:** When `ThumbnailGridVirtualized` is active, `baseRow` is set to
`FirstVisibleFileIndex` (the scroll-viewport origin) instead of `DataHandler.ImageCache.CurrentRow`
(the selected file's index). The guard `desiredRow != baseRow` may incorrectly conclude "already
at destination" and skip navigation when the desired row equals the top of the viewport but differs
from the currently selected file, or vice versa.

---

### Bug-16 · `TimelapseWindow.Dispose` — `GC.SuppressFinalize` removed

**File:** `src/Timelapse/TimelapseWindow.xaml.cs`

**What changed:**
```diff
 public void Dispose()
 {
     Dispose(true);
-    GC.SuppressFinalize(this);
 }
```

**Problem:** The standard `IDisposable` pattern requires `GC.SuppressFinalize(this)` to prevent
the finalizer from running after `Dispose()` has already cleaned up resources. Without it the
finalizer calls `Dispose(false)` on the GC thread after window close, potentially double-disposing
managed handles (SQLite connections, `DataHandler`, image caches) and causing
`ObjectDisposedException` or silent data corruption during finalization.

---

### Bug-17 · `TryForceDeleteDirectory` — `Thread.Sleep` called on the UI thread

**File:** `src/Timelapse/Util/FilesFolders.cs` / `src/Timelapse/TimelapsePartialClasses/TimelapseClosing.cs`

**What changed:** `DeleteTheDeletedFilesFolderIfNeeded()` (runs on the UI thread, called from
`CloseImageSet`) now calls `FilesFolders.TryForceDeleteDirectory()`. That method's retry loop
contains `Thread.Sleep(delayMs)` (default 200 ms × up to 5 retries = up to 1 000 ms). The method
itself contains an inline comment warning "callers on the UI thread should move this call to a
background thread," but the call site remains on the UI thread. Closing Timelapse while the
Deleted folder is locked (e.g., by OneDrive) freezes the application for up to one second.

---

## Low Severity

### Bug-8 · `MarkableCanvas` — `isZooming` → `IsZooming` capitalization (potential breaking reference)

**File:** `src/Timelapse/Images/MarkableCanvas.cs`

**What changed:** The field/property `isZooming` was renamed to `IsZooming`. Any code in other
partial classes or XAML code-behind that still references the old lowercase name will fail to
compile. The diff does not show all referencing files, so external call sites should be audited.

---

### Bug-14 · `DataHandler.ThumbnailGrid` property removed — silent null risk

**File:** `src/Timelapse/ControlsDataEntry/DataEntryHandler.cs`

**What changed:** `public ThumbnailGrid ThumbnailGrid { get; set; }` was deleted along with its
assignment in `TimelapseImageSetLoading`. Any code path still referencing `DataHandler.ThumbnailGrid`
via reflection or a late-bound alias will receive null at runtime. The assignment in
`TimelapseImageSetLoading` was removed without confirmation that all consumers were updated to use
the new `MarkableCanvas.ThumbnailGridVirtualized` path.

---

### Bug-15 · `DeleteDeleteFolder.CountButton_Click` — `Directory.EnumerateFiles` unguarded

**File:** `src/Timelapse/Dialog/DeleteDeleteFolder.xaml.cs`

**What changed:** The constructor no longer receives the file count upfront. The new
`CountButton_Click` handler calls:
```csharp
Directory.EnumerateFiles(DeletedFolderPath, "*", SearchOption.AllDirectories).Count()
```
without a `try/catch`. If `DeletedFolderPath` is null, empty, or the folder was deleted between
dialog open and button click, this throws `ArgumentException`, `DirectoryNotFoundException`, or
`UnauthorizedAccessException`, crashing the dialog.

---

### Bug-18 · `Recognizer.TrimAndSortRecognitionsAsNeeded` — bounding-box coordinates not clamped to `[0, 1]`

**File:** `src/Timelapse/Recognition/Recognizer.cs`

**What changed:** The new bbox-expansion code adds `+0.004` to width/height components but only
clamps values below zero:
```csharp
image.detections[i].bbox[j] = image.detections[i].bbox[j] < 0 ? 0 : Math.Round(...);
```

A bounding box whose width or height coordinate is already near 1.0 (e.g., 0.999) will become
1.003 after expansion. Values > 1.0 extend outside the image boundary and may cause rendering
artifacts or incorrect bounding-box display on near-full-frame detections.

---

### Bug-19 · `ModernNotifications` — default `CloseAfter` reduced from 8 000 ms to 3 000 ms

**File:** `src/Timelapse/Util/ModernNotifications.cs`

**What changed:**
```diff
-    public int CloseAfter { get; set; } = 8000;
+    public int CloseAfter { get; set; } = 3000;
```

Any notification call that does not explicitly set `CloseAfter` now vanishes in 3 seconds instead
of 8. Informational or warning toasts that contain multi-line messages (schema mismatch notices,
CSV import warnings, etc.) may disappear before the user has finished reading them.

---

## Reference: Files Covered by This Analysis

| Diff file | Source files examined |
|---|---|
| `diff_db.txt` | SQLiteWrapper, FileDatabase, CommonDatabase, FileDatabaseUpdate, CsvReaderWriter |
| `diff_ui.txt` | ThumbnailGridVirtualized, MarkableCanvas, TimelapseFileShow, TimelapseImageSetLoading, TimelapseFileSelection |
| `diff_menu_dialog.txt` | TimelapseMenuFile, TimelapseMenuEdit, TimelapseMenuSelection, Dialogs, CustomSelection, DeleteImages, DeleteDeleteFolder, DatabaseSchemaMismatchDialog |
| `diff_window.txt` | TimelapseWindow, DataEntryHandler, TimelapseClosing, TimelapseDetections, TimelapseDuplicates, TimelapseFilePlayer, TimelapseKeyboardShortcuts, TimelapseQuickPaste, ModernNotifications |
| `diff_util.txt` | Recognizer, RecognitionDatabases, BitmapUtilities, FilesFolders, DateTimeHandler, IsCondition, ExifToolManager, ExifToolWrapper, SqlOperationResult, AppLog |

*The remaining ~195 files in the diff contained only ReSharper-style cosmetic changes and were not individually reviewed.*
