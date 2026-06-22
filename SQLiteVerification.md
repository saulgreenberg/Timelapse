# SQLite Change Verification Report
Generated: 2026-06-21

## Scope
Commits 823c9dd..HEAD (10 commits), verified against DB-1 and DB-2 goals.

Commits inspected:
- 670293e DB-1 Stage 0: Surface write failures with shutdown/restart dialog
- ee98942 DB-1 Stage 1: FileDatabaseUpdate.cs
- aac849d DB-1 Stage 2: non-detection FileDatabase.cs call sites
- 38303a7 DB-1 Stage 3: detection/recognition FileDatabase.cs call sites
- 07ac2b9 DB-1 Stage 4: CommonDatabase.cs
- a84b417 DB-1 Stage 5: retry BUSY/LOCKED in ExecuteNonQueryWithRollbackCore
- 5f9899b DB-1 Stage 6: [MustUseReturnValue] safety net on SQLiteWrapper write methods
- ebad1fa Resharper fixes
- a295ce0 DB-2 Pre-stage: SqlErrorState and OnReadError scaffolding
- 7d7a234 DB-2 Stage A: retry loops in all 7 read methods
- e1727fd DB-2 Stage B: ResetAllReadErrorState()
- 1acd154 DB-2 Stage C+D: read-error notice dialog, checkpoints, Debug.Fail cleanup

---

## Summary

**DB-1 (write failure handling)** is substantially complete. All primary write call sites in `FileDatabaseUpdate.cs`, `FileDatabase.cs`, and `CommonDatabase.cs` correctly check `.Success` and call `TimelapseNeedsToShutDownDataWriteErrorDialog`. The `[MustUseReturnValue]` attribute is on all 14 public write method overloads in `SQLiteWrapper.cs`. No old-style `new SqlOperationResult { }` object initializers remain.

**DB-2 (read failure handling)** is substantially complete. All 7 read methods have the 3-attempt retry loop with `_errorFired` gate. `OnReadError` is wired in `TimelapseWindow.xaml.cs` and calls both `SqlErrorState.TryRecord` and `TimelapseReadErrorNoticeDialog` followed by `ResetAllReadErrorState()`. No `#if !DEBUG` guards remain. Stage C checkpoints are in place.

However, several genuine issues were found:

- **Medium**: `SetColumnToACommonValue` is called in `CommonDatabase.cs` twice without checking the returned `SqlOperationResult`, and has no `[MustUseReturnValue]` attribute.
- **Medium**: Multiple `SchemaDeleteColumn`, `SchemaAddColumnToEndOfTable`, `SchemaRenameColumn`, and `SchemaAlterTableWithNewColumnDefinitions` calls in `FileDatabase.cs` discard their `SqlOperationResult` return values silently.
- **Medium**: Multiple `DropTable` calls in `FileDatabase.cs` discard their `SqlOperationResult` without error handling.
- **Low**: `SyncControlsToDatabase()` in `CommonDatabase.cs` returns `false` on write failure but does not call the shutdown dialog.
- **Low**: `TimelapseMenuFile.cs` line 127 has a dangling `if (SqlErrorState.HasError)` statement with the body commented out — syntactically valid but semantically inert (equivalent to a no-op).
- **Low**: The `RecognitionSelector.xaml.cs` comment on `_ =` lines says "DB-2: fails on read error; handled in DB-2 plan" but these are write operations being discarded, which is technically a DB-1 issue (temp-table writes treated as survivable). The comment is conceptually misleading.
- **Info**: `OnReadError` is assigned with `=` (not `+=`), which is intentional but means any secondary window that reassigns it would silently replace the handler.

---

## Findings

### FINDING-01: `SetColumnToACommonValue` — result discarded, no `[MustUseReturnValue]`
**Severity:** Medium
**File:** `src\Timelapse\Database\CommonDatabase.cs:1704` and `:1724`
**Finding:** `Database.SetColumnToACommonValue(...)` is called twice but the returned `SqlOperationResult` is completely discarded (no `_ =`, no `.Success` check, no error dialog). The method returns an `SqlOperationResult` from `ExecuteNonQueryWithRollback`, so a write failure goes silently undetected. Additionally, `SetColumnToACommonValue` on `SQLiteWrapper.cs` at line 458 has no `[MustUseReturnValue]` attribute, so the compiler/ReSharper does not warn at call sites.

```csharp
// CommonDatabase.cs:1704
Database.SetColumnToACommonValue(DBTables.TemplateInfo, DatabaseColumn.VersionCompatibility, versionNumber);
// CommonDatabase.cs:1724
Database.SetColumnToACommonValue(DBTables.TemplateInfo, DatabaseColumn.Standard, standard);
```

