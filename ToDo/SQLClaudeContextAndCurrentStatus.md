# SQLiteWrapper Refactoring Plan — Context & Status

**File under analysis:** `src/Timelapse/Database/SQLiteWrapper.cs`
**Second copy (not covered here):** `src/UtilityPrograms/DialogUpgradeFiles/.../Database/SQLiteWrapper.cs`
**Date created:** 2026-03-08
**Status:** Phase 1 complete. Redundancy review complete. Phase 2 in progress.

---

## Overview

`SQLiteWrapper` is Timelapse's primary interface to all SQLite databases. It is schema-agnostic (the higher-level `FileDatabase` builds Timelapse-specific queries and calls this wrapper). The class currently has:

- No unified error propagation to callers (most failures are logged and swallowed)
- Partial, inconsistent progress support (only `Insert` and `ExecuteNonQueryWrappedInBeginEnd` have it)
- No cancellation support anywhere
- Several bugs, performance inefficiencies, and code redundancies

---

## Method Inventory (complete)

| Method | Region | Visibility | Long-running? | Has Progress? | Has Error Return? |
|--------|--------|------------|---------------|---------------|-------------------|
| `SQLiteWrapper(string inputFile)` | Constructor | public | No | — | No |
| `CreateTable` | Create Table | public | No | No | No (swallows) |
| `IndexExists` | Indexes | public | No | No | No |
| `IndexDropIfExists` | Indexes | public | No | No | No |
| `IndexCreateIfNotExists(single)` | Indexes | public | No | No | No |
| `IndexCreateIfNotExists(list)` | Indexes | public | No | No | No |
| `DataTableColumns_Changed` | Event Handlers | private | No | — | — |
| `GetDataTableFromSelect` | Select | public | **Potentially** | No | No (returns empty DT) |
| `GetDataTableFromSelectAsync` | Select | public | **Yes** | No | No (returns empty DT) |
| `GetDistinctValuesInColumn` | Select | public | **Potentially** | No | **No handler at all** |
| `GetScalarFromSelect` | Select | private | No | No | No (returns null) |
| `Insert(2-param)` | Insert | public | **Yes** | No (delegates) | No |
| `Insert(5-param)` | Insert | public | **Yes** | **Yes** | No |
| `UpsertRow` | Upsert | public | No | No | No |
| `SetColumnToACommonValue` | Update | public | No | No | No |
| `TrimWhitespace` | Update | public | **Potentially** | No | No |
| `UpdateParticularColumnValuesWithNewValues` | Update | public | **Potentially** | No | No |
| `ChangeNullToEmptyString` | Update | public | **Potentially** | No | No |
| `Update(List<ColumnTuplesWithWhere>)` | Update | public | **Yes** | No | No |
| `Update(ColumnTuplesWithWhere)` | Update | public | No | No | No |
| `Update(ColumnTuple)` | Update | public | No | No | No |
| `Update(listOfIDs)` | Update | public | **Yes** | No | No |
| `CreateUpdateQuery` | Update | private static | No | — | — |
| `DeleteRows` | Delete | public | No | No | No — **BUG** |
| `Delete(List<string>)` | Delete | public | **Potentially** | No | No |
| `DeleteAllRowsInTables` | Delete | public | No | No | No |
| `ScalarGetScalarFromSelectAsInt` | Scalars | public | No | No | No |
| `ScalarGetScalarFromSelectAsLong` | Scalars | public | No | No | No |
| `ScalarBoolFromOneOrZero` | Scalars | public | No | No | **BUG (null-crash)** |
| `ScalarGetMaxValueAsLong` | Scalars | public | No | No | No |
| `ScalarGetFloatValue` | Scalars | public | No | No | No |
| `ExecuteNonQuery` | Execute | public | No | No | No |
| `ExecuteNonQueryWrappedInBeginEnd(1-param)` | Execute | public | **Yes** | No (delegates) | No |
| `ExecuteNonQueryWrappedInBeginEnd(4-param)` | Execute | public | **Yes** | **Yes** | No |
| `ExecuteTransactionWithRollback` | Execute | public | No | No | **Yes (bool)** |
| `GetSchema` | Schema | private static | No | — | — |
| `GetSchemaColumnDefinitions` | Schema | private static | No | — | — |
| `GetSchemaColumnNamesAsList` | Schema | private static | No | — | — |
| `GetSchemaColumnNamesAsString` | Schema | private static | No | — | — |
| `CopyAllValuesFromTable` | Copy | private static | **Yes** | No | No |
| `CopyAllValuesBetweenTables` | Copy | private static | **Yes** | No | No |
| `SchemaRenameTable(public)` | Schema Changes | public | No | No | No (rethrows) |
| `SchemaAlterTableWithNewColumnDefinitions` | Schema Changes | public | **Yes** | No | No (rethrows) |
| `SchemaGetColumns` | Schema Changes | public | No | No | No (returns null) |
| `SchemaGetColumnsAndDefaultValues` | Schema Changes | public | No | No | No (returns null) |
| `SchemaIsColumnInTable` | Schema Changes | public | No | No | No (returns false) |
| `SchemaAddColumnToEndOfTable(public)` | Schema Changes | public | No | No | No |
| `SchemaAddColumnToTable` | Schema Changes | public | **Potentially** | No | No (rethrows) |
| `SchemaDeleteColumn` | Schema Changes | public | **Potentially** | No | No (rethrows) |
| `SchemaRenameColumn` | Schema Changes | public | No | No | No |
| `SchemaAlterColumn` | Schema Changes | public | **Potentially** | No | No (rethrows) |
| `SchemaCloneButAlterColumn` | Schema Changes | private static | No | — | — |
| `SchemaAddColumnToEndOfTable(private)` | Schema Changes | private static | No | — | — |
| `SchemaInsertColumn` | Schema Changes | private static | No | — | — |
| `SchemaRemoveColumn` | Schema Changes | private static | No | — | — |
| `SchemaRenameTable(private)` | Schema Changes | private static | No | — | — |
| `DropTable(public)` | Drop/Vacuum | public | No | No | No |
| `DropTable(private)` | Drop/Vacuum | private static | No | — | — |
| `Vacuum` | Drop/Vacuum | public | **Yes** | No | No |
| `GetNewSqliteConnection` | Utilities | public static | No | — | — |
| `TableExists` | Table Exists | public | No | No | No |
| `TableExistsAndNotEmpty` | Table Exists | public | No | No | No |
| `TableHasContent` | Rows Exist | public | No | No | No |
| `PragmaGetQuickCheck` | Pragmas | public | No | No | No (returns false) |
| `PragmaSetForeignKeys` | Pragmas | private static | No | — | — |
| `PragmaSetDeferForeignKeys` | Pragmas | private static | No | — | **UNUSED** |
| `AddColumnToEndOfTable(2 overloads)` | Unused | private static | No | — | **UNUSED — BUG** |
| `BuildWhereListofIds` | Query Helpers | public static | No | No | No |

