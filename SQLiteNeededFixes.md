# SQLite Busy/Locked Handling — Follow-up Fixes Needed

Status: **Phase 1 in progress** (see "Implementation Plan" section at the end). Done so far:
#4 (`Update()` overload parity, commit `a24c139` on `develop`), #3 (`CreateTable`
consistency, uncommitted), #9 (retry-duration comment fix, uncommitted). Remaining in Phase
1: #10 (mailto clipboard fix). This document is written to be self-contained so a fresh
session (or a different person) can pick up the work without needing the original
conversation.

## Session context

- Repo: `D:\@Timelapse\Timelapse-2.5.0.7p1` (git). Branch `master`.
- Reference commit: **`74be7247da166a70115ee6cb322f596d42c26e82`** — "Fix crash and UI
  freezes from SQLite write failures on network shares" (2026-07-11). Run
  `git show 74be7247da166a70115ee6cb322f596d42c26e82` to see the full diff/message — it's the
  starting point for everything below.
- That commit was triggered by a user report: the app crashed with SQLite `Busy`/`Locked`
  ("database is locked") when the `.ddb` file lived on a network file-server share, inside
  `RecognitionSelector.DropSessionTempTables()`.
- The user then supplied a **fuller error log** (`TimelapseErrorLog.txt`, not stored in the
  repo) covering the *same* contention episode, 2026-07-07 → 2026-07-08, all **before** the
  fix commit landed (2026-07-11). That log revealed the crash the commit fixed was only one
  of **four** distinct failure points hit in that single episode. A 2026-07-13 log entry
  (after the fix) also surfaced an unrelated bug in the "mail error log" feature.
- This document is the result of: (1) my own read of the log + diff, (2) two independent
  verification/audit agents given the same background and told to check my claims and hunt
  independently for more issues, (3) manual spot-checks of the most load-bearing new claims.
  Where a finding is agent-sourced, file:line citations were spot-checked against the current
  tree before inclusion.

## The four failures in the original log (all pre-fix, 2026-07-07/08)

| Time | Site | Symptom |
|---|---|---|
| 07-07 14:01:45, 07-07 14:29:06, 07-08 11:46:17 | `DropSessionTempTables` | Unhandled exception → app shutdown |
| 07-07 14:02:38 | `UpdateSyncImageSetToDatabase` | Fatal write-error dialog → shutdown |
| 07-07 14:15:27 | `RepairClassificationCategoriesIfNeeded` | Fatal write-error dialog → shutdown |
| 07-07 14:28:39 | `GetDataTableFromSelectAsync` | Silent read failure (empty result), 27s before failure #1 recurred |

Plus, unrelated: 2026-07-13 12:43:09 — `ProcessExecution.TryProcessStart` failed to launch a
`mailto:` link for the in-app "mail error log" feature.

## What the commit actually fixed (verified CONFIRMED)

**Only failure #1 (`DropSessionTempTables`)** was addressed, exactly matching the commit's
own stated scope.

- `src/Timelapse/Controls/RecognitionSelector.xaml.cs` — `DropSessionTempTables()` now runs
  the drop inside a fire-and-forget `Task.Run(...)` and on failure calls `AppLog.Warning(...)`
  instead of `GlobalReferences.MainWindow.OnUnhandledException(...)`. No longer fatal, no
  longer on the UI thread.
- Core retry mechanism: `SQLiteWrapper.ExecuteNonQueryWithRollbackCore` (`SQLiteWrapper.cs`,
  around line 898) gained an opt-in `busyTimeoutMs` parameter. Verified arithmetic:
  `int maxBusyAttempt = busyTimeoutMs > 0 ? 7 : 4;` (line ~943), linear backoff
  `delayMs = (busyAttempt + 1) * 250`.
  - Default (`busyTimeoutMs = 0`): retries `busyAttempt` 0..3 → **5 total attempts**, delays
    250+500+750+1000 = **2500 ms** total. Matches commit message ("~2.5s").
  - Extended (`busyTimeoutMs = ThrottleValues.BackgroundWriteExtendedBusyTimeoutMs = 3000`):
    retries `busyAttempt` 0..6 → **8 total attempts**, delays sum to
    250+500+...+1750 = **7000 ms**, not the ~9s / 2000ms-max claimed in the commit message and
    in the code comment at `SQLiteWrapper.cs` ~lines 938-942. **This is a documentation
    mismatch, not a functional bug** — see Recommendation 6 below.