**Recommendation:** Either (a) add `[MustUseReturnValue]` to `SetColumnToACommonValue` and check `.Success` at both call sites with the shutdown dialog, or (b) document these as survivable writes and add `_ = ` with a comment. These update the template info table (`TemplateInfo`), so failures affect .tdb files — `isDDBfile` should be `false` at these sites (or computed from `FilePath`).

---

### FINDING-02: Schema-altering methods — results discarded without error handling
**Severity:** Medium
**File:** `src\Timelapse\Database\FileDatabase.cs` (multiple lines)
**Finding:** The following schema-altering calls discard their `SqlOperationResult` entirely — no `_ =`, no `.Success` check, no error dialog, no comment:

- Line 400: `Database.SchemaDeleteColumn(DBTables.FileData, dataLabel);`
- Line 407: `Database.SchemaDeleteColumn(DBTables.Markers, dataLabel);`
- Line 451: `Database.SchemaDeleteColumn(tableName, dataLabel);`
- Line 476: `Database.SchemaAddColumnToEndOfTable(DBTables.FileData, columnDefinition);`
- Line 481: `Database.SchemaAddColumnToEndOfTable(DBTables.Markers, markerColumnDefinition);`
- Line 495-497: `Database.SchemaAddColumnToEndOfTable(MetadataComposeTableNameFromLevel(level), columnDefinition);`
- Line 526: `Database.SchemaRenameColumn(DBTables.FileData, dataLabelToRename.Key, dataLabelToRename.Value);`
- Line 533: `Database.SchemaRenameColumn(DBTables.Markers, ...);`
- Line 545-547: `Database.SchemaRenameColumn(MetadataComposeTableNameFromLevel(level), ...);`
- Line 786: `Database.SchemaAlterTableWithNewColumnDefinitions(DBTables.FileData, columnDefinitions);`
- Line 3284: `this.Database.SchemaAddColumnToEndOfTable(DBTables.Detections, ...);`
- Line 3289: `this.Database.SchemaAddColumnToEndOfTable(DBTables.Detections, ...);`
- Line 3295: `this.Database.SchemaAddColumnToEndOfTable(DBTables.ClassificationCategories, ...);`

And in `CommonDatabase.cs`:
- Line 1874: `database.SchemaAddColumnToEndOfTable(DBTables.Template, scd);`
- Line 1899: `database.SchemaRenameColumn(DBTables.TemplateInfo, "VersionCompatability", ...);`
- Line 1930: `database.SchemaAddColumnToEndOfTable(DBTables.TemplateInfo, scd);`
- Line 1951: `database.SchemaAddColumnToEndOfTable(DBTables.TemplateInfo, scd);`
- Line 1972: `database.SchemaAddColumnToEndOfTable(DBTables.ImageSet, scd);`
- Line 1993: `database.SchemaAddColumnToEndOfTable(DBTables.ImageSet, scd);`

**Evidence:** These methods all return `SqlOperationResult` from `SQLiteWrapper.cs`. The methods `SchemaDeleteColumn`, `SchemaAddColumnToEndOfTable`, `SchemaRenameColumn`, and `SchemaAlterTableWithNewColumnDefinitions` all have try/catch blocks that return `SqlOperationResult.Fail(...)` on exception — so failures exist but are silently ignored by callers.

**Recommendation:** Decide whether schema alteration failures are fatal (requiring shutdown dialog) or survivable. If fatal, add `.Success` checks and shutdown dialogs. If survivable, add `_ = ` with a comment and consider whether `[MustUseReturnValue]` should be absent on these methods. Note: none of these schema methods carry `[MustUseReturnValue]`, which is consistent with them not being in the original 14-method list, but the gap leaves DB-1 incomplete for this class of write.

---

### FINDING-03: `DropTable` and `Vacuum` — results discarded
**Severity:** Medium
**File:** `src\Timelapse\Database\FileDatabase.cs` (multiple lines)
**Finding:** `DropTable` returns `SqlOperationResult` and is called in at least nine places, none of which check `.Success`:

- Line 339: `Database.DropTable(DBTables.Template);`
- Line 344: `Database.DropTable(DBTables.MetadataTemplate);`
- Line 345: `Database.DropTable(DBTables.MetadataInfo);`
- Line 443: `Database.DropTable(tableName);`
- Lines 2790-2794: Five detection-table drops
- Line 3309: `this.Database.DropTable(DBTables.Classifications);`
- Line 3350: `this.Database.DropTable(DBTables.Classifications);`

`Vacuum()` is also called without result checks at `FileDatabase.cs:3351`, `DeleteImages.xaml.cs:404`, and `FileDatabaseResetIdAndVacuum.cs:67`.

**Evidence:** `DropTable(string tableName)` is a public method at `SQLiteWrapper.cs:1690` that returns `SqlOperationResult`. `Vacuum()` at line 1725 also returns `SqlOperationResult`. Neither has `[MustUseReturnValue]`.

