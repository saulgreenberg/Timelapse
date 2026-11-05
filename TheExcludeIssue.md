# Fix: Excludes on a multi-choice field matches almost every file

This note describes the problem and its fix so that the fix can be applied to any version of Timelapse. It doesn't depend on any other change.

## The problem

In the Custom Selection dialog, a MultiChoice field offers **Includes** and **Excludes**. Includes works. Excludes filters out almost nothing: selecting "Excludes Video" still returns files whose value contains Video.

## The cause

A MultiChoice value is stored as comma-separated text, such as `Motion Image,Video`. To avoid matching part of a longer item (such as "Video" within "Bighorn Video"), the query tests the item in each of the four positions where it could appear:

| Pattern | Matches when the item is |
|---|---|
| `Video` | the only item |
| `Video,*` | first |
| `*,Video` | last |
| `*,Video,*` | in the middle |

The same code builds both operators, and joins these four tests with OR:

- **Includes:** `x GLOB 'Video' OR x GLOB 'Video,*' OR ...`. One match is enough, so this is correct.
- **Excludes:** `x NOT GLOB 'Video' OR x NOT GLOB 'Video,*' OR ...`. This is true if the value fails *any one* pattern. No value can match all four patterns at once, so every non-NULL value passes.

For example, the value `Video` fails the `Video,*` pattern, so "Excludes Video" selects it.

Excludes needs AND: the value must fail *all four* patterns.

## The fix

The fix is in the three-argument `SqlPhrase.DataLabelOperatorValue(string dataLabel, string mathOperator, string value)` in `src/Timelapse/Sql.cs`. To find it, search for the comment `// MultiLine Include/Exclude version`. Line numbers will differ between versions.

**Before:**

```csharp
string[] terms = value.Split(',');
string where = string.Empty;
for (int i = 0; i < terms.Length; i++)
{
    where += Sql.OpenParenthesis;
    where += $"{dataLabel} {mathOperator} {Sql.Quote(terms[i])} {Sql.Or}";          // aa:   matches a single term
    where += $"{dataLabel} {mathOperator} {Sql.Quote(terms[i] + ",*")} {Sql.Or}";   // aa,*  matches term at beginning
    where += $"{dataLabel} {mathOperator} {Sql.Quote("*," + terms[i])} {Sql.Or}";   // *,aa  matches term at end
    where += $"{dataLabel} {mathOperator} {Sql.Quote("*," + terms[i] + ",*")}";     // *,aa,*  matchesin middle
```

**After:**

```csharp
// An Includes only has to match the term in one position, while an Excludes has to fail in every position.
string positionOperator = mathOperator.Trim() == "NOT GLOB" ? Sql.And : Sql.Or;
string[] terms = value.Split(',');
string where = string.Empty;
for (int i = 0; i < terms.Length; i++)
{
    where += Sql.OpenParenthesis;
    where += $"{dataLabel} {mathOperator} {Sql.Quote(terms[i])} {positionOperator}";          // aa:   matches a single term
    where += $"{dataLabel} {mathOperator} {Sql.Quote(terms[i] + ",*")} {positionOperator}";   // aa,*  matches term at beginning
    where += $"{dataLabel} {mathOperator} {Sql.Quote("*," + terms[i])} {positionOperator}";   // *,aa  matches term at end
    where += $"{dataLabel} {mathOperator} {Sql.Quote("*," + terms[i] + ",*")}";     // *,aa,*  matchesin middle
```

The comparison with `"NOT GLOB"` relies on `CustomSelection.TermToSqlOperator` returning `" NOT GLOB "` for Excludes. Check that this is still the case in the version you are changing.

## What changes for users

Only searches that use Excludes on a MultiChoice field are affected, and those searches will now return fewer files, which is the intended result.

| Stored value | "Excludes Video" before | "Excludes Video" after |
|---|---|---|
| `Video` | Selected | Not selected |
| `Motion Image,Video` | Selected | Not selected |
| `Motion Image` | Selected | Selected |
| `Bighorn Video` | Selected | Selected |
| empty | Selected | Selected |

With several selected items, such as "Excludes Motion Image,Video", a file is selected only if its value contains none of them. These behaviours don't change:

- Includes.
- Excludes with nothing selected, which matches no files.
- A NULL value never matches Excludes, because SQL comparisons with NULL fail. A NULL value contains no items, so it arguably should match. The fix leaves this as it was.

## How to test it

1. Use a data set with a MultiChoice field, and give files a mix of values, including one item, several items, and an empty value.
2. In Custom Selection, select that field and choose **Excludes** with one item.
3. Confirm that the count drops, and that no selected file contains that item.
4. Confirm that **Includes** with the same item returns the files that Excludes left out, apart from any with NULL values.

When this fix was made, these tests passed through `CustomSelection.GetFilesWhere` on a copy of a practice database, using a test MultiChoice column:

| Search | Files, of 336 | Stored values selected |
|---|---|---|
| Includes Video | 135 | `Video`, `Motion Image,Video` |
| Excludes Video | 201 | `Motion Image`, empty, `Bighorn Video` |
| Excludes Motion Image | 201 | `Video`, empty, `Bighorn Video` |
| Excludes Motion Image,Video | 134 | empty, `Bighorn Video` |
