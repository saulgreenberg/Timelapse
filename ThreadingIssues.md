# Threading Issues — Timelapse 2.5.0.7

Audited June 2026. All file paths are relative to `src/Timelapse/`.

Issues are grouped by type and ordered by severity within each group. Issues already
fixed (ExifTool race conditions) are noted for completeness but not re-listed in detail.

---

## Already Fixed (ExifTool — June 2026)

The following were fixed as part of the crash investigation that prompted this audit:

| File | What was fixed |
|------|---------------|
| `ExifTool/ExifToolWrapper.cs` | `_proc` assigned after `Start()` (local variable pattern) |
| `ExifTool/ExifToolWrapper.cs` | Status + `_proc` re-checked inside `_lockObj` in `SendCommand` |
| `ExifTool/ExifToolWrapper.cs` | `Stop()` stdin write now inside `_lockObj` |
| `ExifTool/ExifToolManager.cs` | `StartIfNotAlreadyStarted` guarded by `_startLock` |
| `ExifTool/ExifToolManager.cs` | `StartIfNotAlreadyStarted` also restarts when `Status == Stopped` |

---

## Category 1 — Blocking Waits (`.Wait()` / `.Result`) on the UI Thread

These are the most dangerous issues because they can cause UI freezes and classic
async deadlocks.  A deadlock occurs when a background task tries to marshal work
back to the UI thread via `Dispatcher.Invoke` while the UI thread is blocked in
`.Wait()`.

### Issue 1.1 — `prefetch.Wait()` inside `TryGetBitmap` (UI thread)

**File:** `Images/ImageCache.cs`, lines 374–378  
**Severity:** High

```csharp
if (prefetechesByID.TryGetValue(fileRow.ID, out Task prefetch))
{
    prefetch.Wait();                              // blocks the UI thread
    bitmap = unalteredBitmapsByID[fileRow.ID];   // TOCTOU — see Issue 3.1
}
```

`TryGetBitmap` is called from the image-navigation code on the UI thread.  If the
prefetch task (started in `TryInitiateBitmapPrefetch`) tries to write back through
`Dispatcher.Invoke` — or if anything in the call chain does — the UI thread deadlocks
because it is blocking on `.Wait()` and cannot process dispatcher messages.

Even without deadlock, this freezes the UI for the entire duration of the bitmap load.

**Fix:** Replace `.Wait()` with `await` by making `TryGetBitmap` async (returning
`Task<bool>`), and propagate the `await` up through the navigation call chain.  All
callers of `TryGetBitmap` are already on the UI thread, so the change is mechanical.

---

### Issue 1.2 — `task.Wait()` inside `BitmapSource` property getter

**File:** `ImageSetLoadingPipeline/ImageLoader.cs`, lines 37–44  
**Severity:** High

```csharp
public BitmapSource BitmapSource
{
    get
    {
        if (field == null)
        {
            var task = File.LoadBitmapAsync(...);
            task.Wait();               // blocks whichever thread reads this property
            var loadResult = task.Result;
            field = loadResult.Item1;
        }
        return field;
    }
}
```

`LoadBitmapAsync` is `Task.Run(...)` internally.  If the property is read on the UI
thread (plausible given `ImageLoader` is used in image-set loading which has a UI
component), this blocks the UI thread.

**Fix:** Remove the lazy-load from the getter entirely.  Expose the value only after
the caller explicitly awaits `LoadImageAsync(...)`, which is the method that already
kicks off the background work.  The property should assert or throw if accessed before
`LoadImageAsync` completes, not silently do blocking I/O.

---

### Issue 1.3 — `LoadAsync(...).Wait()` in `BackgroundWorker.DoWork`

**File:** `TimelapsePartialClasses/TimelapseImageSetLoading.cs`, line 492  
**Severity:** Medium

```csharp
backgroundWorker.DoWork += (_, _) =>
{
    ImageSetLoader loader = new(...);
    loader.LoadAsync(backgroundWorker.ReportProgress, folderLoadProgress, 500).Wait();
    filesSkipped = loader.ImagesSkippedAsFilePathTooLong;
};
```

`DoWork` runs on a thread-pool thread that has no `SynchronizationContext`, so
the deadlock risk is lower than Issue 1.1.  However, `.Wait()` on an `async` method
blocks a thread-pool thread for the full duration of image loading, which can be
many seconds.  This wastes a thread-pool thread and suppresses the composability
benefits of async.  It also means that any `await` inside `LoadAsync` that captures
a non-null context (possible if the code is ever refactored) would silently deadlock.

**Fix:** Change the `DoWork` handler to `async` (the BackgroundWorker pattern does
support `async` DoWork if wrapped carefully) or — better — replace the entire
`BackgroundWorker` with a `Task`-based pipeline awaited from an `async` method, which
is the modern WPF pattern.