**Recommendation:** `DropTable` and `Vacuum` failures are arguably recoverable (the table still exists; the vacuum can be retried), so discarding the result may be intentional. Add `_ = Database.DropTable(...)` with a comment at each call site to make the intentional discard explicit and prevent future confusion. If a drop failure during schema migration (e.g., `DBTables.Template`) is actually fatal, add the shutdown dialog.

---

### FINDING-04: `SyncControlsToDatabase()` — write failure returns `false` without shutdown dialog
**Severity:** Low
**File:** `src\Timelapse\Database\CommonDatabase.cs:831-834`
**Finding:** `SyncControlsToDatabase()` is a private method called from `ReorderControlsInDatabase()`. On write failure it returns `false` instead of calling `TimelapseNeedsToShutDownDataWriteErrorDialog`. The caller at line 976 simply propagates the `bool` return to its own caller but no shutdown dialog is shown for what is a template/database write error.

```csharp
if (!Database.Update(DBTables.Template, columnsTuplesWithWhereList).Success)
{
    return false;   // ← no dialog shown
}
```

**Recommendation:** Either propagate to a caller that shows the shutdown dialog, or call `TimelapseNeedsToShutDownDataWriteErrorDialog` here. Check `isDDBfile` from `this.FilePath?.EndsWith(".ddb", ...)`.

---

### FINDING-05: Dead code in `TimelapseMenuFile.cs` — dangling `if` with no body
**Severity:** Low
**File:** `src\Timelapse\TimelapseMenuCallbacks\TimelapseMenuFile.cs:127-134`
**Finding:** Lines 127–134 contain:

```csharp
if (SqlErrorState.HasError)
//{
//    SqlOperationResult.GenerateExceptionDialog(SqlErrorState.SqlOperationResult, "MenuItemLoadImages_ClickAsync");
//    SqlErrorState.Reset();
//}
// Add it to the list, as its originally invalid, but the user was asked to update it
// So its likely ok now.
return;
```

The `if` has its body commented out. The `return;` on line 134 is not inside the `if` block — it is the next statement, unconditionally executed whenever `Dialogs.DialogIsFileValid` returns false. This is syntactically valid C# (an if-statement with a single empty no-op statement), but:
1. The Stage C checkpoint pattern (`if (SqlErrorState.HasError) { SQLiteWrapper.ResetAllReadErrorState(); }`) is NOT present here.
2. If the intent was to add a Stage C checkpoint, the implementation is incomplete — the commented-out code uses `GenerateExceptionDialog` (the wrong approach for Stage C), and `ResetAllReadErrorState()` is never called at this location.

**Recommendation:** Either (a) add a proper Stage C checkpoint — `if (SqlErrorState.HasError) { SQLiteWrapper.ResetAllReadErrorState(); }` — before the `return;`, or (b) remove the dead `if` statement and the stale comment entirely.

---

### FINDING-06: `RecognitionSelector.xaml.cs` — comment on `_ =` is misleading
**Severity:** Low
**File:** `src\Timelapse\Controls\RecognitionSelector.xaml.cs:419,422,437,449`
**Finding:** The four `_ = Database.Database.ExecuteNonQueryWithRollback(queryN);` calls carry the comment `// DB-2: fails on read error; handled in DB-2 plan`. These are write operations (creating temp tables and indexes). DB-2 is the read-failure plan; DB-1 is the write-failure plan. The comments misidentify the category.

Additionally, if a temp-table create fails, the subsequent queries that depend on those tables will silently produce wrong results (e.g., empty or incorrect recognition counts). Whether this is survivable is a design question, but the comment gives a false impression that DB-2 handles the failure, when DB-2 only applies to read operations.

**Recommendation:** Correct the comments. If the intent is that these writes are survivable (temp tables can fail gracefully), document that explicitly: `// Intentional: temp-table create failure is survivable; downstream queries return empty results.`

---

### FINDING-07: `TimelapseReadErrorNoticeDialog` — `ResetAllReadErrorState()` called inside `OnReadError`
**Severity:** Low / Design note
**File:** `src\Timelapse\TimelapseWindow.xaml.cs:165-170`
**Finding:** The `OnReadError` handler calls `ResetAllReadErrorState()` immediately after showing the dialog. This means `_errorFired` is reset to 0 as soon as the dialog is dismissed. If a second read error fires while the user is reading the first dialog, the `_errorFired` gate has already allowed the first dialog through; since the gate is still `1` during the dialog, the second error would be gated out. But once the user clicks OK and `ResetAllReadErrorState()` runs, a third subsequent error would fire a new dialog immediately, which is the intended behavior.

However, there is a subtle interaction: `SqlErrorState.TryRecord` and `ResetAllReadErrorState()` are called within the same lambda, but `SqlErrorState.TryRecord` records the *first* error only, while `ResetAllReadErrorState()` clears both `_errorFired` and `SqlErrorState`. If `TryRecord` returns false (a second error arrived on a race), the dialog is still shown (because `_errorFired` was already 0 from the first clear) with the second result. This is acceptable behavior but worth noting.

