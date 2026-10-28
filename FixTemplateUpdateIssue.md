# Fix: Template update can leave the .ddb half-updated

## The reported problem

A user added two fields to their template (a Flag and a FixedChoice/picklist), then opened an existing image set. Each time they open it, it fails with:

> SchemaAlterTableWithNewColumnDefinitions failed in UpgradeFileDBSchemaDefaultsFromTemplate

Inspecting the `.ddb` shows:

- **TemplateTable** contains rows for the two new fields.
- **DataTable** has **no columns** for them.

This happens only occasionally. Most template changes reach the database without trouble.

## Background: two tables that must agree

Every `.ddb` stores its field definitions twice:

| Table | What it holds |
|---|---|
| `TemplateTable` | The list of fields (name, type, default, label, …). A copy of the `.tdb` template. |
| `DataTable` | The actual data, with **one column per field** in TemplateTable. |

Timelapse assumes these always match.

## Part 1: why the file now fails on every open (confirmed in code)

1. **Compare the templates.** `UpgradeDatabasesAndCompareTemplates` compares the `.tdb` template with the `.ddb`'s TemplateTable. They are identical, because TemplateTable already holds the new fields. So no Template Changed wizard appears and nothing gets added.
2. **Check default values.** `TimelapseImageSetLoading.cs` (~lines 270-284) compares each existing DataTable column's stored default with the template's default. If any differ, it calls `FileDatabase.UpgradeFileDBSchemaDefaultsFromTemplate()`.
3. **Rebuild DataTable.** That method builds a new table containing one column for every field in **TemplateTable**, including the two new ones. It then copies the data with `CopyAllValuesFromTable` (`SQLiteWrapper.cs` ~line 1492), which takes its column list from the **new** table:
   `INSERT INTO new (…, NewFlag, NewPicklist) SELECT …, NewFlag, NewPicklist FROM DataTable`.
   SQLite fails with "no such column", because the old DataTable never had those columns.
4. **Roll back and show the error.** The rollback means nothing is damaged, but loading stops.
5. **The safety net never runs.** The next step, the missing/extra columns check (`TimelapseImageSetLoading.cs` ~line 291, `DatabaseColumnsMissingDialog`), would have offered to repair the missing columns, but it is never reached.

The method's own comment says it should only be called when the template and DataTable already have the same columns, but nothing checks this.

### Why a default would differ at all

- The user changed a default when editing the template.
- **Comparisons that always report a difference:**
  - Number fields (Integer/Decimal) are always stored with an empty default (`CreateFileDataColumnDefinition`, `FileDatabase.cs` ~lines 775-782). Any number field whose template default isn't empty mismatches on every open.
  - A default containing an apostrophe is stored escaped (`''`), so `pair.Value.Trim('\'') != row.DefaultValue` never matches.

## Part 2: how the file got this way (root cause, confirmed by the user's log below)

When a template change is detected, the wizard appears. Clicking **Open using New Template** runs `FileDatabase.OnExistingDatabaseOpenedAsync`. Each step there is saved to disk on its own:

1. Drop the `.ddb` TemplateTable (~line 351).
2. Recreate it from the `.tdb` (`base.OnDatabaseCreatedAsync`, ~line 410). **Saved.** ← the file is half-updated from here…
3. Delete DataTable columns for removed fields (~line 424).
4. Add DataTable columns for new fields (~line 522). ← …until here.
5. Rename columns (~line 593).

TemplateTable is what the next open compares against, so it acts as the record of whether the template change has been applied. Because it is replaced **first**, an interruption anywhere in steps 3-5 leaves a TemplateTable that says the update is done when it isn't. No later open will retry it.

The wizard, the add-column code and the type handling were reviewed, and no bug was found that skips step 4 on its own. Combined with the fact that this is rare, the likely cause is an occasional interruption, for example:

- an add-column failure because the `.ddb` was locked (OneDrive/Dropbox, antivirus, a second Timelapse instance);
- a crash somewhere in that stretch of code;
- the user closing or killing Timelapse while it was busy.

### Confirmed by the user's error log

The user's log contains:

> `2026-10-26 08:57:31.709 | ERROR | TimelapseNeedsToShutDownAsSQLErrorDialog: … File: 'N:\Commonwealth\2026-27\Fox Pre\TimelapseTemplate_NHT2627.ddb'. Context: The problem occurred in CreateEmptyMetadataInfoTable (MetadataInfo CreateTable). DB error: Failure dropping table 'FolderDataInfo'. code = IoErr (10) … disk I/O error`

