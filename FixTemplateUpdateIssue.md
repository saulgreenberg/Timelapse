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

Every change in the update either commits together or rolls back together. Other parts of Timelapse (merging, export) and external programs never see a half-updated `.ddb`. A failure leaves the file exactly as it was, and the user is told so and can try again. See the design and risk analysis below.

This turned out to be much less invasive than first thought:
- SQLite schema changes (`CREATE`, `DROP`, `ALTER TABLE`) are transactional.
- The bundled SQLite (3.50.4) supports native `ALTER TABLE … RENAME COLUMN` and `DROP COLUMN`, so no table rebuilds are needed.
- `SQLiteWrapper.ExecuteNonQueryWithRollback(pre, statements, post)` already runs a statement list in one transaction, with `PRAGMA foreign_keys = OFF` before `BEGIN` and whole-transaction retry on BUSY, LOCKED or IOERR.

### B. Change DataTable first, replace TemplateTable last, make every step repeatable (rejected)

The next open would redo an interrupted update. **Rejected** because the `.ddb` is still inconsistent between the interruption and the next open, and other code (merging, external programs) may use it in that state. The user would also see the Template Changed wizard again with no explanation.

### C. A saved "update in progress" record (rejected)

This builds on B. It saves the wizard's decisions first, so the next open can finish the update silently. **Rejected** for the same reason as B: the file is inconsistent until the next Timelapse open.

## Option A: design

**Plan, then run.** All reads happen before the transaction, and none happen inside it.

**1. Prepare (read-only; nothing in the `.ddb` changes).**
- Compare the templates and show the wizard, as today.
- Make a backup (see risk 7).
- Read the current schema of every table that will change: DataTable, Markers, level tables, MetadataInfo.
- From `templateSyncResults`, the `.tdb`'s in-memory controls and the schema snapshot, generate the full statement list:

| Change | Statement(s) |
|---|---|
| Added image field | `ALTER TABLE DataTable ADD COLUMN …`; plus `Markers` for counters |
| Renamed field | `ALTER TABLE DataTable RENAME COLUMN a TO b`; plus `Markers` if present |
| Deleted field | `ALTER TABLE DataTable DROP COLUMN x`; plus `Markers`; plus the empty-Markers-rows `DELETE`, with its where-clause built from the **final** counter list |
| Level tables | `CREATE TABLE` (appended level), `DROP TABLE` (level now empty), `ADD/RENAME/DROP COLUMN` |
| Template tables | `DROP` + `CREATE` + `INSERT` for TemplateTable, MetadataTemplate and MetadataInfo, from the `.tdb` |
| Control type changed | `UPDATE ImageSet SET SearchTerms = ''` |
| Attribute-only changes (Condition 4) | `UPDATE TemplateTable SET … WHERE DataLabel = …` |

**2. Run.** Call `ExecuteNonQueryWithRollback([PRAGMA foreign_keys = OFF], script, [PRAGMA foreign_keys = ON])` on a background thread, with a busy indicator.

**3. After the commit.**
- Run `PRAGMA quick_check`.
- Reload the in-memory controls, metadata and data-label maps from the `.ddb`.
- Continue loading as today.

**On failure.** The transaction rolls back. The in-memory objects were never changed, because only the plan used them. Timelapse shows a single dialog:

> *Timelapse could not update your data file to match the changed template. **Nothing was changed**: your data file is exactly as it was. This is usually caused by a temporary network or file-access problem.* **[Try again] [Open with original template] [Cancel]**

**Try again** re-reads the schema, rebuilds the script from the **same wizard answers**, and runs it again. Log the failing script for diagnosis.