**Recommendation:** No change required — the current behavior is correct. Document the intentional order (TryRecord → show dialog → reset) with a comment noting the race is harmless.

---

### FINDING-08: `OnReadError` is assigned with `=` (not `+=`)
**Severity:** Info
**File:** `src\Timelapse\TimelapseWindow.xaml.cs:165`
**Finding:** `SQLiteWrapper.OnReadError = (context, sqlOperationResult) => { ... };` is an assignment, not a `+=` subscription. This is intentional for a single-subscriber pattern. If a second window or component assigned `OnReadError = ...`, the first handler would be silently replaced. Since `OnReadError` is `static`, it is shared across all `SQLiteWrapper` instances.

**Recommendation:** Consider renaming `OnReadError` to make its single-subscriber nature explicit, or add a guard in the setter. Currently acceptable because only `TimelapseWindow`'s constructor sets it.

---

### FINDING-09: Schema-manipulation methods lack `[MustUseReturnValue]`
**Severity:** Info
**File:** `src\Timelapse\Database\SQLiteWrapper.cs`
**Finding:** The following public methods return `SqlOperationResult` but are not decorated with `[MustUseReturnValue]`, meaning ReSharper and the compiler will not warn when their return values are discarded:

- `SetColumnToACommonValue` (line 458)
- `TrimWhitespace` (line 469) — unused currently
- `UpdateParticularColumnValuesWithNewValues` (line 490) — used in `FileDatabase.cs`, checked there
- `ChangeNullToEmptyString` (line 506) — unused currently
- `SchemaRenameTable` (line 1200)
- `SchemaAlterTableWithNewColumnDefinitions` (line 1223)
- `SchemaAddColumnToTable` (line 1371)
- `SchemaDeleteColumn` (line 1432)
- `SchemaRenameColumn` (line 1482)
- `SchemaAlterColumn` (line 1494)
- `SchemaAddColumnToEndOfTable` (line 1358)
- `DropTable` (line 1690)
- `Vacuum` (line 1725)
- `CreateTable` (line 104)

The 14 primary write methods (Insert, Update, Delete, UpsertRow, etc.) all have `[MustUseReturnValue]`. The schema-manipulation and utility write methods do not.

**Recommendation:** Add `[MustUseReturnValue]` to at minimum `SetColumnToACommonValue` and `SchemaAddColumnToEndOfTable` since they are actively called without result checks. The others (DropTable, Vacuum, schema-alter) could be added too for completeness, with intentional discards marked `_ =`.

---

### FINDING-10: `TimelapseImageSetLoading.cs` — Stage C checkpoint placement
**Severity:** Info
**File:** `src\Timelapse\TimelapsePartialClasses\TimelapseImageSetLoading.cs:398-401`
**Finding:** The Stage C checkpoint at line 398 is correctly placed immediately before `return new(true, string.Empty)` and runs after `OnFolderLoadingCompleteAsync`. This covers the image-set-load pipeline as intended.

However, the checkpoint occurs after `TryBeginImageFolderLoad` (line 389-392) only in the `else` branch. The `if (importImagesAsNewDDBFile)` path calls `TryBeginImageFolderLoad` and returns early (line 391: `return new(false, fileDatabaseFilePath)`) on failure without a Stage C checkpoint. Read errors during initial image folder load are therefore not reset in that path.

**Recommendation:** Add `if (SqlErrorState.HasError) { SQLiteWrapper.ResetAllReadErrorState(); }` before the `return new(false, fileDatabaseFilePath)` at line 391, so the checkpoint fires regardless of which branch exits.

---

### FINDING-11: `CopyAllValuesFromTable` / `CopyAllValuesBetweenTables` — unchecked write paths
**Severity:** Info
**File:** `src\Timelapse\Database\SQLiteWrapper.cs:1160-1190`
**Finding:** The private methods `CopyAllValuesFromTable` and `CopyAllValuesBetweenTables` (lines 1160-1190) execute `command.ExecuteNonQuery()` directly on a raw `SQLiteCommand` without any error handling or retry. These are called inside `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`, and `SchemaAlterColumn`. A write failure here bubbles up through the outer try/catch in those methods and is caught, so the outer `SqlOperationResult.Fail` is returned — but see Finding-02, those results are discarded by callers.

Similarly, `SchemaRenameTable` (private, line 1678), `SchemaAddColumnToEndOfTable` (private overload, line 1628), `DropTable` (private overload, line 1705), and `PragmaSetForeignKeys` (line 1821) all call `command.ExecuteNonQuery()` without try/catch. These are only called from within methods that have their own outer try/catch, so failures propagate to the outer handler.

**Recommendation:** No change to these private helpers is required — they correctly rely on their callers' outer try/catch. This is documented for completeness.

---