---

## Clarifying Questions — Answers Received (2026-03-08)

| # | Question | Answer |
|---|----------|--------|
| 1 | Error strategy preference | Strategy A (return result wrapper). Strategy B insufficient because callers need to clean up (rollback, drop temp tables) *inside* the failure path — impossible when the connection is already closed. |
| 2 | Rollback behavior on cancel | Always rollback. No flag needed. Expose the *reason* (cancelled vs failed) in the result type so callers can show appropriate UI messages. |
| 3 | BusyCancelIndicator / CancellationToken | `CancelButton_Click` calls `GlobalReferences.CancelTokenSource.Cancel()`. `Reset()` creates a new `CancellationTokenSource`. SQLiteWrapper should accept `CancellationToken token = default` as a parameter (not read `GlobalReferences` internally) — callers pass `GlobalReferences.CancelTokenSource.Token`. |
| 4 | DialogUpgradeFiles scope | Out of scope. |
| 5 | Unit tests | None exist. |
| 6 | Priority order | Error visibility (Phase 2) first, then cancellation (Phase 3). |

---

## Goal 1 — Unified Error Management

### Current State

Errors are handled in four different ways today:
1. **Silent swallow** — catch logs via `TracePrint.PrintMessage`, returns null/false/empty (most methods)
2. **Rethrow** — catch logs, then `throw;` (`SchemaRenameTable`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`, `SchemaAlterColumn`)
3. **Return bool** — `ExecuteTransactionWithRollback`
4. **No handling** — `GetDistinctValuesInColumn`, `ScalarBoolFromOneOrZero` (crash risk)

Callers currently cannot distinguish "returned empty DataTable because query returned no rows" from "returned empty DataTable because the query failed."

### Chosen Design — `SqlOperationResult` / `SqlOperationResult<T>`

Strategy B (`LastErrorMessage` property) was considered but rejected: callers need to perform cleanup (rollback, drop orphaned temp tables) *inside* the failure path. By the time a caller checks `LastErrorMessage`, the `using SQLiteConnection` block has already closed the connection — too late to act.

**`SqlOperationResult` — new file `Timelapse/Database/SqlOperationResult.cs`:**

```csharp
public enum SqlResultStatus { Success, Failed, Cancelled }

public class SqlOperationResult
{
    public SqlResultStatus Status  { get; init; }
    public bool   Success          => Status == SqlResultStatus.Success;
    public bool   WasCancelled     => Status == SqlResultStatus.Cancelled;
    public string ErrorMessage     { get; init; }
    public Exception Exception     { get; init; }

    public static SqlOperationResult Ok()
        => new() { Status = SqlResultStatus.Success };
    public static SqlOperationResult Fail(string msg, Exception ex = null)
        => new() { Status = SqlResultStatus.Failed, ErrorMessage = msg, Exception = ex };
    public static SqlOperationResult Cancel()
        => new() { Status = SqlResultStatus.Cancelled, ErrorMessage = "Operation was cancelled." };
}

public class SqlOperationResult<T> : SqlOperationResult
{
    public T Value { get; init; }
    public static SqlOperationResult<T> Ok(T value)
        => new() { Status = SqlResultStatus.Success, Value = value };
    public new static SqlOperationResult<T> Fail(string msg, Exception ex = null)
        => new() { Status = SqlResultStatus.Failed, ErrorMessage = msg, Exception = ex };
    public new static SqlOperationResult<T> Cancel()
        => new() { Status = SqlResultStatus.Cancelled, ErrorMessage = "Operation was cancelled." };
}
```

- `void` methods → return `SqlOperationResult`
- Value-returning methods → return `SqlOperationResult<T>`
- `TracePrint.PrintMessage` still called inside catch (diagnostic logging unchanged)
- Rollback happens *inside* the catch block before returning `Fail` or `Cancel`
- `WasCancelled` lets callers show "cancelled" UI vs "error" dialog without string-matching
- No rollback flag needed — always rollback; distinguish reason via `Status`

---

## Goal 2 — Progress Management with CancellationToken

### Current State

- `ExecuteNonQueryWrappedInBeginEnd(4-param)` and `Insert(5-param)` have `IProgress<ProgressBarArguments>` support but **no `CancellationToken`**
- When a cancellation occurs mid-batch, the transaction is not rolled back—SQLite will commit whatever was completed when the connection is closed
- No schema-altering method supports progress or cancellation
- `GetDataTableFromSelectAsync` is async but provides no progress or cancellation

### Methods Needing Progress + Cancellation

**High priority (most likely to take significant time on real data):**

| Method | Why Long-Running | Recommended Change |
|--------|-----------------|-------------------|
| `ExecuteNonQueryWrappedInBeginEnd` | Batches up to 50,000 statements per transaction | Add `CancellationToken`; rollback on cancel |
| `Insert(5-param)` | Delegates to above | Add `CancellationToken` passthrough |
| `Update(List<ColumnTuplesWithWhere>)` | Delegates to batch execute | Add progress + cancel overload |
| `Update(listOfIDs)` | Single query but can affect many rows | Possibly wrap with timeout |
| `SchemaAlterTableWithNewColumnDefinitions` | Copies entire table | Add progress + cancel |
| `SchemaAddColumnToTable` | Copies entire table | Add progress + cancel |
| `SchemaDeleteColumn` | Copies entire table | Add progress + cancel |
| `SchemaAlterColumn` | Copies entire table | Add progress + cancel |
| `GetDataTableFromSelectAsync` | Queries can return millions of rows | Add `CancellationToken` |
| `Vacuum` | Can be long on large DB | Add progress (indeterminate) + cancel |

**Low priority (fast in practice):**
`TrimWhitespace`, `UpdateParticularColumnValuesWithNewValues`, `ChangeNullToEmptyString`, `Delete(List<string>)` — these delegate to `ExecuteNonQueryWrappedInBeginEnd`, so cancellation support would flow through automatically once the core method is updated.

### How CancellationToken Works in Timelapse

`BusyCancelIndicator.CancelButton_Click` calls `GlobalReferences.CancelTokenSource.Cancel()`.
`Reset()` / `ResetAndEnableImmediately()` create a fresh `CancellationTokenSource` each time.
Callers access the token via `GlobalReferences.CancelTokenSource.Token`.

**Design decision:** `SQLiteWrapper` accepts `CancellationToken token = default` as a parameter — it does NOT read `GlobalReferences` directly. This keeps the wrapper decoupled from Timelapse UI infrastructure. `FileDatabase` and other callers pass `GlobalReferences.CancelTokenSource.Token` explicitly:

```csharp
// FileDatabase caller:
var result = Database.ExecuteNonQueryWrappedInBeginEnd(
    queries, progress, msg, freq,
    GlobalReferences.CancelTokenSource.Token);

// Existing callers with no token still compile unchanged:
Database.ExecuteNonQueryWrappedInBeginEnd(queries);
```

### Proposed Approach

1. Add `CancellationToken token = default` to `ExecuteNonQueryWrappedInBeginEnd` (4-param overload)
2. At the top of each loop iteration, call `token.ThrowIfCancellationRequested()`
3. Catch `OperationCanceledException` separately from `Exception`; rollback current transaction, return `SqlOperationResult.Cancel()`
4. For schema-cloning operations, run `SELECT COUNT(*)` before the copy to enable row-level progress
5. Cascade `token` parameter to `Insert(5-param)`, `Update(List<>)`, and schema-cloning methods

---

## Goal 3 — Performance Efficiencies and Redundancies

### Confirmed Bugs

#### BUG-1: `DeleteRows` executes the DELETE twice (line 589–591)
```csharp
GetDataTableFromSelect(query);   // ← runs the DELETE via ExecuteReader — WRONG
ExecuteNonQuery(query);          // ← runs the DELETE again
```
`GetDataTableFromSelect` opens a connection and calls `command.ExecuteReader()`. SQLite will execute a DELETE statement through `ExecuteReader` just as well as through `ExecuteNonQuery`. The result is that the delete runs twice. The first call should simply be removed.
**Affected:** Any caller of `DeleteRows`. Low risk of data loss (deleting already-deleted rows is a no-op), but it wastes a round-trip and could expose a race condition in concurrent scenarios.

#### BUG-2: `ScalarBoolFromOneOrZero` will throw NullReferenceException on query failure (line 684–686)
```csharp
public bool ScalarBoolFromOneOrZero(string query)
{
    return (Convert.ToInt32(GetScalarFromSelect(query)) == 1);
}
```
`GetScalarFromSelect` returns `null` when an exception occurs. `Convert.ToInt32(null)` returns 0 rather than throwing, so technically this won't crash—but it silently returns `false`, misrepresenting the outcome as "no match found" instead of "query failed." Should check for null before converting.

#### BUG-3: `AddColumnToEndOfTable` (unused, line 1657–1665) — inverted condition
```csharp
if (string.IsNullOrEmpty(otherOptions))   // ← should be !IsNullOrEmpty
{
    columnDefinition += " " + otherOptions;
}
```
This appends `otherOptions` only when it's empty. The entire method is in the `#region Unused methods` pragma-suppressed section, so it has no runtime impact today. **Recommendation:** fix or delete.

### Performance Issues

#### PERF-1: String concatenation in loops (O(n²) behavior)
**Affected methods:** `Insert(5-param)` (lines 318–333), `UpsertRow` (lines 363–379), `CreateUpdateQuery` (lines 550–562)

Each of these builds query strings using `+=` inside a `foreach`. For large batches this is quadratic. All should use `StringBuilder` or pre-build columns/values lists and use `string.Join(", ", ...)`.

**Example fix for `Insert`:**
```csharp
// Current:
string columns = string.Empty;
foreach (ColumnTuple column in columnsToUpdate)
    columns += $" {column.Name}" + Sql.Comma;
columns = columns[..^Sql.Comma.Length];

// Better:
string columns = string.Join(Sql.Comma, columnsToUpdate.Select(c => $" {c.Name}"));
```

Estimated impact: notable only when inserting/updating thousands of columns per row, which is rare. Low priority but easy to fix.

#### PERF-2: `GetSchemaColumnNamesAsString` creates an intermediate list unnecessarily (line 1002–1005)
`GetSchemaColumnNamesAsString` calls `GetSchemaColumnNamesAsList` and then joins the list. Could instead read the reader directly and join. Minor.

#### PERF-3: `GetDataTableFromSelectAsync` duplicates `GetDataTableFromSelect` entirely (lines 171–197)
The async version wraps the exact same code in `Task.Run`. It should simply call the synchronous version:
```csharp
public async Task<DataTable> GetDataTableFromSelectAsync(string query)
    => await Task.Run(() => GetDataTableFromSelect(query));
```
This removes ~20 lines of duplicate code.

#### PERF-4: `SchemaAddColumnToTable` (public, line 1193) calls `SchemaAddColumnToEndOfTable` (public) when at end
When the new column position >= existing column count, it calls the public `SchemaAddColumnToEndOfTable`, which creates a new connection object—even though we already have an open connection (line 1075). It should call the private static overload that accepts the existing connection.

#### PERF-5: `CopyAllValuesFromTable` vs `CopyAllValuesBetweenTables` — near-identical logic (lines 1012–1036)
`CopyAllValuesFromTable` is `CopyAllValuesBetweenTables` where `schemaFromSourceTable == schemaFromDestinationTable`. The former could simply call the latter:
```csharp
private static void CopyAllValuesFromTable(SQLiteConnection connection, string schemaFromTable, string dataSourceTable, string dataDestinationTable)
    => CopyAllValuesBetweenTables(connection, schemaFromTable, schemaFromTable, dataSourceTable, dataDestinationTable);
```

### Redundancies and Design Issues

#### REDUND-1: `GetSchemaColumnDefinitions` and `SchemaCloneButAlterColumn` both iterate PRAGMA TABLE_INFO with similar field-by-field logic (lines 930–977, 1354–1425)
Both read the same PRAGMA fields in a switch statement. The common logic should be extracted to a private helper. Moderate refactor, reduces maintenance risk.

#### REDUND-2: Dead code in `CreateUpdateQuery` (lines 544–547)
```csharp
if (columnsToUpdate.Columns.Count < 1)  return string.Empty;   // ← exits here if count is 0
...
if (columnsToUpdate.Columns.Count < 0)  return string.Empty;   // ← UNREACHABLE (count can't be < 0 after < 1 check)
```
The second `if` block is unreachable dead code. Should be removed.

#### REDUND-3: `SchemaAlterTableWithNewColumnDefinitions` hardcodes `"TempTable"` (line 1070)
If a user table happens to be named `"TempTable"`, calling `CreateTable("TempTable", ...)` at line 1074 will silently **drop and recreate** that table (see `CreateTable` lines 70–73). Should use a GUID suffix:
```csharp
string destTable = $"_TempTable_{Guid.NewGuid():N}";
```
Similarly, `SchemaAddColumnToTable` uses `tableName + "NEW"` (line 1206) and `SchemaDeleteColumn` uses `sourceTable + "NEW"` (line 1255). If a table named `"DataTableNEW"` exists, it would be dropped silently.

#### REDUND-4: `ExecuteNonQueryWrappedInBeginEnd` — off-by-one and duplicated percent calculation (lines 787–830)
- `statementsInQuery > MaxStatementCount` should be `>= MaxStatementCount` if the intent is to batch exactly `MaxStatementCount` statements
- The `percent` calculation at the batch boundary (line 824) is identical to the one at the loop top (line 789) — one could be removed

#### REDUND-5: `GetDistinctValuesInColumn` has no exception handling (lines 257–269)
All other Select-type methods have try/catch. This one does not, so an exception propagates to the caller. Should add consistent handling.

#### REDUND-6: `Thread.Sleep` during progress reporting (lines 775, 792, 827)
Using `Thread.Sleep(ThrottleValues.RenderingBackoffTime)` inside a background worker is fragile. If this runs on the UI thread it will block it. The sleep should be verified to only occur off the UI thread, or replaced with `await Task.Delay(...)` in an async context.

---

## Proposed Step-by-Step Plan

### Phase 1 — Bug Fixes (Low Risk, High Value, No API Changes)

**Step 1.1 — Fix `DeleteRows` double-execution bug**
- Remove the `GetDataTableFromSelect(query)` call on line 589
- Outcome: DELETE runs exactly once
- Risk: None — removing a spurious call
- Methods affected: `DeleteRows`

**Step 1.2 — Fix `ScalarBoolFromOneOrZero` silent failure**
- Add null/DBNull guard before `Convert.ToInt32`
- Outcome: Returns false and optionally logs when query failed
- Risk: None
- Methods affected: `ScalarBoolFromOneOrZero`

**Step 1.3 — Fix or delete unused `AddColumnToEndOfTable` inverted condition**
- Either correct the `if (!string.IsNullOrEmpty(...))` or delete the method
- Outcome: No runtime impact (method is unused); code quality improved
- Risk: None
- Methods affected: `AddColumnToEndOfTable` (private, unused)

**Step 1.4 — Remove dead code in `CreateUpdateQuery`**
- Remove the unreachable `if (columnsToUpdate.Columns.Count < 0)` block
- Outcome: Cleaner code
- Risk: None
- Methods affected: `CreateUpdateQuery`

**Step 1.5 — Fix `GetDistinctValuesInColumn` — add exception handling**
- Wrap in try/catch consistent with other Select methods
- Outcome: Exceptions no longer propagate uncaught
- Risk: None
- Methods affected: `GetDistinctValuesInColumn`

**Step 1.6 — Fix temp table naming in schema-cloning operations**
- Replace `"TempTable"` with `$"_TempTable_{Guid.NewGuid():N}"` in `SchemaAlterTableWithNewColumnDefinitions`
- Replace `tableName + "NEW"` with `$"{tableName}_NEW_{Guid.NewGuid():N}"` in `SchemaAddColumnToTable` and `SchemaDeleteColumn`
- Outcome: No silent data loss if a table with the same temp name exists
- Risk: Very low (GUID ensures uniqueness)
- Methods affected: `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`

---

### Phase 2 — Error Management (Significant API Change)

**Step 2.1 — Define `SqlOperationResult` / `SqlOperationResult<T>`**
- Add to `Timelapse.Database` namespace (new file `SqlOperationResult.cs`)
- Outcome: Return type infrastructure available
- Risk: None (additive)

**Step 2.2 — Create `SqlOperationResult.cs` and define the return types**
- New file in `Timelapse/Database/`
- Outcome: Return type infrastructure available; no callers affected yet
- Risk: None (purely additive)

**Step 2.3 — Migrate the core execute methods**
- `ExecuteNonQuery`, `ExecuteNonQueryWrappedInBeginEnd`, `ExecuteTransactionWithRollback`
- These are the foundation — everything else delegates to them
- Outcome: Callers can check `result.Success`; existing error handling becomes explicit
- Risk: **High** — every caller throughout `FileDatabase` and other classes must be updated
- Note: `ExecuteTransactionWithRollback` already returns `bool`; it will be migrated to `SqlOperationResult`

**Step 2.4 — Migrate data-modification methods**
- `Insert`, `Update` (all overloads), `DeleteRows`, `Delete`, `UpsertRow`, `SetColumnToACommonValue`, `TrimWhitespace`, `UpdateParticularColumnValuesWithNewValues`, `ChangeNullToEmptyString`, `DeleteAllRowsInTables`
- Outcome: All write operations surface errors to callers
- Risk: Medium-High — callers must be updated

**Step 2.5 — Migrate schema-modification methods**
- `CreateTable`, `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`, `SchemaAlterColumn`, `SchemaRenameTable`, `SchemaRenameColumn`, `SchemaAddColumnToEndOfTable`, `DropTable`, `Vacuum`
- These currently either swallow or rethrow — all will return `SqlOperationResult`
- Outcome: Schema operations surface errors cleanly; rethrow pattern eliminated
- Risk: Medium — callers must be updated

**Step 2.6 — Migrate read methods**
- `GetDataTableFromSelect`, `GetDataTableFromSelectAsync`, `GetDistinctValuesInColumn`, all Scalar methods, `TableExists`, `TableExistsAndNotEmpty`, `TableHasContent`, `IndexExists`, `PragmaGetQuickCheck`, `SchemaGetColumns`, `SchemaGetColumnsAndDefaultValues`, `SchemaIsColumnInTable`
- Outcome: Callers can distinguish "no data" from "query failed"
- Risk: Medium — many callers; read-only so no rollback concerns

---

### Phase 3 — Progress and Cancellation

**Step 3.1 — Add `CancellationToken` to `ExecuteNonQueryWrappedInBeginEnd`**
- Add `CancellationToken token = default` to the 4-param overload
- Check `token.ThrowIfCancellationRequested()` at top of each loop iteration
- On `OperationCanceledException`: rollback the current transaction before propagating
- Update the 1-param overload to pass `CancellationToken.None`
- Outcome: Batch operations can be cancelled mid-stream with clean rollback
- Risk: Low — default token means existing callers unchanged
- Methods affected: `ExecuteNonQueryWrappedInBeginEnd` (both overloads)

**Step 3.2 — Thread through cancellation to Insert and Update batch callers**
- Add `CancellationToken` parameter to `Insert(5-param)` and `Update(List<>)`
- Pass through to `ExecuteNonQueryWrappedInBeginEnd`
- Outcome: Insert/Update batch operations become cancellable
- Risk: Low — default parameter preserves existing callers
- Methods affected: `Insert(5-param)`, `Update(List<ColumnTuplesWithWhere>)`

**Step 3.3 — Add progress + cancellation to schema-cloning operations**
- For `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`, `SchemaAlterColumn`:
  - Do a `SELECT COUNT(*)` before the copy to get row count
  - Report indeterminate progress during structure creation, then row-count-based progress during copy
  - Check cancellation token between phases; on cancel, attempt to drop temp table and return failed result
- Outcome: Long schema operations show progress and can be cancelled
- Risk: Medium — schema operations are multi-step; cancellation mid-way may leave orphaned temp tables (handle in catch block)
- Methods affected: `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`, `SchemaAlterColumn`, `CopyAllValuesFromTable`, `CopyAllValuesBetweenTables`

**Step 3.4 — Add cancellation to `GetDataTableFromSelectAsync`**
- Pass `CancellationToken` into the `Task.Run` lambda
- Use `SQLiteCommand.Cancel()` or the connection's `Interrupt()` method to abort a running query
- Outcome: Long SELECT queries can be cancelled
- Risk: Medium — `SQLiteDataReader.Load()` does not natively accept a token; may need to use `connection.Interrupt()` from another thread

**Step 3.5 — Add indeterminate progress to `Vacuum`**
- `VACUUM` cannot report row-level progress; wrap with BusyCancelIndicator in indeterminate mode
- Outcome: UI shows spinner during vacuum
- Risk: Low

---

### Phase 4 — Performance and Redundancy

**Step 4.1 — Deduplicate `GetDataTableFromSelectAsync` (easy)**
- Replace body with: `return await Task.Run(() => GetDataTableFromSelect(query));`
- Outcome: ~20 lines removed
- Risk: None

**Step 4.2 — Deduplicate `CopyAllValuesFromTable` / `CopyAllValuesBetweenTables`**
- Make `CopyAllValuesFromTable` a one-liner calling `CopyAllValuesBetweenTables`
- Outcome: Single implementation to maintain
- Risk: None

**Step 4.3 — Fix `SchemaAddColumnToTable` calling public instead of private overload at line 1193**
- Change call to private `SchemaAddColumnToEndOfTable(connection, tableName, columnDefinition.ToString())`
- Outcome: No unnecessary connection creation when adding column at end
- Risk: None

**Step 4.4 — Replace string `+=` with `string.Join` in query builders**
- `Insert(5-param)`, `UpsertRow`, `CreateUpdateQuery`
- Outcome: O(n) string building instead of O(n²)
- Risk: Low — easy to verify equivalence with unit tests

**Step 4.5 — Extract shared PRAGMA TABLE_INFO iteration to a helper**
- Consolidate the repeated field-by-field switch logic in `GetSchemaColumnDefinitions` and `SchemaCloneButAlterColumn` into a shared private helper
- Outcome: Single place to maintain schema reading logic
- Risk: Low-Medium — careful equivalence testing needed

**Step 4.6 — Fix off-by-one in `ExecuteNonQueryWrappedInBeginEnd` batch check**
- Change `statementsInQuery > MaxStatementCount` to `>= MaxStatementCount`
- Remove duplicate percent calculation
- Outcome: Batching behavior is as intended
- Risk: Low

---

## Risks Summary

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Phase 2 changes break callers throughout FileDatabase | High | Do per-method PRs; search for all callers before each change |
| Cancel mid-schema-clone leaves orphaned temp table | Medium | Always clean up in finally/catch; GUID names prevent collisions |
| `ExecuteNonQueryWrappedInBeginEnd` cancel rolls back incomplete batch | Medium (by design) | Document clearly; callers must expect partial rollback |
| `GetDataTableFromSelectAsync` cancel is not guaranteed to interrupt running query immediately | Medium | Use `connection.Interrupt()` from the cancellation callback |
| Phase 4.5 (helper extraction) introduces a regression in schema reading | Low | Write unit tests covering all schema field combinations before refactoring |

---

---

## Progress Tracking

### Phase 1 — Bug Fixes ✓ COMPLETE
- [x] 1.1 — Fix `DeleteRows` double-execution bug
- [x] 1.2 — Fix `ScalarBoolFromOneOrZero` silent failure
- [x] 1.3 — Delete unused `AddColumnToEndOfTable` overloads (inverted condition)
- [x] 1.4 — Remove dead code in `CreateUpdateQuery`
- [x] 1.5 — Add exception handling to `GetDistinctValuesInColumn`
- [x] 1.6 — Fix temp table naming (GUID-based) — 4 locations found and fixed: `SchemaAlterTableWithNewColumnDefinitions`, `SchemaAddColumnToTable`, `SchemaDeleteColumn`, `SchemaAlterColumn`

### Phase 2 — Error Management
- [ ] 2.1 — Define `SqlOperationResult` / `SqlOperationResult<T>` (new file)
- [ ] 2.2 — Create `SqlOperationResult.cs`
- [ ] 2.3 — Migrate core execute methods (`ExecuteNonQuery`, `ExecuteNonQueryWrappedInBeginEnd`, `ExecuteTransactionWithRollback`)
- [ ] 2.4 — Migrate data-modification methods
- [ ] 2.5 — Migrate schema-modification methods
- [ ] 2.6 — Migrate read methods

### Phase 3 — Cancellation and Progress
- [ ] 3.1 — `CancellationToken` in `ExecuteNonQueryWrappedInBeginEnd`
- [ ] 3.2 — Thread cancellation to `Insert` / `Update` batch callers
- [ ] 3.3 — Progress + cancel for schema-cloning operations
- [ ] 3.4 — Cancellation for `GetDataTableFromSelectAsync`
- [ ] 3.5 — Indeterminate progress for `Vacuum`

### Phase 4 — Redundancies (partially complete)
- [x] R1 / 4.1 — Deduplicate `GetDataTableFromSelectAsync` (one-liner delegation)
- [x] R2 / 4.6a — Remove duplicate percent calculation at batch boundary
- [x] R3 / 4.6b — Fix off-by-one in batch size check (`>` → `>=`)
- [~] R4 / 4.2 — `CopyAllValuesFromTable` / `CopyAllValuesBetweenTables` — **documented only, not merged**. The two methods serve structurally different purposes (same-name vs positional column mapping); merging would obscure intent and require non-trivial guard handling for minimal gain.
- [x] R5 / 4.3 — `SchemaAddColumnToTable` now calls private static overload, reusing existing connection
- [~] R6 / 4.5 — Extract shared PRAGMA TABLE_INFO iteration helper — **decided not to do**. Return types differ (`List<string>` vs `string`), attribute-substitution logic in `SchemaCloneButAlterColumn` touches every field, a shared helper would be as complex as the existing code, and there are no unit tests to catch regressions.
- [ ] 4.4 — Replace string `+=` with `string.Join` in `Insert`, `UpsertRow`, `CreateUpdateQuery`

---

*This document is the shared context for all future sessions working on this refactoring. Update the Progress Tracking section as each phase is completed. Update the Clarifying Questions section with answers as they are provided.*
