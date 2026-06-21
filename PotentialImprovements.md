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
| 2 | V-1 | Virtualized thumbnail grid for unbounded file counts | High | 🔄 In Progress |
| 3 | DB-1 | SQLite write failures silently swallowed at ~33 call sites | Medium | 🔄 Partially Fixed |
| 4 | DB-2 | SQLite read failures silently swallowed — debug assert only, no user dialog | Low–Medium | ⬜ Pending (after DB-1) |

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

## V-1 · Virtualized Thumbnail Grid for Unbounded File Counts

### Nomenclature

- **Home image** — the full-sized image representing the currently displayed file, shown in the main view before the user starts to zoom.
- **Home thumbnail** — the thumbnail representation of that same file, displayed in the virtualized grid.
- **Anchor** — the thumbnail representing the file currently displayed in the top-right corner of the virtualized grid. The anchor and home thumbnail may differ if the user has scrolled the grid after activation.

### Issue

The existing `ThumbnailGrid` allocates one `ThumbnailInCell` WPF control per visible cell and rebuilds the entire grid whenever the zoom level or window size changes. The number of simultaneously rendered controls is bounded only by the available screen space, so the approach scales to at most a few hundred thumbnails at once. There is no way for a user to scroll through a dataset of thousands or millions of files without first navigating to the right starting position and then re-entering the grid — every grid view is a fixed snapshot of a contiguous window starting at `FileTableStartIndex`. Large datasets are therefore impractical to browse visually.

### Risk of Fixing

**High** — but contained. The new control is written from scratch alongside the existing `ThumbnailGrid`, which is left entirely untouched. Integration risk is limited to:

- Adding a Ctrl+scroll-wheel activation path in `MarkableCanvas` (a small, isolated change).
- Sharing `ThumbnailInCell` as the per-cell renderer; any bugs introduced there would affect both grids.
- Selection state now spans off-screen rows (a `HashSet<int>` of `FileTable` indices), which is a new concept that must not leak into code that reads selection from `ThumbnailGrid`.

The existing grid continues to activate on plain scroll-wheel. The new grid is accessed only via Ctrl+scroll-wheel. Both can be active at the same time in different states without conflict.

### Consequence of Not Fixing

Users with datasets of tens of thousands to millions of images cannot meaningfully browse thumbnails. They must rely on sequential single-image navigation or external tools to find images visually. This is the primary UX gap relative to other media management applications and a common feature request.

### Potential Improvement

A new `ThumbnailGridVirtualized` control that:

- Displays the same per-cell information as `ThumbnailInCell` (episode number, filename, time, play button, duplicate indicator, bounding boxes).
- Scrolls vertically through the entire `FileTable` — potentially millions of rows — with constant memory overhead regardless of dataset size.
- Adjusts cell size and column count via mouse wheel (same gesture as today).
- Scrolls via Shift+mouse-wheel or the vertical scroll bar.
- Supports the same click / Ctrl+click / Shift+click selection model, with Shift+click selecting a linear index range that may span off-screen rows.
- Activates via Ctrl+scroll-wheel and coexists with the unmodified `ThumbnailGrid` during a testing period, after which it is intended to replace it.

### Architecture

**Rendering approach: Canvas-based manual virtualization**

WPF's built-in `VirtualizingWrapPanel` is not a standard control and third-party implementations are unreliable at a million items. A `ScrollViewer` wrapping a `Canvas` with manually managed cell placement is the correct choice at this scale.

```
ScrollViewer (vertical scroll, clip)
  └─ Canvas  (Height = totalRows × rowStride, Width = gridWidth)
      └─ [pool of ThumbnailInCell instances — only visible rows + 2 buffer rows]
```

The `Canvas` height is set to `ceil(FileTable.RowCount / columnCount) × (cellHeight + gap)` — this gives the scroll bar the correct total range without allocating any objects for off-screen rows.

**Cell pool**

A fixed pool of `ThumbnailInCell` controls is created on activation, sized to `(visibleRows + 4) × columnCount`. On each `ScrollChanged` event:

1. Compute the first visible row index: `firstRow = floor(verticalOffset / rowStride)`.
2. Compute the `FileTable` index range `[firstRow × cols, (firstRow + visibleRows + 2) × cols)`.
3. For each pooled control, assign the `ImageRow`, `FileTableIndex`, `GridIndex`, and position it on the `Canvas` via `Canvas.SetTop` / `Canvas.SetLeft`.
4. Controls whose assigned index is out of range are hidden (`Visibility = Collapsed`).

This means at most `(visibleRows + 4) × columnCount` controls exist at any time — typically 50–200 controls regardless of dataset size.

**Bitmap loading**

Reuse the existing two-pass `BackgroundWorker` pattern (images first, videos second). On scroll, cancel the current load, reassign pool controls, restart the load for the new visible range. A per-load timestamp prevents stale bitmap assignments from a cancelled load overwriting fresh ones.

**Zoom (cell size)**

Mouse wheel (without modifiers) calls `ResizeCells(delta)`:

- Increases or decreases `cellHeight` by a fixed step, clamped to `[MinimumThumbnailHeight, gridHeight]`.
- Derives `cellWidth = cellHeight × aspectRatio`.
- Derives `columnCount = floor(gridWidth / cellWidth)`.
- Recomputes Canvas height, repositions all pool controls, reloads bitmaps.

This is analogous to the `Level`-based zoom in `ThumbnailGrid` but uses a continuous pixel height rather than integer levels.

**Scrolling**

Shift+mouse-wheel adjusts `ScrollViewer.ScrollToVerticalOffset` by `±rowStride`. The scroll bar is always visible and functional. Keyboard arrow keys and Page Up / Page Down are also wired to the `ScrollViewer`.

**Selection state**

```csharp
private HashSet<int> _selectedIndices = new();
private int _anchorIndex = -1;   // last single-click or ctrl-click target
```

- **Single click**: `_selectedIndices = { clickedIndex }`, `_anchorIndex = clickedIndex`. Refresh visual state of all pool controls.
- **Ctrl+click**: toggle `clickedIndex` in `_selectedIndices`. Update `_anchorIndex`. Refresh the one affected pool control.
- **Shift+click**: compute range `[min(_anchorIndex, clickedIndex), max(_anchorIndex, clickedIndex)]`. Add all indices in the range to `_selectedIndices`. Do NOT update `_anchorIndex`. Refresh all pool controls (some visible, rest have no control to update).
- **Drag**: same as today — bounding-box selection over visible cells only; adds all cells whose `FileTableIndex` falls in the box to `_selectedIndices`.
- **Double-click**: raise a `DoubleClick` event (same signature as `ThumbnailGrid`) to navigate the main view to that image; deactivate the virtual grid.

On each pool-control reassignment (scroll or zoom), each control's `IsSelected` property is set from `_selectedIndices.Contains(fileTableIndex)`.

**Public API (mirrors ThumbnailGrid where possible)**

```csharp
FileTable FileTable { get; set; }
DataEntryControls DataEntryControls { get; set; }
string RootPathToImages { get; set; }
int AvailableColumns { get; }         // current column count
bool IsGridActive { get; }            // true when activated
List<int> GetSelected();              // FileTable indices of selected items
int SelectedCount();
void SelectNone();
ThumbnailGridRefreshStatus Refresh(double gridWidth, double gridHeight, bool? zoomIn);
void CancelUpdate();
event EventHandler<ThumbnailGridEventArgs> DoubleClick;
```

**Activation / deactivation (MarkableCanvas changes)**

In the scroll-wheel handler in `MarkableCanvas`:

- Plain scroll (no modifier) → existing `ThumbnailGrid` activation path (unchanged).
- Ctrl+scroll → `ThumbnailGridVirtualized` activation path.
  - If not yet active: call `Refresh(...)` with the current `FileTableStartIndex` as the initial scroll position.
  - If already active: call `Refresh(...)` with `zoomIn = (delta < 0)`.
  - Ctrl+scroll to zoom level 0 (single cell) deactivates the virtual grid, same as plain scroll does for `ThumbnailGrid`.