### FINDING-12: `MergeDatabases.cs` — no `return` after shutdown dialog
**Severity:** Low
**File:** `src\Timelapse\Database\MergeDatabases.cs:121-127`
**Finding:**
```csharp
if (!destinationDdb.ExecuteNonQueryWithRollback(query).Success)
{
    Dialogs.TimelapseNeedsToShutDownDataWriteErrorDialog(GlobalReferences.MainWindow,
        destinationDdb.FilePath?.EndsWith(".ddb", StringComparison.OrdinalIgnoreCase),
        "The problem occurred in CheckoutDatabaseWithRelativePath", destinationDdb.FilePath);
}
```
There is no `return` after the dialog call. Since `TimelapseNeedsToShutDownDataWriteErrorDialog` calls `Application.Current.Shutdown()`, the application will terminate before any following code runs — so the missing `return` is not a practical bug. However, execution will continue past the `if` block after the dialog is shown but before `Shutdown()` fully completes on the dispatcher (Shutdown is asynchronous). This mirrors the same pattern in a few other sites (e.g., `FileDatabase.cs:1134`, `FileDatabase.cs:1657`).

**Recommendation:** Add `return;` after every `TimelapseNeedsToShutDownDataWriteErrorDialog` call for clarity and to prevent any post-failure code from executing. The compiler does not know that Shutdown terminates execution, so it will not warn about unreachable code. This is a defensive coding quality issue rather than a runtime bug.

---

## Coverage Map

