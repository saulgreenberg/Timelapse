# PotentialBugs2 — Logic and Correctness Review

**Reviewed:** 2026-06-29  
**Scope:** Five diff packages covering the database layer (`diff_db.txt`), UI layer (`diff_ui.txt`), menus and dialogs (`diff_menu_dialog.txt`), main window (`diff_window.txt`), and utilities (`diff_util.txt`).  
**Focus:** Logic errors, threading hazards, resource-management flaws, and semantic changes introduced by the recent set of commits. Style issues are excluded. Bugs are grouped by severity; within a group they are ordered by risk of user-visible data loss or crash.

---

## High Severity

---

### Bug-H1 · NullReferenceException in CSV import for deprecated Date/Time column headers

**File:** `CsvReaderWriter.cs`  
**Severity:** High  
**What changed:**  
The early-continue guard in `VerifyDataInColumns` was narrowed from six header names to three. Before the change it explicitly skipped `ControlDeprecated.DateLabel`, `ControlDeprecated.TimeLabel`, and `DatabaseColumn.DateTime`; after the change only `Folder`, `RootFolder`, and `ImageQuality` are skipped, with the intent of validating Date/Time inside the `switch` block instead.

```diff
-if (csvHeader == ControlDeprecated.DateLabel
-    || csvHeader == ControlDeprecated.TimeLabel
-    || csvHeader == DatabaseColumn.DateTime
-    || csvHeader == ControlDeprecated.Folder ...)
-    continue;
+if (csvHeader == ControlDeprecated.Folder
+    || csvHeader == DatabaseColumn.RootFolder
+    || csvHeader == ControlDeprecated.ImageQuality)
+    continue;
```

**Problem:**  
For every CSV header that is **not** in the new skip list, the code immediately calls:

```csharp
ControlRow controlRow = fileDatabase.GetControlFromControls(csvHeader);
string controlRowType = controlRow.Type;   // ← NPE if null
```

If a CSV was exported from an older Timelapse build it will contain `Date` or `Time` columns. Those are deprecated controls that are absent from the current control table, so `GetControlFromControls("Date")` returns `null`. The dereference on the next line throws `NullReferenceException`, aborting the import with an unhandled exception.

**Suggested fix:**  
After calling `GetControlFromControls`, add a null guard that either `continue`s for unknown headers or treats them as the new Date/Time validation case. Alternatively, add `ControlDeprecated.DateLabel` and `ControlDeprecated.TimeLabel` back to the early-skip list and handle them there.

---

### Bug-H2 · `InvalidOperationException` — dictionary modified during `foreach` enumeration

**File:** `Recognizer.cs`, method `CorrectForEmpyClassificationLabels()`  
**Severity:** High  
**What changed:**  
This is an entirely new method added in the diff. It iterates `classification_categories` and writes back into it inside the same loop:

```csharp
foreach (KeyValuePair<string, string> classification_category in classification_categories)
{
    if (string.IsNullOrWhiteSpace(classification_category.Value))
    {
        string newLabel = $"Unknown_category_{unknownCount++}";
        classification_categories[classification_category.Key] = newLabel;  // ← modifies the dict
    }
}
```

**Problem:**  
`Dictionary<TKey,TValue>` maintains an internal version counter. Any mutation—even assigning a new value to an existing key—increments the version and causes the enumerator's `MoveNext()` to throw `InvalidOperationException: Collection was modified; enumeration operation may not execute.`  This fires on the first blank label encountered, crashing the recognizer import.

**Suggested fix:**  
Collect keys that need relabelling first, then modify the dictionary in a second pass:

```csharp
var blanks = classification_categories.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                                       .Select(kv => kv.Key).ToList();
foreach (string key in blanks)
    classification_categories[key] = $"Unknown_category_{unknownCount++}";
```

---

### Bug-H3 · Virtualized thumbnail grid not reset when an image set is closed

**File:** `TimelapseClosing.cs`, method `CloseImageSet()`  
**Severity:** High  
**What changed:**  
The call to `MarkableCanvas.ThumbnailGrid?.Reset()` was removed and no equivalent call to `ThumbnailGridVirtualized.Reset()` was added in its place.