Both grids are hosted in `MarkableCanvas` and independently visible/hidden via `Visibility`. They do not share state.

### ThumbnailInCell Compatibility

`ThumbnailInCell` can be used almost entirely as-is. No structural changes to the control are needed.

**Works without modification:**
- All overlays (`RefreshEpisodeInfo`, `RefreshBoundingBoxes`, `RefreshDuplicateInfo`) — take explicit `fileTable` / `fileIndex` arguments, no coupling to grid layout.
- `IsSelected` setter — directly updates background and checkmark; setting `cell.IsSelected = _selectedIndices.Contains(fileTableIndex)` on each pool reassignment is sufficient.
- `GetThumbnail` / `SetThumbnail` — take explicit dimensions, no layout coupling.
- `DateTimeLastBitmapWasSet` — stale-bitmap guard works identically in a pool.
- `CellHeight` / `CellWidth` being `readonly` — the pool is rebuilt when zoom changes anyway, so this is by design.

**One defensive fix required in `ThumbnailInCell_Loaded` (line 112):**

`Loaded` fires when a control is first added to the Canvas. In the pool pattern all controls are added at pool-creation time before `ImageRow` is assigned, so `ImageRow` is `null` at that point and `ImageRow.IsVideo` throws. Fix:

```csharp
// change:
if (ImageRow.IsVideo)
// to:
if (ImageRow?.IsVideo == true)
```

**One usage-side responsibility (no change to `ThumbnailInCell` needed):**

`InitializePlayButton` is called in `Loaded` and therefore runs only once per pool control. When a pooled control is later reassigned from a non-video to a video row during scrolling, the virtual grid's reassignment code must handle the play button explicitly:

```csharp
if (cell.ImageRow.IsVideo)
{
    cell.InitializePlayButton();     // already guards against double-init
    cell.PlayButton.Visibility = Visibility.Visible;
}
else
{
    cell.PlayButton.Visibility = Visibility.Collapsed;
}
```

### Execution Process

Each step is summarised and confirmed before implementation. After each step, test instructions and uncertainties are provided. Progress is recorded here.

### Plan

| Step | Description | Status |
|------|-------------|--------|
| 1 | Apply `ThumbnailInCell_Loaded` null guard | ✅ Done |
| 2 | Create `ThumbnailGridVirtualized` skeleton (XAML + API shell) | ✅ Done |
| 3 | Implement cell pool — create, position, recycle `ThumbnailInCell` instances on scroll and zoom | ✅ Done |
| 4 | Implement bitmap loading — two-pass `BackgroundWorker` with scroll cancellation | ✅ Done |
| 5 | Implement selection — `HashSet<int>` model, single/Ctrl/Shift/drag/double-click | ✅ Done |
| 6 | Wire Ctrl+scroll in `MarkableCanvas` | ✅ Done |
| 6b | Post-wiring bug fixes and scroll/zoom refinements | ✅ Done |
| 7a | Wire `DoubleClick` in `TimelapseWindow` | ✅ Done |
| 7b | Wire `SelectionChanged` in `TimelapseWindow` | ✅ Done |
| 8 | Smoke-test on small dataset; stress-test at 10K, 100K, 1M-row `FileTable` | ⬜ Pending |
| 9 | Cut over after approval — remove `ThumbnailGrid`, restore plain scroll-wheel, delete Ctrl+scroll routing | ⬜ Pending |

### Step 6b — Bug Fixes and Refinements (completed)

The following issues were identified and fixed during testing after Step 6:

**Zoom symmetry** (`Refresh`): replaced continuous `cellHeight *= 0.8` with a discrete `zoomLevel` integer. Level 1 = initial view (`newGridHeight / 3`). All cell heights are computed as `initialCellHeight * ZoomStep^(level-1)`, so N zoom-outs requires exactly N zoom-ins to exit the grid.