| Call Site | File:Line | Method | isDDBfile | Return checked? | Dialog shown? | Status |
|---|---|---|---|---|---|---|
| `UpdateFile` | FileDatabaseUpdate.cs:43 | `Database.Update` | `true` (hardcoded) | ✅ | ✅ | ✅ Handled |
| `UpdateFileAsync` | FileDatabaseUpdate.cs:68 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateFiles(List)` | FileDatabaseUpdate.cs:152 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateFiles(ColumnTuplesWithWhere)` | FileDatabaseUpdate.cs:161 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateFiles(ColumnTuple)` | FileDatabaseUpdate.cs:169 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateFilesCore` | FileDatabaseUpdate.cs:291 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateSyncImageSetToDatabase` | FileDatabaseUpdate.cs:314 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateSyncMarkerToDatabase` | FileDatabaseUpdate.cs:326 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateMarkers` | FileDatabaseUpdate.cs:341 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateAdjustedFileTimes` | FileDatabaseUpdate.cs:437 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateExchangeDayAndMonth` | FileDatabaseUpdate.cs:484 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateRelativePathByReplacingPrefix` | FileDatabaseUpdate.cs:531 | `ExecuteNonQueryWithRollback` | `true` | ✅ | ✅ | ✅ Handled |
| `OnDatabaseCreatedAsync (ImageSet)` | FileDatabase.cs:278 | `Database.Insert` | `true` | ✅ | ✅ | ✅ Handled |
| `OnExistingDatabaseOpenedAsync (MetadataInfo)` | FileDatabase.cs:363 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `OnExistingDatabaseOpenedAsync (Markers)` | FileDatabase.cs:422 | `Database.DeleteRows` | `true` | ✅ | ✅ | ✅ Handled |
| `OnExistingDatabaseOpenedAsync (MetadataInfo all rows)` | FileDatabase.cs:507 | `Database.DeleteAllRowsInTables` | `true` | ✅ | ✅ | ✅ Handled |
| `OnExistingDatabaseOpenedAsync (SearchTerms)` | FileDatabase.cs:573 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `RepairClassificationCategoriesIfNeeded` | FileDatabase.cs:636 | `ExecuteNonQueryWithRollback` | `true` | ✅ | ✅ | ✅ Handled |
| `AddFiles` | FileDatabase.cs:1132 | `ExecuteNonQueryWithRollback` | `true` | ✅ | ✅ | ✅ Handled |
| `DeleteFilesAndMarkers` | FileDatabase.cs:1351 | `ExecuteNonQueryWithRollback` | `true` | ✅ | ✅ | ✅ Handled |
| `InsertRows` | FileDatabase.cs:1655 | `Database.Insert` | `true` | ✅ | ✅ | ✅ Handled |
| `MarkersTryInsertNewMarkerRow` | FileDatabase.cs:1727 | `Database.Insert` | `true` | ✅ | ✅ | ✅ Handled |
| `MarkersRemoveMarkerRow` | FileDatabase.cs:1778 | `Database.Delete` | `true` | ✅ | ✅ | ✅ Handled |
| `MetadataTablesAndDatabaseUpsertRow (insert)` | FileDatabase.cs:1886 | `Database.Insert` | `true` | ✅ | ✅ | ✅ Handled |
| `MetadataTablesAndDatabaseUpsertRow (update)` | FileDatabase.cs:1899 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `MetadataUpdateFolderDataPath` | FileDatabase.cs:1916 | `UpdateParticularColumnValues` | `true` | ✅ | ✅ | ✅ Handled |
| `InsertDetection` | FileDatabase.cs:2124 | `Database.Insert` | `true` | ✅ | ✅ | ✅ Handled |
| `InsertDetectionsVideo` | FileDatabase.cs:2132 | `Database.Insert` | `true` | ✅ | ✅ | ✅ Handled |
| `recognizer import (remove unneeded)` | FileDatabase.cs:2436 | `ExecuteNonQueryWithRollback` | `true` | ✅ | ✅ | ✅ Handled |
| `TrySetBoundingBoxDisplayThreshold` | FileDatabase.cs:3035 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `UpdateOldStyleRecognitionTables` | FileDatabase.cs:3341 | `Database.Update` | `true` | ✅ | ✅ | ✅ Handled |
| `AddControlToDataTableAndDatabase` | CommonDatabase.cs:518 | `Database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `RemoveControlFromDataTableAndDatabase (delete)` | CommonDatabase.cs:565 | `Database.DeleteRows` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `RemoveControlFromDataTableAndDatabase (update)` | CommonDatabase.cs:593 | `Database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SyncControlToDatabase` | CommonDatabase.cs:808 | `Database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SyncControlsToDatabase (reorder)` | CommonDatabase.cs:831 | `Database.Update` | — | ✅ | ❌ | ⚠️ Partial |
| `SyncControlsToEmptyDatabase` | CommonDatabase.cs:853 | `Database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SyncMetadataControlsToEmptyDatabase` | CommonDatabase.cs:879 | `Database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SyncMetadataInfoToEmptyDatabase` | CommonDatabase.cs:904 | `Database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `MetadataAddControlToDataTableAndDatabase` | CommonDatabase.cs:1262 | `Database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `RemoveMetadataControlFromDataTableAndDatabase (delete)` | CommonDatabase.cs:1306 | `Database.DeleteRows` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `RemoveMetadataControlFromDataTableAndDatabase (update)` | CommonDatabase.cs:1342 | `Database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `MetadataDeleteLevelFromDatabase` | CommonDatabase.cs:1369 | `ExecuteNonQueryWithRollback` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `MetadataMoveLevelForwards/Backwards` | CommonDatabase.cs:1420 | `ExecuteNonQueryWithRollback` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SyncMetadataControlsToDatabase (single)` | CommonDatabase.cs:1512 | `Database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SyncMetadataControlsToDatabase (list)` | CommonDatabase.cs:1538 | `Database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `CreateAndPopulateTemplateInfoTable` | CommonDatabase.cs:1680 | `database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `SetTemplateVersionCompatibility` | CommonDatabase.cs:1704 | `SetColumnToACommonValue` | — | ❌ | ❌ | ❌ Missing |
| `SetTemplateStandard` | CommonDatabase.cs:1724 | `SetColumnToACommonValue` | — | ❌ | ❌ | ❌ Missing |
| `UpsertMetadataInfoTableRow` | CommonDatabase.cs:1748 | `Database.UpsertRow` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `PopulateTemplateTableWithStandardControls` | CommonDatabase.cs:1858 | `database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `AddExportToCSVColumnIfNeeded (Update)` | CommonDatabase.cs:1879 | `database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `AddTemplateInfoTableOrRowIfNeeded` | CommonDatabase.cs:1913 | `database.Insert` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `AddStandardToTemplateInfoColumnIfNeeded` | CommonDatabase.cs:1934 | `database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `AddBackwardsCompatibilityToTemplateInfoColumnIfNeeded` | CommonDatabase.cs:1955 | `database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `AddStandardToImageSetColumnIfNeeded` | CommonDatabase.cs:1976 | `database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `AddBackwardsCompatibilityToImageSetColumnIfNeeded` | CommonDatabase.cs:1997 | `database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `CheckoutDatabaseWithRelativePath` | MergeDatabases.cs:121 | `ExecuteNonQueryWithRollback` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled (no return) |
| `UpdateMetadataTableAndMetadataDatabase` | MetadataDataEntryHandler.cs:710 | `Database.Update` | FilePath?.EndsWith | ✅ | ✅ | ✅ Handled |
| `ResetIDsAndVacuum` | FileDatabaseResetIdAndVacuum.cs:52 | `ExecuteNonQueryWithRollback` | — | ✅ | ✅ (GenerateExceptionDialog) | ✅ Handled |
| `RecognitionSelector temp tables (4×)` | RecognitionSelector.xaml.cs:419,422,437,449 | `ExecuteNonQueryWithRollback` | — | `_ =` (intentional) | ❌ | ℹ️ Intentional-skip (misleading comment) |
| `IndexCreateIfNotExists (single)` | SQLiteWrapper.cs:155 | `ExecuteNonQueryWithRollback` | — | `_ =` (intentional) | ❌ | ℹ️ Intentional-skip |
| `IndexCreateIfNotExists (list)` | SQLiteWrapper.cs:168 | `ExecuteNonQueryWithRollback` | — | `_ =` (intentional) | ❌ | ℹ️ Intentional-skip |
| `IndexDropIfExists` | SQLiteWrapper.cs:142 | `ExecuteNonQueryWithRollback` | — | `_ =` (intentional) | ❌ | ℹ️ Intentional-skip |
| `SchemaDeleteColumn (FileData)` | FileDatabase.cs:400 | `SchemaDeleteColumn` | — | ❌ | ❌ | ❌ Missing |
| `SchemaDeleteColumn (Markers)` | FileDatabase.cs:407 | `SchemaDeleteColumn` | — | ❌ | ❌ | ❌ Missing |
| `SchemaDeleteColumn (level table)` | FileDatabase.cs:451 | `SchemaDeleteColumn` | — | ❌ | ❌ | ❌ Missing |
| `SchemaAddColumnToEndOfTable (FileData)` | FileDatabase.cs:476 | `SchemaAddColumnToEndOfTable` | — | ❌ | ❌ | ❌ Missing |
| `SchemaAddColumnToEndOfTable (Markers)` | FileDatabase.cs:481 | `SchemaAddColumnToEndOfTable` | — | ❌ | ❌ | ❌ Missing |
| `SchemaAddColumnToEndOfTable (metadata level)` | FileDatabase.cs:495 | `SchemaAddColumnToEndOfTable` | — | ❌ | ❌ | ❌ Missing |
| `SchemaRenameColumn (FileData)` | FileDatabase.cs:526 | `SchemaRenameColumn` | — | ❌ | ❌ | ❌ Missing |
| `SchemaRenameColumn (Markers)` | FileDatabase.cs:533 | `SchemaRenameColumn` | — | ❌ | ❌ | ❌ Missing |
| `SchemaRenameColumn (metadata level)` | FileDatabase.cs:545 | `SchemaRenameColumn` | — | ❌ | ❌ | ❌ Missing |
| `SchemaAlterTableWithNewColumnDefinitions` | FileDatabase.cs:786 | `SchemaAlterTableWithNewColumnDefinitions` | — | ❌ | ❌ | ❌ Missing |
| `SchemaAddColumnToEndOfTable (Detections ×2)` | FileDatabase.cs:3284,3289 | `SchemaAddColumnToEndOfTable` | — | ❌ | ❌ | ❌ Missing |
| `SchemaAddColumnToEndOfTable (ClassificationCat)` | FileDatabase.cs:3295 | `SchemaAddColumnToEndOfTable` | — | ❌ | ❌ | ❌ Missing |
| `DropTable (Template)` | FileDatabase.cs:339 | `DropTable` | — | ❌ | ❌ | ⚠️ Partial |
| `DropTable (MetadataTemplate/Info ×2)` | FileDatabase.cs:344-345 | `DropTable` | — | ❌ | ❌ | ⚠️ Partial |
| `DropTable (level table)` | FileDatabase.cs:443 | `DropTable` | — | ❌ | ❌ | ⚠️ Partial |
| `DropTable (5× detection tables)` | FileDatabase.cs:2790-2794 | `DropTable` | — | ❌ | ❌ | ⚠️ Partial |
| `DropTable (Classifications ×2)` | FileDatabase.cs:3309,3350 | `DropTable` | — | ❌ | ❌ | ⚠️ Partial |
| `Vacuum (×3)` | FileDatabase.cs:3351, DeleteImages.xaml.cs:404, FileDatabaseResetIdAndVacuum.cs:67 | `Vacuum` | — | ❌ | ❌ | ⚠️ Partial |
| `SchemaAddColumnToEndOfTable (×4 in CommonDatabase)` | CommonDatabase.cs:1874,1930,1951,1972,1993 | `SchemaAddColumnToEndOfTable` | — | ❌ | ❌ | ❌ Missing |
| `SchemaRenameColumn` | CommonDatabase.cs:1899 | `SchemaRenameColumn` | — | ❌ | ❌ | ❌ Missing |
| `SetColumnToACommonValue (×2)` | CommonDatabase.cs:1704,1724 | `SetColumnToACommonValue` | — | ❌ | ❌ | ❌ Missing |

---

## Missed Read Paths

The 7 wrapped read methods in `SQLiteWrapper.cs` that have retry + `OnReadError` coverage:
- `GetDataTableFromSelect`
- `GetDataTableFromSelectAsync`
- `GetDistinctValuesInColumn`
- `GetScalarFromSelect` (covers all 5 scalar wrapper methods: `ScalarGetScalarFromSelectAsInt`, `ScalarGetScalarFromSelectAsLong`, `ScalarBoolFromOneOrZero`, `ScalarGetMaxValueAsLong`, `ScalarGetFloatValue`)
- `SchemaGetColumns`
- `SchemaGetColumnsAndDefaultValues`
- `SchemaIsColumnInTable`

**Read paths that bypass the 7 methods and have no retry or OnReadError:**

1. **`TableExists` / `TableExistsAndNotEmpty` / `TableHasContent`** (`SQLiteWrapper.cs:1755-1784`): These call `GetDataTableFromSelect` or `ScalarGetScalarFromSelectAsInt`, so they do go through the covered read path. No gap here.

2. **`PragmaGetQuickCheck`** (`SQLiteWrapper.cs:1791-1816`): Opens its own connection, calls `ExecuteReader`, and catches all exceptions with bare `catch { return false; }`. It has no retry, no `OnReadError` invocation, and returns a silent `false` on any failure. This is intentional (it's an integrity-check probe), but it does suppress all errors.

3. **`GetSchema` (private)** (`SQLiteWrapper.cs:1067-1072`): Called from private helpers `GetSchemaColumnNamesAsList`, `GetSchemaColumnNamesAsString`, `GetSchemaColumnDefinitions`. These are only called from within methods that have outer try/catch blocks (`SchemaGetColumns`, `SchemaGetColumnsAndDefaultValues`, `SchemaAlterColumn`, etc.), so failures propagate to those outer handlers. No direct gap.

4. **`SchemaRenameTable`, `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAlterColumn`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`**: These open their own connections and call `GetSchemaColumnNamesAsList` inside their try blocks. Any read failure there is caught by the outer `catch (Exception exception)` and returned as `SqlOperationResult.Fail(...)`. However, callers discard those results (see Finding-02), so failures are silently lost.