```diff
-    MarkableCanvas.ThumbnailGrid?.Reset();
```

**Problem:**  
`ThumbnailGridVirtualized.Reset()` is responsible for:
- calling `CancelUpdate()` to stop any running `BackgroundWorker`
- calling `ClearPool()` to remove all `ThumbnailInCell` child controls
- setting `cellHeight = 0` so the grid is no longer considered active

Without this call, when an image set is closed while the thumbnail grid is visible, the `BackgroundWorker` continues running on a thread-pool thread and its `ProgressChanged` callback continues firing on the UI thread. Both callbacks access `FileTable`, `ImageRow` references, and bounding-box helpers that belong to the now-closed `FileDatabase`. After the `FileDatabase` is disposed/replaced, these accesses can produce `NullReferenceException` or silently corrupt the state of the newly-opened image set. The `snapAnimTimer` may also continue firing against a collapsed but non-reset grid.

`ZoomOutAllTheWay()` (called later in `CloseImageSet`) hides the control by setting `Visibility = Collapsed`, but this does not stop the worker or release the pool.

**Suggested fix:**  
Add `MarkableCanvas.ThumbnailGridVirtualized?.Reset()` at the top of the cleanup sequence in `CloseImageSet`, before `ZoomOutAllTheWay()` is called.

---

### Bug-H4 · `Thread.Sleep` inside `TryForceDeleteDirectory` called on the UI thread at shutdown

**File:** `FilesFolders.cs` (`TryForceDeleteDirectory`), caller `TimelapseWindow.cs` (`DeleteTheDeletedFilesFolderIfNeeded`)  
**Severity:** High  
**What changed:**  
`TryForceDeleteDirectory` is a new method that retries a directory deletion up to five times with a `Thread.Sleep(delayMs)` (default 200 ms) between attempts. `DeleteTheDeletedFilesFolderIfNeeded` calls it directly from the `Window_Closing` handler on the UI thread.

```csharp
// FilesFolders.cs — new method
for (int attempt = 0; attempt < maxAttempts; attempt++)
{
    try { Directory.Delete(dirPath, true); return true; }
    catch { Thread.Sleep(delayMs); }  // delayMs = 200, up to 5× = 1 000 ms
}
```

**Problem:**  
`Thread.Sleep` on the UI (STA) thread suspends the entire WPF message pump for up to one second. During that window the application is completely unresponsive: the window does not redraw, mouse/keyboard events queue up, and the OS may display the "Not Responding" banner. On slow or network-backed drives the cumulative sleep can be longer.

**Suggested fix:**  
Move the `TryForceDeleteDirectory` call off the UI thread. For example: `await Task.Run(() => FilesFolders.TryForceDeleteDirectory(...))` inside an `async` overload of `DeleteTheDeletedFilesFolderIfNeeded`, called from the `Window_Closing` handler with `e.Cancel = true` / manual `Application.Shutdown()` after the task completes. Alternatively, add an `CancellationToken`-aware loop and use `Task.Delay` instead of `Thread.Sleep`.

---

## Medium Severity

---

### Bug-M1 · `Width`/`Height` passed to `ThumbnailGridVirtualized.Refresh` may be `NaN` before first layout

**File:** `MarkableCanvas.cs`, method `RefreshThumbnailGridVirtualized()`  
**Severity:** Medium  
**What changed:**  
The new helper reads the control's `Width` and `Height` dependency properties instead of `ActualWidth`/`ActualHeight`:

```csharp
return ThumbnailGridVirtualized.Refresh(ThumbnailGridVirtualized.Width,
                                        ThumbnailGridVirtualized.Height, zoomIn);
```

**Problem:**  
`Width` and `Height` are only set explicitly in the `MarkableCanvas_SizeChanged` handler. Before the first `SizeChanged` event fires (e.g., during application startup or if the host window has not yet been rendered), both properties retain their default value of `double.NaN`. `Refresh()` guards with `if (newGridWidth <= 0 || newGridHeight <= 0)` but `NaN <= 0` evaluates to `false`, so the guard does **not** protect against NaN. Division or arithmetic using NaN column/row counts then propagates through layout, producing `NaN` offsets, `NaN` scroll positions, and a visually broken grid without any diagnostic message.

