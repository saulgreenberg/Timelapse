using System;
using System.Collections.Generic;
using System.Globalization;
using Timelapse.Constant;
using Timelapse.Enums;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

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
        public const string BeginTransaction = " BEGIN TRANSACTION ";
        public const string BooleanEquals = " == ";

        public const string BeginTransactionSemiColon = BeginTransaction + Semicolon;
        public const string Between = " BETWEEN ";
        public const string Case = " CASE ";
        public const string CaseWhen = Case + " WHEN ";
        public const string Cast = " CAST ";
        public const string Coalesce = " COALESCE ";
        public const string Commit = " COMMIT ";
        public const string Concatenate = " || ";
        public const string Count = " Count ";
        public const string CountStar = Count + OpenParenthesis + Star + CloseParenthesis;
        public const string CreateIndex = " CREATE INDEX ";
        public const string CreateIndexIfNotExists = CreateIndex + " IF NOT EXISTS";
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
        public const string Descending = " DESC ";
        public const string Dot = ".";
        public const string DotStar = Dot + Star;
        public const string Do = " DO ";
        public const string DoUpdate = " Do UPDATE ";
        public const string DropIndex = " DROP INDEX ";
        public const string DropIndexIfExists = DropIndex + IfExists;
        public const string DropTable = " DROP TABLE ";
        public const string DropTableIfExists = DropTable + IfExists;
        public const string Else = " ELSE ";
        public const string EmptyAsDoubleQuote = " '' ";
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
        public const string IsNotNull = " IS NOT NULL ";
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
        public const string NullIf = " NULLIF ";
        public const string Ok = "ok";
        public const string On = " ON ";
        public const string OnConflict = " ON CONFLICT ";
        public const string OpenParenthesis = " ( ";
        public const string Or = " OR ";
        public const string OrderBy = " ORDER BY ";
        public const string OrderByRandom = OrderBy + " RANDOM() ";
        public const string PartitionBy = " PARTITION BY ";
        public const string Placeholder = " PLACEHOLDER ";
        public const string Plus = " + ";
        public const string Pragma = " PRAGMA ";
        public const string PragmaCacheSize = Pragma + " cache_size ";
        public const string PragmaForeignKeysEquals = Pragma + " foreign_keys " + Equal;
        public const string PragmaTableInfo = Pragma + " TABLE_INFO ";
        public const string PragmaSetForeignKeys = PragmaForeignKeysEquals + " 1 ";
        public const string PragmaForeignKeysOff = PragmaForeignKeysEquals + " OFF ";
        public const string PragmaForeignKeysOn = PragmaForeignKeysEquals + " ON ";
        public const string PragmaJournalModeWall = Pragma + " journal_mode = WAL";
        public const string PragmaQuickCheck = Pragma + " QUICK_CHECK ";
        public const string PragmaSynchronousNormal = Pragma + " synchronous = NORMAL";
        public const string PragmaTempStoreMemory = Pragma + " temp_store = MEMORY";
        public const string PrimaryKey = " PRIMARY KEY ";
        public const string RealType = " REAL ";
        public const string RenameTo = " RENAME TO ";
        public const string Replace = " REPLACE ";
        public const string Returning = " RETURNING ";
        public const string RowNumberOver = " ROW_NUMBER() OVER ";
        public const string QuotedEmptyString = " '' ";
        public const string Select = " SELECT ";
        public const string SelectDistinct = " SELECT DISTINCT ";
        public const string SelectDistinctStar = " SELECT DISTINCT * ";
        public const string SelectOne = " SELECT 1 ";
        public const string SelectOneFrom = " SELECT 1 " + Sql.From;
        public const string SelectStar = Select + Star; // SELECT * "
        public const string SelectStarFrom = SelectStar + From; // SELECT * FROM "

        public const string SelectCount = " SELECT COUNT ";
        public const string Distinct = " DISTINCT ";
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
        public const string Sum = " SUM ";
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
        public const string Vacuum = " VACUUM ";
        public const string Values = " VALUES ";
        public const string When = " WHEN ";
        public const string Where = " WHERE ";
        public const string With = " WITH ";
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

    #region SqlLine 
    // Generates short but complete generic SQL command lines ending with a semicolon and new line
    public static class SqlLine
    {
        // Form: (usually used to add an offset to an Id column)
        //   UPDATE tableName SET columnName = (offset + tableName.columnName);
        public static string AddOffsetToColumnInTable(string tableName, string columnName, long offset)
        {
            if (offset == 0)
            {
                // A zero offset means that we don't need to update the column as it  would have no effect
                return string.Empty;
            }

            return $"{Sql.Update} {tableName} {Sql.Set} {columnName} {Sql.Equal} {Sql.OpenParenthesis} {offset} {Sql.Plus} {tableName}{Sql.Dot}{columnName} {Sql.CloseParenthesis} {Sql.Semicolon} {Environment.NewLine}";
        }

        // Form:
        //  ATTACH DATABASE 'databasepath' AS alias;
        public static string AttachDatabaseAs(string databasePath, string alias)
        {
            return $"{Sql.AttachDatabase} {Sql.Quote(databasePath)} {Sql.As} {alias} {Sql.Semicolon} {Environment.NewLine}";
        }

        // Form: 
        //  BEGIN TRANSACTION  ; 
        public static string BeginTransaction()
        {
            return $"{Sql.BeginTransactionSemiColon} {Environment.NewLine}";
        }

        // Form:
        //  DROP TABLE IF EXISTS tempTable;
        //  CREATE TEMPORARY TABLE tempTable AS SELECT * FROM dataBaseName.tableName;
        public static string CreateTemporaryTableFromExistingTable(string tempTable, string dataBaseName, string tableName)
        {
            string query = SqlLine.DropTableIfExists(tempTable);
            query += $"{Sql.CreateTemporaryTable} {tempTable}  {Sql.As} {Sql.SelectStarFrom} {dataBaseName}{Sql.Dot}{tableName} {Sql.Semicolon} {Environment.NewLine}";
            return query;
        }

        // Form:
        //  DROP TABLE IF EXISTS tempTable;
        //  CREATE TEMPORARY TABLE tempTable AS SELECT column1, column2 etc FROM dataBaseName.tableName;
        public static string CreateTemporaryTableFromExistingTable(string tempTable, string dataBaseName, string tableName, string commaSeparatedcolumns)
        {
            string query = SqlLine.DropTableIfExists(tempTable);
            query += $"{Sql.CreateTemporaryTable} {tempTable}  {Sql.As} {Sql.Select} {commaSeparatedcolumns} {Sql.From} {dataBaseName}{Sql.Dot}{tableName} {Sql.Semicolon} {Environment.NewLine}";
            return query;
        }

        // Form:
        //  DROP TABLE IF EXISTS tableName;
        public static string DropTableIfExists(string tableName)
        {
            return $"{Sql.DropTableIfExists} {tableName} {Sql.Semicolon} {Environment.NewLine}";
        }

        // Form: 
        //  End TRANSACTION  ; 
        public static string EndTransaction()
        {
            return $"{Sql.EndTransactionSemiColon} {Environment.NewLine}";
        }

        // Create a query that returns the maximum value in the provided table. For example, it will get the maximum value in the column Id
        // Form:
        //  "Select Max(columnName) from tableName
        public static string GetMaxColumnValue(string columnName, string tableName)
        {
            return $"{Sql.Select} {Sql.Max} {Sql.OpenParenthesis} {columnName} {Sql.CloseParenthesis} {Sql.From} {tableName} {Sql.Semicolon} {Environment.NewLine}";
        }

        //  INSERT INTO table1 SELECT * FROM table2;
        public static string InsertTable2DataIntoTable1(string table1, string table2)
        {
            return $"{Sql.InsertInto} {table1} {Sql.SelectStarFrom} {table2} {Sql.Semicolon} {Environment.NewLine}";
        }

        //  INSERT INTO table1 SELECT column1, column2, ... FROM table2;
        public static string InsertTable2DataIntoTable1(string table1, string table2, List<string> listColumns)
        {
            string columns = string.Empty;
            foreach (string datalabels in listColumns)
            {
                columns += datalabels + ",";
            }
            columns = columns.TrimEnd(',');
            return $"{Sql.InsertInto} {table1} {Sql.Select} {columns} {Sql.From} {table2} {Sql.Semicolon} {Environment.NewLine}";
        }

        //  Form: INSERT INTO table SELECT * FROM dataBase.table;
        public static string InsertTableDataFromAnotherDatabase(string table, string fromDatabase)
        {
            return $"{Sql.InsertInto} {table} {Sql.SelectStarFrom} {fromDatabase}{Sql.Dot}{table} {Sql.Semicolon} {Environment.NewLine}";
        }

        // Form:
        //   PRAGMA  foreign_keys  =  OFF; 
        public static string ForeignKeyOff()
        {
            return $"{Sql.PragmaForeignKeysOff}{Sql.Semicolon}{Environment.NewLine}";
        }

        // Form:
        //   PRAGMA  foreign_keys  =  On; 
        public static string ForeignKeyOn()
        {
            return $"{Sql.PragmaForeignKeysOn}{Sql.Semicolon}{Environment.NewLine}";
        }
        // Form: Update tableName SET columnName = newValue;
        public static string UpdateColumnInTable(string tableName, string columnName, object newValue)
        {
            if (newValue is string)
            {
                // If the new value is a string, its best to quote it
                newValue = Sql.Quote(newValue.ToString());
            }
            return $"{Sql.Update} {tableName} {Sql.Set} {columnName} {Sql.Equal} {newValue} {Sql.Semicolon} {Environment.NewLine}";
        }
    }
    #endregion

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

            return phrase + Sql.From + DBTables.FileData +
                Sql.LeftJoin + DBTables.Detections +
                Sql.On + DBTables.FileData + Sql.Dot + DatabaseColumn.ID +
                Sql.Equal + DBTables.Detections + Sql.Dot + DatabaseColumn.ID +
                Sql.Where + DBTables.Detections + Sql.Dot + DatabaseColumn.ID + Sql.IsNull;
        }

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

            switch (sqlDataType)
            {
                // TODO 1 Urgent With Ints and reals: The Sql expressions are inconsistent with 0 vs blank. Debug with column where some values are blank and others are 0 with = and <>
                // Empty strings are Cast to 0. However, the empty string must be quoted as otherwise there is no SQL value!). 
                // I suspect those cases are handled in the Were early, as this expression is also returned with blanks
                // but that happens AS THE STRING IS BEING ENTERED!!!! So maybe should only update the count after the Custom Select string is committed
                // SELECT COUNT  ( * )  FROM DataTable WHERE ( ( img_individual_count IS NULL  OR img_individual_count =  ''  ) )
                case Sql.IntegerType:
                {
                    if (false == IsCondition.IsNumeric(value))
                    {
                        value = "0"; // Sql.Quote(value);
                    }
                    return Sql.Cast + Sql.OpenParenthesis + dataLabel + Sql.AsInteger + Sql.CloseParenthesis + mathOperator + value;
                }
                case Sql.RealType:
                {
                    if (false == IsCondition.IsNumeric(value))
                    {
                        value = "0";//Sql.Quote(value);
                    }
                    return Sql.Cast + Sql.OpenParenthesis + dataLabel + Sql.AsReal + Sql.CloseParenthesis + mathOperator + value;
                }
                default:
                    return dataLabel + mathOperator + Sql.Quote(value);
            }
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
            return DBTables.Detections + "." + DetectionColumns.Classification + Sql.Equal + classificationCategory;
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

        // This uses InvariantCulture to ensure that the decimal point is always a '.' in case of region formats that use a comma
        public static string DetectionsByDetectionCategoryAndConfidence(double detectionConfLower, double detectionConfHigher)
        {
            return $"{Sql.And} {DBTables.Detections}.{DetectionColumns.Conf} {Sql.Between} {detectionConfLower.ToString(CultureInfo.InvariantCulture)} {Sql.And} {detectionConfHigher.ToString(CultureInfo.InvariantCulture)} ";
        }

        // Count the number of classifications held by a particular detection.
        // First two number specifies detecton confidence range, second two numbers the classification confidence range, and the classifications is the particular classification of interest
        // Form:  AND  Detections.conf  BETWEEN  0.3  AND  1  AND  Detections.classification  =  '17' AND  Detections.classification_conf  BETWEEN  0.5  AND  1
        // It uses InvariantCulture to ensure that the decimal point is always a '.' in case of region formats that use a comma
        public static string ClassificationsByDetectionsAndClassificationCategoryAndConfidence(double detectionConfLower, double detectionConfHigher, string classificationCategory, double classificationConfLower,
            double classificationConfHigher)
        {
            return $"{Sql.And} {DBTables.Detections}.{DetectionColumns.Conf} {Sql.Between} {detectionConfLower.ToString(CultureInfo.InvariantCulture)} {Sql.And} {detectionConfHigher.ToString(CultureInfo.InvariantCulture)} " +
                   $"{Sql.And} {DBTables.Detections}.{DetectionColumns.Classification} {Sql.Equal} {Sql.Quote(classificationCategory)}" +
                   $"{Sql.And} {DBTables.Detections}.{DetectionColumns.ClassificationConf} {Sql.Between} {classificationConfLower.ToString(CultureInfo.InvariantCulture)} {Sql.And} {classificationConfHigher.ToString(CultureInfo.InvariantCulture)}";
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

        // A partial SQL phrase used as a prefix when selecting a Random sample
        // Form literal: Select * from DataTable WHERE Id IN (SELECT Id FROM (
        public static string GetRandomSamplePrefix()
        {
            // Select * from DataTable WHERE id IN (SELECT id FROM (
            return $"{Sql.SelectStarFrom} {DBTables.FileData} {Sql.Where} {Constant.DatabaseColumn.ID} " +
                     $"{Sql.In} {Sql.OpenParenthesis} {Sql.Select} {Constant.DatabaseColumn.ID} {Sql.From} {Sql.OpenParenthesis}";
        }

        public static string GetRandomSampleSuffix(int randomSampleCount)
        {
            return $"{Sql.CloseParenthesis} {Sql.OrderByRandom} {Sql.Limit} {randomSampleCount} {Sql.CloseParenthesis}";
        }

        public static string GetOrderByTerm(string sortingTerm)
        {
            return $"{Sql.OrderBy} {sortingTerm}";
        }

        public static string GetCommaThenTerm(string term)
        {
            return $"{Sql.Comma} {term}";
        }

        public static string GetCastCoalesceSorttermAsType(string sortTermDataLabel, string realOrIntType)
        {
            return $"{Sql.Cast} {Sql.OpenParenthesis} {Sql.Coalesce} {Sql.OpenParenthesis} {Sql.NullIf}" +
                   $"{Sql.OpenParenthesis} {sortTermDataLabel} {Sql.Comma} {Sql.EmptyAsDoubleQuote} {Sql.CloseParenthesis} {Sql.Comma} '-1' {Sql.CloseParenthesis} {realOrIntType} {Sql.CloseParenthesis}";
        }
    }
}
