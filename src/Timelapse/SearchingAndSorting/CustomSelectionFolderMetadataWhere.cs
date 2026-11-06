using System.Collections.Generic;
using System.Linq;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.SearchingAndSorting
{
    // This file contains the part of the query composed from the folder metadata search terms.
    public partial class CustomSelection
    {
        private const string FoldersAlias = "Folders";
        private const string LevelAlias = "L";

        // Combine the where clause built from the other search terms with the folder metadata terms.
        // They are always ANDed, as the folder metadata And/Or applies only among the folder metadata terms.
        // The result is parenthesized as a whole, so that any recognition terms added afterwards apply to all of it.
        // Form: WHERE ( (other terms) AND folderTerms )
        // The where clause is returned as is if there are no folder metadata terms, which leaves the existing queries unchanged.
        private string CombineWithFolderMetadataTerms(string where, string whereString)
        {
            string folderWhere = GetFolderMetadataWhere();
            if (string.IsNullOrEmpty(folderWhere))
            {
                return where;
            }
            if (string.IsNullOrWhiteSpace(where))
            {
                return whereString + folderWhere;
            }

            string otherTerms = where[whereString.Length..];
            return whereString + Sql.OpenParenthesis + Sql.OpenParenthesis + otherTerms + Sql.CloseParenthesis + Sql.And + folderWhere + Sql.CloseParenthesis;
        }

        // The terms are evaluated once for each folder containing files rather than once for each file, as there are far fewer folders.
        // Form: DataTable.RelativePath IN ( SELECT Folders.RelativePath FROM ( SELECT DISTINCT RelativePath FROM DataTable ) AS Folders WHERE level2Terms AND/OR level3Terms )
        // Returns an empty string if no folder metadata terms are used.
        private string GetFolderMetadataWhere()
        {
            if (FolderMetadata is not { UseFolderMetadata: true })
            {
                return string.Empty;
            }

            string combiningOperator = FolderMetadata.CombiningOperator == CustomSelectionOperatorEnum.Or ? Sql.Or : Sql.And;
            List<string> levelWheres = [.. FolderMetadata.Levels.Select(level => GetFolderLevelWhere(level, combiningOperator)).Where(levelWhere => false == string.IsNullOrEmpty(levelWhere))];
            if (levelWheres.Count == 0)
            {
                return string.Empty;
            }

            return $"{DBTables.FileData}{Sql.Dot}{DatabaseColumn.RelativePath}{Sql.In}{Sql.OpenParenthesis}"
                   + $"{Sql.Select}{FoldersAlias}{Sql.Dot}{DatabaseColumn.RelativePath}{Sql.From}"
                   + $"{Sql.OpenParenthesis}{Sql.SelectDistinct}{DatabaseColumn.RelativePath}{Sql.From}{DBTables.FileData}{Sql.CloseParenthesis}{Sql.As}{FoldersAlias}"
                   + $"{Sql.Where}{string.Join(combiningOperator, levelWheres)}{Sql.CloseParenthesis}";
        }

        // Combine the terms in use for this level using the operator that applies to all the folder metadata terms.
        // A folder matches this level's terms only if it has a row in this level's table, no matter what the terms or the operator are.
        // Form: ( EXISTS ( SELECT 1 FROM Level2 AS L WHERE folderIsInThatRow ) AND ( term1 AND/OR term2 ) )
        private static string GetFolderLevelWhere(FolderLevelSearchTerms levelTerms, string combiningOperator)
        {
            List<string> termWheres = [.. levelTerms.SearchTerms
                .Where(term => term.UseForSearching)
                .Select(term => GetFolderTermWhere(levelTerms.Level, term))];
            if (termWheres.Count == 0)
            {
                return string.Empty;
            }
            string hasRow = $" EXISTS {Sql.OpenParenthesis}{Sql.SelectOneFrom}{FileDatabase.MetadataComposeTableNameFromLevel(levelTerms.Level)}{Sql.As}{LevelAlias}{Sql.Where}{FolderIsInLevelRow()}{Sql.CloseParenthesis}";
            return Sql.OpenParenthesis + hasRow + Sql.And + Sql.OpenParenthesis + string.Join(combiningOperator, termWheres) + Sql.CloseParenthesis + Sql.CloseParenthesis;
        }

        // The same forms as the other search terms, except that the column is replaced by the folder's value.
        // As that value is never null, an empty value can be treated like any other value.
        private static string GetFolderTermWhere(int level, SearchTerm term)
        {
            string value = GetFolderValueExpression(level, term);
            string sqlOperator = TermToSqlOperator(term.Operator);

            if (string.IsNullOrEmpty(term.DatabaseValue) && term.Operator == SearchTermOperator.Equal)
            {
                return value + Sql.Equal + Sql.Quote(string.Empty);
            }

            if (term.ControlType == Control.MultiChoice && term.Operator is SearchTermOperator.Includes or SearchTermOperator.Excludes)
            {
                return SqlPhrase.DataLabelOperatorValue(value, sqlOperator, term.DatabaseValue);
            }

            string sqlType = term.ControlType switch
            {
                Control.Counter or Control.IntegerAny or Control.IntegerPositive => Sql.IntegerType,
                Control.DecimalAny or Control.DecimalPositive => Sql.RealType,
                _ => Sql.Text
            };
            string where = SqlPhrase.DataLabelOperatorValue(value, sqlOperator, term.DatabaseValue, sqlType);

            // Because flags can have capitals or lower case, we need to make the search case insensitive
            return term.ControlType == Control.Flag ? where + Sql.CollateNocase : where;
        }

        // An expression for the value of this term's field in the folder at this level that contains the file, where:
        // - a null value in that folder's row is an empty value,
        // - an empty flag is false, as flags can only be searched for as true or false.
        // Form: COALESCE ( ( SELECT L.field FROM Level2 AS L WHERE folderIsInThatRow LIMIT 1 ), '' )
        private static string GetFolderValueExpression(int level, SearchTerm term)
        {
            string row = $"{Sql.OpenParenthesis}{Sql.Select}{LevelAlias}{Sql.Dot}{term.DataLabel}{Sql.From}{FileDatabase.MetadataComposeTableNameFromLevel(level)}{Sql.As}{LevelAlias}"
                         + $"{Sql.Where}{FolderIsInLevelRow()} LIMIT 1{Sql.CloseParenthesis}";
            return term.ControlType == Control.Flag
                ? $"COALESCE(NULLIF({row}, ''), {Sql.Quote(BooleanValue.False)})"
                : $"COALESCE({row}, '')";
        }

        // Whether the folder containing the files is the level table row's folder, or is within it
        // Form: Folders.RelativePath = L.FolderDataPath OR substr(Folders.RelativePath, 1, length(L.FolderDataPath) + 1) = L.FolderDataPath || '\'
        private static string FolderIsInLevelRow()
        {
            string path = $"{LevelAlias}{Sql.Dot}{DatabaseColumn.FolderDataPath}";
            string folder = $"{FoldersAlias}{Sql.Dot}{DatabaseColumn.RelativePath}";
            return $"{folder}{Sql.Equal}{path}{Sql.Or}substr({folder}, 1, length({path}) + 1){Sql.Equal}{path} || '\\'";
        }
    }
}
