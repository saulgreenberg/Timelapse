# Timelapse – Potential Improvements

Analysis of the Timelapse 2.5.0.7 codebase for interactive performance bottlenecks and coding correctness issues.
Each item follows a standard structure: **Issue**, **Risk of Fixing**, **Potential Improvement**, **Plan**, **How to Test**.

---

## Repair Workflow

When you type **`Next issue`**, the assistant will:

1. State which repair number this is and its ID (e.g. "Repair 1 of 12 — C-3").
2. Summarise the issue, risk, and proposed solution.
3. Ask whether to proceed.
4. On confirmation, make the code change.
5. Produce a short **Git commit message** (ready to paste) and a **test procedure**.

Repairs are ordered lowest-risk / highest-payoff first. Skip or defer any item by saying so — the
queue simply advances to the next.

| # | ID | Short description | Risk | Status |
|---|----|-------------------|------|--------|
| 1 | C-3 | Wrap `MemoryStream` in `using` after video load | Low | ✅ Done |
| 2 | P-4 | Add missing index on `Classifications.DetectionID` | N/A | ❌ Removed — `Classifications` is a legacy migration table; modern databases do not have it |
| 3 | C-5 | Unsubscribe `DataTableColumns_Changed` after `Load` | Low | ✅ Done |
| 4 | C-1 | Move `unalteredBitmapsByID.TryRemove` inside lock in `TryInvalidate` | Low | ✅ Done |
| 5 | P-5 | Cache EXIF orientation per file path | N/A | 🚫 Won't fix — `MetadataExtractorGetOrientation` is only called when the bitmap is not in the cache; the bitmap cache already acts as an implicit orientation cache. Re-reads only occur on full bitmap reloads where the EXIF cost is negligible. An explicit orientation cache would waste memory (up to ~350 MB at 1M images) for near-zero benefit. |
| 6 | P-1 | Cache FFMpeg tool-path discovery across video loads | N/A | 🚫 Won't fix — two attempts both broke video thumbnails with no identifiable root cause despite correct path resolution; overhead is ~5–20 ms against ~200–500 ms FFMpeg extraction (<5% of total), imperceptible in practice |
| 7 | C-2 | Cancel in-flight prefetch tasks on `forceUpdate` | N/A | 🚫 Won't fix — rare edge case (only on missing-file restore), self-healing via LRU eviction, never user-visible; fix complexity outweighs benefit |
| 8 | P-2 | Make Custom Selection COUNT query async | Medium | ✅ Done |
| 9 | CA-4 | Move `BindDataGrid` out of `Task.Run` (dormant threading bug) | Low | ✅ Done |
| 10 | C-4 | Thread `CancellationToken` into video frame extraction | N/A | 🚫 Won't fix — FFMpeg is a blocking external process call that cannot be interrupted mid-execution; token would only skip not-yet-started tasks (narrow window) and post-completion cache bookkeeping; medium-risk multi-file change for marginal gain |
| 11 | P-6 | Cache detection/classification COUNT query results | N/A | 🚫 Won't fix — the only slow caller (Custom Selection timer) was fixed by P-2; all remaining callers use FileSelectionEnum.All which is a trivial SELECT COUNT(*) with sub-millisecond cost; caching complexity outweighs any remaining benefit |
| 12 | P-3 | Async-ify `prefetch.Wait()` (architectural change) | High | ⬜ Pending |

**Current position:** All repairs complete.

---

## Part 1 – Interactive Performance

---

### P-1 · FFMpeg Path Discovery Repeated on Every Video Load

**File:** `src/Timelapse/Images/BitmapUtilities.cs` — `GetBitmapFromVideoFile()`, lines 124–194

**Issue:**
Every time a video frame is extracted, a new `FFMpegConverter` is created and the code re-discovers the
correct path to `ffmpeg.exe` from scratch. This includes:
1. Calling `GetEntryAssembly().Location` to get the install directory.
2. Creating and immediately deleting a temporary test file to check whether the directory is writable.
3. Potentially copying `ffmpeg.exe` to the temp directory.

All three of these happen on every single video display, even when nothing has changed. The test-file
write + delete alone is a synchronous I/O round-trip that adds ~5–20 ms of latency before any frame
extraction begins.

**Risk of Fixing:** Low. The fix is purely additive—cache the resolved `ffmpeg.exe` path in a `static`
field the first time it is determined, and skip all discovery logic on subsequent calls. The cached path
can be validated cheaply with `File.Exists`.

**Potential Improvement:**
Eliminates ~5–20 ms of avoidable synchronous I/O per video load. Also reduces unnecessary temporary-file
churn on the filesystem.

**Plan:**
```csharp
// In BitmapUtilities.cs, add a static cached path:
private static string cachedFfmpegToolPath;

// Replace the discovery block in GetBitmapFromVideoFile:
if (cachedFfmpegToolPath == null)
{
    cachedFfmpegToolPath = ResolveFfmpegToolPath(); // extract existing logic into helper
}
ffMpeg.FFMpegToolPath = cachedFfmpegToolPath;
```
Extract the existing path-resolution block (lines 134–193) into a private static helper
`ResolveFfmpegToolPath()`. The first call runs the full discovery; subsequent calls return the cached
string immediately.

**How to Test:**
1. Open a template with video files.
2. Use a stopwatch or `Diagnostics.Stopwatch` to time the first and second video displays.
3. Confirm the second display is measurably faster (the file-write overhead disappears).
4. Verify behavior is correct in both writable (AppData install) and read-only (Program Files install)
   deployment scenarios.

---

### P-2 · `CountAllFilesMatchingSelectionCondition` Runs Synchronously on the UI Thread

**File:** `src/Timelapse/Dialog/CustomSelection.xaml.cs` — `CountTimer_Tick()`, line 1772;
`src/Timelapse/Database/FileDatabaseCountOrSelectFiles.cs` — `CountAllFilesMatchingSelectionCondition()`, line 31

**Issue:**
Every time the user changes anything in the Custom Selection dialog, a debounce timer fires and calls
`CountAllFilesMatchingSelectionCondition()` synchronously from the UI (timer-tick) thread. This method
ultimately calls `ScalarGetScalarFromSelectAsInt()`, which opens a SQLite connection and executes a
query synchronously on the UI thread.

On a large database the COUNT query can involve multi-table JOINs across Detections and Classifications
(the code itself comments "PERFORMANCE can be a slow query on very large databases"). A slow query
here freezes the Custom Selection dialog entirely while SQLite is working.

