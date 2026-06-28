# Improvements Worth Doing

Code quality issues identified by professional review (2026-06-25). These are recurring patterns across the codebase, not one-off oddities. Excludes test coverage (known gap, accepted).

---

## Lower Priority (worth noting, not urgent)

### Long methods / high cyclomatic complexity
`QuickPaste.cs:152–232` (~80 lines, 4 nested loops) mixes validation, synchronization, and reconstruction. Worth splitting into smaller focused methods when touching that area.

---

## Warning: VS2026 Intellisense "Stylistic" Suggestions Break Things

**Date discovered:** 2026-06-26  
**Broken commit:** `e61d3cf` — "Stylistic changes recommended by VS2026 Intellisense" (72 files)  
**Fixed by:** `311e48c` — full revert of e61d3cf (session-commit changes in 5 overlap files were preserved manually)

### Symptoms introduced by e61d3cf
- Scroll-wheel toast ("Use Ctrl-scrollwheel to display the overview…") no longer appeared on plain scroll.
- Ctrl+scroll no longer activated the ThumbnailGridVirtualized (overview).
- Images appeared intermixed with video player controls.

### Root cause
Not pinpointed to a single line. The entire commit was reverted to fix the regressions. The most suspicious changes were the `[DllImport]`/`extern` → `[LibraryImport]`/`partial` P/Invoke conversions in:
- `NativeMethods.cs` (`GetKeyState`, `GetCursorPos`, `GetDeviceCaps`, etc.)
- `ModernNotifications.cs` (`GetCursorPos`)
- `BusyableDialogWindow.cs` (`GetSystemMenu`, `EnableMenuItem`)

`GetKeyState` is called by `NativeMethods.IsCtrlKeyDown()`, which is used in the scroll-wheel handler — a mismatch there would explain all three symptoms. However, the exact mechanism was not confirmed; the full revert resolved everything.

### Rule for future sessions
**Do not apply VS2026 Intellisense "Apply all suggestions" in bulk.** The P/Invoke `[LibraryImport]` suggestion in particular should be treated with caution until the source-generator behavior is verified against the existing `[DllImport]` behavior. All other stylistic suggestions (switch expressions, collection expressions, `is not`, `?.` for assignments) appear safe but were also reverted as part of the wholesale fix.
