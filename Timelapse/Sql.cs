﻿using System.Globalization;
using Timelapse.Constant;
using Timelapse.Enums;

// ReSharper disable UnusedMember.Global
namespace Timelapse
{
    // Create SQL commands using constants rather than typing the SQL keywords. 
    // This really helps avoid typos, bugs due to spacing such as not having spaces in between keywords, etc.
    public static class Sql
    {
        public const string AddColumn = " ADD COLUMN ";
        public const string AlterTable = " ALTER TABLE ";
        public const string And = " AND ";
        public const string As = " AS ";
        public const string AsInteger = " AS INTEGER ";
        public const string AsReal = " AS REAL ";
        public const string Ascending = " ASC ";
        public const string AttachDatabase = " ATTACH DATABASE ";
        public const string BooleanEquals = " == ";
        public const string Concatenate = " || ";
        public const string Descending = " DESC ";
        public const string Dot = ".";
        public const string DotStar = Dot + Star;
        public const string BeginTransaction = " BEGIN TRANSACTION ";
        public const string BeginTransactionSemiColon = BeginTransaction + Semicolon;
        public const string Between = " BETWEEN ";
        public const string CaseWhen = " CASE WHEN ";
        public const string Cast = " CAST ";
        public const string Count = " Count ";
        public const string CountStar = Count + OpenParenthesis + Star + CloseParenthesis;
        public const string CreateIndex = " CREATE INDEX ";
        public const string CreateTable = " CREATE TABLE ";
        public const string CreateTemporaryTable = " CREATE TEMPORARY TABLE ";
        public const string CreateUniqueIndex = " CREATE UNIQUE INDEX ";
        public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
        public const string CloseParenthesis = " ) ";
        public const string CollateNocase = " COLLATE NOCASE ";
        public const string Comma = ", ";
        public const string DataSource = "Data Source=";
        public const string DateFunction = " Date ";
        public const string DateTimeFunction = " DateTime ";
        public const string Default = " DEFAULT ";
        public const string DeleteFrom = "DELETE FROM ";
        public const string Do = " DO ";
        public const string DoUpdate = " Do UPDATE ";
        public const string DropIndex = " DROP INDEX ";
        public const string DropTable = " DROP TABLE ";
        public const string DropTableIfExists = " DROP TABLE IF EXISTS ";
        public const string Else = " ELSE ";
        public const string End = " END ";
        public const string EndTransaction = " END TRANSACTION ";
        public const string EndTransactionSemiColon = EndTransaction + Semicolon;
        public const string Equal = " = ";
        public const string EqualsCaseID = " = CASE Id";
        public const string From = " FROM ";
        public const string Glob = " GLOB ";
        public const string GreaterThanEqual = " >= ";
        public const string GreaterThan = " > ";
        public const string GroupBy = " GROUP BY ";
        public const string Having = " HAVING ";
        public const string HoursQuoted = "' hours'";
        public const string IfNotExists = " IF NOT EXISTS ";
        public const string IfExists = " IF EXISTS ";
        public const string In = " In ";
        public const string InnerJoin = " INNER JOIN ";
        public const string InsertInto = " INSERT INTO ";
        public const string InsertOrReplaceInto = " INSERT OR REPLACE INTO ";
        public const string Instr = " INSTR ";
        public const string IntegerType = " INTEGER ";
        public const string IsNull = " IS NULL ";
        public const string Join = " JOIN ";
        public const string LeftJoin = " LEFT JOIN ";
        public const string Length = " LENGTH ";
        public const string LessThanEqual = " <= ";
        public const string LessThan = " < ";
        public const string Like = " LIKE ";
        public const string Limit = " LIMIT ";
        public const string LimitOne = Limit + " 1 ";
        public const string Max = " MAX ";
        public const string MasterTableList = "sqlite_master";
        public const string Minus = " - ";
        public const string Name = " NAME ";
        public const string NameFromSqliteMaster = " NAME FROM SQLITE_MASTER ";
        public const string Not = " NOT ";
        public const string NotEqual = " <> ";
        public const string NotNull = " NOT NULL ";
        public const string NotLike = Not + Like;
        public const string Null = " NULL ";
        public const string NullAs = Null + " " + As;
        public const string NullAsPlaceHolder = NullAs + Placeholder;
        public const string Ok = "ok";
        public const string On = " ON ";
        public const string OnConflict = " ON CONFLICT ";
        public const string OpenParenthesis = " ( ";
        public const string Or = " OR ";
        public const string OrderBy = " ORDER BY ";
        public const string OrderByRandom = OrderBy + " RANDOM() ";
        public const string Placeholder = " PLACEHOLDER ";
        public const string Plus = " + ";
        public const string Pragma = " PRAGMA ";
        public const string PragmaForeignKeysEquals = Pragma + " foreign_keys " + Equal;
        public const string PragmaTableInfo = Pragma + " TABLE_INFO ";
        public const string PragmaSetForeignKeys = PragmaForeignKeysEquals + " 1 ";
        public const string PragmaForeignKeysOff = PragmaForeignKeysEquals + " OFF ";
        public const string PragmaForeignKeysOn = PragmaForeignKeysEquals + " ON ";
        public const string PragmaQuickCheck = Pragma + " QUICK_CHECK ";
        public const string PrimaryKey = " PRIMARY KEY ";
        public const string RealType = " REAL ";
        public const string RenameTo = " RENAME TO ";
        public const string Replace = " REPLACE ";
        public const string Returning = " RETURNING ";
        public const string QuotedEmptyString = " '' ";
        public const string Select = " SELECT ";
        public const string SelectDistinct = " SELECT DISTINCT ";
        public const string SelectDistinctStar = " SELECT DISTINCT * ";
        public const string SelectOne = " SELECT 1 ";
        public const string SelectStar = Select + Star; // SELECT * "
        public const string SelectStarFrom = SelectStar + From; // SELECT * FROM "