- A handful of confirmed-background-thread write call sites (bulk delete, ID-reset/vacuum,
  checkin merge) were opted into the extended budget; a few previously-silent discards
  (empty-table drops, Vacuum) now log via `AppLog.Warning`; `FileData`/`TemplateInfo`/
  `Template` table-creation failures now show the fatal write-error dialog instead of
  degrading later into a confusing generic "SQL read error."
- Marker add/delete (`TimelapseMarkingAndCounting.cs`) now writes via `UpdateFileAsync`
  (fire-and-forget) instead of blocking the UI thread synchronously on every click.

## What was NOT fixed, and other issues found (verified findings, prioritized)

### 1. Read-path retry never covers Busy/Locked — systemic, and this is literally failure #4

`src/Timelapse/Database/SQLiteWrapper.cs`:
- `GetDataTableFromSelect` (~line 188) and `GetDataTableFromSelectAsync` (~line 231)
- `GetDistinctValuesInColumn` (~line 285)
- `GetScalarFromSelect` and everything built on it (`ScalarGetScalarFromSelectAsInt`,
  `ScalarGetScalarFromSelectAsLong`, `ScalarBoolFromOneOrZero`, `ScalarGetMaxValueAsLong`,
  `ScalarGetFloatValue`) (~line 328)

All of these retry **only** on `SQLiteErrorCode.CantOpen` / `IoErr` (`attempt < 3`, 200ms
steps). None catch `Busy`/`Locked`. A locked SELECT falls straight into the generic
`catch (Exception)`, fires `OnReadError` once (gated by an `_errorFired` flag), and returns an
empty/default result immediately — zero backoff. This is architecturally separate from
`ExecuteNonQueryWithRollbackCore` (the write path this commit fixed) and was completely
untouched. This is exactly what happened at 14:28:39 in the log, 27 seconds before
`DropSessionTempTables` crashed again — i.e., reads have *less* resilience than writes did
even *before* this commit, and the commit didn't change that asymmetry.

**Recommendation:** add a `Busy`/`Locked` retry branch (short, UI-safe budget — reads are
often on the UI thread) to these read primitives, mirroring the write-path pattern but with a
shorter ceiling so it doesn't introduce new UI freezes.

### 2. `PragmaGetQuickCheck` swallows Busy/Locked as "database is corrupt"

`src/Timelapse/Database/SQLiteWrapper.cs:1844-1869`:
```csharp
public bool PragmaGetQuickCheck()
{
    try { ... }
    catch
    {
        // this will be a System.Data.SQLite.SQLiteException
        return false;
    }
}
```
Bare `catch { return false; }`, no distinction between "genuinely corrupt" and "transiently
locked," no logging at all. Callers (`FileDatabase.cs:793`, `CommonDatabase.cs:151`) treat
`false` as "the database file is likely corrupt" and abandon opening the file. This runs at
database-open time — precisely when a network-share lock is most likely to be held by another
user. **Failure scenario:** user opens a `.ddb` on a busy share, hits a transient lock, and
Timelapse tells them their database is corrupt instead of retrying or reporting the real
cause. This is arguably worse than the "confusing generic SQL read error" problem the
commit's own message says it was eliminating, on a path the commit never touched.

**Recommendation:** log the actual exception via `AppLog`; distinguish
`SQLiteErrorCode.Busy`/`Locked` from other failures before concluding "corrupt," and consider
a short retry.

### 3. The `CreateTable` "tiering fix" was applied to only ~3 of ~15 call sites, including two immediately adjacent to a fixed one

Fixed: `FileDatabase.cs:243` (FileData), `CommonDatabase.cs:1683` (TemplateInfo),
`CommonDatabase.cs:1847` (Template) — all now check the result and show the fatal write-error
dialog on failure.

**Not fixed** (bare `Database.CreateTable(...)`, result discarded), verified in the same
method as the fixed FileData call:
- `FileDatabase.cs:263` — `ImageSet` table (20 lines after the fixed FileData check)
- `FileDatabase.cs:309` — `Markers` table (same method)
- `FileDatabase.cs:2004` — a generic table-creation path
- `CommonDatabase.cs:1025` — MetadataTemplate
- `CommonDatabase.cs:1039` — MetadataInfo
- `RecognitionDatabases.cs:62,70,79,103,117,133` — Info, DetectionCategories,
  ClassificationCategories, Detections, Classifications, DetectionsVideo (all six recognition
  tables)

**Failure scenario:** FileData's `CREATE TABLE` succeeds, but the very next statement
(ImageSet, line 263) hits transient contention — the app proceeds with a half-created schema
and fails later with exactly the confusing generic error the commit's message says it fixed.

**Recommendation:** apply the same check-and-dialog pattern to all `CreateTable` call sites,
not just the three that happened to be in the direct path of the original bug report.