**Scroll-position anchor across zoom**: added `zoomAnchorFileIndex` (the file index at the top-left) and `suppressAnchorUpdate` flag. On each zoom the anchor file index is passed to `RebuildPool`, which computes `startRow = anchorFileIndex / newColumnCount` — keeping the same file near the top-left regardless of column-count change. `suppressAnchorUpdate` prevents the zoom-triggered `ScrollChanged` from overwriting the anchor; only genuine user scrolls update it. Note: when the column count changes, the anchor file stays on the visible top row but may not be at column 0 (unavoidable grid layout constraint).

**Row-snap after scrolling** (`ScrollViewer_ScrollChanged`): a 150 ms `DispatcherTimer` (`snapScrollTimer`) is restarted on every genuine user scroll. When it fires it rounds `VerticalOffset` to the nearest `cellHeight` boundary, ensuring the top row always shows complete images after the user stops scrolling.

**Scrollbar track click = one row** (`OnLoaded`): after the visual tree is ready, the vertical `ScrollBar`'s internal `Track.IncreaseRepeatButton` and `Track.DecreaseRepeatButton` have their `Command` nulled and a `Click` handler attached that scrolls by exactly `cellHeight`. The `RepeatButton`'s built-in auto-repeat provides held-click continuous scrolling with no extra timer.

**E key / episode refresh** (`TimelapseMenuView.EpisodeShowHide`): added `else if (IsThumbnailGridVirtualizedVisible)` branch calling `ThumbnailGridVirtualized.RefreshBoundingBoxesAndEpisodeInfo()`.

**H key / bounding-box refresh** (`MarkableCanvas` PreviewKeyDown/Up): same pattern — added `IsThumbnailGridVirtualizedVisible` branch.

**BackgroundWorker race condition**: lambda closures now capture a local `BackgroundWorker worker = new()` instead of the field, preventing a cancelled worker's `ReportProgress` from firing on a new load.

**Drag-select guard** (`isDraggingFromCanvas`): only set to `true` when `PreviewMouseLeftButtonDown` lands on a valid cell; prevents scrollbar-drag from triggering a spurious drag-select.