---

## Category 2 — Data Races on Shared Mutable State

### Issue 2.1 — `filesSkipped` written on DoWork thread, read on UI thread

**File:** `TimelapsePartialClasses/TimelapseImageSetLoading.cs`, lines 493 and 505  
**Severity:** Medium

```csharp
// DoWork handler (background thread):
filesSkipped = loader.ImagesSkippedAsFilePathTooLong;   // line 493

// ProgressChanged handler (UI thread):
if (filesSkipped.Count > 0)                             // line 505
    Dialogs.FilePathTooLongDialog(this, filesSkipped);
```

`filesSkipped` is a `List<string>` captured by both lambdas from the enclosing scope.
The DoWork thread assigns the reference; the UI thread reads it.  Without a memory
barrier the UI thread can see a stale reference (still pointing to the empty
initializer list) and miss the dialog, or — on a weakly-ordered architecture —
observe a partially constructed list.

**Fix:** Do not share mutable state via a captured variable.  Pass the result through
the `ProgressChanged` event's `UserState` parameter:

```csharp
backgroundWorker.ReportProgress(0, loader.ImagesSkippedAsFilePathTooLong);

// in ProgressChanged:
if (ea.UserState is List<string> skipped && skipped.Count > 0)
    Dialogs.FilePathTooLongDialog(this, skipped);
```

---

### Issue 2.2 — `GlobalReferences.CancelTokenSource` replaced from a background thread

**File:** `Database/FileDatabase.cs`, line 2108  
**Severity:** Medium

```csharp
catch (Exception e)
{
    if (e is TaskCanceledException)
    {
        GlobalReferences.CancelTokenSource = new();   // runs inside Task.Run
    }
}
```

This executes inside a `Task.Run` lambda.  The property setter writes a new
`CancellationTokenSource` reference to a shared static while the UI thread and other
background threads may be reading the existing reference, calling `.IsCancellationRequested`,
or registering callbacks on it.  The write is not atomic at the application level
even if the underlying reference write is atomic on x64: the new (uncancelled) source
replaces the old one before callers have had a chance to observe the cancellation.

`BusyCancelIndicator.cs` lines 183 and 198 also replace it, but those callers are on
the UI thread, which is safer.

**Fix:** `CancelTokenSource` should only be replaced from the UI thread at a
well-defined synchronisation point (e.g. at the very start of a new top-level
operation, before any background tasks are spawned).  The background task should
communicate its cancelled status through the return value or a progress report, not
by resetting the shared token source.

---

### Issue 2.3 — `GlobalReferences` bool fields without `volatile`

**File:** `DataStructures/GlobalReferences.cs`, lines 29–32  
**Severity:** Low

```csharp
public static bool DetectionsExists { get; set; }
public static bool HideBoundingBoxes { get; set; } = false;
```

Both flags are written by the UI thread and read by background threads (e.g.,
`RecognitionSelector.xaml.cs` reads `DetectionsExists` inside `Task.Run`).  Without
`volatile`, the JIT is free to cache the value in a register and never re-read it
from memory, producing a thread that loops forever on a stale value.

**Fix:** Mark both as `volatile`, or wrap the backing field with `Volatile.Read` /
`Volatile.Write`.  Alternatively, replace the plain auto-properties with properties
that use `Interlocked` or a lock.

---

## Category 3 — Lock Anti-patterns

### Issue 3.1 — `lock` on WPF UI controls

**Files:**  
- `Images/MarkableCanvas.cs`, lines 491, 900, 911  
- `Controls/VideoPlayer.xaml.cs`, line 988  
**Severity:** Low (currently), Medium (if code evolves)

```csharp
lock (ImageToDisplay)   { ... }   // MarkableCanvas.cs:491
lock (VideoPlayer)      { ... }   // MarkableCanvas.cs:900
lock (ThumbnailGrid)    { ... }   // MarkableCanvas.cs:911
lock (MediaElement)     { ... }   // VideoPlayer.xaml.cs:988
```

`ImageToDisplay`, `VideoPlayer`, `ThumbnailGrid`, and `MediaElement` are WPF
`FrameworkElement` subclasses.  Using them as monitor objects is an anti-pattern for
two reasons:

1. WPF controls have thread affinity.  Any background thread that ever acquires one of
   these locks would already be violating WPF's threading rules before it could do any
   useful work.
2. WPF may use the object's monitor internally for its own synchronisation; locking the
   same object from application code can conflict.

All four sites appear, at present, to execute only on the UI thread, making the lock
redundant.  The real concern is that this pattern suggests the developer intended to
protect against multi-threaded access — if that intent is ever realised by adding
background-thread access, the lock will not protect against WPF thread-affinity
violations.

