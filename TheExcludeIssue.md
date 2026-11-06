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

---

# Status of the surrounding work, as of 2026-11-05

The Excludes fix above stands alone: to apply it to another version, you only need the sections above. This section is a separate summary for resuming work on branch `develop`.

## Commits so far

All of these are local. `develop` is ahead of GitHub, and nothing has been pushed since `17e1b11`.

| Commit | What it did |
|---|---|
| `fe90a94` | Species editor: replaced Done and Cancel with Save to file and one exit button |
| `e441754` | CamtrapDP taxonomic dialog: GBIF lookups and Populate from a Species List file |
| `f3d87d7` | Species editor: Append qualifiers now changes the scientific names in the list |
| `57711e1` | The Excludes fix and this note |
| The commit containing this section | Folder metadata search terms in Custom Selection |

## Folder metadata search terms

**Status:** all six planned steps are done and committed. They haven't been tested by hand in the real app.

**Files:**
- `Controls/FolderMetadataSelector.cs`: the panel.
- `Dialog/CustomSelection.xaml(.cs)`: hosts the panel.
- `SearchingAndSorting/FolderMetadataSelection.cs`: the terms, and matching saved terms to the current folder template.
- `SearchingAndSorting/CustomSelectionFolderMetadataWhere.cs`: the SQL.
- `SearchingAndSorting/CustomSelectionGetFilesWhere.cs`: one call that adds the folder terms to the query.
- `Database/FileDatabase.cs` and `Database/CommonDatabase.cs`: restoring the terms, and adding the new column.

**Design decisions:**
- A single And/Or in the panel applies to all its terms, both within a level and between levels. It applies only within the panel, which is always ANDed with the other search terms.
- The form is `((standard AND other terms) AND panel)`, followed by the recognition conditions, ANDed.
- An empty value in a folder's row is a value. An empty flag counts as false. Empty numbers count as 0 and empty dates sort first, which was agreed "for now".
- A folder with no row at a level is never selected by a term at that level (changed from the first version). The top level isn't offered, as it applies to all images.
- Terms are saved in their own `FolderSearchTerms` column of `ImageSetTable`, and are left out of the `SearchTerms` JSON, so older versions don't see them.
- With no folder terms in use, the generated SQL is unchanged. This was confirmed against a baseline of 72 selections.
- The recognition, folder metadata and main terms areas share one scroll area (`SelectionScrollViewer`). Only the recognition lists keep their own scrollbars, and the mouse wheel moves on to the dialog at their ends.
- No index was added. One count took 0.27 s at 4,000 folders, 6.3 s at 20,000, and 126 s at 100,000. None is needed: about 100 folders is the expected top end.

**Hand tests still needed:**
1. Use the panel with the mouse and the Tab key, on a data set with folder metadata.
2. Tick items in a multi-choice drop-down.
3. Close and reopen Timelapse to check that terms return, and try Cancel.
4. With folder terms in use, choose All files or a folder from the Select menu, and check that the terms clear.
5. Try a data set with real episode data.
6. Try a data set with detections in several folders.
7. Open a copy of an upgraded database in an older version of Timelapse.

**Example data:** `C:\Users\saulg\Desktop\TestSets\PracticeImageSet\TimelapseData.ddb`. Its Level tables are empty, so test on a copy with values filled in.

## Open decisions

- Whether to revisit how empty numbers and dates behave.
- Species editor: whether to check Append qualifiers automatically on import, and to keep qualifiers on rows with no rank. Today, `Felis sp.` with no rank loses its qualifier, and importing with the box unchecked strips qualifiers.
- CamtrapDP dialog: whether to shorten the vernacular names button label, and whether clearing a list should stop saving `"vernacularNames":{}`.
- Whether species lists will be stored with the template. Today, Save to file is the only way to keep one, and the manual text says so.
- The example template's labels such as "Deployment Start Date_" and "Time_-lapse Image" look like the result of an old find-and-replace.

## WordPress

- The six manual pages (9115 to 9130) are in the trash until it's emptied, and their Publications links now go to the Guides page.
- Five of the six Recurring rows have no link icon.
- The webinar mp4 still needs uploading, and 2022 and 2023 are still skipped.