5. **`FileDatabaseCountOrSelectFiles.cs`**: Does not contain any direct write calls. It delegates to `GetDataTableFromSelect` and scalar methods which are all covered.

---

## [MustUseReturnValue] Coverage

Present on all 14 primary public write method overloads:
- `Insert` (×2 overloads) ✅
- `UpsertRow` ✅
- `Update` (×4 overloads) ✅
- `DeleteRows` ✅
- `Delete` ✅
- `DeleteAllRowsInTables` ✅
- `ExecuteNonQueryWithRollback` (×4 overloads) ✅

Absent on schema/utility write methods:
- `SetColumnToACommonValue` ❌ (called without result check)
- `SchemaAddColumnToEndOfTable` ❌ (called without result check)
- `SchemaDeleteColumn` ❌ (called without result check)
- `SchemaRenameColumn` ❌ (called without result check)
- `SchemaAlterColumn` ❌ (called without result check)
- `SchemaAlterTableWithNewColumnDefinitions` ❌ (called without result check)
- `DropTable` (public overload) ❌ (called without result check)
- `Vacuum` ❌ (called without result check)
- `TrimWhitespace` ❌ (currently unused externally)
- `ChangeNullToEmptyString` ❌ (currently unused externally)

---

## Verdict

**DB-1: Substantially complete, with gaps.** The primary write-failure coverage across `FileDatabaseUpdate.cs`, `FileDatabase.cs`, and `CommonDatabase.cs` is thorough and correct. However, the schema-manipulation methods (`SchemaDeleteColumn`, `SchemaAddColumnToEndOfTable`, `SchemaRenameColumn`, `SchemaAlterTableWithNewColumnDefinitions`), `DropTable`, `Vacuum`, and `SetColumnToACommonValue` are not covered — their results are discarded silently with no error dialog and no intentional-skip comments. Whether these are considered survivable is a design decision that should be explicitly documented.