**Fix:** Replace each with a dedicated `private readonly object _zoomLock = new()`
(or the equivalent `Lock`) declared alongside the control.  If all callers are
guaranteed to be on the UI thread, remove the lock entirely since it serves no purpose
there.

---

### Issue 3.2 — Inconsistent locking between `CacheBitmap` and `TryGetBitmap`

**File:** `Images/ImageCache.cs`, lines 312 and 378  
**Severity:** Low

```csharp
// CacheBitmap (called from background Task.Run):
lock (mostRecentlyUsedIDs)
{
    unalteredBitmapsByID.AddOrUpdate(id, bitmap, ...);   // inside lock
    mostRecentlyUsedIDs.SetMostRecent(id);
}

// TryGetBitmap (UI thread), after prefetch.Wait():
bitmap = unalteredBitmapsByID[fileRow.ID];               // no lock — line 378
```

`CacheBitmap` wraps its write inside `lock (mostRecentlyUsedIDs)`.  `TryGetBitmap`
reads `unalteredBitmapsByID` after `prefetch.Wait()` without that lock.
`unalteredBitmapsByID` is a `ConcurrentDictionary`, so the read itself is
thread-safe.  However, the eviction logic in `CacheBitmap` can remove the entry
for another ID between when the prefetch task finishes and when line 378 executes —
and in a pathological interleaving it could evict the very ID being read (if the
cache is at capacity and another prefetch just completed).  This is likely the cause
of the `KeyNotFoundException` noted in the comment at line 349.

**Fix:** Change the `unalteredBitmapsByID[fileRow.ID]` indexer at line 378 to
`TryGetValue` with a fallback to the synchronous load path:

```csharp
prefetch.Wait();
if (!unalteredBitmapsByID.TryGetValue(fileRow.ID, out bitmap))
    bitmap = fileRow.LoadBitmap(Database.RootPathToImages, out _);
```

---

## Category 4 — Fire-and-Forget Tasks

### Issue 4.1 — `SqlOperationResult.GenerateExceptionDialog` is fire-and-forget

**File:** `Database/SqlOperationResult.cs`, lines 117–148  
**Severity:** Medium

```csharp
Task.Run(() =>
{
    if (Application.Current.Dispatcher.CheckAccess())
    {
        // Dead code — CheckAccess() is always false on a thread-pool thread.
        // If somehow reached, creates a WPF dialog on a background thread.
        Dialog.ExceptionShutdownDialog dialog = new(...);
        dialog.ShowDialog();
    }
    else
    {
        Application.Current.Dispatcher.Invoke(() => { ... dialog ... });
    }
});
// No await — caller returns immediately, dialog may never be seen
```

There are two problems:

1. **Fire-and-forget**: the `Task.Run` is not awaited.  If `GenerateExceptionDialog`
   is called from an `async` context, the caller returns immediately and the dialog
   may appear seconds later — or never, if the application shuts down before the
   thread-pool thread is scheduled.

2. **Dead branch**: `CheckAccess()` always returns `false` on a thread-pool thread,
   so the first branch (which would create a dialog directly on the background thread,
   an error) is unreachable.  It should be deleted to avoid confusion.

**Fix:** Remove the `Task.Run` wrapper entirely.  Call `Dispatcher.Invoke` directly
from whatever thread invokes `GenerateExceptionDialog`.  If the caller is already on
the UI thread, `Dispatcher.Invoke` is a no-op and the dialog opens synchronously.

---

## Category 5 — `MediaPlayer` on a Thread-Pool Thread (32-bit / FFmpeg fallback path)

### Issue 5.1 — `GetVideoBitmapFromFileUsingMediaEncoder` called from `Task.Run`

**File:** `Images/BitmapUtilities.cs`, lines 252–350  
**Severity:** Medium (low frequency — 32-bit OS or FFmpeg failure only)

Call chain:
```
ImageCache.TryInitiateBitmapPrefetch  →  Task.Run
ImageRow.LoadBitmapAsync              →  Task.Run
    └─ LoadBitmap → BitmapUtilities.GetBitmapFromVideoFile
           └─ (32-bit OS or FFmpeg failure)
                  GetVideoBitmapFromFileUsingMediaEncoder
                       creates MediaPlayer on thread-pool thread
```