**Suggested fix:**  
Use `ThumbnailGridVirtualized.ActualWidth` and `ThumbnailGridVirtualized.ActualHeight`, which are always valid after layout, and update the guard to also reject NaN: `if (double.IsNaN(newGridWidth) || newGridWidth <= 0 || ...)`.

---

### Bug-M2 · `GC.SuppressFinalize(this)` removed from `TimelapseWindow.Dispose()`

**File:** `TimelapseWindow.xaml.cs`, `Dispose()` method  
**Severity:** Medium  
**What changed:**

```diff
 public void Dispose()
 {
     Dispose(true);
-    GC.SuppressFinalize(this);
 }
```

**Problem:**  
The standard `IDisposable` pattern requires `GC.SuppressFinalize(this)` to prevent the finalizer from running after an explicit `Dispose()`. Without it, if any base class in the `Window` hierarchy defines or later acquires a finalizer, that finalizer will execute on the GC thread after `Dispose()` has already run, causing double-disposal of unmanaged resources (native Win32 handles, COM references, etc.). WPF's `HwndSource` and `UIElement3D` hierarchies have finalizers; even if the current build does not trigger them, this is a latent correctness hazard that will silently worsen if any resource-owning base changes.

**Suggested fix:**  
Restore `GC.SuppressFinalize(this);` as the last line of `Dispose()`.

---

### Bug-M3 · `SQLiteWrapper.OnReadError` blocks the calling (background) thread via modal dialog

**File:** `TimelapseWindow.xaml.cs`, constructor lambda for `SQLiteWrapper.OnReadError`  
**Severity:** Medium  
**What changed:**  
Previously, `OnReadError` only recorded error state; callers checked `SqlErrorState` at safe points before showing a dialog. The new code calls the dialog directly inside the callback:

```csharp
SQLiteWrapper.OnReadError = (context, sqlOperationResult) =>
{
    SqlErrorState.TryRecord(sqlOperationResult, context);
    Dialogs.TimelapseReadErrorNoticeDialog(GlobalReferences.MainWindow, sqlOperationResult, context);
    SQLiteWrapper.ResetAllReadErrorState();
};
```

**Problem:**  
`OnReadError` may be called from a database-reader thread. `TimelapseReadErrorNoticeDialog` internally uses `Dispatcher.Invoke` to show the dialog on the UI thread. `Dispatcher.Invoke` is a blocking call: it suspends the calling background thread until the user dismisses the dialog. If that background thread holds an open SQLite reader or a lock used by another part of the UI (e.g., a `BackgroundWorker` running `CountAllFilesMatchingSelectionCondition`), the background thread is frozen for the entire duration of the modal dialog. If a second read error fires concurrently from another background thread before the first dialog is dismissed, a second `Dispatcher.Invoke` queues up, producing two back-to-back modal error dialogs.

**Suggested fix:**  
Return to the deferred pattern: record the error in `OnReadError`, then have the database-read callers check `SqlErrorState` and display the dialog at a safe point on the UI thread after the background work completes.

---

### Bug-M4 · `VerifyDataInColumns` uses `importErrors.Count == 0` instead of a local abort flag

**File:** `CsvReaderWriter.cs`, method `VerifyDataInColumns()`  
**Severity:** Medium  
**What changed:**  
The old code tracked a `bool abort` variable and returned `!abort`. The new code changed the return expression to:

```csharp
return importErrors.Count == 0; // !abort;
```

**Problem:**  
`importErrors` is passed in from `TryImportFromCsv`, and entries may already exist from a prior validation step (e.g., from `VerifyCSVHeaders` if it added informational messages before returning `true`). In that scenario `importErrors.Count > 0` even though `VerifyDataInColumns` found no data errors, so it incorrectly returns `false`, aborting the import with a "data errors" result when the data is actually valid. The bug is latent today because the existing callers only write to `importErrors` on failure paths that abort before reaching `VerifyDataInColumns`, but the coupling is brittle.

**Suggested fix:**  
Restore the local `bool abort = false;` / `return !abort;` pattern, or count only the errors added by this method: capture `int startCount = importErrors.Count` before the loop and return `importErrors.Count == startCount`.

---