        public const string SelectCount = " SELECT COUNT ";
        public const string SelectDistinctCount = " SELECT DISTINCT COUNT ";
        public const string SelectCountStarFrom = SelectCount + OpenParenthesis + Star + CloseParenthesis + From;
        public const string SelectDistinctCountStarFrom = SelectDistinctCount + OpenParenthesis + Star + CloseParenthesis + From;
        public const string SelectExists = " SELECT EXISTS ";
        public const string SelectNameFromPragmaTableInfo = Select + Name + From + " PRAGMA_TABLE_INFO ";
        public const string SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals = Select + Name + From + SqlMaster + Where + TypeEqualsTable + And + Name + Equal;
        public const string SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals = SelectCountStarFrom + SqlMaster + Where + TypeEqualsIndex + And + Name + Equal;
        public static string SelectSqlFromSqliteMasterWhereTypeEqualTableAndNameEquals = $"{Select} sql {From} {SqlMaster} {Where} {TypeEqualsTable} {And} {Name} {Equal}";
        public const string Semicolon = " ; ";
        public const string Set = " SET ";
        public const string SqlMaster = " sqlite_master ";
        public const string Star = "*";
        public const string Strftime = " Strftime ";
        public const string StringType = " STRING ";
        public const string Substr = " SUBSTR ";

        public const string Real = " REAL ";
        public const string TBLINFO = " TBLINFO ";
        public const string Text = "TEXT";
        public const string TimeFunction = " Time ";
        public const string Then = " THEN ";
        public const string Trim = " TRIM ";
        public const string True = " TRUE ";
        public const string TypeEquals = " TYPE " + Equal;
        public const string TypeEqualsTable = TypeEquals + " 'table' ";
        public const string TypeEqualsIndex = TypeEquals + " 'index' ";
        public const string UnionAll = " UNION ALL";
        public const string Update = " UPDATE ";
        public const string Using = " USING ";
        public const string Values = " VALUES ";
        public const string When = " WHEN ";
        public const string Where = " WHERE ";
        public const string WhereExists = " WHERE EXISTS ";
        public const string WhereIDIn = Where + "Id IN ";
        public const string WhereIDNotIn = Where + " Id NOT IN ";
        public const string WhereIDEquals = Where + " Id " + Equal;