`MediaPlayer` is a WPF class that internally relies on a `Dispatcher` for event
delivery.  Thread-pool threads do not have a `Dispatcher` (and do not have a COM
message pump, which WPF's media subsystem also requires).  The `Thread.Sleep` polling
loop (lines 283–297) that waits for `NaturalVideoWidth > 0` is a symptom of this:
the width never populates because the `MediaOpened` event is never delivered to a
thread that can receive it.

On 32-bit OS or when FFmpeg cannot be found, this path silently fails or hangs
until `timesTried` expires and returns a blank-video placeholder.

**Fix:** Run `GetVideoBitmapFromFileUsingMediaEncoder` on a dedicated STA thread
that pumps a `Dispatcher`:

```csharp
BitmapSource result = null;
var thread = new Thread(() =>
{
    result = GetVideoBitmapFromFileUsingMediaEncoder(...);
    Dispatcher.CurrentDispatcher.InvokeShutdown();
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
```

Alternatively, replace the method with a call to FFmpeg (via `NReco.VideoConverter`)
on any thread, eliminating the MediaPlayer dependency entirely — which appears to be
the intent given the comment at line 250 ("While it works, it's ~twice as slow").

---

## Category 6 — `Thread.Sleep` Misuse in Background Tasks

### Issue 6.1 — `Thread.Sleep` does not yield time to the UI thread

**Files:** ~40 locations across `Database/`, `Dialog/`, `Recognition/`, `Images/`  
**Severity:** Informational / Performance

Representative example from `Database/FileDatabase.cs:2035`:
```csharp
// Inside Task.Run:
Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
```

Sleeping a thread-pool thread has no effect on the WPF UI thread.  The UI thread
updates when control returns to the dispatcher loop — that is, between `await`
continuations or when the UI thread itself is not busy.  Sleeping the background
thread only delays the background work; it does not "yield" anything to the UI thread.

The actual mechanism that lets the UI update is `IProgress<T>.Report(...)`, which
marshals a callback onto the UI thread via `Dispatcher.BeginInvoke`.  If progress
reporting is in place, the `Thread.Sleep` calls are redundant.

**Fix:** Remove the `Thread.Sleep` calls from background tasks.  If throttling is
genuinely needed (e.g., to limit CPU usage), use `await Task.Delay(n).ConfigureAwait(false)` 
inside an async method, which releases the thread-pool thread rather than blocking it.

---

## Summary Table

| # | File(s) | Lines | Severity | Category | Effort to fix |
|---|---------|-------|----------|----------|---------------|
| 1.1 | `Images/ImageCache.cs` | 374–378 | **High** | Blocking wait on UI thread | Medium — propagate async up the nav stack |
| 1.2 | `ImageSetLoadingPipeline/ImageLoader.cs` | 37–44 | **High** | Blocking wait on UI thread | Low — remove getter lazy-load |
| 1.3 | `TimelapsePartialClasses/TimelapseImageSetLoading.cs` | 492 | Medium | Blocking wait in DoWork | High — replace BackgroundWorker with Task pipeline |
| 2.1 | `TimelapsePartialClasses/TimelapseImageSetLoading.cs` | 493, 505 | Medium | Data race on captured variable | Low — pass via UserState |
| 2.2 | `Database/FileDatabase.cs` | 2108 | Medium | CancelTokenSource replaced from background thread | Medium — redesign cancellation ownership |
| 2.3 | `DataStructures/GlobalReferences.cs` | 29–32 | Low | Missing `volatile` | Low — add `volatile` or `Interlocked` |
| 3.1 | `Images/MarkableCanvas.cs`, `Controls/VideoPlayer.xaml.cs` | 491, 900, 911, 988 | Low | Lock on WPF UI elements | Low — replace with dedicated lock objects |
| 3.2 | `Images/ImageCache.cs` | 312, 378 | Low | Inconsistent locking around cache read | Low — use TryGetValue with fallback |
| 4.1 | `Database/SqlOperationResult.cs` | 117–148 | Medium | Fire-and-forget Task + dead branch | Low — remove Task.Run wrapper |
| 5.1 | `Images/BitmapUtilities.cs` | 252–350 | Medium | MediaPlayer on thread-pool thread | High — STA thread wrapper or FFmpeg-only |
| 6.1 | ~40 locations | various | Info | Thread.Sleep in background tasks | Low — remove or replace with Task.Delay |

---

## Fix Priority Order

1. **1.1** — Most likely to cause a user-visible deadlock/freeze during image navigation.
2. **1.2** — Secondary freeze risk; also exposes a design smell in `ImageLoader`.
3. **4.1** — Easy win: the fire-and-forget exception dialog may silently disappear.
4. **2.1** — Easy win: eliminates the `filesSkipped` race with a one-line change.
5. **2.2** — Requires redesigning who owns the CancellationTokenSource lifecycle.
6. **5.1** — Low frequency (32-bit or FFmpeg failure) but correct fix requires an STA thread.
7. **3.2** — Fixes a known `KeyNotFoundException` (see comment at ImageCache.cs:349).
8. **2.3, 3.1, 6.1** — Low risk; fix when touching surrounding code.
9. **1.3** — Largest refactor; replace BackgroundWorker with a proper async pipeline.