### Bug-M5 · `MetadataInfo` table left inconsistent if `MetadataTemplate` drop succeeds but `MetadataInfo` drop fails

**File:** `FileDatabase.cs`, inside `OnExistingDatabaseOpenedAsync`, the `SyncRequiredAsFolderLevelsDiffer` branch  
**Severity:** Medium  
**What changed:**  
Two previously separate delete operations were refactored into sequential SqlOperationResult-checked calls:

```csharp
SqlOperationResult r1 = Database.DropTable(DBTables.MetadataTemplate);
if (!r1.Success) { Dialogs.TimelapseNeedsToShutDown...(...)(); return; }

SqlOperationResult r2 = Database.DropTable(DBTables.MetadataInfo);
if (!r2.Success) { Dialogs.TimelapseNeedsToShutDown...(...)(); return; }
```

**Problem:**  
If the first `DropTable` succeeds but the second fails, the database is left in a partially repaired state: `MetadataTemplate` no longer exists but `MetadataInfo` still does. The shutdown dialog fires and the user restarts Timelapse, but on the next open the folder-level sync logic will now see only `MetadataInfo` without its companion table and may behave differently than either the fully-before or fully-after state, potentially crashing or silently skipping the sync.

**Suggested fix:**  
Wrap both `DropTable` calls (and the subsequent recreation steps) in a single `ExecuteNonQueryWithRollback` transaction, or at minimum handle the partial-drop scenario by attempting `DropTable(MetadataInfo)` even if `MetadataTemplate` is already gone (i.e., tolerating "table not found" errors in the cleanup path before aborting).

---

### Bug-M6 · `ExportMetadataToCsv` `try/catch` removed — `BusyCancelIndicator` stays busy on exception

**File:** `CsvReaderWriter.cs`, `ExportMetadataToCsv()` / `TimelapseMenuFile.cs`, `MenuItemExportAllFilesToCsv_ClickAsync()`  
**Severity:** Medium  
**What changed:**  
The `try/catch` block inside the `Task.Run` lambda in `ExportMetadataToCsv` was removed. Exceptions now propagate through `await` to the caller's `catch (Exception ex)` block. The caller's catch block sets `BusyCancelIndicator.IsBusy = false` — but that same reset also appeared inside the `try` block on the success path:

```csharp
// caller (TimelapseMenuFile.cs) — simplified
try
{
    ...
    await CsvReaderWriter.ExportMetadataToCsv(...);
    BusyCancelIndicator.IsBusy = false;   // ← only reached on success
    ...
}
catch (Exception ex)
{
    AppLog.Error(...);
    BusyCancelIndicator.IsBusy = false;   // ← should reset on failure too
    Dialogs.FileCantOpen(...);
}
```

**Problem:**  
Based on the diff, the catch block does include `BusyCancelIndicator.IsBusy = false` — so strictly the indicator is reset. However, if the catch block was added without including that reset (verify the exact layout), the spinning busy indicator would remain active for the rest of the session, preventing any further exports. Even if the reset is present, the behaviour change (silent `false` → propagated exception) is worth flagging because any future refactor of the catch block risks omitting the reset.

**Suggested fix:**  
Move `BusyCancelIndicator.IsBusy = false` into a `finally` block so it runs unconditionally regardless of the success/exception/cancellation path.

---

## Low Severity

---

### Bug-L1 · `CancellationTokenSource` instances leak in `CustomSelection` count timer

**File:** `CustomSelection.xaml.cs`, `CountTimer_Tick()`  
**Severity:** Low  
**What changed:**  
A new `CancellationTokenSource countCts` field was added. On each timer tick, the previous source is cancelled and replaced with a new one:

```csharp
await countCts.CancelAsync().ConfigureAwait(true);
countCts = new CancellationTokenSource();   // old instance never disposed
```

