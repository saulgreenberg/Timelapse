# Timelapse – Potential Improvements

Analysis of the Timelapse 2.5.0.7 codebase for architectural improvements, performance bottlenecks, and coding correctness issues.
Each item follows a standard structure: **Issue**, **Risk of Fixing**, **Consequence of Not Fixing**, **Potential Improvement**, **Plan**, **How to Test**.

---

## Workflow

When you type **`Next issue`**, the assistant will:

1. State the issue ID and title.
2. Summarise the issue, risk, consequence of not fixing, and proposed solution.
3. Ask whether to proceed.
4. On confirmation, make the code change.
5. Produce a short **Git commit message** (ready to paste) and a **test procedure**.

Items are ordered lowest-risk / highest-payoff first. Skip or defer any item by saying so.

| # | ID | Short description | Risk | Status |
|---|----|-------------------|------|--------|
| 1 | L-1 | Progressive DataTable population for large databases | High | ⬜ Pending |
| 2 | V-1 | Virtualized thumbnail grid for unbounded file counts | High | ✅ Complete|
| 3 | DB-1 | SQLite write failures silently swallowed at ~33 call sites | Medium | ✅ Complete |
| 4 | DB-2 | SQLite read failures silently swallowed — debug assert only, no user dialog | Low–Medium | ✅ Complete |
| 5 | MC-1 | View-only mode: MultiChoice values lose items containing spaces | Low | ✅ Complete (uncommitted, needs hand test) |

---

## L-1 · Progressive DataTable Population for Large Databases

### Issue

When the user selects all images on a large database (100K+ rows), the entire result set is loaded into the in-memory `DataTable` before the UI becomes usable. This produces a visible freeze proportional to database size — the dominant wait-time complaint from users working with large datasets. The freeze occurs because the current architecture treats the DataTable as an all-or-nothing load: the UI cannot display anything until every row is resident in memory.

### Risk of Fixing

**High.** This is an architectural change that touches the core data pipeline. The in-memory DataTable and the SQLite database are expected to be in strict sync at all times — every row in memory is an accurate reflection of the database, and all writes go to both simultaneously. Partial loading introduces a new concept (an incomplete-but-accurate DataTable) that every operation in the codebase that assumes completeness must be made aware of. Key risk areas:

- Navigation to a row that has not yet been loaded requires a strategy (stall, jump-fetch, or placeholder).
- Operations that span the full selection (Copy to All, counts, anomaly detection, date corrections) must check whether the load is complete before executing, or be redesigned to handle partial state.
- Anomaly dialogs currently fire synchronously at the end of load — they must be decoupled and run after background loading finishes.
- Any regression in the sync invariant would produce silent data corruption (writes to loaded rows appearing correct while unloaded rows are untouched).

### Consequence of Not Fixing

**High impact on large-dataset users.** Researchers working with 100K+ image databases experience a full UI freeze on every "Select All" or large selection operation. On spinning disks or network-hosted databases this can exceed 30 seconds. This is the single most-reported performance complaint. The freeze is not incremental — the user sees nothing until the entire load completes.

### Potential Improvement

The UI becomes responsive within a second of initiating a large selection. The user can immediately navigate to their last-viewed image and resume data entry while the remainder of the database loads in the background. Operations that require the full dataset are queued until loading completes, with a clear visual indicator.

### Architecture

The key insight is that the goal is not to replace the in-memory model (which is correct and fast) but to reduce how much must be loaded before the user can start working. The DataTable remains the authoritative in-memory store and the sync invariant is never broken — every row present in the DataTable at any point is a complete, accurate reflection of the database. The DataTable simply starts with fewer rows and grows to completeness in the background.

**Core concept: Priority window + background completion**

1. On selection, immediately load a priority window: the last-viewed image row (by remembered ID) plus a small neighbourhood (e.g. ±200 rows). Mark the DataTable as `PartiallyLoaded`.
2. Show the UI. The user can navigate within the loaded window and do data entry immediately.
3. A background thread appends the remaining rows in chunks. Each chunk is fully synced. When the last chunk lands, mark as `FullyLoaded`.
4. A subtle progress indicator (status bar or spinner) shows background loading is in progress.

**Completeness tracking**

Add an `IsFullyLoaded` boolean (or a `LoadState` enum: `Empty` / `PartiallyLoaded` / `FullyLoaded`) to `FileDatabase` or `DataHandler`. Every operation that assumes a complete DataTable checks this flag:

- **Can proceed on partial load:** navigation within loaded rows, data entry, single-image operations.
- **Must wait for full load:** Copy to All, date corrections across the selection, counts displayed in the status bar, anomaly detection dialogs, CSV export, recognition import.
- **Waiting strategy:** disable the control with a tooltip ("Loading — please wait") or queue the operation to run automatically when `FullyLoaded` is reached.

**Navigation to unloaded rows**

When the user navigates to a row index that has not yet arrived in the background load:

- **Jump-fetch:** issue a single `SELECT WHERE ID = x` to retrieve just that row, insert it at the correct position in the DataTable, and resume navigation. This is a single primary-key lookup — essentially instant. The background loader skips that row when it reaches it.
- This preserves the sync invariant: the fetched row is fully accurate and immediately part of the DataTable.

**Anomaly dialogs**

Currently fired synchronously at the end of selection. Decouple: run anomaly detection as a background task that starts after `FullyLoaded`. Surface results as a non-blocking notification (toast or status bar badge) that the user can dismiss or act on. This is also a UX improvement independent of loading strategy.

### Plan (high-level)