**Risk of Fixing:** Low–Medium. The synchronous `GetScalarFromSelect` path already has an async
analogue (`GetDataTableFromSelectAsync`). The main risk is ensuring the UI reflects the result of the
most-recent query and not a stale in-flight one (cancellation of superseded queries).

**Potential Improvement:**
The Custom Selection dialog stays responsive while counts are computed. On very large databases this
can turn a 500 ms+ freeze per keystroke into a non-blocking update.

**Plan:**
1. Add `ScalarGetScalarFromSelectAsIntAsync(string query, CancellationToken token)` to `SQLiteWrapper`
   wrapping `Task.Run(() => ScalarGetScalarFromSelectAsInt(query), token)`.
2. Add `CountAllFilesMatchingSelectionConditionAsync(FileSelectionEnum, CancellationToken)` that
   awaits the above.
3. In `CustomSelection.xaml.cs`, keep a `CancellationTokenSource` field; cancel and replace it each
   time `CountTimer_Tick` fires, then `await` the async count on the UI thread with
   `.ConfigureAwait(true)`.

**How to Test:**
1. Load a large database with recognition data (thousands of files and detections).
2. Open Custom Selection, enable detection filtering, and rapidly change the confidence slider.
3. Confirm the dialog remains interactive (slider moves smoothly) while "files match" count updates
   asynchronously.
4. Confirm the displayed count is always correct after the last change settles.

---

### P-3 · `prefetch.Wait()` Can Block the UI Thread During Image Navigation

**File:** `src/Timelapse/Images/ImageCache.cs` — `TryGetBitmap()`, line 377

**Issue:**
When navigating to an image whose background prefetch task is still running, `TryGetBitmap` calls
`prefetch.Wait()` which synchronously blocks the calling thread until the load completes. If `TryGetBitmap`
is called from the UI thread (e.g., via a navigation keystroke handler), this causes a visible freeze
proportional to how much prefetch time remains. The comment at lines 378–381 correctly documents the
follow-on race, but does not address the blocking itself.

**Risk of Fixing:** High. Async-ifying this code path requires the entire image display pipeline—from
the keypress or slider event all the way down to where `GetCurrentImage` is consumed and assigned to
the WPF `Image` element—to be converted to `async/await`. This is a significant, multi-file
architectural change with real regression surface. This should only be undertaken after thorough
end-to-end testing.

**Potential Improvement:**
Image navigation becomes non-blocking. The UI stays responsive even when the next image hasn't fully
loaded from disk yet (a progress indicator or placeholder could be shown instead of a freeze).

**Plan (high-level):**
1. Convert `TryGetBitmap()` to `TryGetBitmapAsync()` returning `Task<(bool, BitmapSource)>`.
2. The prefetch wait becomes `await prefetch.ConfigureAwait(false)`, then `TryGetValue` as today.
3. Propagate `async` upward through `TryGetCurrentImage`, `MoveNext`, `MovePrevious`, etc.
4. All callers in `TimelapseWindow` and UI event handlers (navigation keys, slider) must `await` the
   image load and update the `Image` control on the UI thread afterwards.

**How to Test:**
1. Open a folder with large JPEG files (>5 MB each) on a slow HDD.
2. Navigate forward rapidly and confirm no UI freeze between images.
3. Profile with PerfView or VS Diagnostic Tools: the UI thread should show no blocking waits on I/O.

---

### P-4 · Missing Index on `Classifications.DetectionID`

**File:** `src/Timelapse/Recognition/RecognitionDatabases.cs` — line 114;
`src/Timelapse/Database/FileDatabaseIndices.cs`

**Issue:**
The `Classifications` table is created with a `DetectionID` integer column (line 114) that acts as a
foreign key to `Detections.detectionID`. However, no index is created on this column. Classification
queries use the join:

```sql
INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID
```

Without an index, this join requires a full-table scan of `Classifications` for each detection row,
making the query O(N × M) where N is the number of matching detections and M is total classifications.
On large datasets this can make classification-mode custom selections extremely slow. (The existing
`IndexDetectionsClassificationConfidence` index covers `Detections` columns but not the
`Classifications.DetectionID` foreign-key column.)

**Risk of Fixing:** Low. Adding an index is non-destructive. `CREATE INDEX IF NOT EXISTS` is safe to
call at any time and SQLite creates the index without locking reads.

**Potential Improvement:**
Classification-mode custom selections and counts can be 10×–100× faster on large datasets, changing
from seconds to milliseconds.

**Plan:**
In `FileDatabaseIndices.cs`, add to the `IndexCreateForDetectionsIfNeeded` tuple list:
```csharp
new(DatabaseValues.IndexClassificationDetectionID,
    DBTables.Classifications,
    ClassificationColumns.DetectionID),
```
Add the constant `IndexClassificationDetectionID = "IndexClassificationDetectionID"` to
`DatabaseValues`. The index is created automatically on the next database open.

**How to Test:**
1. Load a database with many classifications (>10,000 rows in the Classifications table).
2. Open Custom Selection → Classification mode with a confidence filter.
3. Measure query time with and without the index using SQLite's `EXPLAIN QUERY PLAN` (via the
   debug print at line 196 of FileDatabaseCountOrSelectFiles.cs).
4. Confirm "files match" count updates noticeably faster.

---

### P-5 · EXIF Orientation Read Not Cached (Re-read on Every Sized Image Load)

**File:** `src/Timelapse/Images/BitmapUtilities.cs` — `GetBitmapFromImageFile()`, line 61

**Issue:**
When loading an image at a specific size (`desiredWidthOrHeight != null`), the code calls
`MetadataExtractorGetOrientation(filePath, ...)` to determine the EXIF rotation tag before creating
the `BitmapImage`. This opens the file, reads and parses EXIF metadata, and returns. The result is
never cached, so navigating to the same image a second time (or when it is reloaded after a cache
eviction) re-reads the EXIF metadata from disk. This adds 5–30 ms per load depending on disk speed
and JPEG size.

**Risk of Fixing:** Low. A `ConcurrentDictionary<string, Rotation>` keyed on the full file path
provides a simple, thread-safe cache. The orientation of a JPEG file is immutable, so stale entries
are not a concern in normal usage.

**Potential Improvement:**
Eliminates redundant EXIF disk I/O for every image navigation. On mechanical drives the improvement
is most noticeable (~10–30 ms saved per image that has been displayed before).