**Ctrl+scroll event routing** (`CtrlMouseWheelScrolled`): `ScrollViewer_PreviewMouseWheel` sets `e.Handled = true` for all wheel events (blocking the ScrollViewer's own scroll). For Ctrl+scroll it raises `CtrlMouseWheelScrolled`; `MarkableCanvas` subscribes and routes to `TryZoomInOrOutVirtualized`.

**Initial scroll position on activation** (`TimelapseFileShow.cs` ~line 149): `ThumbnailGridVirtualized.FileTableStartIndex` is now set alongside `ThumbnailGrid.FileTableStartIndex` on every file navigation, so the grid always activates at the currently displayed image rather than the last grid position.

### Step 7 — Remaining Work (resume here next session)

Most of Step 7 was completed as part of Step 6b. What remains are two event wires in `TimelapseWindow`:

**7a — Wire `DoubleClick`**: ✅ Done. Subscribed `MarkableCanvas.ThumbnailGridVirtualized.DoubleClick += ThumbnailGridVirtualized_DoubleClick` at `TimelapseWindow.xaml.cs:133`. Handler calls `Reset()` then `SwitchToImageView()`/`SwitchToVideoView()` (based on `e.ImageRow.IsVideo`) then `FileShow`.

**7b — Wire `SelectionChanged`**: ✅ Done. Three changes: (1) `IsDisplayingSingleImage()` now returns `false` when the virtual grid is visible; (2) `DataGridSelectionsTimer_Tick` has a new `else if (IsThumbnailGridVirtualizedVisible)` branch using `ThumbnailGridVirtualized.GetSelected()`; (3) subscribed `ThumbnailGridVirtualized.SelectionChanged += ThumbnailGridVirtualized_SelectionChanged` — handler calls `DataGridSelectionsTimer_Reset()`. Note: `DataEntryControls.SetEnableState` is already called inside `EnableOrDisableControlsAsNeeded()` before `SelectionChanged` is raised, so the handler only needs the DataGrid sync.

### How to Test

1. Open a database. Ctrl+scroll-wheel — confirm the virtualized grid activates and shows thumbnails at the correct starting position.
2. Scrollbar drag and click above/below thumb — confirm smooth scrolling; confirm the top row snaps to a complete image after releasing; confirm click above/below thumb scrolls by exactly one row, and held click auto-scrolls.
3. Mouse-wheel (no modifier) — confirm cell size changes and column count updates; confirm the scroll bar range recalculates correctly; confirm N zoom-outs requires exactly N zoom-ins to exit.
4. Confirm all overlays render: episode number, filename, time, play button on videos, duplicate indicator, bounding boxes.
5. Press E and H — confirm episode and bounding-box overlays update across all visible pool cells, not just the first.
6. Single-click, Ctrl+click, Shift+click across a scroll boundary — confirm selection state is preserved after scrolling away and back.
7. Drag-select a region — confirm all cells in the bounding box are selected; confirm clicking the scrollbar does not trigger a drag-select.
8. Double-click a thumbnail — confirm main view navigates to that image and the virtual grid deactivates.
9. Plain scroll-wheel — confirm existing `ThumbnailGrid` activates independently; confirm neither grid interferes with the other.
10. Profile with a 100K-row `FileTable`: confirm control count stays constant regardless of scroll position; confirm memory is not growing with scrolling.
11. Resize the window while the virtual grid is active — confirm the pool resizes and cell positions recalculate correctly.

---

---

## DB-1 · SQLite Write Failures Silently Swallowed at ~33 Call Sites

### Background — Reported Crash (v2.5.0.6)

A user reported a crash while drag-reordering controls in the Template Editor. The full stack trace:

```
code = CantOpen (14), message = System.Data.SQLite.SQLiteException: unable to open database file
   at SQLite3.Open(...)
   at SQLiteConnection.Open()
   at SQLiteWrapper.ExecuteNonQueryWithRollbackCore(...)
   at SQLiteWrapper.ExecuteNonQueryWithRollback(...)
   at SQLiteWrapper.Update(...)
   at CommonDatabase.SyncControlsToDatabase()
   at CommonDatabase.UpdateControlDisplayOrder(...)
   at TemplateEditorWindow.TemplateDoUpdateControlOrder()
   at TemplateDataEntryPreviewPanel.ControlsPanel_DragDrop(...)
```

**Root cause:** `connection.Open()` was called outside the `try` block in `ExecuteNonQueryWithRollbackCore`, so a `SQLiteException(CANTOPEN, 14)` propagated as an unhandled crash rather than returning `SqlOperationResult.Fail`. The trigger was the `.tdb` file being on OneDrive or a network share, which temporarily locks files during sync. The existing `BusyTimeout` mechanism cannot help because it only applies after a connection is successfully opened.

### What Was Fixed (2026-06-11)

**1. `SQLiteWrapper.cs` — retry loop around `connection.Open()`**

Added a 5-attempt retry loop with 200 ms delay specifically for `SQLiteErrorCode.CantOpen`. On exhaustion, returns `SqlOperationResult.Fail` instead of throwing. All other exception types fall through to the existing catch block unchanged.

**2. `CommonDatabase.cs` — `SyncControlsToDatabase` and `UpdateControlDisplayOrder` return `bool`**

Both methods were `void` and discarded the `Database.Update` return value. Changed to `bool`; failure propagates up instead of being silently swallowed.

**3. `TemplateCode.cs` — `TemplateDoUpdateControlOrder` returns `bool`**

Changed from `void` to `bool`; propagates the result from `UpdateControlDisplayOrder`.

**4. `TemplateDataEntryPreviewPanel.xaml.cs` and `TemplateSpreadsheetPreviewControl.xaml.cs`**

Both drag-drop/reorder handlers now check the return value and call `Dialogs.CouldNotSaveControlOrderDialog` on failure, so the user is informed rather than seeing silent data loss.

**5. `Dialogs.cs` — new `CouldNotSaveControlOrderDialog`**

`Warning`-icon dialog explaining the likely OneDrive/network cause and suggesting a retry.

### Remaining Work

Approximately **33 additional call sites** across three files still discard the `SqlOperationResult` return value from `Database.Update`, `Database.Insert`, and `Database.Delete`. A `CANTOPEN` (or any other write failure) at any of these sites silently loses data with no user feedback.

**Priority order:**

| File | Sites | Severity | Notes |
|------|-------|----------|-------|
| `FileDatabaseUpdate.cs` | 8 | **Highest** | Runs during normal tagging — silent failure loses annotation data the user just entered |
| `FileDatabase.cs` | 13 | High | Covers file insertions, deletions, detection data, bounding box updates |
| `CommonDatabase.cs` | 12 | High | Template control and metadata inserts/updates/deletes |
| `SQLiteWrapper.cs` | 3 | Low | `IndexDropIfExists`, `IndexCreateIfNotExists` — index failures are survivable |

---

### Strategy Analysis: Show Dialog + Shutdown/Restart (2026-06-12)

#### The approach

Rather than tracing every write failure back to the UI and managing undo, show a dialog on any write failure explaining that the last action could not be saved, then shut down. On restart, Timelapse reloads entirely from the database, which is always in a valid (if slightly older) state — effectively undoing the unsaved in-memory changes automatically.

#### Code-level analysis of representative write sites

**`UpdateFile` (FileDatabaseUpdate.cs:29-43) — single field edit**

```
image.SetValueFromDatabaseString(dataLabel, value);  // in-memory mutated FIRST
Database.Update(DBTables.FileData, columnToUpdate);  // result discarded
```

If the write fails, the DataTable has the new value, the DB has the old. On restart the DB is reloaded and the old value is restored. Loss = one field edit. **Restart works cleanly.**

**`UpdateAdjustedFileTimes` (FileDatabaseUpdate.cs:345-413) — bulk time adjustment**

All in-memory rows are mutated before the single batch DB write. `CreateBackupIfNeeded()` runs just before the write, so the backup and the live DB are both in the pre-adjustment state — consistent with each other. On restart, the original times reload. Loss = the bulk adjustment, which can be redone. **No corruption.**

**`DeleteFilesAndMarkers` (FileDatabase.cs:1297-1317)**

The in-memory `FileTable` is not modified inside this method — the caller is responsible for calling `FilesSelectAndShow` afterward to reload it. If the DB delete fails, the rows remain in the DB and reload correctly on the subsequent `FilesSelectAndShow`. **Natural recovery even without restart.**

Nuance: physical file deletion happens before the DB delete (DeleteImages.xaml.cs:370-377), so if the DB write fails, rows remain in the DB pointing to now-missing files. This is a pre-existing ordering issue and not made worse by the restart approach; Timelapse already handles missing-file rows gracefully.

**`SyncControlToDatabase` (CommonDatabase.cs:784-796) — template control edit**

```
Database.Update(DBTables.Template, ctw);              // result discarded
LoadControlsFromTemplateDBSortedByControlOrder();     // immediately reloads from DB
```

This is **self-healing**: the immediate reload after the write reverts in-memory state to the DB automatically if the write failed. No restart needed, but the user sees their change silently disappear without any feedback — confusing.

**`InsertFiles` during initial scan (FileDatabase.cs:1617)**

If an insert fails partway through scanning a new folder, some images land in memory but not in the DB. On restart those images do not appear and the user must rescan. This is the worst case, but it occurs only during the one-time load operation, not during normal annotation work.

#### Is the approach sound?

**Yes.** The key invariant is: in-memory state is always derived from the database at load time, so discarding in-memory state and reloading from the DB always produces a fully consistent state. Restart converts an in-memory-vs-DB divergence back to a clean baseline. Given that failures are rare (transient OneDrive/network locks), the maximum data loss is bounded to whatever happened since the last successful write — typically one action.

This is a standard pattern for desktop applications facing unrecoverable I/O failures. The existing `ExceptionShutdownDialog` even says "When you restart, Timelapse usually picks up where you left off" — exactly describing the intended behavior.

#### Alternative approaches considered

**1. Extend the retry loop (low effort, high value — recommended as a complement)**

The current retry (5 × 200 ms) covers only `CANTOPEN` at `connection.Open()`. Extending it to also retry the statement execution for `BUSY` and `LOCKED` codes would handle the majority of transient network failures transparently, before ever reaching the dialog. Most OneDrive locks resolve within 1–2 seconds.

**2. Central failure hook in `SQLiteWrapper` (eliminates the 33-site problem)**

Rather than auditing 33 call sites individually, add failure handling directly in the three write methods (`Update`, `Insert`, `Delete`). When the returned `SqlOperationResult` is a failure, call `SqlErrorState.TryRecord(result, context)` (already exists) and schedule `GenerateExceptionDialog` on the dispatcher. This is 3 method changes instead of 33, and every future write site is automatically protected. The `SqlErrorState` + `GenerateExceptionDialog` infrastructure exists precisely for this use case; `TimelapseMenuFile.cs:126-129` already has the checking code (commented out). The dialog already has a "Continue anyway" option for when the user judges the failure non-critical.

**3. Persistent connection (medium effort, addresses CANTOPEN root cause)**

Opening and closing a connection per write means every write risks `CANTOPEN`. A single persistent connection opened once at database-open time would eliminate `CANTOPEN` entirely during a session. Risk: persistent connections on network shares behave differently (stale handles after network disconnect, different locking semantics). Worth evaluating for a future version.

**4. Exception-based propagation (low call-site effort, fragile in async contexts)**

Throwing `SqlOperationException` on write failure lets the WPF `DispatcherUnhandledException` handler catch it automatically — zero changes at each call site. However, as documented in `SqlOperationResult.cs:356-370`, this is unreliable for `Task.Run` paths that are not awaited all the way up, and several write paths in `FileDatabaseUpdate.cs` use fire-and-forget tasks. Not recommended as the primary strategy.

**5. Local write-ahead journal (high effort, zero data loss)**

Append every intended write to a `.journal` file on local disk before attempting the DB write. On failure, retain the journal. On restart, replay unconfirmed entries. Guarantees no data loss even across crashes. Significantly complex to implement correctly; overkill given the rarity of failures.

#### Recommended plan

1. **Extend the retry loop** in `ExecuteNonQueryWithRollbackCore` to cover `BUSY` and `LOCKED` in addition to `CANTOPEN`, and to retry the statement execution as well as the `connection.Open()`. Most failures are resolved by retry alone.

2. **Central failure hook**: in `SQLiteWrapper.Update`, `Insert`, and `Delete`, when the result is a failure, call `SqlErrorState.TryRecord` and schedule `GenerateExceptionDialog` via the dispatcher. This covers all 33 remaining sites and all future sites with 3 targeted changes.

3. **Update the dialog message** for write failures specifically: clarify that (a) the last unsaved action will be lost but the database is intact, (b) restarting restores a consistent state, and (c) continuing is inadvisable since subsequent writes may also fail.

4. **Add an auto-restart button** (`Process.Start(Application.ResourceAssembly.Location)` + `Application.Current.Shutdown()`) so recovery is one click.

5. **Do not implement undo**: restart achieves equivalent recovery at a fraction of the cost.

---

### How to Test

1. Open a `.tdb` or `.ddb` file on a OneDrive or network share.
2. In the Template Editor, drag a control to reorder it while OneDrive is actively syncing — confirm the dialog appears instead of a crash.
3. Repeat with the spreadsheet column reorder.
4. Simulate a `CANTOPEN` failure for the remaining `FileDatabaseUpdate.cs` sites (e.g. briefly make the file read-only while tagging) — confirm the error dialog appears and that restarting returns to a consistent pre-failure state.
5. Confirm the retry logic handles a brief lock (< 1 second) transparently without showing any dialog.

*Analysis updated June 2026.*

---

## DB-2 · SQLite Read Failures Silently Swallowed

### Issue

When the database drive is disconnected or otherwise unavailable, SQLite read operations fail but produce only a debug assertion in the Output window — no user-facing dialog is shown and Timelapse continues running in a degraded state. The failure path observed during testing (drive ejected before a timezone date correction):

```
---- DEBUG ASSERTION FAILED ----
SQL read failure in GetScalarFromSelect: unable to open database file
Query:  SELECT COUNT  ( * )  FROM DataTable

   at SQLiteWrapper.GetScalarFromSelect(...)                         line 319
   at SQLiteWrapper.ScalarGetScalarFromSelectAsInt(...)              line 729
   at FileDatabase.DoGetCountFromSelect(...)                         line 197
   at FileDatabase.CountAllFilesMatchingSelectionCondition(...)      line 187
   at Dialogs.CheckIfPromptNeeded(...)                               line 545
   at Dialogs.MaybePromptToApplyOperationOnSelectionDialog(...)      line 484
   at TimelapseWindow.MenuItemDaylightSavingsTimeCorrection_ClickAsync()
```

`GetScalarFromSelect` returns `null` on failure; `ScalarGetScalarFromSelectAsInt` returns `0`; callers receive a plausible-looking value and continue executing with corrupted state (wrong counts, incorrect file navigation, stale selections) and no indication anything went wrong.

### Risk of Fixing

**Low–Medium.** Read failures cannot corrupt the database itself, so the risk of making things worse is low. The main care required is:

- `GetScalarFromSelect` and the scalar helpers are called from many sites — auditing all callers is necessary before changing the return contract.
- Showing a shutdown dialog on a read failure is appropriate for most cases (if the drive is gone, writes will also fail), but some read sites may warrant a softer response (retry, or a warning without shutdown).
- The dispatcher-marshalling fix already in place for `TimelapseNeedsToShutDownDataWriteErrorDialog` means any background-thread read failure will also need the same guard.

### Consequence of Not Fixing

After a drive disconnection, Timelapse silently enters a broken state: selection counts may be wrong, operations that check "how many files are selected" receive `0` and may take incorrect branches (e.g. skipping a confirmation dialog that should appear). The user has no indication that a read failed and may trust results that are incorrect. In the timezone-correction case above, the operation continued and then hit a write failure; but if the write check had not been present, data loss would have occurred based on a false read result.

### Potential Improvement

- `SQLiteWrapper.GetScalarFromSelect` (and related scalar helpers) should return a discriminated result type — either the value, or an indication of failure — rather than silently returning `null`/`0`.
- On failure, call `TimelapseNeedsToShutDownDataWriteErrorDialog` (with `isDDBfile` set appropriately) since a drive that cannot be read will also refuse writes, making continued operation unsafe.
- The same dispatcher-marshalling pattern (`owner.Dispatcher.CheckAccess()` / `Dispatcher.Invoke`) already in the write-failure dialog covers background-thread read failure sites without further changes.

### Plan

1. Audit all callers of `GetScalarFromSelect`, `ScalarGetScalarFromSelectAsInt`, `ScalarGetScalarFromSelectAsLong`, and any other scalar-read helpers — identify every site that currently uses the return value as if it were guaranteed valid.
2. Introduce a `SqlReadResult<T>` wrapper (or reuse a pattern consistent with `SqlOperationResult`) to propagate failure cleanly.
3. At each call site, check for failure and call `TimelapseNeedsToShutDownDataWriteErrorDialog` (read failure → drive unavailable → shutdown is the safe response).
4. Consider whether any read sites warrant retry logic (same `CANTOPEN` 5-attempt loop used for writes) before escalating to shutdown.

**Do not begin until DB-1 (all stages) is complete.**

### How to Test

1. Open a `.ddb` file on a removable drive.
2. Start a timezone date correction (or any operation that triggers a file-count query before its main write).
3. Eject the drive before the count query fires.
4. Confirm `TimelapseNeedsToShutDownDataWriteErrorDialog` appears (not a silent assert in the Output window).
5. Confirm "Restart Timelapse" relaunches with the correct file path argument.

*Added June 2026. Trigger: drive ejected before timezone date correction; `GetScalarFromSelect` returned 0 silently.*