1. Add `LoadState` to `FileDatabase` and expose it via `DataHandler`.
2. Implement `SelectFilesWithPriorityWindowAsync(lastViewedID, windowSize)` — a variant of `FilesSelectAndShowAsync` that loads the priority window first, sets `PartiallyLoaded`, shows the UI, then continues loading in the background.
3. Add a background loading loop in `FileDatabase` that appends rows in chunks (e.g. 1,000 rows) and updates `LoadState` when complete.
4. Add a completeness guard (check + disable/queue) to every operation identified in the survey below.
5. Implement jump-fetch in the navigation path for unloaded rows.
6. Decouple anomaly dialogs from the load sequence.

**Operations requiring completeness survey**

Before implementation, audit all callers that iterate or count the DataTable to produce a definitive list of operations that need the completeness guard. This survey should be done as a first implementation step.

### How to Test

1. Open a database with 50K+ images. Confirm the UI is responsive within ~1 second of selecting all files. Confirm the last-viewed image is displayed immediately.
2. Navigate forward and backward within the priority window — confirm instant response.
3. Navigate rapidly past the end of the loaded window — confirm jump-fetch retrieves the row without visible corruption.
4. Attempt Copy to All before load completes — confirm it is disabled or queued, not silently partial.
5. Wait for `FullyLoaded`. Confirm all previously disabled operations become available.
6. Confirm anomaly dialog fires (if applicable) after full load, not blocking initial display.
7. Make a data entry change during partial load — confirm the write is correctly reflected in both memory and database.
8. Profile memory usage before and after: confirm the final loaded DataTable is the same size as the current single-phase load.

---

## MC-1 · View-only mode: MultiChoice values lose items containing spaces

### Issue

The multi-select drop-down (`WatermarkCheckComboBox`, built on the toolkit's `CheckComboBox`) re-reads its `Text` into checked items only when its `IsEditable` property is true (`CheckComboBox.OnTextChanged` returns early otherwise). When it does, `CheckComboBox.UpdateFromText()` (src/TimelapseWpf.Toolkit/CheckComboBox/Implementation/CheckComboBox.cs) removes *every* space and then splits on commas: `Text.Replace(" ", string.Empty).Split(...)`. `Elk,Red deer` becomes `Elk` and `Reddeer`; `Reddeer` matches no item, so only Elk is checked, and the control then **rewrites its own `Text` to `Elk`**.

Verified 2026-11-21 with a small test harness against the built toolkit:

| IsEditable | After `Text = "Elk,Red deer"` | After the drop-down opens |
|---|---|---|
| false (default) | Text unchanged, nothing checked yet | Elk and Red deer checked (correct) |
| true | **Text becomes `Elk`** | Elk only |

Where `IsEditable` is true:

- `DataEntryMultiChoice` and `MetadataDataEntryMultiChoice` set `IsEditable = isReadOnly` (the property is used inversely, as a read-only flag). Data entry controls are only read-only in **view-only mode** (`DataEntryControl.ContentReadOnly`).
- Custom Select's check combo boxes (MultiChoice, and Fixed Choice *is any of*) and editable data entry leave `IsEditable` false, so they are **not** affected: their checked items are set when the drop-down opens, from `Text.Split(',')`, which keeps spaces.

### Risk of Fixing

**Low.** The fix is one line in the toolkit. `CheckComboBox` is shared by every MultiChoice control, but the changed line only runs when `IsEditable` is true (view-only mode).

### Consequence of Not Fixing

In view-only mode, a MultiChoice value containing an item with a space (e.g., *Elk,Red deer*) is displayed as *Elk*. Worse, if the menu also has the same item without the space, the wrong item is shown: with items *AABB* and *AA BB*, a value of *AA BB* becomes *AABB* and is displayed as the other item. Edits are blocked in view-only mode, so this should only affect what is shown, not the stored data (not traced through every save path).

### Potential Improvement

Split the text on commas and trim each piece, instead of removing all spaces: `Text.Split([Delimiter], StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim())`. Items keep their inner spaces, and spaces after commas are still ignored.

Related, minor: the drop-down open handlers (`ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened`, `DataEntryMultiChoice.ContentControl_DropDownOpened`) split on commas without trimming, so a value written with spaces after commas (e.g., `Elk, Red deer`, perhaps from an imported CSV) would not check *Red deer*. Values written by the control itself have no such spaces.

### Status (2026-11-21)

Done, not yet committed or hand tested. `CheckComboBox.UpdateFromText()` now splits with `TrimEntries` instead of removing all spaces. The drop-down open handlers also trim each item (`ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened`, `DataEntryMultiChoice` open handler and `IntializeMenuFromCommaSeparatedList`, and the template editor's default-value MultiChoice in `TemplateDataGridControl` and `MetadataDataGridControl`): the harness showed that, in normal mode, a value written as `Elk, Red deer` lost Red deer when the drop-down opened. Verified with the harness for AA, AABB, AA BB and their combinations, and for `Elk,Red deer` and `Elk, Red deer`, in both modes.

### Plan

1. Change `CheckComboBox.UpdateFromText()` to trim each piece rather than removing all spaces.
2. Optionally trim the pieces in the two drop-down open handlers too.
3. Retest MultiChoice controls in view-only and normal modes.

### How to Test

1. Make a MultiChoice field whose items include one with a space (e.g., *Red deer*) and one without (*Elk*). Set a file's value to Elk and Red deer.
2. Open the image set in view-only mode. Before the fix, the field shows *Elk*; after it, *Elk,Red deer*. Check the folder metadata MultiChoice the same way.
3. In normal mode, confirm the field still shows both, the drop-down checks both, and ticking a third item keeps both.
4. With menu items *AA*, *AABB* and *AA BB*, set values of each and of combinations; confirm in view-only and normal modes that each is shown as itself and never as one of the others.
5. Custom Select: confirm a MultiChoice selection, and a Fixed Choice *is any of* selection, with Red deer still reopen with Red deer checked.

---