        /// <summary>
        /// Format the passed value for use as string value in a SQL statement or query.
        /// Nulls are quoted as empty strings
        /// </summary>
        public static string Quote(string value)
        {
            // promote null values to empty strings
            return (value == null)
                ? "''"
                : "'" + value.Replace("'", "''") + "'";
        }
    }

    /// <summary>
    /// Instead of having lots of long SQL phrase fragments constructed in various files, we construct and collect them here
    /// </summary>
    public static class SqlPhrase
    {
        // Given a table name, return its creation phrase that gets that table's schema
        // Note that when executed, it returns 'Create table <tableName> (schema...) so it
        // has limited value unless the table name is altered or the schema extracted e.g. if one wants to create a new table from it
        public static string GetSchemaFromTable(string tableName)
        {
            return $"{Sql.SelectSqlFromSqliteMasterWhereTypeEqualTableAndNameEquals} {Sql.Quote(tableName)}";
        }

        /// <summary>
        /// Sql Phrase - Create partial query to return all missing detections
        /// </summary>
        ///  <param name="selectType">If true, return a SELECT COUNT vs a SELECT from</param>
        /// <returns> 
        /// Count Form:  SELECT COUNT  ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
        /// Star Form: SELECT DataTable.*               FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
        /// One Form:  SELECT 1                         FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
        /// </returns>


        public static string SelectMissingDetections(SelectTypesEnum selectType)
        {
            string phrase = string.Empty;
            if (selectType == SelectTypesEnum.Count)
            {
                phrase = Sql.SelectCount + Sql.OpenParenthesis + DBTables.FileData + Sql.Dot + DatabaseColumn.ID + Sql.CloseParenthesis;
            }
            else if (selectType == SelectTypesEnum.Star)
            {
                phrase = Sql.Select + DBTables.FileData + Sql.DotStar;
            }
            else if (selectType == SelectTypesEnum.One)
            {
                phrase = Sql.SelectOne;
            }
            //string phrase = useCountForm
            //    ? Sql.SelectCount + Sql.OpenParenthesis + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis
            //    : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
            return phrase + Sql.From + DBTables.FileData +
                Sql.LeftJoin + DBTables.Detections +
                Sql.On + DBTables.FileData + Sql.Dot + DatabaseColumn.ID +
                Sql.Equal + DBTables.Detections + Sql.Dot + DatabaseColumn.ID +
                Sql.Where + DBTables.Detections + Sql.Dot + DatabaseColumn.ID + Sql.IsNull;
        }
        //public static string SelectMissingDetections(bool useCountForm)
        //{
        //    string phrase = useCountForm
        //        ? Sql.SelectCount + Sql.OpenParenthesis + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis
        //        : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
        //    return phrase + Sql.From + Constant.DBTables.FileData +
        //        Sql.LeftJoin + Constant.DBTables.Detections +
        //        Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
        //        Sql.IdenticalToSet2 + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID +
        //        Sql.Where + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID + Sql.IsNull;
        //}

        /// <summary>
        /// Sql Phrase - Create partial query to return detections
        /// </summary>
        /// <param name="selectType"></param>
        /// <returns>
        /// Count Form:  SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
        /// Star Form:   SELECT DataTable.*                     FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
        /// One Form:   SELECT 1                                FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
        /// </returns>
        public static string SelectDetections(SelectTypesEnum selectType)
        {
            string phrase = string.Empty;
            if (selectType == SelectTypesEnum.Count)
            {
                phrase = Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct + DBTables.FileData + Sql.DotStar;
            }
            else if (selectType == SelectTypesEnum.Star)
            {
                phrase = Sql.Select + DBTables.FileData + Sql.DotStar;
            }
            else if (selectType == SelectTypesEnum.One)
            {
                phrase = Sql.SelectOne;
            }
            return phrase + Sql.From + DBTables.Detections + Sql.InnerJoin + DBTables.FileData +
                    Sql.On + DBTables.FileData + Sql.Dot + DatabaseColumn.ID + Sql.Equal + DBTables.Detections + "." + DetectionColumns.ImageID;
        }
        //public static string SelectDetections(bool useCountForm)
        //{
        //    string phrase = useCountForm
        //        //? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectStar
        //        ? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct + Constant.DBTables.FileData + Sql.DotStar
        //        : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;