**Plan:**
```csharp
// In BitmapUtilities.cs, add:
private static readonly ConcurrentDictionary<string, Rotation> orientationCache = new();

// In GetBitmapFromImageFile, replace the direct call:
if (!orientationCache.TryGetValue(filePath, out Rotation rotation))
{
    MetadataExtractorGetOrientation(filePath, out _, out rotation, out _);
    orientationCache[filePath] = rotation;
}
```

**How to Test:**
1. Navigate to an image, navigate away, navigate back.
2. Use `Stopwatch` timing around `BitmapUtilities.GetBitmapFromImageFile` to confirm the second call
   is faster.
3. Verify images with non-standard EXIF rotations (90°, 180°, 270°) still display correctly.

---

### P-6 · Complex Detection/Classification COUNT Queries Have No Result Cache

**File:** `src/Timelapse/Database/FileDatabaseCountOrSelectFiles.cs`, lines 31–188;
`src/Timelapse/Dialog/CustomSelection.xaml.cs`, line 1772

**Issue:**
`CountAllFilesMatchingSelectionCondition` is called from multiple places (Custom Selection dialog,
menu items, file loading). For the detection/classification cases it constructs and executes complex
multi-table queries each time. There is no result cache: two successive calls with identical selection
parameters re-run the full query twice. In the Custom Selection dialog the count is re-queried every
time the debounce timer fires, even when the underlying selection criteria have not changed (e.g.,
when the dialog merely re-renders).

**Risk of Fixing:** Medium. Invalidating the cache correctly requires hooking into every code path
that modifies the database or changes the selection parameters. A stale count displayed in the dialog
would be confusing to users.

**Potential Improvement:**
Eliminates duplicate queries in the Custom Selection dialog. For large databases this can cut the
number of slow detection-COUNT queries by 50–80% during typical dialog interactions.

**Plan:**
1. In `FileDatabase`, add a `(string query → int count)` cache along with a `bool
   _countCacheInvalid` flag.
2. Invalidate the flag in every method that writes to the database (inserts, updates, deletes,
   recognition import).
3. In `CountAllFilesMatchingSelectionCondition`, check the flag; if invalid, execute and cache the
   result; if valid, return the cached value.
4. A simpler alternative: in `CountTimer_Tick`, hash the current selection parameters and skip the
   database round-trip if the hash matches the last computed hash.

**How to Test:**
1. Open Custom Selection on a large database with recognition data.
2. Move the confidence slider to a fixed position, wait for the count.
3. Click elsewhere in the dialog without changing the selection criteria; confirm the query is NOT
   re-executed (add debug logging to `DoGetCountFromSelect`).
4. Change the criteria; confirm the cache is invalidated and the new count is correct.

---

## Part 2 – Coding Issues

---

### C-1 · `TryInvalidate` Removes from `unalteredBitmapsByID` Outside the Lock

**File:** `src/Timelapse/Images/ImageCache.cs` — `TryInvalidate()`, lines 255–271;
`CacheBitmap()`, lines 310–327

**Issue:**
`CacheBitmap` takes `lock(mostRecentlyUsedIDs)` and, while holding it, evicts from *and* adds to
`unalteredBitmapsByID`. This ensures the two collections stay consistent.

`TryInvalidate`, however, calls `unalteredBitmapsByID.TryRemove()` at line 267 *before* acquiring
the lock, then acquires the lock at line 268 to remove from `mostRecentlyUsedIDs`.