**DB-2: Complete.** All 7 read methods have retry loops, `_errorFired` gates, and `OnReadError` invocations using `SqlOperationResult.Fail(...)`. No `#if !DEBUG` guards remain. `ResetAllReadErrorState()` is wired correctly. Stage C checkpoints are present at the 5 specified locations, with one gap (Finding-10): the new-image-folder-load path in `TimelapseImageSetLoading.cs` exits without resetting error state.

**Recommended follow-up actions (priority order):**
1. Decide policy for schema-alter and DropTable/Vacuum failures — either add shutdown dialogs or explicit `_ = ` discards with comments (Finding-02, Finding-03).
2. Fix `SetColumnToACommonValue` call sites in `CommonDatabase.cs` — add `[MustUseReturnValue]` and check result (Finding-01).
3. Fix Stage C checkpoint gap in `TimelapseImageSetLoading.cs` (Finding-10).
4. Fix dead-code `if (SqlErrorState.HasError)` in `TimelapseMenuFile.cs` (Finding-05).
5. Fix missing `return` after shutdown dialog calls at `MergeDatabases.cs:126` and other sites that currently fall through (Finding-12).
6. Correct misleading DB-2 comment in `RecognitionSelector.xaml.cs` (Finding-06).
7. Add shutdown dialog or document intent for `SyncControlsToDatabase()` failure (Finding-04).