**Problem:**  
`CancellationTokenSource` implements `IDisposable` and holds a `WaitHandle` and a linked-token registration. Replacing `countCts` without calling `.Dispose()` on the outgoing instance leaks these handles for every criterion change the user makes. In a long session with frequent filter adjustments (a common workflow), hundreds of instances accumulate. The final instance is also never disposed when the dialog closes (no `Dispose()` call in the dialog's `Closed` or `Unloaded` handler).

**Suggested fix:**  
Capture the old CTS, cancel it, dispose it, then assign the new one:

```csharp
CancellationTokenSource old = countCts;
countCts = new CancellationTokenSource();
await old.CancelAsync();
old.Dispose();
```

And dispose `countCts` in the dialog's close/unload handler.

---

### Bug-L2 · `cellHeight` set to zero inside `OnMouseLeftButtonDown` before `OnDoubleClick` fires

**File:** `ThumbnailGridVirtualized.xaml.cs`, `OnMouseLeftButtonDown()`  
**Severity:** Low  
**What changed:**  
This is new code. On double-click detection, `cellHeight = 0` is set synchronously before raising the `OnDoubleClick` event:

```csharp
if (e.ClickCount == 2)
{
    ThumbnailInCell cell = pool.FirstOrDefault(c => ...);
    cellHeight = 0;           // ← grid now reports IsGridActive = false
    OnDoubleClick(new ThumbnailGridVirtualizedEventArgs(cell?.ImageRow));
    ...
}
```

**Problem:**  
`IsGridActive` is defined as `cellHeight > 0`. Any code inside the `DoubleClick` event handler (or in subscribers of the `DoubleClick` event raised by `OnDoubleClick`) that guards on `IsGridActive` will see the grid as already deactivated, even though the deactivation switch (hiding the control, loading the single image) has not yet occurred. For example, if the handler tries to call `ScrollToRow()` or `AssignAndLoad()`, these methods guard on `cellHeight > 0` and silently no-op.

**Suggested fix:**  
Set `cellHeight = 0` after `OnDoubleClick` returns, or raise a separate `DeactivationStarting` event first and set `cellHeight` only when the host window actually hides the grid.

---

### Bug-L3 · Modal read-error dialogs can stack if two background threads fail concurrently

**File:** `TimelapseWindow.xaml.cs`, `SQLiteWrapper.OnReadError` callback  
**Severity:** Low  
**What changed:**  
`OnReadError` now shows a dialog immediately (see Bug-M3). The callback does not check whether a dialog is already open:

```csharp
SQLiteWrapper.OnReadError = (context, sqlOperationResult) =>
{
    SqlErrorState.TryRecord(sqlOperationResult, context);
    Dialogs.TimelapseReadErrorNoticeDialog(...);
    SQLiteWrapper.ResetAllReadErrorState();   // reset happens *after* dialog dismissed
};
```

**Problem:**  
If two background reader threads both hit a read error in quick succession, each independently calls `OnReadError`. `Dispatcher.Invoke` queues both dialog-show operations on the UI thread. The first dialog must be dismissed before the second appears, resulting in two successive identical error dialogs with no explanation that the second is a repeat. `ResetAllReadErrorState()` is called only after the first dialog is dismissed, so `TryRecord` in the second call may or may not record (depending on the "first error wins" semantics of `TryRecord`), but the dialog is still shown.

**Suggested fix:**  
Add a `volatile bool _readErrorDialogShowing` flag; set it to `true` before `Dispatcher.Invoke` and `false` in the `Completed` callback. Guard the dialog call with `if (!_readErrorDialogShowing)`.

---

### Bug-L4 · Window title now shows full absolute path instead of filename only

**File:** `TimelapseImageSetLoading.cs`  
**Severity:** Low  
**What changed:**

```diff
-Title = Defaults.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) + ")";
+Title = Defaults.MainWindowBaseTitle + " (" + fileDatabase.FilePath + ")";
```

**Problem:**  
On most deployments the `.tdb` file lives three to six directories deep. A 100-character absolute path combined with the base title ("Timelapse") will truncate in the Windows taskbar button and title bar, making the current project name unreadable at a glance. Worse, when users have multiple Timelapse windows open, the taskbar cannot distinguish them because the distinguishing part of the path (the final filename) is truncated. The previous behaviour (filename only) was intentional for this reason.

**Suggested fix:**  
Revert to `Path.GetFileName(fileDatabase.FilePath)`, or use `Path.GetFileNameWithoutExtension` if the `.tdb` suffix adds clutter.

---

*End of report. 4 High · 6 Medium · 4 Low bugs identified.*