        //    return phrase + Sql.From + Constant.DBTables.Detections + Sql.InnerJoin + Constant.DBTables.FileData +
        //            Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.IdenticalToSet2 + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectType"></param>
        /// <returns>
        /// Count Form:  Select COUNT  ( * )  FROM (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        /// Star Form:   SELECT  DISTINCT                           DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        /// One Form     SELECT ONE           FROM (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        /// 
        /// </returns>
        public static string SelectClassifications(SelectTypesEnum selectType)
        {
            string phrase = string.Empty;
            if (selectType == SelectTypesEnum.Count)
            {
                phrase = Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct;
            }
            else if (selectType == SelectTypesEnum.Star)
            {
                phrase = Sql.SelectDistinct;
            }
            else if (selectType == SelectTypesEnum.One)
            {
                phrase = Sql.SelectOne + Sql.From + Sql.OpenParenthesis + Sql.SelectDistinct;
            }

            phrase += DBTables.FileData + Sql.DotStar + Sql.From + DBTables.Classifications +
                    Sql.InnerJoin + DBTables.FileData + Sql.On + DBTables.FileData + Sql.Dot + DatabaseColumn.ID +
                    Sql.Equal + DBTables.Detections + "." + DetectionColumns.ImageID;

            // and now append INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
            phrase += Sql.InnerJoin + DBTables.Detections + Sql.On +
                DBTables.Detections + Sql.Dot + DetectionColumns.DetectionID + Sql.Equal +
                DBTables.Classifications + "." + DetectionColumns.DetectionID;

            return phrase;
        }
        //public static string SelectClassifications(bool useCountForm)
        //{
        //    string phrase = useCountForm
        //        ? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct
        //        : Sql.SelectDistinct;
        //    //     : Sql.SelectDistinct + Constant.DBTables.Classifications + Sql.Dot + Constant.ClassificationColumns.Conf + Sql.Comma;
        //    phrase += Constant.DBTables.FileData + Sql.DotStar + Sql.From + Constant.DBTables.Classifications +
        //            Sql.InnerJoin + Constant.DBTables.FileData + Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
        //            Sql.IdenticalToSet2 + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
        //    // and now append INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        //    phrase += Sql.InnerJoin + Constant.DBTables.Detections + Sql.On +
        //        Constant.DBTables.Detections + Sql.Dot + Constant.DetectionColumns.DetectionID + Sql.IdenticalToSet2 +
        //        Constant.DBTables.Classifications + "." + Constant.DetectionColumns.DetectionID;
        //    return phrase;
        //}

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="datalabel"></param>
        /// <returns> ( label IS NULL OR  label = '' ) ;</returns>
        public static string LabelIsNullOrDataLabelEqualsEmpty(string datalabel)
        {
            return Sql.OpenParenthesis + datalabel + Sql.IsNull + Sql.Or + datalabel + Sql.Equal + Sql.QuotedEmptyString + Sql.CloseParenthesis;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="dataLabel"></param>
        /// <param name="mathOperator"></param>
        /// <param name="value"></param>
        /// <param name="sqlDataType"></param>
        /// <returns>DataLabel operator "value", e.g., DataLabel > "5"</returns>
        public static string DataLabelOperatorValue(string dataLabel, string mathOperator, string value, string sqlDataType)
        {
            value = value == null ? string.Empty : value.Trim();

            if (sqlDataType == Sql.IntegerType)
            {
                return Sql.Cast + Sql.OpenParenthesis + dataLabel + Sql.AsInteger + Sql.CloseParenthesis + mathOperator + Sql.Quote(value);
            }
            if (sqlDataType == Sql.RealType)
            {
                return Sql.Cast + Sql.OpenParenthesis + dataLabel + Sql.AsReal + Sql.CloseParenthesis + mathOperator + Sql.Quote(value);
            }
            return dataLabel + mathOperator + Sql.Quote(value);
        }

        /// <returns>Match the Date portion only  by extracting the Date from the DateTime string value, e.g., DataLabel operator "value", e.g., Date_(datetime)= Date_('2016-08-19 19:08:22')</returns>
        public static string DataLabelDateTimeOperatorValue(string dataLabel, string mathOperator, string value)
        {
            value = value == null ? string.Empty : value.Trim();
            return Sql.DateFunction + Sql.OpenParenthesis + dataLabel + Sql.CloseParenthesis + mathOperator + Sql.DateFunction + Sql.OpenParenthesis + Sql.Quote(value) + Sql.CloseParenthesis;
        }

        /// <returns>Match the Time portion only  by extracting the Time_ from the DateTime string value, e.g., DataLabel operator "value", e.g., Time_ (datetime) = '19:08:22'</returns>
        public static string DataLabelTimeOperatorValue(string dataLabel, string mathOperator, string value)
        {
            value = value == null ? string.Empty : value.Trim();
            return Sql.TimeFunction + Sql.OpenParenthesis + dataLabel + Sql.CloseParenthesis + mathOperator + Sql.TimeFunction + Sql.OpenParenthesis + Sql.Quote(value) + Sql.CloseParenthesis;
        }

        // MultiLine Include/Exclude version
        // We want to avoid matches that could happen if a term is a substring of another term, e.g.
        // if the menu contains "Sheep" and "Bighorn Sheep" we want to make sure that a selection containing
        // "Sheep" does not return "Bighorn Sheep". Because Glob expression are limited, we do this by
        // searching for the exact term as it would appear in different possible positions in the comma separated list,
        // i.e., at the beginning, middle and end plus as exact matches.
        public static string DataLabelOperatorValue(string dataLabel, string mathOperator, string value)
        {
            value = value == null ? string.Empty : value.Trim();
            if (value == string.Empty)
            {
                // special case for empty string
                return DataLabelOperatorValue(dataLabel, mathOperator, "*", Control.MultiLine);
            }
            string[] terms = value.Split(',');
            string where = string.Empty;
            for (int i = 0; i < terms.Length; i++)
            {
                where += Sql.OpenParenthesis;
                where += $"{dataLabel} {mathOperator} {Sql.Quote(terms[i])} {Sql.Or}";          // aa:   matches a single term
                where += $"{dataLabel} {mathOperator} {Sql.Quote(terms[i] + ",*")} {Sql.Or}";   // aa,*  matches term at beginning 
                where += $"{dataLabel} {mathOperator} {Sql.Quote("*," + terms[i])} {Sql.Or}";   // *,aa  matches term at end  
                where += $"{dataLabel} {mathOperator} {Sql.Quote("*," + terms[i] + ",*")}";     // *,aa,*  matchesin middle
                where += Sql.CloseParenthesis;
                if (i < terms.Length - 1)
                {
                    where += Sql.And;
                }
            }
            return where;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="detectionCategory"></param>
        /// <returns>Detections.Category = detectionCategory</returns>
        public static string DetectionCategoryEqualsDetectionCategory(string detectionCategory)
        {
            // TODO DetectionsVideo
            return DBTables.Detections + "." + DetectionColumns.Category + Sql.Equal + detectionCategory;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="classificationCategory"></param>
        /// <returns>Classifications.Category = classificationCategory</returns>
        ///
        public static string ClassificationsCategoryEqualsClassificationCategory(string classificationCategory)
        {
            return DBTables.Classifications + "." + DetectionColumns.Category + Sql.Equal + classificationCategory;
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="lowerBound"></param>
        /// <param name="upperBound"></param>
        /// <returns>Group By Detections.Id Having Max ( Detections.conf ) BETWEEN lowerBound AND upperBound</returns>
        public static string GroupByDetectionsIdHavingMaxDetectionsConf(double lowerBound, double upperBound)
        {
            return Sql.GroupBy + DBTables.Detections + "." + DetectionColumns.ImageID +
                Sql.Having + Sql.Max +
                Sql.OpenParenthesis + DBTables.Detections + "." + DetectionColumns.Conf + Sql.CloseParenthesis +
                Sql.Between + lowerBound.ToString(CultureInfo.InvariantCulture) + Sql.And + upperBound.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sql phrase used in Where
        /// </summary>
        /// <param name="lowerBound"></param>
        /// <param name="upperBound"></param>
        /// <returns>GROUP BY Classifications.classificationID HAVING MAX  ( Classifications.conf ) BETWEEN lowerBound AND upperBound</returns>
        public static string GroupByClassificationsIdHavingMaxClassificationsConf(double lowerBound, double upperBound)
        {
            return Sql.GroupBy + DBTables.Classifications + "." + ClassificationColumns.ClassificationID +
                Sql.Having + Sql.Max +
                Sql.OpenParenthesis + DBTables.Classifications + "." + DetectionColumns.Conf + Sql.CloseParenthesis +
                Sql.Between + lowerBound.ToString(CultureInfo.InvariantCulture) + Sql.And + upperBound.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Episode-related phrases. Used in constructing a front wrapper for selecting or counting  files where all files in an episode have at least one file matching the surrounded search condition 
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="episodeNoteField"></param>
        /// <param name="countOnly"></param>
        public static string CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(string tableName, string episodeNoteField, bool countOnly)
        {
            // using DataTable and Episode,
            // Select Complete form:  String.Format("Select * from DataTable WHERE SUBSTR(DataTable.{0}, 0, instr(DataTable.{0}, ':')) in (Select substr({0}, 0, instr({0}, ':')) From (", episodeNoteField);
            // Count Complete formstring frontWrapper = String.Format("Select  COUNT  ( * ) from DataTable WHERE SUBSTR(DataTable.{0}, 0, instr(DataTable.{0}, ':')) in (Select substr({0}, 0, instr({0}, ':')) From ", this.CustomSelection.EpisodeNoteField);
            // Line by line form:  
            // Count Form:  Select Count (*) from 
            // Select Form: Select * from 
            // DataTable WHERE SUBSTR(DataTable.{0}, 0,
            //                           instr(DataTable.{0}, ':'))
            // IN (Selectsubstr({0}, 0
            //                          instr({0}, ':'))
            // FROM 
            // Count form:   
            // Select form:  (
            string frontwrapper = countOnly
                ? Sql.SelectCountStarFrom
                : Sql.SelectStarFrom;
            frontwrapper += tableName + Sql.Where + Sql.Substr + Sql.OpenParenthesis + tableName + Sql.Dot + episodeNoteField + Sql.Comma + "0" + Sql.Comma
                                + Sql.Instr + Sql.OpenParenthesis + tableName + Sql.Dot + episodeNoteField + Sql.Comma + Sql.Quote(":") + Sql.CloseParenthesis + Sql.CloseParenthesis
                                + Sql.In + Sql.OpenParenthesis + Sql.Select + Sql.Substr + Sql.OpenParenthesis + episodeNoteField + Sql.Comma + "0" + Sql.Comma
                                + Sql.Instr + Sql.OpenParenthesis + episodeNoteField + Sql.Comma + Sql.Quote(":") + Sql.CloseParenthesis + Sql.CloseParenthesis
                                + Sql.From;
            frontwrapper += countOnly
                ? string.Empty
                : Sql.OpenParenthesis;
            return frontwrapper;
        }

        // Create a query that returns the maximum value in the provided table
        // For example, if the column is Id, it will get the maximum Id
        // Form: "Select Max(columnName) from tableName"
        public static string GetMaxColumnValue(string columnName, string tableName)
        {
            return Sql.Select + Sql.Max + Sql.OpenParenthesis + columnName + Sql.CloseParenthesis + Sql.From + tableName;
        }
    }
}