**Rules for the implementation**
- Nothing inside the update may call `TimelapseNeedsToShutDownAsSQLErrorDialog`. Generating the script must never touch the database.
- Foreign keys must be off **before** `BEGIN`. SQLite ignores the pragma inside a transaction.
- Generate SQL with the existing `Sql.Quote` helpers. Data labels and defaults, including apostrophes, must be quoted exactly as `CreateFileDataColumnDefinition` does.
- If the schema snapshot shows a column can't be dropped natively (it's indexed, unique, used in a view or trigger, or a primary key), fall back to a table rebuild **inside the same transaction**. Foreign keys are already off, so that's safe.
- Suggested order of work: image level first (DataTable, Markers, template tables), then folder levels (the most complex part: appended levels, empty levels, incompatible hierarchies).

## Option A: what could go wrong

| # | Risk | What happens | Mitigation |
|---|---|---|---|
| 1 | **I/O error mid-transaction** (this user's case) | The wrapper rolls back and retries the whole script (about 2.5 s), then shows the "nothing changed" dialog. If the rollback itself fails, SQLite leaves a hot journal (`.ddb-journal`). The next writer to open the file (Timelapse's first `quick_check`) rolls it back automatically. | Standard SQLite behaviour; nothing extra needed. |
| 2 | **The journal is lost:** a user deletes `.ddb-journal`, a sync tool syncs the `.ddb` without it, or the `.ddb` is copied elsewhere before being reopened | Real corruption. This is already true of any Timelapse write, but a long transaction widens the window. | Reliable backup (7); post-commit `quick_check`; the dialog says not to move or copy the file until Timelapse has reopened it. |
| 3 | **Foreign-key cascade:** `Detections` references `DataTable` with `ON DELETE CASCADE`, connections use `ForeignKeys = true`, and `DROP TABLE` on a parent deletes its rows first | All Detections and Classifications are deleted | Native `ALTER` never drops `DataTable`; foreign keys are off before `BEGIN`. (See the bug fix below, which this risk uncovered.) |
| 4 | **Another program has the file open** (a second user, an R or Python script, a merge) | `COMMIT` returns BUSY; the wrapper retries, then fails cleanly | The dialog says the file seems to be in use: close other programs and try again. |
| 5 | **Two users update the same `.ddb` at once** | The first commits; the second fails (for example, duplicate column) and rolls back | Safe. On reopening, the templates match. |
| 6 | **Time and space** | `ADD` and `RENAME` only edit the schema. `DROP COLUMN` rewrites every row, so the journal is about the size of the table and that much free space is needed (if it's lacking, the update rolls back cleanly). Other programs are locked out meanwhile. | Background thread and busy indicator. Today's rebuilds cost at least as much. |
| 7 | **The pre-update backup is unreliable today:** `CreateBackupIfNeeded` skips it if one was made recently, uses `File.Copy` (which ignores the journal), and gives up silently | A backup taken while the file is being written, or while a hot journal exists, can be corrupt | For template updates, always back up using SQLite's online backup API (`connection.BackupDatabase`). If that fails, warn and let the user choose. |
| 8 | **Native `DROP COLUMN` refused** | Clean rollback | Detect from the schema snapshot and rebuild that table inside the transaction. |
| 9 | **A bug in the generated SQL** | Clean rollback; never half-applied | Unit tests on copies of real `.ddb` files. |
| 10 | **Failure after the commit, during the in-memory reload** | The file is consistent (new state); only the session's in-memory data is stale | Close the image set with the read-error dialog. The next open is fine. |
| 11 | **SQLite on network file systems:** atomic commit depends on the FS honouring flushes and locks; SQLite warns that some don't | Possible corruption in rare FS failures | Not fully avoidable; the same as every Timelapse write. Rely on the backup and the post-commit `quick_check`. |

### Testing

- **Fault injection:** add a test hook that makes statement *k* fail. For every *k*, the `.ddb`'s schema and row checksums must be identical to before.
- **Success cases:** on copies of real `.ddb` files with adds, deletes, renames and level changes, the result must match what the current code produces.
- **Network:** on a share, disconnect mid-update and hold a read lock from another process. Expect the "nothing changed" dialog and a clean reopen.

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