1. **It happened in the middle of a template update.** `OnExistingDatabaseOpenedAsync` had dropped TemplateTable. `base.OnDatabaseCreatedAsync` (`CommonDatabase.cs` ~191-206) had already recreated it **empty** (`CreateEmptyTemplateTable`). Next, `CreateEmptyMetadataInfoTable` failed to drop `FolderDataInfo` because of a disk I/O error.
2. **The trigger** is an I/O error on a network drive (`N:\`). SQLite on network shares is known to produce occasional `disk I/O error`s. This is why the problem is rare.
3. **Execution continued after the "shut down" dialog.**
   - `CreateEmptyMetadataInfoTable` is `void`. It shows `TimelapseNeedsToShutDownAsSQLErrorDialog` and returns normally.
   - The code runs inside `Task.Run`, and the dialog is invoked through `Dispatcher.Invoke`, so the background thread waits for the user's click and then carries on.
   - `Application.Current.Shutdown()` only begins an orderly shutdown.

   So `SyncControlsToEmptyDatabase` then wrote the new fields into TemplateTable, and the process exited before the add-columns step ran. That is exactly the reported state. The user's TemplateTable being *full* rather than empty is the evidence that writing continued.
4. **If the user clicked "Restart Timelapse",** a second instance opened the same `.ddb` straight away (`Dialogs.cs` ~4528), possibly while the first was still writing. It saw matching templates, so no wizard appeared, and the Part 1 failure loop followed.

**Stopping writes after a fatal error is not enough on its own.** At the moment of this failure, TemplateTable had already been recreated empty. Stopping there would leave an empty TemplateTable beside a full DataTable. The next open would treat every field as new and fail trying to add columns that already exist. The real fault is rewriting TemplateTable before changing DataTable, with no way to undo partway. Hence option A below.

## Fix options for Part 2

### A. One all-or-nothing transaction (chosen)

Every change in the update either commits together or rolls back together. Other parts of Timelapse (merging, export) and external programs never see a half-updated `.ddb`. A failure leaves the file exactly as it was, and the user is told so and can try again. See the implementation and risk analysis below.

It is implemented (see "Option A: implementation" below). It was less invasive than first thought, because SQLite schema changes (`CREATE`, `DROP`, `ALTER TABLE`) are transactional, and every connection to the database is opened inside `SQLiteWrapper`.

### B. Change DataTable first, replace TemplateTable last, make every step repeatable (rejected)

The next open would redo an interrupted update. **Rejected** because the `.ddb` is still inconsistent between the interruption and the next open, and other code (merging, external programs) may use it in that state. The user would also see the Template Changed wizard again with no explanation.

### C. A saved "update in progress" record (rejected)

This builds on B. It saves the wizard's decisions first, so the next open can finish the update silently. **Rejected** for the same reason as B: the file is inconsistent until the next Timelapse open.

## Option A: implementation

The first design generated the whole update as a list of SQL statements before running it. It was replaced by a simpler approach with the same guarantee: the **existing** update code runs unchanged, on one shared connection inside one transaction. This reuses the SQL Timelapse already runs, and later steps still see earlier steps' changes. Steps such as `SyncControlsToEmptyDatabase` reload in-memory controls from the database partway through, which a pre-built statement list could not support.

**`SQLiteWrapper.RunAtomicallyAsync(work)`** (an "atomic scope"):
- Opens one connection, sets `PRAGMA foreign_keys = OFF` (before `BEGIN`, where SQLite honours it), then `BEGIN IMMEDIATE`. This takes the write lock at the start, so if another program is writing, the update fails before doing anything.
- While `work` runs, every wrapper method called from it uses that connection and transaction. This includes calls from tasks it starts, and calls on any other wrapper for the same file. The scope is tracked per async flow (`AsyncLocal`), so unrelated code keeps using its own connections: its reads see the old state, and its writes wait for the lock.
- Inside the scope, the wrapper's own transactions, retries and foreign-key pragmas are skipped, because the scope owns the transaction.
- **Any failure fails the whole scope**, even when the caller ignores the result. This covers failed writes, failed reads, and a transaction that SQLite ended itself after an I/O error. Once a step has failed, every later operation in the scope is refused, so nothing can run outside the transaction.
- At the end, it commits only if the work returned Ok and nothing failed. Otherwise it rolls back and returns the first failure.
- During a scope, `TimelapseNeedsToShutDownAsSQLErrorDialog` and `TimelapseOperationRetryDialog` are not shown. Helpers inside the update (for example `SyncControlToDatabase`) call them on failure. Instead, they record the failure, so the scope rolls back and reports it once.

**`FileDatabase.OnExistingDatabaseOpenedAsync`:**
1. Makes the backup, as before.
2. Runs `UpdateDatabaseToMatchTemplateAsync` (the former body, which now returns failures instead of showing the shutdown dialog) inside `RunAtomicallyAsync`. This covers image-level and folder-level changes in one transaction.
3. **On failure:**
   - Everything is rolled back.
   - The in-memory controls and metadata are reloaded from the unchanged file.
   - `Dialogs.TemplateUpdateFailedDialog` explains that *nothing was changed*, and offers three choices:
     - **Try again** repeats the update with the same wizard answers, which are held in `templateSyncResults`.
     - **Use original template** opens the file unchanged, with the template stored in it.
     - **Cancel** stops opening the file. `FileDatabase.TemplateUpdateCancelled` tells the image-set loader to abort quietly.
4. After a successful commit, it runs the recognition-table migrations (`UpdateOldStyleRecognitionTablesIfNeeded`, `RepairClassificationCategoriesIfNeeded`). These are outside the scope because the first one VACUUMs, which is impossible inside a transaction. They were already independent, repeatable steps.

**Not included in the scope:**
- `UpgradeFileDBSchemaDefaultsFromTemplate`, which runs later in the load and changes only column defaults. It is a single transaction of its own.
- The backwards-compatibility upgrades in `UpgradeDatabasesAndCompareTemplatesAsync`, which run before the wizard.

**Departures from the first design:**
- There is no post-commit `quick_check`. It reads the whole file, which is slow for large files on network drives, and SQLite's journal already guarantees an all-or-nothing commit.
- The update is not moved to a background thread.
- The backup is still made by `CreateBackupIfNeeded` (risk 7).

## Option A: what could go wrong

| # | Risk | What happens | Mitigation |
|---|---|---|---|
| 1 | **I/O error mid-transaction** (this user's case) | The scope rolls back, with no automatic retry (it can't know what the error did to the transaction), and the "nothing was changed" dialog offers Try again. If the rollback itself fails, SQLite leaves a hot journal (`.ddb-journal`), and the next connection to the file rolls it back automatically. | Standard SQLite behaviour. Tested: a failed step, an ignored failed step and an exception all leave the file unchanged. |
| 2 | **The journal is lost:** a user deletes `.ddb-journal`, a sync tool syncs the `.ddb` without it, or the `.ddb` is copied elsewhere before being reopened | Real corruption. This is already true of any Timelapse write, but a long transaction widens the window. | The dialog says not to move, copy or delete the file or its `-journal` file until Timelapse has reopened it. Reliable backup (7). |
| 3 | **Foreign-key cascade:** `Detections` references `DataTable` with `ON DELETE CASCADE`, and the update rebuilds (drops) `DataTable` | All Detections and Classifications are deleted | Foreign keys are off for the whole scope, set before `BEGIN`. Tested: rename, delete and add on `DataTable` in a scope leave every Detections row intact. (See the bug fix below, which this risk uncovered.) |
| 4 | **Another program is writing to the file** (a second user, an R or Python script, a merge) | `BEGIN IMMEDIATE` fails after SQLite's busy wait (about 3.5 s in testing), before anything is changed | The dialog lists this cause; the user closes the other program and clicks Try again. |
| 5 | **Two users update the same `.ddb` at once** | The second can't start until the first commits; it then fails (for example, a duplicate column) and rolls back | Safe. On reopening, the templates match. |
| 6 | **Time and space** | Table rebuilds (delete or rename a column) copy every row, so the journal is about the size of the table and that much free space is needed. If it's lacking, the update rolls back cleanly. Other programs can read but not write meanwhile. | The same work as before, now in one transaction instead of several. |
| 7 | **The pre-update backup is unreliable:** `CreateBackupIfNeeded` skips it if one was made recently, uses `File.Copy` (which ignores the journal), and gives up silently | A backup taken while the file is being written, or while a hot journal exists, can be corrupt | **Not yet done:** for template updates, always back up using SQLite's online backup API (`connection.BackupDatabase`); if that fails, warn and let the user choose. Less urgent now that a failed update changes nothing. |
| 8 | **Code in the scope that cannot run in a transaction** (`VACUUM`, `PRAGMA journal_mode`) | It fails, and the update rolls back | Kept out of the scope (the recognition migrations run after the commit). |
| 9 | **Other code writing to the same file during the update** (for example, a UI timer) | Its write waits on the scope's lock, and may fail with its own retry dialog | The update runs while the image set is loading, when nothing else uses the file. |
| 10 | **Failure after the commit,** in the rest of the load | The file is consistent (new state); only the session's in-memory data may be stale | Existing load-time error handling. The next open is fine. |
| 11 | **SQLite on network file systems:** atomic commit depends on the FS honouring flushes and locks; SQLite warns that some don't | Possible corruption in rare FS failures | Not fully avoidable; the same as every Timelapse write. Rely on the backup. |

### Testing

- **Done:** a scratch harness ran `RunAtomicallyAsync` on test databases with a cascading Detections table. Seven checks passed:
  - success commits everything, with recognitions intact;
  - a returned failure, an ignored failed operation and an exception each leave the file unchanged, and operations after a failure are refused;
  - reads inside the scope, including from `Task.Run`, see its writes;
  - a reader outside the scope sees the old state without blocking;
  - behaviour outside a scope is unchanged;
  - another writer holding the lock makes the scope fail cleanly.
- **In Timelapse:** on copies of real `.ddb` files, check that adds, deletes, renames and level changes give the same result as before, with recognitions intact.
- **Failure path in Timelapse:**
  1. Open the `.ddb` in DB Browser for SQLite.
  2. Make an edit without clicking "Write Changes", so DB Browser holds the write lock.
  3. Open the image set in Timelapse with a changed template. Expect the "nothing was changed" dialog.
  4. Revert the edit in DB Browser, then click Try again. The update should succeed.
- **Network:** on a share, disconnect mid-update. Expect the dialog and a clean reopen.

### What option A does not do

It doesn't repair files that are already half-updated, like the reported user's. Those need the backup restore below, or the deferred Part 1 repair.

## Bug fixed: renaming an image field could delete all recognition data

`SQLiteWrapper.SchemaAlterColumn`, which `SchemaRenameColumn` uses, rebuilds the table inside a transaction. It did **not** turn foreign keys off before `BEGIN`. Its `DropTable(connection, …)` toggles `PRAGMA foreign_keys` *inside* the transaction, which SQLite ignores. So renaming a field in the Template Changed wizard, on a `.ddb` with recognitions, dropped `DataTable` with foreign keys on, which cascade-deleted every row in `Detections` and `Classifications`.

This was confirmed with a standalone SQLite reproduction of the same statement sequence: 0 of 5 Detections rows survived before the fix, and 5 of 5 after it.

**The fix:** `PragmaSetForeignKeys(connection, false)` before `BeginTransaction`, and `true` restored in a `finally`. This is the same pattern `SchemaDeleteColumn` and `SchemaAlterTableWithNewColumnDefinitions` already used. Option A replaces this code path for template updates anyway.

Related, not yet fixed: `DeleteAllRowsInTables` embeds `PRAGMA foreign_keys = OFF/ON` in its statement string. That string runs inside `ExecuteNonQueryWithRollback`'s transaction, where SQLite ignores the pragmas, despite the method's comment saying otherwise. Its callers are:
- `MetadataInfo`, which has no foreign keys;
- `ClearRecognitionTables`, which clears Detections, DetectionsVideo and related tables. Foreign-key cascades still run there, but they only reach rows in tables that are being cleared anyway, so **no data is lost**.

The effect is that the pragma's stated purpose, speeding up deletes on large tables, isn't achieved. The fix would be to pass the pragmas as pre- and post-transaction statements.

## Related Part 1 fixes (deferred)

1. Run the missing/extra columns check and its repair **before** the default-value sync.
2. Make `UpgradeFileDBSchemaDefaultsFromTemplate` copy only the columns present in both tables (or skip the rebuild when the columns don't match the template).
3. Compare number defaults (and escaped apostrophes) the way they're actually stored, so the default check doesn't rebuild DataTable on every open.

## Reproducing the reported state

1. Copy any working image set.
2. In the Template Editor, add a Flag and change some field's default value. Save the `.tdb`.
3. Open the set in Timelapse and choose **Open using New Template**. Close Timelapse.
4. In the copy's `.ddb` (DB Browser for SQLite), drop the new Flag's column from `DataTable`. Leave `TemplateTable` alone.
5. Make the default differ again (edit the default in the `.tdb`, or in the matching `.ddb` TemplateTable row).
6. Open in Timelapse. You should get the same `SchemaAlterTableWithNewColumnDefinitions failed in UpgradeFileDBSchemaDefaultsFromTemplate` error.

## Workaround for the affected user

If the image set's `Backups` folder has a `.ddb` backup from before the template edit, restore it and reopen. The wizard will appear again and add the columns normally.