Concurrent race (rare but possible):
1. Thread A (`TryInvalidate`) calls `TryRemove(id)` on `unalteredBitmapsByID` — succeeds.
2. Thread B (`CacheBitmap`, completing a prefetch) acquires the lock, checks
   `!ContainsKey(id)` (true after A's removal), skips eviction, then calls
   `AddOrUpdate(id, bitmap)` and `SetMostRecent(id)` — re-adds the (stale) bitmap.
3. Thread A acquires the lock and calls `mostRecentlyUsedIDs.TryRemove(id)` — removes the
   freshly-added MRU entry, leaving the bitmap in `unalteredBitmapsByID` with no MRU tracking.

The orphaned bitmap is never evicted by the LRU algorithm and occupies cache space permanently until
a full reset.

**Risk of Fixing:** Low. Moving `unalteredBitmapsByID.TryRemove` inside the existing lock block in
`TryInvalidate` makes the two-collection update atomic and matches the discipline used by
`CacheBitmap`. The lock is already `lock(mostRecentlyUsedIDs)` and is not held for long durations,
so there is no deadlock risk.

**Potential Improvement:**
Eliminates a rare but real cache-consistency bug that causes permanent memory leaks in the LRU
bitmap cache.

**Plan:**
```csharp
public bool TryInvalidate(long id)
{
    if (Current?.ID == id)
    {
        Reset();
    }
    lock (mostRecentlyUsedIDs)
    {
        unalteredBitmapsByID.TryRemove(id, out _);   // moved inside lock
        return mostRecentlyUsedIDs.TryRemove(id);
    }
}
```
Remove the `ContainsKey` guard — it was only needed to decide whether to early-return, and that
decision can now safely be based on `TryRemove`'s return value.

**How to Test:**
1. Write a unit test that calls `CacheBitmap` and `TryInvalidate` concurrently from multiple threads
   in a tight loop.
2. After N iterations, assert that `unalteredBitmapsByID.Count == mostRecentlyUsedIDs.Count`.
   Before the fix, this assertion will occasionally fail.

---

### C-2 · `forceUpdate` Clears Caches Without Cancelling In-Flight Prefetch Tasks

**File:** `src/Timelapse/Images/ImageCache.cs` — `TryGetBitmap()`, lines 355–365

**Issue:**
When `forceUpdate` is true (triggered, for example, after a missing image is restored), the code
clears both `prefetechesByID` and `unalteredBitmapsByID` at lines 361–362. This is correct in
intent — it forces the current image to reload fresh from disk.

However, clearing `prefetechesByID` only removes the dictionary entries; it does not cancel the
background `Task.Run` lambdas that were already dispatched to the thread pool. Those tasks continue
running, and when they complete they call `CacheBitmap()`, which *re-populates* `unalteredBitmapsByID`
with the bitmap that was loaded before the force-refresh. If a stale prefetch completes after
`forceUpdate` but before the updated bitmap is displayed, the cache holds the old (possibly
placeholder-for-missing) image under that ID.

The severity is low in most cases because the `forceUpdate` bitmap itself is cached immediately
after (line 363), so a subsequent navigation will use the correct bitmap. But during the window
between cache-clear and cache-repopulation, a concurrent prefetch can insert an inconsistent entry.

**Risk of Fixing:** Medium. The cleanest fix requires CancellationToken support threaded through
`TryInitiateBitmapPrefetch` and the lambda inside it. This is a moderate refactor of the prefetch
path.

**Potential Improvement:**
Removes a small but real window where a stale (possibly "file missing") bitmap could appear
immediately after a forced image refresh.

**Plan:**
1. Add a `CancellationTokenSource prefetchCts` field to `ImageCache`.
2. In `TryInitiateBitmapPrefetch`, pass `prefetchCts.Token` to `Task.Run`.
3. In the `forceUpdate` branch, cancel `prefetchCts`, recreate it, clear the dictionaries, then
   cache the freshly-loaded bitmap.

```csharp
// forceUpdate branch becomes:
prefetchCts.Cancel();
prefetchCts = new CancellationTokenSource();
prefetechesByID.Clear();
unalteredBitmapsByID.Clear();
bitmap = fileRow.LoadBitmap(Database.RootPathToImages, out _);
CacheBitmap(fileRow.ID, bitmap);
```

**How to Test:**
1. In debug mode, add a `Thread.Sleep(200)` at the start of the prefetch lambda.
2. Navigate forward (to trigger a prefetch), then immediately trigger a `forceUpdate`.
3. Confirm the cached bitmap after `forceUpdate` is the refreshed one, not the prefetched one.

---

### C-3 · `MemoryStream` Not Disposed After Video Bitmap Load

**File:** `src/Timelapse/Images/BitmapUtilities.cs` — `GetBitmapFromVideoFile()`, line 124

**Issue:**
`outputBitmapAsStream` is assigned `new MemoryStream()` at line 124 and is used as the source stream
for `BitmapImage`. After `bitmap.EndInit()` and `bitmap.Freeze()`, the `BitmapImage` holds all pixel
data internally and no longer needs the stream. However, `outputBitmapAsStream` is never disposed.

For a typical video thumbnail at 1280×720, the JPEG data in the MemoryStream is typically 100–500 KB.
Under rapid video navigation, many such streams accumulate on the managed heap until the GC collects
them. While not a classic unmanaged resource leak, this adds GC pressure and can cause
intermittent pauses.

**Risk of Fixing:** Low. Wrapping `outputBitmapAsStream` in a `using` statement is a one-line change
with no behavioural effect (the `BitmapImage` has already consumed the stream by the time `Freeze()`
returns).

**Potential Improvement:**
Reduces managed heap pressure during rapid video browsing; reduces likelihood of GC pauses during
navigation.

**Plan:**
```csharp
// Replace:
Stream outputBitmapAsStream = new MemoryStream();

// With:
using MemoryStream outputBitmapAsStream = new();
```
The stream is declared at the top of the outer `try` block; the `using` statement will dispose it at
the end of that block, well after `bitmap.Freeze()`.

**How to Test:**
1. In Visual Studio's Diagnostic Tools, record memory snapshots before and after navigating through
   ~50 video files rapidly.
2. Confirm the heap snapshot after the fix shows fewer `MemoryStream` instances than before.

---

### C-4 · No Cancellation for Video Frame Extraction When Navigating Away

**File:** `src/Timelapse/Images/BitmapUtilities.cs` — `GetBitmapFromVideoFile()`, lines 96–248;
`GetVideoBitmapFromFileUsingMediaEncoder()`, lines 253–285

**Issue:**
Both the FFMpeg and the MediaEncoder video frame extraction paths run to completion with no way to
abort them. If the user navigates away from a video (e.g., presses the arrow key repeatedly while on
a slow video), the extraction for the skipped frame continues in the background until it finishes,
consuming CPU and I/O unnecessarily.

The MediaEncoder path is particularly expensive (up to ~175 ms per frame as noted in the code), and
its polling loop (`while (NaturalVideoWidth < 1)`) with `Thread.Sleep` calls cannot be interrupted at
all.

**Risk of Fixing:** Medium. Cancellation must be threaded from the image-navigation call site through
`ImageCache`, `ImageRow.LoadBitmap`, and finally into `BitmapUtilities`. The MediaEncoder path also
runs on a dedicated STA thread (`staThread.Join()` at line 281), so cancellation requires either a
cooperative check inside the polling loop or `Thread.Interrupt`.

**Potential Improvement:**
Rapid forward/backward navigation through videos no longer queues up multiple background extraction
tasks. This reduces CPU spikes and the time before the user sees the image they actually stopped on.

**Plan (high-level):**
1. Add a `CancellationToken` parameter to `GetBitmapFromVideoFile` and `GetBitmapFromVideoFileUsingMediaEncoder`.
2. In the FFMpeg path, pass the token to `Task.Run` (if it is ever moved off the calling thread) and
   check `token.ThrowIfCancellationRequested()` after the blocking `GetVideoThumbnail` call.
3. In the MediaEncoder polling loop, add `if (token.IsCancellationRequested) break;` inside the
   `while` loop and `PumpDispatcherMessages()` call.
4. Thread the token up from `ImageCache.TryInitiateBitmapPrefetch` → `ImageRow.LoadBitmap` →
   `BitmapUtilities.GetBitmapFromVideoFile`.

**How to Test:**
1. Open a folder containing large video files.
2. Hold the forward-navigation key for 2–3 seconds, then release.
3. Before the fix: observe high CPU usage as multiple frame extractions run to completion.
   After the fix: CPU usage drops quickly when navigation stops.
4. Confirm that the final displayed frame is correct and not a cancelled/blank frame.

---

### C-5 · `DataTableColumns_Changed` Event Subscribed but Never Unsubscribed

**File:** `src/Timelapse/Database/SQLiteWrapper.cs` — `GetDataTableFromSelect()`, line 190;
`GetDataTableFromSelectAsync()`, line 234

**Issue:**
Both `GetDataTableFromSelect` and `GetDataTableFromSelectAsync` subscribe to
`dataTable.Columns.CollectionChanged += DataTableColumns_Changed` but never unsubscribe. Since
`dataTable` is a local variable that is returned to the caller, the subscription holds a reference
to the `SQLiteWrapper` instance via the delegate, preventing the `DataTable` from being collected
while the wrapper lives (which is typically the lifetime of the application).

In practice, `DataTable` objects returned by these methods are used transiently and quickly go out of
scope, so the GC eventually breaks the cycle. However, during the period between creation and
collection, the event subscription keeps the `DataTable`'s column collection alive longer than
needed, and in high-throughput paths (rapid navigation triggers many queries) can cause minor GC
pressure.

**Risk of Fixing:** Low. The handler `DataTableColumns_Changed` exists solely to handle a WPF/SQLite
interoperability quirk during the `dataTable.Load(reader)` call. It is safe to unsubscribe
immediately after `Load` returns. Alternatively, switching to `dataTable.Columns.CollectionChanged -=
DataTableColumns_Changed` after `Load` on a finally path is one-line and zero-risk.

**Potential Improvement:**
Cleaner object lifecycle; eliminates the retention of `DataTable` column collections beyond their
useful life.

**Plan:**
```csharp
dataTable.Columns.CollectionChanged += DataTableColumns_Changed;
try
{
    dataTable.Load(reader);
}
finally
{
    dataTable.Columns.CollectionChanged -= DataTableColumns_Changed;
}
```
Apply the same pattern in both `GetDataTableFromSelect` and `GetDataTableFromSelectAsync`.

**How to Test:**
1. Confirm the app loads and queries databases correctly after the change (all existing tests pass).
2. Use a memory profiler to confirm `DataTable` instances in the heap drop promptly after queries
   complete rather than lingering until a GC.

---

## Summary Table

| # | Area | File | Lines | Impact | Fix Risk |
|---|------|------|-------|--------|----------|
| P-1 | FFMpeg path discovery per video | BitmapUtilities.cs | 124–194 | High | Low |
| P-2 | Sync COUNT query blocks UI thread | CustomSelection.xaml.cs | 1772; FileDatabaseCountOrSelectFiles.cs | High | Low–Med |
| P-3 | `prefetch.Wait()` freezes navigation | ImageCache.cs | 377 | High | High |
| P-4 | Missing index on Classifications.DetectionID | RecognitionDatabases.cs, FileDatabaseIndices.cs | 114, — | High | Low |
| P-5 | EXIF orientation re-read each load | BitmapUtilities.cs | 61 | Medium | Low |
| P-6 | Detection COUNT result not cached | FileDatabaseCountOrSelectFiles.cs | 31–188 | High | Medium |
| C-1 | Cache removal race in `TryInvalidate` | ImageCache.cs | 255–271, 310–327 | Medium | Low |
| C-2 | `forceUpdate` doesn't cancel prefetches | ImageCache.cs | 355–365 | Medium | Medium |
| C-3 | `MemoryStream` not disposed (video load) | BitmapUtilities.cs | 124 | Medium | Low |
| C-4 | No cancellation for video extraction | BitmapUtilities.cs | 96–248, 253–285 | Medium | Medium |
| C-5 | `DataTableColumns_Changed` never unsubscribed | SQLiteWrapper.cs | 190, 234 | Low | Low |

---

## Part 3 – ConfigureAwait Audit

### How ConfigureAwait Works

When you `await` a task in C#, the runtime needs to know **where to resume execution** after the
task finishes. In a WPF application the UI thread owns a `DispatcherSynchronizationContext`. Before
every `await`, .NET captures the "current synchronization context". When the task completes:

- **`ConfigureAwait(true)`** (or plain `await` with no ConfigureAwait) — the continuation resumes
  **on the captured context**, which in WPF means back on the UI thread.
- **`ConfigureAwait(false)`** — the continuation may resume **on any available thread-pool thread**;
  the captured context is discarded.

The setting only affects where the code *after* the `await` runs. Code *before* the `await` is
unaffected.

---

### The Three Rules for WPF Applications

| Situation | Correct setting | Why |
|-----------|----------------|-----|
| Code after the await reads or writes any WPF control, Window, or dialog property | `true` | WPF controls are thread-affine: accessing them off the UI thread throws `InvalidOperationException` |
| Code after the await is pure computation, file I/O, or database work with no UI touches | `false` | Avoids the overhead of marshalling back to the UI thread unnecessarily |
| Code after the await is inside a `Task.Run` lambda | either — `Task.Run` strips the context, so both values behave identically inside the lambda body | See note below |

**The `Task.Run` nuance.** `Task.Run` deliberately removes the synchronization context before
running its lambda. Any `ConfigureAwait(true)` on an `await` *inside* a `Task.Run` lambda has
**no effect** — the dispatcher context is already gone. The continuation will still resume on a
thread-pool thread. Only the `.ConfigureAwait(...)` on the outer `await Task.Run(...)` call itself
(outside the lambda) matters.

---

### The Risks of Getting It Wrong

**Using `true` when `false` would suffice:**
Functionally correct; slightly less efficient because the runtime marshals a continuation back to the
UI thread that didn't need to be there. In practice this is imperceptible for single awaits but adds
up in tight loops. Risk: **None** (no correctness impact).

**Using `false` when `true` is needed:**
Any WPF control access after the await will throw `InvalidOperationException: "The calling thread
cannot access this object because a different thread owns it."` This is a runtime crash and is
immediately visible during testing. Risk: **High** (guaranteed crash on first exercised code path).

**The deadlock scenario (most insidious):**
When `ConfigureAwait(true)` is used anywhere in an async call chain, and a caller on the UI thread
blocks synchronously with `.Wait()` or `.Result`, a deadlock occurs:

1. The UI thread calls `someTask.Wait()` — it is now blocked.
2. The async method eventually reaches an `await x.ConfigureAwait(true)`.
3. `x` completes, but the continuation needs the UI dispatcher — which is **blocked in step 1**.
4. The task can never complete → deadlock.

In the current Timelapse codebase this deadlock **does not occur** because both `.Wait()` calls are
on background threads: `prefetch.Wait()` runs from the UI thread during image navigation (see item
P-3), but the prefetch task itself does not contain any `ConfigureAwait(true)` awaits. The other
`.Wait()` — `loader.LoadAsync(...).Wait()` at `TimelapseImageSetLoading.cs:491` — runs inside a
`BackgroundWorker.DoWork` delegate, which is never the UI thread.

---

### Audit of Every ConfigureAwait Usage in This Codebase

110 total uses across 41 files were found. They fall into four categories.

---

#### Category 1 — `ConfigureAwait(true)`: Correct and Necessary

These usages appear in methods where the code after the `await` directly updates WPF UI elements.
Changing them to `false` would cause an `InvalidOperationException` at runtime.

| File | Lines | What happens after the await |
|------|-------|------------------------------|
| `Images/MarkableCanvasImageAdjustment.cs` | 122, 148, 190 | `ImageToDisplay.Source = bf` — sets the displayed image |
| `Images/ImageProcess.cs` | 97 | Returns a `BitmapFrame`; caller immediately sets it on `Image.Source` |
| `TimelapsePartialClasses/TimelapseFileSelection.cs` | 26, 36, 107, 126 | Moves to a file and refreshes the whole display (dozens of UI updates) |
| `TimelapsePartialClasses/TimelapseImageSetLoading.cs` | 81, 172, 228, 394, 597, 711 | Image set display, progress UI, navigation state updates |
| `TimelapseMenuCallbacks/TimelapseMenuEdit.cs` | 170, 220, 311, 352, 733, 792, 824, 855, 888, 920, 959, 1108, 1130 | Calls `FilesSelectAndShowAsync` then updates status bar / selection UI |
| `TimelapseMenuCallbacks/TimelapseMenuSelection.cs` | 108, 112, 227, 250, 310, 350, 406 | Same pattern |
| `TimelapseMenuCallbacks/TimelapseMenuSort.cs` | 87, 113, 138, 159 | Re-displays sorted image set |
| `TimelapseMenuCallbacks/TimelapseMenuFile.cs` | 163, 270, 525, 559 | Opens template / loads image set into UI |
| `TimelapseMenuCallbacks/TimelapseMenuRecognitions.cs` | 140, 289 | Post-import UI feedback |
| `TimelapseMenuCallbacks/TimelapseMenuCamptrapDP.cs` | 45 | Selection UI update |
| `Dialog/DeleteImages.xaml.cs` | 417, 490 | Updates feedback DataGrid and closes dialog |
| `Dialog/ExportAllSelectedFiles.xaml.cs` | 90, 200 | Shows export result in dialog |
| `Dialog/DateTimeCorrectAmbiguous.xaml.cs` | 240 | Fills feedback grid |
| `Dialog/DateTimeDaylightSavingsCorrection.xaml.cs` | 199 | Fills feedback grid |
| `Dialog/DateTimeFixedCorrection.xaml.cs` | 195 | Fills feedback grid |
| `Dialog/DateTimeLinearCorrection.xaml.cs` | 257 | Fills feedback grid |
| `Dialog/DateTimeRereadFromFiles.xaml.cs` | 273 | Fills feedback grid |
| `Dialog/DarkImagesThreshold.xaml.cs` | 354, 626 | Updates progress/feedback UI |
| `Dialog/PopulateCamtrapDataFields.xaml.cs` | 98, 283 | Dialog feedback UI |
| `Dialog/PopulateFieldWithDetectionCounts.xaml.cs` | 100, 127 | Dialog feedback UI |
| `Dialog/PopulateFieldWithEpisodeData.xaml.cs` | 116, 219 | Dialog feedback UI |
| `Dialog/PopulateFieldWithGUID.xaml.cs` | 181 | Dialog feedback UI |
| `Dialog/PopulateFieldWithRecognitionData.xaml.cs` | 142, 184 | Dialog feedback UI |
| `Dialog/FileMetadataPopulateAll.xaml.cs` | 236 | Dialog feedback UI |
| `Dialog/FileMetadataPopulateDatesOnly.xaml.cs` | 220 | Dialog feedback UI |
| `Dialog/FileMetadataPopulateBase.cs` | 209, 242 | Dialog feedback UI |
| `Dialog/MergeCheckinDatabaseFiles.xaml.cs` | 262, 308 | Dialog feedback UI |
| `Dialog/MergeCheckoutChooseSubfolder.xaml.cs` | 178 | Dialog feedback UI |
| `Dialog/MergeCreateEmptyDatabase.xaml.cs` | 83 | Dialog feedback UI |
| `Standards/CamtrapDPExportFiles.cs` | 435, 569, 948 | Reports progress via UI-bound callback |
| `Database/CsvReaderWriter.cs` | 259, 441, 838 | Called from UI-layer methods that update dialogs |
| `Recognition/RecognitionUtilities.cs` | 365, 400 | Called from menu handlers that update the display |

**Verdict: All of these are correctly set to `true`. Do not change them.**

---

#### Category 2 — `ConfigureAwait(true)`: Harmless but Unnecessary

These are in internal database lifecycle methods (`CreateOrOpenAsync`,
`OnDatabaseCreatedAsync`, etc.) where the code immediately after each `await` is pure database
setup work — no UI access — before eventually passing results back to the caller. The caller is on
the UI thread, so `true` safely preserves that context. Using `false` here would be fractionally
more efficient but makes no observable difference.

| File | Lines | Notes |
|------|-------|-------|
| `Database/FileDatabase.cs` | 159, 184, 189, 234, 284, 366, 689, 726, 817, 2119, 2408 | Factory / upgrade / selection methods; UI only needed in the final `BindToDataGrid()` call further down the chain |
| `Database/CommonDatabase.cs` | 107, 140, 225, 1001 | Factory and template-load methods |
| `Database/FileDatabaseCountOrSelectFiles.cs` | 559 | Post-await code updates `FileTable` binding |
| `Database/FileDatabaseCompareTemplates.cs` | 279, 397 | Returns results to UI caller |
| `Database/MergeDatabases.cs` | 32, 42 | Returns database objects to UI caller |
| `TemplateEditor/MenuCallbacks/MenuFile.cs` | 424, 489 | Template editor UI flow |
| `TemplateEditor/EditorCode/TemplateCode.cs` | 32, 67 | Template editor UI flow |
| `TimelapsePartialClasses/TimelapseHandleArgumentsOnOpen.cs` | 108, 133 | Startup flow; leads to UI |

**Verdict: Correctly set to `true` as a safe conservative choice. Changing to `false` is safe but
not worth the effort unless profiling shows measurable overhead in these paths.**

---

#### Category 3 — `ConfigureAwait(false)`: Correct

| File | Lines | Why `false` is correct |
|------|-------|------------------------|
| `ImageSetLoadingPipeline/ImageSetLoader.cs` | 251, 276 | Called from `BackgroundWorker.DoWork` — there is no `DispatcherSynchronizationContext` on that thread anyway; `false` correctly signals "library-style code, don't assume a context" |

**Verdict: Correct. Do not change.**

---

#### Category 4 — Potential Issue: `Task.Run` Wrapping a Method That Calls `BindDataGrid`

**File:** `Database/CommonDatabase.cs` — `LoadControlsFromTemplateDBSortedByControlOrderAsync()`, line 293

```csharp
await Task.Run(LoadControlsFromTemplateDBSortedByControlOrder).ConfigureAwait(true);
```

The synchronous method `LoadControlsFromTemplateDBSortedByControlOrder()` (line 296–302) calls:
```csharp
dataGrid.DataContext = DataTable;
dataGrid.ItemsSource = DataTable.DefaultView;
```
These are WPF operations. They run **inside `Task.Run`**, i.e., on a thread-pool thread. The
`.ConfigureAwait(true)` on the *outer* `Task.Run` call only controls where code **after** the
`await` resumes — not where the lambda body runs. The lambda body, including the `BindDataGrid`
call, runs on the thread-pool thread regardless of the outer `ConfigureAwait` value.

**In practice this never fires.** Tracing every caller:

- `CommonDatabase.DoCreateOrOpenAsync()` (line 162) creates a *fresh* `CommonDatabase` instance
  with `editorDataGrid = null` before calling the async version.
- `FileDatabase.OnExistingDatabaseOpenedAsync()` (lines 309, 733) is a main-app database class that
  never calls `BindToEditorDataGrid`, so `editorDataGrid` is always `null` there too.
- The only place `editorDataGrid` is ever set is `BindToEditorDataGrid()` (line 937), which calls
  the **synchronous** version directly and never touches the async path.

Because of the `if (dataGrid != null)` guard in `BindDataGrid`, the WPF `DataContext`/`ItemsSource`
assignments are always skipped. The only code that actually runs on the thread-pool thread is the
`DataTable.RowChanged` subscription, which is safe on any thread.

**The bug is architecturally real but permanently dormant.** Current consequence: zero.

**Latent risk:** If a future developer calls `BindToEditorDataGrid` and then calls the async version
(a plausible mistake — nothing in the code signals the ordering constraint), the WPF calls will
execute on a thread-pool thread and crash. The fix removes that trap entirely.

**Fix:**
```csharp
public virtual async Task LoadControlsFromTemplateDBSortedByControlOrderAsync()
{
    // Move only the non-UI database read into Task.Run
    DataTable templateTable = await Task.Run(() =>
        Database.GetDataTableFromSelect(
            Sql.SelectStarFrom + DBTables.Template + Sql.OrderBy + Control.ControlOrder)
    ).ConfigureAwait(true);

    // Populate and bind on the UI thread (after ConfigureAwait(true) returns here)
    Controls = new(templateTable, row => new(row));
    Controls.BindDataGrid(editorDataGrid, onTemplateTableRowChanged);
}
```

---

### Summary: The "Almost Always True" Rule

For a WPF application structured the way Timelapse is — async chains initiated from UI event
handlers or menu callbacks that ultimately update controls — **`ConfigureAwait(true)` as the default
is correct and safe.** The reasoning:

1. The chain always originates on the UI thread (button click, menu item, timer tick).
2. The chain always ends with UI updates (showing images, refreshing dialogs, updating status bars).
3. Any intermediate `await` that uses `true` merely ensures you stay on the UI thread for the
   code immediately following it — which is either another async call or a UI update.

**The only time `ConfigureAwait(false)` is definitively better:**
- In methods that are clearly "library style" with no UI knowledge, called from multiple contexts
  (both UI and background threads). `ImageSetLoader` is the right example.
- In tight loops with many sequential `await` calls where the marshalling overhead is measurable.

**The one scenario that could make "always true" dangerous:**
If any caller ever blocks on an async method using `.Wait()` or `.Result` **from the UI thread**,
a deadlock will result because the `ConfigureAwait(true)` continuation needs the UI thread that is
already blocked. Currently no such call exists in the UI thread. Guard against this in future code:
never use `.Wait()` or `.Result` on an async method from a WPF event handler or any method that
might run on the UI thread.

---

## Part 4 – Next Steps (Further Analysis Areas)

These areas have not yet been audited in detail. Each entry includes a severity rating,
fix-risk rating, and the planned approach. Items are ordered by expected impact.

Severity scale: **Critical** / **High** / **Medium** / **Low**
Fix risk scale: **High** / **Medium** / **Low**

---

### NS-1 · SQLite Transaction Usage

**Severity:** High — **Fix Risk:** Low

**Why it matters:**
SQLite without an explicit transaction wraps every individual `INSERT` or `UPDATE` in its own
implicit transaction, which means a separate `fsync` disk write per statement. Bulk operations
that issue many statements in a loop — image set loading, recognition import, CSV import, date
corrections, metadata population — can be 50–100× slower than the same operation wrapped in a
single `BEGIN … COMMIT`. On a 100K-image dataset a missing transaction can turn a 1-second
operation into minutes.

**Approach:**
Audit all methods that call `ExecuteNonQuery` or equivalent in a loop, or that call high-level
write helpers (`Insert`, `Update`, `Delete`) multiple times in sequence. Verify whether each
is already wrapped in a transaction. Where not, add `ExecuteNonQueryWithRollback` wrappers
or introduce a `BeginTransaction` / `Commit` bracket. The existing `SQLiteWrapper` already
has transaction infrastructure; the fix is applying it consistently.

**Note:** SQL statements are often built by string concatenation, making static analysis
imprecise. Each candidate site must be read in full context to confirm it is a genuine
bulk operation without a transaction.

*Status: ✅ T-1 (CSV import) and T-2 (AddFiles) fixed. T-3 documentation only — low priority.*

#### Transaction Infrastructure

`SQLiteWrapper.cs` has solid transaction infrastructure. The key overload is
`ExecuteNonQueryWithRollback(List<string>)` which batches all statements in the list into
**one** `BEGIN … COMMIT`. All the write helpers (`Update`, `Insert`, `Delete`) ultimately
delegate to this. The question is whether callers supply all their statements at once or
call it in a loop.

#### Findings

| # | File | Method | Lines | Issue | Severity |
|---|------|--------|-------|-------|----------|
| T-1 | `CsvReaderWriter.cs` | `TryImportFromCsv` | ~811–836 | Updates flushed every 10,000 rows — each flush is a separate transaction. A 1M-row import creates 100 separate fsyncs. | **High** |
| T-2 | `FileDatabase.cs` | `AddFiles` | ~859–1095 | INSERTs batched in groups of 5,000 rows, each group in its own transaction. 100K-file import = 20 separate transactions instead of 1. | **Medium** |
| T-3 | `SQLiteWrapper.cs` | `Update` (ID-list variant) | ~571–591 | Already chunks large ID lists into 500-clause groups, all within one transaction. Correct; add a comment documenting the 500-clause limit so future maintainers don't remove it. | **Low (doc only)** |
| T-4 | All other bulk-write paths | `UpdateAdjustedFileTimes`, `RecognitionsPopulateFieldWithData`, `MergeSourceIntoDestinationDdb`, etc. | various | Already correctly use a single transaction for the full operation. | ✅ No issue |

#### Recommended Fixes

**T-1 (High) — CSV import:**
Accumulate all `imagesToUpdate` entries across the entire file, then call `UpdateFiles` once
at the end instead of every 10,000 rows. Memory cost is negligible (column-tuple objects);
the fsync reduction on a 1M-row import goes from 100 disk writes to 1.

**T-2 (Medium) — AddFiles:**
Collect all per-batch INSERT statements into a single `List<string>` and pass the whole list
to `ExecuteNonQueryWithRollback(List<string>)` once, outside the loop. The existing
5,000-row batching logic controls statement size (avoiding SQLite's max-variable limit);
keeping all batches in one transaction is purely a transaction-boundary change.

---

### NS-2 · N+1 Query Pattern and Loop-Based String Concatenation

**Severity:** High — **Fix Risk:** Low–Medium

**Why it matters:**
Two related patterns both degrade to O(n) or O(n²) behaviour on large datasets:

1. **N+1 queries** — executing one SQL query per image row inside a loop. For 100K images
   this multiplies a 10 ms query into a 17-minute loop.
2. **String concatenation in loops** — building a comma-separated ID list with `+=` is O(n²)
   because each concatenation copies the entire accumulated string. Already observed one instance
   in `FileDatabaseCountOrSelectFiles.cs` (`commaSeparatedListOfIDs += image.ID + ","`).

**Approach:**
Search for `+=` on string variables inside loops that build SQL fragments, and for query calls
inside `foreach` / `for` loops. Replace string concatenation with `StringBuilder` or
`string.Join`. Replace per-row queries with a single parameterised batch query or an
`IN (…)` clause.

*Status: ✅ Audit complete. No N+1 queries found. One O(n²) string concatenation fixed in `FileDatabaseCountOrSelectFiles.cs:549` (`SelectMissingFilesFromCurrentlySelectedFiles`) — replaced `+=` accumulation with `List<long>` + `string.Join`.*

---

### NS-3 · WPF List/Grid Virtualization for Large Datasets

**Severity:** High — **Fix Risk:** Low–Medium

**Why it matters:**
If any `DataGrid`, `ListView`, or `ItemsControl` that displays the image set has UI
virtualization disabled — either explicitly or because it is wrapped in a `ScrollViewer`
that disables it — WPF will attempt to create a UI element for every row at load time.
For 1M images this causes extreme memory use and a freeze that can last minutes.

**Approach:**
Audit all list/grid controls in XAML files that bind to the file table or any large
collection. Verify `VirtualizingPanel.IsVirtualizing="True"` and
`VirtualizingPanel.VirtualizationMode="Recycling"` are set (or inherited from the default).
Check that no `ScrollViewer` with `CanContentScroll="False"` wraps these controls, as that
disables pixel-based scrolling and defeats virtualisation.

*Status: ✅ Audited — no action needed. Main DataGrid uses WPF default virtualization (on). Thumbnail grid is a custom renderer, not an ItemsControl. Three dialogs disable virtualization intentionally for small bounded row counts.*

---

### NS-4 · Exception-Swallowing Audit

**Severity:** Medium — **Fix Risk:** Low

**Why it matters:**
Bare `catch {}` and `catch { return null/false; }` blocks silently discard exceptions,
hiding real errors from developers and making production bugs extremely difficult to
diagnose. The codebase already has several (video loading fallback, database path resolution).
A systematic audit identifies which are intentional safety nets and which are masking real
bugs.

**Approach:**
Search for bare `catch` blocks and `catch` blocks with no logging. Categorise each as:
(a) intentional fallback with a documented reason — leave as-is;
(b) silent swallow of a recoverable error — add `TracePrint.PrintMessage`;
(c) silent swallow of an unexpected error — convert to logged rethrow or specific exception type.

*Status: ✅ Audit complete. Three sites fixed: `ExportToCsv` catch now re-throws (partial file cleaned up; caller's existing handler shows exception details to user); `ExportMetadataToCsv` inner try/catch removed (exceptions propagate to caller's catch which resets busy indicator); `FilesFolders` file-discovery catches now log via `TracePrint` (debug-mode diagnostic).*

---

### NS-5 · IDisposable Comprehensive Audit

**Severity:** Medium — **Fix Risk:** Low

**Why it matters:**
Beyond the `MemoryStream` fixed in C-3, other `IDisposable` objects may be created without
`using` statements: `SQLiteConnection`, `SQLiteCommand`, `DataTable`, file streams, and
bitmap-related objects. Undisposed connections in particular can cause database locking issues
on Windows, and undisposed commands can hold schema locks.

**Approach:**
Search for `new SQLiteConnection`, `new SQLiteCommand`, `new DataTable`, `new FileStream`,
and bitmap constructors outside `using` blocks. Verify each is either in a `using` or has an
explicit `Dispose` call on all exit paths. The `SQLiteWrapper` class already uses `using` for
its connections — audit the callers that bypass the wrapper.

*Status: Not yet audited.*

---

### NS-6 · Nullable Reference Analysis

**Severity:** Low–Medium — **Fix Risk:** Low

**Why it matters:**
Null dereferences on image/file paths and database row reads are a common source of
`NullReferenceException` crashes that only appear on edge-case inputs (missing files,
empty database rows, cancelled operations). Many are already guarded; a focused audit
looks for unguarded `?.` chains where the null case is silently ignored rather than handled.

**Approach:**
Search for `.` dereferences on values that are documented or typed as nullable — particularly
return values from database reads (`DataRow["column"]`), file-path operations, and
collection lookups. Verify each null case is either impossible by invariant, handled explicitly,
or logged.

*Status: Not yet audited.*

---

*Analysis performed June 2026 against commit `2fe3fd7` on the `develop` branch.*
