# SQLite Busy/Locked Handling — Follow-up Fixes Needed

Status: **All three phases complete.** Every finding from the original plan (#1-#11) is now
found/fixed. Phase 1 done: #4 (commit `a24c139`), #3 (commit `2acdef0`), #9 (commit `6d0acc5`),
#10 (mailto → clipboard + Explorer-reveal, plus an added error-log relocation to
`%LocalAppData%\Timelapse\ErrorLogs\` with migration — implemented, currently uncommitted).

**#11 — done:** `ResetIDsAndVacuum` used to permanently switch the database to WAL journal
mode, which SQLite's own docs say is unreliable over network shares — directly relevant to the
original bug's network-share deployment, and plausibly a bigger contributor to real-world
"database is locked" reports than lock-contention timing. Fixed (stopped setting WAL, added an
on-open migration for already-affected databases) and verified empirically — see finding #11
below for full detail, including a provenance check confirming this pragma predates any
Claude-assisted work on this project (traces to the repo's first commit, 2025-12-11).

**#1 — done, accepted at code-review-level confidence (decided 2026-07-14).** All four read
primitives in `SQLiteWrapper.cs` now retry on `Busy`/`Locked` with the short budget. Three
separate live-contention test attempts never managed to reproduce a real `SQLITE_BUSY`/`LOCKED`
exception, so the new catch branches were never empirically observed to fire — but given #11
was plausibly the dominant real cause of the original read failures and is now fixed
independently, the decision was to accept the code as structurally sound (minimal mirror of
already-proven write-path logic) rather than keep chasing a repro. See finding #1 below for the
full reasoning and the fallback plan if read-lock errors resurface later.

**Phase 3 (#5/#6) — done (2026-07-14):** `UpdateSyncImageSetToDatabase` and
`RepairClassificationCategoriesIfNeeded` now show a new `Dialogs.TimelapseOperationRetryDialog`
(single "Retry" button, closing = proceed to fatal) after their existing automatic retries are
exhausted, capped at one manual retry, before falling through to the unchanged fatal dialog.
See Phase 3 below for the full design history and reasoning.

**Remaining open items (all manual/UI verification, not design decisions):** finding #10's
manual test (mailto/clipboard/Explorer-reveal — implemented and build-verified, end-to-end
confirmation in the running app still outstanding) and Phase 3's manual test (trigger a
failure, confirm Retry/fatal-fallback behavior — needs a real or simulated lock-contention
scenario to reach). This document is written to be self-contained so a fresh session (or a
different person) can pick up the work without needing the original conversation.

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

### 1. Read-path retry never covers Busy/Locked — systemic, and this is literally failure #4 — FIXED, accepted at code-review-level confidence

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

**Fixed:** added a `Busy`/`Locked` retry branch (short, UI-safe budget matching the write
path's default schedule) to all four read primitives, narrowly scoped alongside the existing
`CantOpen`/`IoErr` clause, preserving the `_errorFired` single-fire gate unchanged. Build
verified clean.

**Verification status — accepted at code-review-level confidence, not empirically proven.**
Three separate attempts to reproduce a real `SQLITE_BUSY`/`LOCKED` exception in a live-contention
test (same-process `BEGIN IMMEDIATE`, same-process with `PRAGMA locking_mode=EXCLUSIVE`, and a
genuinely separate OS process holding a confirmed lock) all failed — every read succeeded
near-instantly regardless of the lock, so the new catch branches were never observed to
actually fire. Decision (2026-07-14): accept this as done anyway, reasoning that finding #11
(WAL mode silently enabled on databases that ever ran `ResetIDsAndVacuum`, undocumented as
unreliable over network shares by SQLite itself) was plausibly the dominant real-world cause of
the original read failures, and is now fixed independently. The retry code here remains a
structurally sound, minimal-diff mirror of already-proven write-path logic — low residual risk
even without a positive contention test — but if read-lock errors ever resurface in a future
log after the #11 fix ships, revisit this verification gap first.

`src/Timelapse/Database/SQLiteWrapper.cs`, originally:
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
locked," no logging at all. Callers (`FileDatabase.cs:802`, `CommonDatabase.cs:151`) treat
`false` as "the database file is likely corrupt" and abandon opening the file. This runs at
database-open time — precisely when a network-share lock is most likely to be held by another
user. **Failure scenario:** user opens a `.ddb` on a busy share, hits a transient lock, and
Timelapse tells them their database is corrupt instead of retrying or reporting the real
cause.

**Fixed:** added a `Busy`/`Locked`-only retry loop (same short budget as finding #1's read-path
fix — 5 attempts, 250ms steps, ~2.5s max), narrowly scoped so any *other* `SQLiteException`
(genuine corruption, format errors) falls straight through to the generic catch without
retrying. On retry exhaustion, logs via `AppLog.Warning` that the failure was contention, not
corruption; the generic catch (real corruption) logs as such. Contract unchanged — still
returns `bool`, callers untouched.

**Verified empirically** (deterministic, unlike finding #1's lock-contention test): a valid
scratch database returned `true` in 16ms; a genuinely corrupt file (plain garbage text, not a
SQLite file at all) returned `false` in 22ms — confirming the exception filter correctly
distinguishes the two cases and does **not** delay a real corruption diagnosis with retries
(a mistakenly-broad filter would have shown ~1250ms+, not 22ms).

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

### 7. Un-migrated background-thread bulk writes that could safely have opted into the extended budget but weren't touched — FIXED

All candidates were individually traced to confirm they genuinely run inside `Task.Run` (not
inferred from surrounding `BusyCancelIndicator`/dialog pattern alone) before opting anything in
— this surfaced a real fan-in hazard along the way (see below).

**`UpdateAdjustedFileTimes` (`FileDatabaseUpdate.cs:437`)** — all 4 callers confirmed
background: `DateTimeFixedCorrection.xaml.cs:117`, `DateTimeLinearCorrection.xaml.cs:161/167`,
`DateTimeDaylightSavingsCorrection.xaml.cs:115` (found during verification, not in the
original candidate list), all via a `DatabaseUpdateFileDates` helper called from inside each
file's own `Task.Run`. Opted in directly.

**`UpdateExchangeDayAndMonthInFileDates` (`FileDatabaseUpdate.cs:485`)** — sole caller
`DateTimeCorrectAmbiguous.xaml.cs:170`, confirmed background. Opted in directly.

**`UpdateFiles(List<ColumnTuplesWithWhere>)` (`FileDatabaseUpdate.cs:151`) — the fan-in hazard.**
This one has **11 callers**, not the single site originally listed. Checking all of them found
**10 confirmed background** (`DeleteImages.xaml.cs:409`, `CsvReaderWriter.cs:833` and `:1288`,
`DateTimeRereadFromFiles.xaml.cs:238`, `FileMetadataPopulateAll.xaml.cs:232`,
`FileMetadataPopulateDatesOnly.xaml.cs:216`, `PopulateCamtrapDataFields.xaml.cs:278`,
`PopulateFieldWithEpisodeData.xaml.cs:214`, `PopulateFieldWithGUID.xaml.cs:177`,
`DarkImagesThreshold.xaml.cs:350`) — but **1 confirmed UI-thread-synchronous**:
`ControlsDataEntry/DataEntryHandler.cs:1035` (`DateTimeUpdate`, a plain method with no
`Task.Run`/`await`, called directly from a date/time picker's value-changed event on every
single edit). Extending the shared method's *default* behavior would have silently given that
per-edit UI-thread write the long ~7s budget — exactly the freeze-inducing mistake this whole
effort exists to avoid, on a method that runs on literally every date/time field edit.

**Fix applied:** added an opt-in `int busyTimeoutMs = 0` parameter to
`UpdateFiles(List<ColumnTuplesWithWhere>, int busyTimeoutMs = 0)` — default preserves current
(short-budget) behavior for every caller including `DataEntryHandler.cs`, which was left
completely untouched. Only the 10 confirmed-background call sites now explicitly pass
`ThrottleValues.BackgroundWriteExtendedBusyTimeoutMs`.
  as following the same `Task.Run`+dialog pattern used throughout `src/Timelapse/Dialog/`.

**Recommendation:** audit all `Task.Run`-wrapped database-mutation call sites in
`src/Timelapse/Dialog/` for the same opt-in, not just the ones the original bug report
happened to touch.

### 8. No global `UnobservedTaskException`/`AppDomain.UnhandledException` safety net for the new fire-and-forget tasks — FIXED

`ExecuteNonQueryWithRollbackCore` does have a genuine catch-all (`catch (Exception)`), so the
discarded task in `DropSessionTempTables` won't leak an exception under normal SQLite failure
modes today. However, no `TaskScheduler.UnobservedTaskException` or
`AppDomain.UnhandledException` handler existed anywhere in the app. Given the growing reliance
on fire-and-forget tasks (`DropSessionTempTables`, marker `UpdateFileAsync` calls), a future
exception escaping one of them would have vanished with zero diagnostic trail.

**Fixed:** `App.xaml.cs` now has an `OnStartup` override registering
`TaskScheduler.UnobservedTaskException`, logging via `AppLog.Warning` and calling
`args.SetObserved()`. Diagnostics-only, as expected — on this project's target framework
(`net10.0-windows`), unobserved task exceptions don't crash the process by default, so this
doesn't change failure behavior, only ensures a future silent failure leaves a log trace.

### 9. Retry-duration doc/comment mismatch (not functional, but misleading) — FIXED

Covered in the "What the commit fixed" section above — the code comment and commit message
both said "~9s" / "2000ms max" for the extended budget; the actual arithmetic gives ~7s /
1750ms max. **Resolved:** corrected the comment at `SQLiteWrapper.cs:938-942` to state the
real numbers (8 attempts, up to 1750ms/step, ~7s total) rather than changing
`maxBusyAttempt` to match the originally-stated (incorrect) ~9s — chosen because it's a
zero-behavior-change documentation fix rather than a real, if small, change to
already-working retry logic.

### 10. Mailto error-log feature: separate, pre-existing bug (not caused by the SQLite fix) — IMPLEMENTED, awaiting manual test

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

**Resolved (implemented in `TimelapseMenuHelp.cs`, `MenuItemEmailErrorLog_Click`):** stop
putting the log content in the `mailto:` URL at all — any length cap is guessing at a moving,
OS/mail-client-dependent target and still throws away content. A true auto-attached file was
considered and rejected: `mailto:` has no attachment mechanism in its spec (RFC 6068), and the
only way to get a real auto-attach is legacy Simple MAPI (`MAPISendMail`), which modern
default-mail setups (Windows 11 Mail, webmail-as-default, Thunderbird without a MAPI plugin)
generally don't support — not worth the added P/Invoke complexity for partial coverage.
Implemented instead:
1. Copy `logContents` to the clipboard using the pattern already proven elsewhere in this
   codebase — `Dialog/ExceptionShutdownDialog.xaml.cs:150-155`
   (`Clipboard.Clear(); Clipboard.SetText(...)`).
2. Reveal the log file selected in File Explorer via the existing
   `ProcessExecution.TryProcessStartUsingFileExplorerToSelectFile(logPath)`
   (`ProcessExecution.cs:126-140`) — lets the user drag the actual file in as a real attachment
   instead of pasted text, if their mail client makes that easier.
3. Build the `mailto:` URI with just the subject and a short, fixed instructional body
   mentioning both the clipboard copy and the Explorer selection, not the log itself — this
   keeps the URI a small, fixed size so it can no longer overflow `ShellExecute`'s length
   ceiling.
4. The existing failure-path `MessageBox` (was lines 249-251) now also mentions both fallbacks,
   so the user has a path forward even if the mailto launch itself fails.

This removes the failure mode entirely (fixed-size URL can't overflow) rather than just
delaying it, reuses two patterns already proven in this codebase, and gives the user a choice
of attach-the-real-file or paste-the-text depending on what their mail client supports. Build
verified (0 warnings/errors); manual end-to-end test (does the mail client open with subject
filled, log on clipboard, and Explorer showing the file selected) still pending.

### 11. `ResetIDsAndVacuum` permanently switches the database to WAL journal mode — likely a root-cause contributor — FIXED and verified

`src/Timelapse/Database/FileDatabaseResetIdAndVacuum.cs:93`, inside `GetPreTransactionStatements()`:
```csharp
$"{Sql.PragmaJournalModeWall}",   // Sql.cs:122 → "PRAGMA journal_mode = WAL"
```
alongside `PragmaSynchronousNormal`, `PragmaTempStoreMemory`, and `PragmaCacheSize`, all
labeled "Performance pragmas — safe to set any time." That label is true for three of the four,
but **not for `journal_mode`**: unlike `synchronous`, `temp_store`, `cache_size`, and
`foreign_keys` (all per-connection settings that reset to defaults the moment the connection
closes), `journal_mode` is a **persistent, database-file-level property** stored in the file's
own header. Once set to WAL, it stays WAL for every future connection to that file, by every
future version of the app, until something explicitly issues `PRAGMA journal_mode = DELETE` (or
another non-WAL mode) — which nothing in this codebase ever does. Confirmed via full-file read
of `FileDatabaseResetIdAndVacuum.cs`: `GetPostTransactionStatements()` only restores
`foreign_keys = ON` and drops temp tables; journal mode is never reverted, anywhere.

`ResetIDsAndVacuum` is not a rare, manually-invoked maintenance action — per this file's own
header comment, it runs automatically "when a large ID value is detected when loading the
database" or "when a merge check-in completes." Both are ordinary events for an actively-used
project. So in practice, most databases that have been in use for a while, merged, or had files
deleted in bulk have likely already been silently, permanently converted to WAL mode, with zero
user-facing indication that anything changed.

**Why this matters for the exact bug this whole investigation started from:** SQLite's own
documentation explicitly warns that WAL mode requires shared-memory-mapped file support (the
companion `-shm`/`-wal` files) that many network filesystems do not implement correctly, and
recommends against using WAL-mode databases on network shares — recommending the traditional
rollback journal instead for exactly that deployment. The original bug report's database lived
at `W:\Projects\16P0245_HD_Murray_River\Wildlife Cams\Photos\2026\...` — a mapped network
drive. If that database had ever been through `ResetIDsAndVacuum` (plausible for an
actively-used, multi-contributor wildlife-camera project with periodic merges), it would have
been running in WAL mode over a network share ever since — a configuration SQLite itself says
not to use. This could plausibly be a bigger, more direct contributor to the "database is
locked" reports than plain lock-contention timing, and would explain why the errors read as
somewhat erratic/inconsistent rather than a clean, deterministic contention pattern.

**Provenance check (requested by the user):** this WAL pragma is not something introduced by
Claude in any session, including the July 11 reference commit. Traced via `git log -p -S` to
commit `711ee0053f55dfc3f9af618d76fe525bf7f75366`, dated **2025-12-11**, titled "Repository
starts with version 2.4.0.1..." — the repository's very first commit. It's original,
human-authored legacy code, predating any AI-assisted work on this project by over seven
months.

**Resolved — Option A implemented (stop using WAL entirely for this operation):**
- `FileDatabaseResetIdAndVacuum.cs:93` — removed the `PragmaJournalModeWall` line from
  `GetPreTransactionStatements()`. The other three performance pragmas
  (`synchronous=NORMAL`, `temp_store=MEMORY`, `cache_size`) remain and provide the bulk-operation
  speedup without the permanent, file-level mode change. `ResetIDsAndVacuum` no longer converts
  any database to WAL going forward.
- **Migration for already-affected databases:** added `RevertJournalModeFromWalIfNeeded`
  (`FileDatabase.cs`), called from the existing `UpgradeDatabasesForBackwardsCompatabilityAsync`
  `Task.Run` block (`FileDatabase.cs:906-942`) — the same unconditional, on-every-open
  "IfNeeded" check sequence already used for schema migrations (`AddExportToCSVColumnIfNeeded`,
  `AddStandardToImageSetColumnIfNeeded`, etc.), which runs regardless of the template-sync
  branching elsewhere in the open flow and is already off the UI thread. Reads the current
  `journal_mode`; if (and only if) it's `wal`, reverts to `delete`. Supporting additions:
  `Sql.PragmaJournalMode` (bare read form) and `Sql.PragmaJournalModeDelete` in `Sql.cs`, and
  `SQLiteWrapper.ScalarGetScalarFromSelectAsString(query)` (mirrors the existing
  `ScalarGetScalarFromSelectAsInt`/`AsLong` wrappers, reusing the same already-hardened private
  `GetScalarFromSelect` helper — including this session's new Busy/Locked read retry).
- **Performance characteristics (asked about explicitly):** the check runs once per database
  open. For the common case (mode already non-WAL — true for every database going forward, and
  for any never affected by the old bug) it's one cheap scalar read, then return — negligible,
  same league as its sibling checks. For a genuinely WAL-affected database, the one-time revert
  forces a real SQLite checkpoint (flushes WAL contents into the main file, removes the
  `-wal`/`-shm` companion files) — a real but bounded, one-time-per-database cost, off the UI
  thread. After that single successful revert, every subsequent open of that same database is
  back to the cheap no-op path, permanently.
- **Verified empirically** (not just build-verified): a standalone test — create a scratch db,
  force it to `wal`, run the exact same two calls `RevertJournalModeFromWalIfNeeded` makes —
  confirmed: mode before = `wal`; revert call returned `delete`; mode after = `delete`; the
  `-wal`/`-shm` companion files were actually removed (confirming a real checkpoint occurred, not
  just a flag change); a second check on the now-reverted database confirmed the cheap
  no-op path. Full pass, no ambiguity (unlike the lock-contention test for finding #1).

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
3. Status: Phase 1 done, Phase 2 in progress (paused). See "Implementation Plan" below.
4. **Resolved:** finding #11 (WAL mode) — Option A implemented (stopped setting WAL in
   `ResetIDsAndVacuum`), plus an on-open migration for already-affected existing databases,
   both verified empirically. See finding #11 for full detail.
5. **New, unresolved — finding #1 verification:** the read-path retry code is written and
   builds clean, but live-contention testing didn't manage to reproduce a real `SQLITE_BUSY`/
   `LOCKED` exception to confirm the new catch branches actually fire (see the Phase 2 status
   note above). Options going forward: try a lower-level repro (e.g. a raw Win32 file lock
   instead of SQLite-level contention, to more literally simulate "another process/network
   share has this file locked"), accept code-review-level confidence and move on, or revisit
   once #11 is resolved (since #11 may turn out to be the more direct explanation for the
   original read failures, making further contention-timing tuning less urgent by comparison).

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
4. **#10 — Mailto: use the clipboard + Explorer-reveal instead of an oversized URL body.
   DONE, awaiting manual test.** In `TimelapseMenuHelp.cs:219-253`
   (`MenuItemEmailErrorLog_Click`): copy `logContents` to the clipboard using the existing
   pattern at `Dialog/ExceptionShutdownDialog.xaml.cs:150-155`
   (`Clipboard.Clear(); Clipboard.SetText(...)`); also reveal the log file selected in File
   Explorer via `ProcessExecution.TryProcessStartUsingFileExplorerToSelectFile(logPath)`
   (`ProcessExecution.cs:126-140`) so the user can attach the real file instead of pasting
   text; build the `mailto:` URI with just the subject and a short fixed instructional body
   mentioning both fallbacks, not the log content itself, so the URI can no longer overflow
   `ShellExecute`'s length ceiling; the existing failure-path `MessageBox` now also mentions
   both fallbacks. Build verified; manual test (open Help > Email Error Log and confirm mail
   client opens, clipboard has the log, Explorer shows the file selected) still pending.

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

1. **#1 — DONE, accepted at code-review-level confidence.** Added a `Busy`/`Locked` branch to
   the exception filter in `GetDataTableFromSelect`, `GetDataTableFromSelectAsync`,
   `GetDistinctValuesInColumn`, and `GetScalarFromSelect` in `SQLiteWrapper.cs`, using the short
   budget (matches the write path's default 5-attempt/~2.5s schedule), preserving the
   `_errorFired` single-fire gate unchanged. Live-contention testing never reproduced a real
   `Busy`/`Locked` exception (three attempts), so this was accepted on code-review-level
   confidence rather than empirical proof — see finding #1 for the full reasoning.
2. **#2 — `PragmaGetQuickCheck` misdiagnosis. DONE, verified empirically.** Replaced the bare
   `catch { return false; }` in `SQLiteWrapper.cs` with one that retries `Busy`/`Locked` briefly
   (same short budget as #1) then logs via `AppLog` on exhaustion, while any other SQLite error
   code (genuine corruption) falls through immediately without retrying and logs as such. No
   contract change for callers (`FileDatabase.cs:802`, `CommonDatabase.cs:151`). Verified with a
   valid DB (true, 16ms) and a genuinely corrupt file (false, 22ms — confirming corruption isn't
   delayed by the retry loop).
3. **#7 — DONE.** Every candidate individually traced to confirm `Task.Run` backgrounding
   before opting in (never inferred from file name/pattern alone). `UpdateAdjustedFileTimes`
   and `UpdateExchangeDayAndMonthInFileDates` opted in directly (all callers confirmed
   background). `UpdateFiles(List<ColumnTuplesWithWhere>)` turned out to have 11 callers, not
   1 — 10 confirmed background (opted in via a new `busyTimeoutMs = 0` default parameter,
   explicitly passed only at those 10 call sites) and 1 confirmed UI-thread-synchronous
   (`DataEntryHandler.cs:1035`, left untouched on the default short budget). See finding #7
   for the full site list and the fan-in hazard this verification step caught.
4. **#8 — DONE.** `App.xaml.cs` now has an `OnStartup` override registering
   `TaskScheduler.UnobservedTaskException`, logging via `AppLog.Warning` and calling
   `args.SetObserved()`. Diagnostics only, as expected — doesn't change failure behavior.

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

### Phase 3 — findings #5, #6 — DONE

`UpdateSyncImageSetToDatabase` (`FileDatabaseUpdate.cs`) and `RepairClassificationCategoriesIfNeeded`
(`FileDatabase.cs`) both used to go straight from automatic-retry-exhaustion to the fatal
`TimelapseNeedsToShutDownDataWriteErrorDialog`, with no intermediate chance to retry.

**Decided design (2026-07-14), in three rounds of clarification:**
1. Automatic short-budget retries (~2.5s, inside `Database.Update`/`ExecuteNonQueryWithRollback`)
   already happened before either method's caller ever saw a failure — this was already true
   before this fix and needed no change.
2. On failure, show a new dialog — **exactly one "Retry" button**, no "Ignore and continue."
   Closing the dialog (X, Esc, clicking away) is treated the same as a failed retry.
3. **Capped at one manual retry total** — if that single retry also fails (or the user closes
   the dialog instead of retrying), fall through to the existing fatal dialog, unchanged.
4. Same treatment at both call sites and regardless of close-time vs. in-session context (no
   special-casing `TimelapseClosing.cs` vs. e.g. `TimelapseMenuSort.cs`) — since the logic lives
   entirely inside the two shared methods, every caller of `UpdateSyncImageSetToDatabase`
   (`TimelapseMenuSort.cs`, `TimelapseQuickPaste.cs`, `TimelapseMenuEdit.cs` ×2,
   `TimelapseImageSetLoading.cs`, `TimelapseCheckAndCorrectFolders.cs`, `TimelapseClosing.cs`)
   gets the new behavior automatically and consistently, with no per-call-site changes needed.

**Implemented:**
- New `Dialogs.TimelapseOperationRetryDialog(Window owner, string operationDescription, SqlOperationResult result)`
  in `Dialogs.cs`, modeled on the existing `TimelapseReadErrorNoticeDialog` (same
  `FormattedDialog`/`MessageBoxButtonType.OK` scaffolding), with the OK button relabeled
  `"Retry"`. Returns `bool?` — `true` only if Retry was clicked; `null`/`false` (including
  window-close) means "proceed to the fatal dialog."
- Both `UpdateSyncImageSetToDatabase` and `RepairClassificationCategoriesIfNeeded` now: on
  failure, show this dialog; if `true`, re-run the exact same write/repair statement once; if
  that still fails, fall through to `TimelapseNeedsToShutDownDataWriteErrorDialog` exactly as
  before.

Build verified (0 warnings/errors). Manual end-to-end test (trigger a failure, click Retry,
confirm one more attempt, confirm fatal dialog still appears if that fails too) not yet done —
would need a real or simulated lock-contention scenario to trigger the failure path in the
first place.

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