### 4. Three of the four `Update()` overloads structurally cannot opt into the extended retry budget

`src/Timelapse/Database/SQLiteWrapper.cs`:
- `Update(string tableName, List<ColumnTuplesWithWhere> updateQueryList)` — line 522 — **no**
  `busyTimeoutMs` parameter
- `Update(string tableName, ColumnTuplesWithWhere columnsToUpdate)` — line 553 — **no**
  `busyTimeoutMs` parameter
- `Update(string tableName, ColumnTuple columnToUpdate)` — line 565 — **no** `busyTimeoutMs`
  parameter
- `Update(string tableName, string IDColumnName, List<long> listOfIDs, string columnName, string value, int busyTimeoutMs = 0)`
  — line 595 — **only this one** got the opt-in parameter

This means `UpdateSyncImageSetToDatabase` (finding #5 below) and anything else using the
first three overloads has no way to request the longer budget even if someone wanted to fix
it today without also touching `SQLiteWrapper.cs`.

**Recommendation:** add the same `int busyTimeoutMs = 0` parameter (threaded down to
`ExecuteNonQueryWithRollback`) to all four `Update` overloads for consistency, before using
it to fix finding #5.

### 5. `UpdateSyncImageSetToDatabase` — untouched, still fatal, runs synchronously on the UI thread

`src/Timelapse/Database/FileDatabaseUpdate.cs:307-316`. Calls
`Database.Update(DBTables.ImageSet, ...)` (the line-522 overload — see #4, can't opt into
extended timeout even if desired) with the default short budget, and still calls
`Dialogs.TimelapseNeedsToShutDownDataWriteErrorDialog(...)` (fatal) on failure.

Traced caller: `TimelapseClosing.cs:116`, inside `CloseTimelapseAndSaveState` /
`CloseImageSet()` — a plain synchronous call chain with **no** `Task.Run`, confirmed running
on the UI thread. Also called from `TimelapseMenuSort.cs:155`, `TimelapseQuickPaste.cs:330`,
`TimelapseMenuEdit.cs:110,1175`, `TimelapseImageSetLoading.cs:413`,
`TimelapseCheckAndCorrectFolders.cs:46` — all likely UI-thread callers (menu/close/load
handlers), not yet individually traced.

This is failure #2 in the log, and it's one of the *first* things to hit lock contention in a
session (53 seconds after the log starts) — not an edge case.

**Recommendation:** this is a UI-thread-synchronous call site, so per the commit's own
policy it should **not** simply get the long budget (that would trade the crash for a UI
freeze). Instead: keep the short budget, but downgrade the failure from a fatal
app-shutdown dialog to a non-fatal warning (matching the `DropSessionTempTables` treatment) —
losing a sort-term/search-term sync to the ImageSet row is not data loss of file data, it's
UI-state persistence. Needs a product judgment call: is losing image-set sync state
acceptable to swallow, or does it need to stay fatal? (See Open Questions.)

### 6. `RepairClassificationCategoriesIfNeeded` — untouched, still fatal, runs synchronously on the UI thread during database open

`src/Timelapse/Database/FileDatabase.cs:709-728`. Still
`this.Database.ExecuteNonQueryWithRollback(query)` with no `busyTimeoutMs`, still calls the
fatal dialog at line 725.

Traced call chain: `RepairClassificationCategoriesIfNeeded` ← `OnExistingDatabaseOpenedAsync`
(line 702) ← `FileDatabase.CreateOrOpenAsync` (line 189,
`await ... .ConfigureAwait(true)`) ← `TryOpenTemplateAndBeginLoadFoldersAsync`
(`TimelapseImageSetLoading.cs:81`) ← UI handlers (`TimelapseMenuFile.cs:309`,
`TimelapseHandleArgumentsOnOpen.cs:95,120`). No `Task.Run` wraps this segment — confirmed
UI-thread-synchronous, during database-open. This is failure #3 in the log.

**Recommendation:** same tension as #5 — this is a one-time repair/migration step touching
classification labels, not file data. Consider whether failure here should be downgraded to
non-fatal-with-warning (the repair can presumably be retried next time the database is
opened) rather than blocking the user from opening their data at all. Needs the same product
judgment call as #5.

### 7. Un-migrated background-thread bulk writes that could safely have opted into the extended budget but weren't touched

These run inside `Task.Run(...)` (background thread, confirmed by their surrounding
`BusyCancelIndicator`/dialog pattern) — the exact class of call the commit's message says it
targeted ("bulk updates, deletes, recognition counting...") — but were missed:

- `DeleteImages.xaml.cs:409` → `FileDatabase.UpdateFiles(imagesToUpdate)` →
  `FileDatabaseUpdate.cs:154` — inside `Task.Run` in `DeleteImages.xaml.cs`. Note its sibling
  call `DeleteFilesAndMarkers` (`FileDatabase.cs:1448`) *was* opted in — this one wasn't.
- `DateTimeFixedCorrection.xaml.cs:117` → `FileDatabase.UpdateAdjustedFileTimes(...)` →
  `FileDatabaseUpdate.cs:437` — `ExecuteNonQueryWithRollback(query)`, no timeout, called
  from inside `Task.Run`.
- Likely the same pattern in `DateTimeLinearCorrection.xaml.cs:161/167` and
  `DateTimeCorrectAmbiguous.xaml.cs:170` (→ `UpdateExchangeDayAndMonthInFileDates` →
  `FileDatabaseUpdate.cs:485`) — not yet individually confirmed, flagged by the audit agent
  as following the same `Task.Run`+dialog pattern used throughout `src/Timelapse/Dialog/`.

**Recommendation:** audit all `Task.Run`-wrapped database-mutation call sites in
`src/Timelapse/Dialog/` for the same opt-in, not just the ones the original bug report
happened to touch.

### 8. No global `UnobservedTaskException`/`AppDomain.UnhandledException` safety net for the new fire-and-forget tasks

`ExecuteNonQueryWithRollbackCore` does have a genuine catch-all (`catch (Exception)`), so the
discarded task in `DropSessionTempTables` won't leak an exception under normal SQLite failure
modes today. However, no `TaskScheduler.UnobservedTaskException` or
`AppDomain.UnhandledException` handler exists anywhere in the app (searched `App.xaml.cs` and
the whole `src` tree — no matches). The commit adds more fire-and-forget work
(`DropSessionTempTables`'s `Task.Run`, plus the marker `UpdateFileAsync` calls in
`TimelapseMarkingAndCounting.cs:64,141`). If any future change introduces an exception type
that escapes one of these discarded tasks (e.g., a null-ref during a shutdown race), it will
vanish with zero diagnostic trail rather than surface anywhere.

**Recommendation:** register a `TaskScheduler.UnobservedTaskException` handler (cheap
insurance) that at minimum logs via `AppLog`, given the app is now relying on more
fire-and-forget tasks than before.

### 9. Retry-duration doc/comment mismatch (not functional, but misleading) — FIXED

Covered in the "What the commit fixed" section above — the code comment and commit message
both said "~9s" / "2000ms max" for the extended budget; the actual arithmetic gives ~7s /
1750ms max. **Resolved:** corrected the comment at `SQLiteWrapper.cs:938-942` to state the
real numbers (8 attempts, up to 1750ms/step, ~7s total) rather than changing
`maxBusyAttempt` to match the originally-stated (incorrect) ~9s — chosen because it's a
zero-behavior-change documentation fix rather than a real, if small, change to
already-working retry logic.

### 10. Mailto error-log feature: separate, pre-existing bug (not caused by the SQLite fix)

`src/Timelapse/TimelapseMenuCallbacks/TimelapseMenuHelp.cs:219-253`
(`MenuItemEmailErrorLog_Click`): reads the app log, truncates to the **last 1000 lines**
(truncation was added earlier, in commit `ff95066`, predating the SQLite fix), URL-encodes it
via `Uri.EscapeDataString`, and builds a `mailto:...&body=...` URI passed to
`ProcessExecution.TryProcessStart(Uri)` (`ProcessExecution.cs:18-40`). When the resulting URI
exceeds what Windows' `ShellExecute` can hand off (which is what happened on 2026-07-13 —
accumulated verbose SQL-dump crash entries pushed the encoded body over the practical length
ceiling), `process.Start()` throws; this is caught, logged via `AppLog.Warning`, and a beep
plays. **Correction to my earlier read:** the caller does check the return value and **does**
show a `MessageBox.Show("Could not open your email client...")` on failure (lines 247-252),
so the user isn't left with zero explanation — just an unhelpful one that doesn't say *why*
(URI too long) or offer an alternative (e.g., "log copied to clipboard" or "save log to file
and attach manually").

**Recommendation (decided):** stop putting the log content in the `mailto:` URL at all — any
length cap is guessing at a moving, OS/mail-client-dependent target and still throws away
content. Instead, in `MenuItemEmailErrorLog_Click` (`TimelapseMenuHelp.cs:219-253`):
1. Copy `logContents` to the clipboard using the exact pattern already proven elsewhere in
   this codebase — `Dialog/ExceptionShutdownDialog.xaml.cs:150-155`
   (`Clipboard.Clear(); Clipboard.SetText(...)`).
2. Build the `mailto:` URI with just the subject and a short, fixed instructional body
   ("The error log has been copied to your clipboard — paste it here with Ctrl+V before
   sending"), not the log itself — this keeps the URI a small, fixed size so it can no longer
   overflow `ShellExecute`'s length ceiling.
3. Update the existing failure-path `MessageBox` (line 249-251) to also mention the log is
   already on the clipboard, so the user can paste it into webmail/Slack/etc. even if the
   mailto launch itself fails.

This removes the failure mode entirely (fixed-size URL can't overflow) rather than just
delaying it, reuses existing code, needs no MAPI/attachment complexity, and — as a side
benefit — the user can now paste the *whole* log rather than being capped by URL length (the
1000-line file-read cap can stay or be relaxed, since it's no longer load-bearing for the URI).
Low priority relative to the SQLite items above, but directly impacts the user's ability to
report *future* bugs like these.

## Performance impact of implementing these fixes

**Essentially zero impact on routine (uncontended) operations.** Every fix above only changes
behavior *inside an exception handler that fires after SQLite has already returned
`Busy`/`Locked`*. On a single-user local database with no lock contention, that code path
never executes — reads, writes, opens, and closes take exactly as long as they do today. The
only "cost" is more waiting during an *already-bad* situation (contention), which is the
intended trade-off, identical in kind to the one the reference commit already made for the
write path.

| Change | Cost when uncontended | Cost when contended |
|---|---|---|
| Read-path Busy/Locked retry (#1) | Zero — one more `SQLiteErrorCode` comparison in an already-existing `catch` filter | Adds retry/backoff before giving up — turns an instant silent failure into a multi-second wait |
| `PragmaGetQuickCheck` fix (#2) | Zero | A few hundred ms to a couple seconds instead of an instant false "corrupt" verdict |
| `CreateTable` consistency (#3) | Zero — runs once, at database/schema creation, not per-operation | Same dialog behavior as the already-fixed sites |
| `Update()` overload parity (#4) | Zero — additive optional parameter, unused unless a caller passes it | N/A by itself |
| `UpdateSyncImageSetToDatabase`/`RepairClassificationCategoriesIfNeeded` downgrade (#5, #6) | Zero — same short retry budget, only the *outcome* after it expires changes | No change in wait time if done as recommended (keep the short budget, just stop showing the fatal dialog) |
| Extending background bulk writes (#7) | Zero | Up to ~7s longer before giving up, but only for operations that already run off the UI thread behind a progress dialog |
| `UnobservedTaskException` handler (#8) | Negligible — one static event subscription at startup | N/A |
| Comment fix (#9), mailto fix (#10) | Zero — doc/unrelated feature | N/A |

**The one place this needs judgment, not mechanical copying:** which retry budget applies to
which thread. If the read-path fix (#1) is applied naively — giving UI-thread-synchronous
reads (`GetDataTableFromSelect`, used directly in several places rather than the async
version) the *long* 8-attempt/~7s budget — that trades "silent failure" for "UI freezes for
7 seconds," exactly the failure mode the reference commit went out of its way to avoid on
write paths. The read-path fix should use a **short**, UI-safe budget (mirroring the existing
5-attempt/~2.5s default) unless a specific call site is confirmed to run on a background
thread.

## Likelihood these fixes introduce new bugs

Three risk tiers:

**Low risk, high confidence — #3, #4, #9, #10.** These are mechanical: applying a pattern
already written, reviewed, and merged elsewhere in the same reference commit, just to more
call sites, or adding an unused optional parameter, or fixing a comment/unrelated feature. The
main way to get these wrong is copy-paste carelessness (e.g., an early `return` added inside
`OnDatabaseCreatedAsync` skipping a later step that used to run unconditionally) — worth a
careful control-flow read-through after each edit, but not a design risk.

**Medium risk — #1, #2, #7, #8.** Not because the SQLite retry mechanics are unproven (the
same `Busy`/`Locked` retry-with-backoff idea already works in this codebase), but because each
requires the same threading judgment call the reference commit's own author got burned by once
("database checkout... turned out not to be backgrounded despite appearances," per the commit
message). Specifics:
- **#7:** each candidate call site needs to be *traced*, not assumed, to confirm it truly runs
  off the UI thread before opting into the long budget — finish the per-site verification the
  two audit agents started rather than pattern-matching by eye.
- **#1:** the existing `_errorFired`/`Interlocked.Exchange` single-fire gate must be preserved
  so adding a retry loop doesn't change how many times `OnReadError` fires, and the retry
  condition must not accidentally swallow a genuine non-transient error by misclassifying it
  as retryable.
- **#2:** the retry condition must be narrowly scoped to `Busy`/`Locked` only — a truly corrupt
  database returns a *different* SQLite error code, and making that retryable would turn a
  fast "corrupt" diagnosis into a multi-second hang before still reporting corrupt.
- **#8:** nearly risk-free technically, but on this project's target framework
  (`net10.0-windows`), unobserved task exceptions don't crash the process by default anyway —
  so this handler is pure diagnostics (worth doing, but not itself a safety net against
  anything).

**Requires a product decision before it's "safe" — #5, #6.** These aren't code-risk so much as
*silent-failure risk* dressed as a fix. Changing "show fatal dialog" to "log and continue" for
`UpdateSyncImageSetToDatabase` and `RepairClassificationCategoriesIfNeeded` means a user's
sort/search-term state or a classification-label repair can now fail **without the user ever
knowing** — trading "the app crashed" for "the app quietly didn't do what it said it did."
That's a regression in a different dimension (silent correctness) even though it's zero lines
of new logic risk. This is why the "Open questions" section below leaves it as a decision
rather than a recommendation: it shouldn't be implemented as a pure copy of the
`DropSessionTempTables` treatment without also deciding whether the user needs some
non-modal indicator (status-bar message, a surfaced log entry) that something didn't save.

**Overall:** the mechanical consistency fixes (#3, #4, #9, #10) are close to risk-free and
should be done first. The read-path and background-write-opt-in fixes (#1, #7) are sound in
principle and low-risk *if* each call site's threading context is individually verified rather
than assumed — that verification step is what stands between "confident fix" and "possible new
freeze." The two fatal-to-warning downgrades (#5, #6) are the only items where the risk isn't
really about bugs at all — it's about whether silent-continue is an acceptable user experience,
which needs an answer before it's safe to call "fixed."

## Open questions for the user (product decisions, not just code)

1. **Resolved (partially):** for findings #5/#6, the user has decided NOT to reuse the
   silent log-and-continue treatment. They are leaning toward a modal, informative dialog
   (distinct from today's fatal shutdown dialog) but have not yet settled the exact design —
   see "Findings #5, #6" under Phase 3 of the Implementation Plan below for the specific
   sub-questions still open (Retry/Ignore semantics, whether close-time vs. in-session failure
   should behave differently).
2. **Resolved:** sequencing — phased by risk tier, safest-first, as separate commits/PRs (see
   Implementation Plan below). #5/#6 ship last, after the Phase 3 checkpoint is resolved.
3. Status: plan approved, no code written yet. See "Implementation Plan" below for the
   concrete, phased execution plan.

## Implementation Plan (approved)

Sequencing decision: **phased by risk tier**, as separate commits/PRs, safest-first. Findings
#5/#6 are **deferred** — see Phase 3 below for why and what's still open.

### Phase 1 — Mechanical, low-risk (findings #3, #4, #9, #10)

These are direct copies of a pattern already reviewed and merged in the reference commit
(`74be7247da166a70115ee6cb322f596d42c26e82`) — the fatal-dialog pattern, the `AppLog.Warning`
non-fatal pattern, and the `busyTimeoutMs` opt-in parameter — applied to more call sites, or a
comment/unrelated-feature fix. No new design decisions, no threading judgment calls.

1. **#4 — `Update()` overload parity.** Add `int busyTimeoutMs = 0` to the three overloads in
   `SQLiteWrapper.cs` (currently lines 522, 553, 565) that lack it, threading it into their
   `ExecuteNonQueryWithRollback(...)` calls exactly as the fourth overload (line 595) already
   does. Purely additive — no caller changes yet. Do this before #3 touches adjacent code in
   the same files.
2. **#3 — `CreateTable` consistency.** Apply the same check-result-and-show-dialog pattern
   (already used for FileData/TemplateInfo/Template) to the remaining bare
   `Database.CreateTable(...)` calls: `FileDatabase.cs:263` (ImageSet), `FileDatabase.cs:309`
   (Markers), `FileDatabase.cs:2004`, `CommonDatabase.cs:1025` (MetadataTemplate),
   `CommonDatabase.cs:1039` (MetadataInfo), and `RecognitionDatabases.cs:62,70,79,103,117,133`
   (six recognition tables). **Care point:** each new early `return` on failure must not skip
   a later step that previously ran unconditionally — read the surrounding method's full
   control flow before adding the guard.
3. **#9 — Retry-duration doc fix. DONE.** The code comment and commit message both claimed
   "~9s / 2000ms max" for the extended busy budget; actual arithmetic
   (`maxBusyAttempt = busyTimeoutMs > 0 ? 7 : 4` in `ExecuteNonQueryWithRollbackCore`,
   `SQLiteWrapper.cs` ~898-943) gives ~7s/1750ms max. Fixed by correcting the comment at
   `SQLiteWrapper.cs:938-942` to match reality (not by bumping `maxBusyAttempt`, to avoid a
   behavior change for a documentation-only problem).
4. **#10 — Mailto: use the clipboard instead of an oversized URL body.** In
   `TimelapseMenuHelp.cs:219-253` (`MenuItemEmailErrorLog_Click`): copy `logContents` to the
   clipboard using the existing pattern at `Dialog/ExceptionShutdownDialog.xaml.cs:150-155`
   (`Clipboard.Clear(); Clipboard.SetText(...)`); build the `mailto:` URI with just the
   subject and a short fixed instructional body ("paste the log from your clipboard with
   Ctrl+V"), not the log content itself, so the URI can no longer overflow `ShellExecute`'s
   length ceiling; update the existing failure-path `MessageBox` (line 249-251) to also
   mention the clipboard copy as a fallback path.

**Verification (no lock-contention harness needed):** run the app uncontended end-to-end
(new database, existing database, recognition features) and confirm every touched
`CreateTable` site still succeeds silently as today; then force a failure per site
(temporarily rename/lock the `.ddb` mid-open, or inject a bad query) to confirm the dialog now
fires where it silently didn't before. For #10, generate an oversized encoded body and confirm
the cap/fallback triggers. For #4, confirm behavior is unchanged (new parameter unused so far).

**Rollback:** one commit per finding (4 commits) — trivially bisectable, each independently
revertible without touching phase 2/3 work.

### Phase 2 — Medium-risk, read-path and threading fixes (findings #1, #2, #7, #8)

Mechanically similar to the already-proven write-path retry logic, but each requires
*verifying*, not assuming, thread context — exactly where the reference commit's own author
got burned once ("database checkout... turned out not to be backgrounded despite
appearances").

1. **#1 — Read-path Busy/Locked retry.** Add a `Busy`/`Locked` branch to the exception filter
   in `GetDataTableFromSelect` (~188), `GetDataTableFromSelectAsync` (~231),
   `GetDistinctValuesInColumn` (~285), and `GetScalarFromSelect` (~328, backs several
   `Scalar*` helpers) in `SQLiteWrapper.cs`. Use the **short** budget only (mirror the
   existing 5-attempt/~2.5s pattern) — these are frequently called synchronously from the UI
   thread, and the long budget would trade silent failure for a multi-second freeze. Preserve
   the existing `Interlocked.Exchange(ref _errorFired, 1)` single-fire gate unchanged; the new
   retry loop must sit before that gate fires, not duplicate or bypass it. Scope the exception
   filter narrowly to `SQLiteErrorCode.Busy || SQLiteErrorCode.Locked` alongside the existing
   `CantOpen || IoErr` clause so genuinely non-transient errors still fall through immediately.
2. **#2 — `PragmaGetQuickCheck` misdiagnosis.** Replace the bare `catch { return false; }` in
   `SQLiteWrapper.cs:1844-1869` with one that logs via `AppLog` and distinguishes
   `Busy`/`Locked` (retry briefly on the same short budget, then fall through) from other
   SQLite error codes (treat as genuine corruption, `false`, as today). No contract change for
   callers (`FileDatabase.cs:793`, `CommonDatabase.cs:151`).
3. **#7 — Opt confirmed background bulk writes into the extended budget.** Candidates:
   `DeleteImages.xaml.cs:409` → `FileDatabase.UpdateFiles` → `FileDatabaseUpdate.cs:154`;
   `DateTimeFixedCorrection.xaml.cs:117` → `FileDatabase.UpdateAdjustedFileTimes` →
   `FileDatabaseUpdate.cs:437`; and (pending the same verification) likely
   `DateTimeLinearCorrection.xaml.cs:161/167` and `DateTimeCorrectAmbiguous.xaml.cs:170` →
   `FileDatabaseUpdate.cs:485`. **Mandatory verification per site before opt-in:** walk callers
   upward from the DB call until hitting either a `Task.Run` boundary (confirmed backgrounded)
   or a direct UI-event-handler with no such boundary (must stay on the short budget). Default
   assumption is "UI thread" until a `Task.Run` is literally read in the call chain — never
   infer from file name or apparent intent alone.
4. **#8 — Global `UnobservedTaskException` safety net.** `App.xaml.cs` is currently a bare
   `public partial class App;` with no `OnStartup` override (startup is driven entirely by
   `StartupUri` in `App.xaml`). Add an `OnStartup` override (or static constructor) that
   registers `TaskScheduler.UnobservedTaskException` (and consider
   `AppDomain.CurrentDomain.UnhandledException` for parity), logging via `AppLog.Warning` at
   minimum. Diagnostics only — on `net10.0-windows`, unobserved task exceptions don't crash the
   process by default, so this doesn't change failure behavior, only visibility.

**Verification — reuse the reference commit's own technique:** hold a lock via a second
`SQLiteConnection` executing `BEGIN IMMEDIATE` against a scratch copy of a `.ddb`, for a
controlled duration:
- For #1/#2: trigger the read / `PragmaGetQuickCheck` while the lock is held; confirm
  retry-then-success if released within the short budget, and correct classification (not
  corrupt, not crashed) if the hold outlasts it. Do this while actively interacting with the
  UI (drag the window, click a menu) to confirm no freeze beyond the short budget.
- For #7: hold the lock during a bulk delete / date-correction operation and confirm the
  progress dialog and its cancel button remain responsive for the full extended wait.
- For #8: deliberately throw inside an uncaught `Task.Run` in a scratch build and confirm the
  new handler logs it.

**Rollback:** one commit per finding; #7's per-site opt-ins can be split further (one commit
per site) if any single site's threading trace turns out inconclusive.

### Phase 3 — Deferred decision (findings #5, #6): checkpoint, not code yet

`UpdateSyncImageSetToDatabase` (`FileDatabaseUpdate.cs:307-316`) and
`RepairClassificationCategoriesIfNeeded` (`FileDatabase.cs:709-728`) both remain on the fatal
`TimelapseNeedsToShutDownDataWriteErrorDialog` path after phases 1-2 — **intentionally**. The
user flagged both as touching state that matters (persisted sort/search/UI state; a
classification-label repair) and is not ready to reuse the `DropSessionTempTables` silent-log
treatment for them. Their words: *"both operations are important, where a failure to update
or do the repair is problematic, so I need to consider how to deal with it. A likely solution
is to raise a dialog box instead of a notification, as this is an important issue that the
user needs to understand, where the dialog box includes details of what is going on."*

**Before writing any code here, return to the user with concrete options, e.g.:**
- A modal "This operation failed: `<what>`, reason: `<Busy/Locked detail>`" dialog with
  **Retry** / **Ignore and continue** buttons, reusing `Dialogs`-style infrastructure already
  in the codebase.
- Whether **Ignore** should behave identically at both call sites, or whether close-time
  failure (`TimelapseClosing.cs:116`) warrants different options than in-session failure
  (e.g. `TimelapseMenuSort.cs:155`) — closing with unsynced state may carry different risk
  than continuing to work with it.
- Whether Retry should reuse the existing short retry budget again, or trigger a fresh attempt
  on demand.

Only after that's settled, implement using the same `SqlOperationResult`/`Dialogs` pattern
established elsewhere in the codebase, applying it consistently to both call sites (and
`UpdateSyncImageSetToDatabase`'s other five UI-thread callers: `TimelapseMenuSort.cs`,
`TimelapseQuickPaste.cs`, `TimelapseMenuEdit.cs` (x2), `TimelapseImageSetLoading.cs`,
`TimelapseCheckAndCorrectFolders.cs`).

**Verification when unblocked:** same lock-hold harness, applied at close-time and at
database-open-time; confirm the new dialog (not the fatal one) appears, Retry re-attempts
correctly, and Ignore leaves the app in a documented, consistent state.

### Critical files

- `src/Timelapse/Database/SQLiteWrapper.cs` — core retry logic, `Update()` overloads, read
  primitives, `PragmaGetQuickCheck`
- `src/Timelapse/Database/FileDatabase.cs` — `CreateTable` sites, `RepairClassificationCategoriesIfNeeded`
- `src/Timelapse/Database/FileDatabaseUpdate.cs` — `UpdateSyncImageSetToDatabase`, bulk update paths
- `src/Timelapse/Database/CommonDatabase.cs`, `RecognitionDatabases.cs` — remaining `CreateTable` sites
- `src/Timelapse/TimelapseMenuCallbacks/TimelapseMenuHelp.cs` — mailto body cap
- `src/Timelapse/App.xaml.cs` — `UnobservedTaskException` handler
- `src/Timelapse/Dialog/DeleteImages.xaml.cs`, `Dialog/DateTimeFixedCorrection.xaml.cs`,
  `Dialog/DateTimeLinearCorrection.xaml.cs`, `Dialog/DateTimeCorrectAmbiguous.xaml.cs` —
  background-write opt-in candidates for #7
