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

## Part 2: how the file got this way (root cause, inferred)

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

## Fix options for Part 2

### A. One all-or-nothing transaction

Every step either succeeds together or is undone together. This is the strongest guarantee, but each wrapper method (`SchemaDeleteColumn`, `SchemaRenameColumn`, `CreateTable`, `Insert`, …) opens its own connection and runs its own transaction. Sharing a single one would mean reworking much of `SQLiteWrapper`. That is too much change and risk for a rare problem.

### B. Change DataTable first, replace TemplateTable last, make every step safe to repeat (recommended)

1. Add new columns, skipping any that already exist.
2. Rename columns, skipping any already renamed.
3. Delete removed columns. This step already skips columns that are gone.
4. **Replace TemplateTable last.** This becomes the "done" marker.

If the update is interrupted before step 4, TemplateTable is still the old version. The next open sees the template difference, shows the wizard again, and redoes the work. Steps that already finished are skipped. Interruptions can still happen, but the file fixes itself on the next open.

### C. A "sync in progress" flag

Write a flag to the ImageSet table at the start of the update and clear it at the end. If the flag is found on open, redo the update. This works, but it adds new saved state without doing anything B doesn't.

## Implementation notes for option B

All changes are in `FileDatabase.OnExistingDatabaseOpenedAsync`, plus small guards:

- **Reorder.** Drop and recreate TemplateTable (current ~lines 345-417) **after** the column changes, not before.
- **Look up new fields in the `.tdb`.** The add step currently finds a new field via `GetControlIDFromControls(dataLabel)` / `Controls.Find(id)` on the `.ddb`'s in-memory list of fields. That only works because TemplateTable was already replaced. Use `templateDatabase.GetControlFromControls(dataLabel)` instead. The same applies to folder-level fields (`templateDatabase.GetControlFromMetadataControls(dataLabel, level)`).
- **Make adding a column repeatable.** Skip it if `SchemaIsColumnInTable` says the column already exists. The new counter's column in the Markers table needs the same check.
- **Make renaming repeatable.** If the old name is gone and the new name already exists, skip it. Today `SchemaAlterColumn` throws in that case.
- **Markers clean-up query.** When a counter is deleted, the code removes empty rows from the Markers table using a where-clause built from `Controls` counters. Build it from the counter columns that actually exist in the Markers table at that moment. Otherwise it may refer to a column that was just deleted or hasn't been added yet.
- **Folder-level tables** (`MetadataTemplate`, `MetadataInfo`, the per-level tables) have the same ordering problem. Apply the same pattern. When a new folder level is added, its table is created from the new template's level info, so creating that table (`CreateFolderMetadataTablesIfNeeded`) must use the `.tdb`'s level info rather than the `.ddb`'s.
- **Suggested order of work:** image-level fields (DataTable and Markers) first, then folder-level tables.

### Behaviour after an interruption

- The user picks **Open using New Template** again: the update completes normally.
- The user picks **Open using Original Template** instead: DataTable may have extra columns. The existing extra-columns check already handles this (it offers delete or continue).
- **Renames:** a second pass shows the wizard again, and the user could make a different rename choice than the first time. Acceptable, but worth knowing.

### What option B does not do

It does not repair files that are already half-updated, like the reported user's. Those need the backup restore below, or a separate repair-on-open fix (the Part 1 fixes), which were intentionally deferred.

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
