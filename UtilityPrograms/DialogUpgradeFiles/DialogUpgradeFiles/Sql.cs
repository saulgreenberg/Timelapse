namespace DialogUpgradeFiles
{
    // Create SQL commands using constants rather than typing the SQL keywords. 
    // This really helps avoid typos, bugs due to spacing such as not having spaces inbetween keywords, etc.
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
        public const string Concatenate = " || ";
        public const string Descending = " DESC ";
        public const string Dot = ".";
        public const string DotStar = Sql.Dot + Sql.Star;
        public const string BeginTransaction = " BEGIN TRANSACTION ";
        public const string BeginTransactionSemiColon = Sql.BeginTransaction + Sql.Semicolon;
        public const string Between = " BETWEEN ";
        public const string CaseWhen = " CASE WHEN ";
        public const string Cast = " CAST ";
        public const string Count = " Count ";
        public const string CountStar = Sql.Count + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis;
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
        public const string DoUpgrade = " Do UPDATE ";
        public const string DropIndex = " DROP INDEX ";
        public const string DropTable = " DROP TABLE ";
        public const string Else = " ELSE ";
        public const string End = " END ";
        public const string EndTransaction = " END TRANSACTION ";
        public const string EndTransactionSemiColon = Sql.EndTransaction + Sql.Semicolon;
        public const string Equal = " = ";
        public const string EqualsCaseID = " = CASE Id";
        public const string ForeignKey = " FOREIGN KEY ";
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
        public const string LeftJoin = " LEFT JOIN ";
        public const string LessThanEqual = " <= ";
        public const string LessThan = " < ";
        public const string Like = " LIKE ";
        public const string Limit = " LIMIT ";
        public const string LimitOne = Limit + " 1 ";
        public const string Max = " MAX ";
        public const string MasterTableList = "sqlite_master";
        public const string Name = " NAME ";
        public const string NameFromSqliteMaster = " NAME FROM SQLITE_MASTER ";
        public const string Not = " NOT ";
        public const string NotEqual = " <> ";
        public const string NotNull = " NOT NULL ";
        public const string Null = " NULL ";
        public const string NullAs = Null + " " + As;
        public const string Ok = "ok";
        public const string On = " ON ";
        public const string OnConflict = " ON CONFLICT ";
        public const string OnDeleteCascade = " ON Delete Cascade ";
        public const string OpenParenthesis = " ( ";
        public const string Or = " OR ";
        public const string OrderBy = " ORDER BY ";
        public const string OrderByRandom = Sql.OrderBy + " RANDOM() ";
        public const string Plus = " + ";
        public const string Pragma = " PRAGMA ";
        public const string PragmaForeignKeysEquals = Sql.Pragma + " foreign_keys " + Sql.Equal;
        public const string PragmaTableInfo = Sql.Pragma + " TABLE_INFO ";
        public const string PragmaSetForeignKeys = Sql.PragmaForeignKeysEquals + " 1 ";
        public const string PragmaForeignKeysOff = PragmaForeignKeysEquals + " OFF ";
        public const string PragmaForeignKeysOn = PragmaForeignKeysEquals + " ON ";
        public const string PragmaQuickCheck = Sql.Pragma + " QUICK_CHECK ";
        public const string PrimaryKey = " PRIMARY KEY ";
        public const string References = " References ";
        public const string RenameTo = " RENAME TO ";
        public const string Replace = " REPLACE ";
        public const string QuotedEmptyString = " '' ";
        public const string Select = " SELECT ";
        public const string SelectDistinct = " SELECT DISTINCT ";
        public const string SelectDistinctStar = " SELECT DISTINCT * ";
        public const string SelectOne = " SELECT 1 ";
        public const string SelectStar = Sql.Select + Sql.Star; // SELECT * "
        public const string SelectStarFrom = Sql.SelectStar + Sql.From; // SELECT * FROM "

        public const string SelectCount = " SELECT COUNT ";
        public const string SelectDistinctCount = " SELECT DISTINCT COUNT ";
        public const string SelectCountStarFrom = Sql.SelectCount + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis + Sql.From;
        public const string SelectDistinctCountStarFrom = Sql.SelectDistinctCount + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis + Sql.From;
        public const string SelectExists = " SELECT EXISTS ";
        public const string SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals = Sql.Select + Sql.Name + Sql.From + Sql.SqlMaster + Sql.Where + Sql.TypeEqualsTable + Sql.And + Sql.Name + Sql.Equal;
        public const string SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals = Sql.SelectCountStarFrom + Sql.SqlMaster + Sql.Where + Sql.TypeEqualsIndex + Sql.And + Sql.Name + Sql.Equal;
        public const string Semicolon = " ; ";
        public const string Set = " SET ";
        public const string SqlMaster = " sqlite_master ";
        public const string Star = "*";
        public const string Strftime = " Strftime ";
        public const string StringType = " STRING ";
        public const string Substr = " SUBSTR ";

        public const string Real = " REAL ";
        public const string Text = "TEXT";
        public const string Then = " THEN ";
        public const string Trim = " TRIM ";
        public const string True = " TRUE ";
        public const string TypeEquals = " TYPE " + Sql.Equal;
        public const string TypeEqualsTable = Sql.TypeEquals + " 'table' ";
        public const string TypeEqualsIndex = Sql.TypeEquals + " 'index' ";
        public const string UnionAll = " UNION ALL";
        public const string Update = " UPDATE ";
        public const string Values = " VALUES ";
        public const string When = " WHEN ";
        public const string Where = " WHERE ";
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
    //public static class SqlPhrase
    //{
    //    /// <summary>
    //    /// Sql Phrase - Create partial query to return all missing detections
    //    /// </summary>
    //    ///  <param name="useCountForm">If true, return a SELECT COUNT vs a SELECT from</param>
    //    /// <returns> 
    //    /// Count Form:  SELECT COUNT  ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
    //    /// Star Form: SELECT DataTable.*               FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
    //    /// One Form:  SELECT 1                         FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
    //    public static string SelectMissingDetections(SelectTypesEnum selectType)
    //    {
    //        string phrase = string.Empty;
    //        if (selectType == SelectTypesEnum.Count)
    //        {
    //            phrase = Sql.SelectCount + Sql.OpenParenthesis + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis;
    //        }
    //        else if (selectType == SelectTypesEnum.Star)
    //        {
    //            phrase = Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
    //        }
    //        else if (selectType == SelectTypesEnum.One)
    //        {
    //            phrase = Sql.SelectOne;
    //        }
    //        //string phrase = useCountForm
    //        //    ? Sql.SelectCount + Sql.OpenParenthesis + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis
    //        //    : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
    //        return phrase + Sql.From + Constant.DBTables.FileData +
    //            Sql.LeftJoin + Constant.DBTables.Detections +
    //            Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
    //            Sql.Equal + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID +
    //            Sql.Where + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID + Sql.IsNull;
    //    }
    //public static string SelectMissingDetections(bool useCountForm)
    //{
    //    string phrase = useCountForm
    //        ? Sql.SelectCount + Sql.OpenParenthesis + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis
    //        : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
    //    return phrase + Sql.From + Constant.DBTables.FileData +
    //        Sql.LeftJoin + Constant.DBTables.Detections +
    //        Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
    //        Sql.Equal + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID +
    //        Sql.Where + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID + Sql.IsNull;
    //}

    /// <summary>
    /// Sql Phrase - Create partial query to return detections
    /// </summary>
    /// <param name="useCountForm">If true, form is SELECT COUNT vs SELECT</param>
    /// <returns>
    /// Count Form:  SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
    /// Star Form:   SELECT DataTable.*                     FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
    /// One Form:   SELECT 1                                FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
    /// </returns>
    //public static string SelectDetections(SelectTypesEnum selectType)
    //{
    //    string phrase = string.Empty;
    //    if (selectType == SelectTypesEnum.Count)
    //    {
    //        phrase = Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct + Constant.DBTables.FileData + Sql.DotStar;
    //    }
    //    else if (selectType == SelectTypesEnum.Star)
    //    {
    //        phrase = Sql.Select + Constant.DBTables.FileData + Sql.DotStar;
    //    }
    //    else if (selectType == SelectTypesEnum.One)
    //    {
    //        phrase = Sql.SelectOne;
    //    }
    //    return phrase + Sql.From + Constant.DBTables.Detections + Sql.InnerJoin + Constant.DBTables.FileData +
    //            Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.Equal + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
    //}
    //public static string SelectDetections(bool useCountForm)
    //{
    //    string phrase = useCountForm
    //        //? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectStar
    //        ? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct + Constant.DBTables.FileData + Sql.DotStar
    //        : Sql.Select + Constant.DBTables.FileData + Sql.DotStar;


    //    return phrase + Sql.From + Constant.DBTables.Detections + Sql.InnerJoin + Constant.DBTables.FileData +
    //            Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID + Sql.Equal + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
    //}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="useCountForm"></param>
    /// <returns>
    /// Count Form:  Select COUNT  ( * )  FROM (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
    /// Star Form:   SELECT  DISTINCT                           DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
    /// One Form     SELECT ONE           FROM (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
    /// 
    /// </returns>
    //public static string SelectClassifications(SelectTypesEnum selectType)
    //{
    //    string phrase = string.Empty;
    //    if (selectType == SelectTypesEnum.Count)
    //    {
    //        phrase = Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct;
    //    }
    //    else if (selectType == SelectTypesEnum.Star)
    //    {
    //        phrase = Sql.SelectDistinct;
    //    }
    //    else if (selectType == SelectTypesEnum.One)
    //    {
    //        phrase = Sql.SelectOne + Sql.From + Sql.OpenParenthesis + Sql.SelectDistinct;
    //    }

    //    phrase += Constant.DBTables.FileData + Sql.DotStar + Sql.From + Constant.DBTables.Classifications +
    //            Sql.InnerJoin + Constant.DBTables.FileData + Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
    //            Sql.Equal + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;

    //    // and now append INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
    //    phrase += Sql.InnerJoin + Constant.DBTables.Detections + Sql.On +
    //        Constant.DBTables.Detections + Sql.Dot + Constant.DetectionColumns.DetectionID + Sql.Equal +
    //        Constant.DBTables.Classifications + "." + Constant.DetectionColumns.DetectionID;

    //    return phrase;
    //}
    //public static string SelectClassifications(bool useCountForm)
    //{
    //    string phrase = useCountForm
    //        ? Sql.SelectCountStarFrom + Sql.OpenParenthesis + Sql.SelectDistinct
    //        : Sql.SelectDistinct;
    //    //     : Sql.SelectDistinct + Constant.DBTables.Classifications + Sql.Dot + Constant.ClassificationColumns.Conf + Sql.Comma;
    //    phrase += Constant.DBTables.FileData + Sql.DotStar + Sql.From + Constant.DBTables.Classifications +
    //            Sql.InnerJoin + Constant.DBTables.FileData + Sql.On + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID +
    //            Sql.Equal + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID;
    //    // and now append INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
    //    phrase += Sql.InnerJoin + Constant.DBTables.Detections + Sql.On +
    //        Constant.DBTables.Detections + Sql.Dot + Constant.DetectionColumns.DetectionID + Sql.Equal +
    //        Constant.DBTables.Classifications + "." + Constant.DetectionColumns.DetectionID;
    //    return phrase;
    //}

    /// <summary>
    /// Sql phrase used in Where
    /// </summary>
    /// <param name="datalabel"></param>
    /// <returns> ( label IS NULL OR  label = '' ) ;</returns>
    //public static string LabelIsNullOrDataLabelEqualsEmpty(string datalabel)
    //{
    //    return Sql.OpenParenthesis + datalabel + Sql.IsNull + Sql.Or + datalabel + Sql.Equal + Sql.QuotedEmptyString + Sql.CloseParenthesis;
    //}

    ///// <summary>
    ///// Sql phrase used in Where
    ///// </summary>
    ///// <param name="dataLabel"></param>
    ///// <param name="mathOperator"></param>
    ///// <param name="value"></param>
    ///// <returns>DataLabel operator "value", e.g., DataLabel > "5"</returns>
    //public static string DataLabelOperatorValue(string dataLabel, string mathOperator, string value, bool castAsInteger)
    //{
    //    value = value == null ? string.Empty : value.Trim();

    //    return castAsInteger
    //        ? Sql.Cast + Sql.OpenParenthesis + dataLabel + Sql.AsInteger + Sql.CloseParenthesis + mathOperator + Sql.Quote(value) 
    //        : dataLabel + mathOperator + Sql.Quote(value);
    //}

    ///// <returns>DataLabel operator "value", e.g., Date(datetime)= Date('2016-08-19 19:08:22')</returns>
    //public static string DataLabelDateTimeOperatorValue(string dataLabel, string mathOperator, string value)
    //{
    //    value = value == null ? string.Empty : value.Trim();
    //    return Sql.DateFunction + Sql.OpenParenthesis + dataLabel + Sql.CloseParenthesis + mathOperator + Sql.DateFunction + Sql.OpenParenthesis + Sql.Quote(value) + Sql.CloseParenthesis;
    //}

    ///// <summary>
    ///// Sql phrase used in Where
    ///// </summary>
    ///// <param name="detectionCategory"></param>
    ///// <returns>Detections.Category = <DetectionCategory></returns>
    //public static string DetectionCategoryEqualsDetectionCategory(string detectionCategory)
    //{
    //    return Constant.DBTables.Detections + "." + Constant.DetectionColumns.Category + Sql.Equal + detectionCategory;
    //}

    ///// <summary>
    ///// Sql phrase used in Where
    ///// </summary>
    ///// <param name="classificationCategory"></param>
    ///// <returns>Classifications.Category = <ClassificationCategory></returns>
    //public static string ClassificationsCategoryEqualsClassificationCategory(string classificationCategory)
    //{
    //    return Constant.DBTables.Classifications + "." + Constant.DetectionColumns.Category + Sql.Equal + classificationCategory;
    //}

    ///// <summary>
    ///// Sql phrase used in Where
    ///// </summary>
    ///// <param name="lowerBound"></param>
    ///// <param name="upperBound"></param>
    ///// <returns>Group By Detections.Id Having Max ( Detections.conf ) BETWEEN <lowerBound> AND <upperBound></returns>
    //public static string GroupByDetectionsIdHavingMaxDetectionsConf(double lowerBound, double upperBound)
    //{
    //    return Sql.GroupBy + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID +
    //        Sql.Having + Sql.Max +
    //        Sql.OpenParenthesis + Constant.DBTables.Detections + "." + Constant.DetectionColumns.Conf + Sql.CloseParenthesis +
    //        Sql.Between + lowerBound.ToString() + Sql.And + upperBound.ToString();
    //}

    ///// <summary>
    ///// Sql phrase used in Where
    ///// </summary>
    ///// <param name="lowerBound"></param>
    ///// <param name="upperBound"></param>
    ///// <returns>GROUP BY Classifications.classificationID HAVING MAX  ( Classifications.conf ) BETWEEN <lowerBound> AND <upperBound></returns>
    //public static string GroupByClassificationsIdHavingMaxClassificationsConf(double lowerBound, double upperBound)
    //{
    //    return Sql.GroupBy + Constant.DBTables.Classifications + "." + Constant.ClassificationColumns.ClassificationID +
    //        Sql.Having + Sql.Max +
    //        Sql.OpenParenthesis + Constant.DBTables.Classifications + "." + Constant.DetectionColumns.Conf + Sql.CloseParenthesis +
    //        Sql.Between + lowerBound.ToString() + Sql.And + upperBound.ToString();
    //}

    ///// <summary>
    ///// Episode-related phrases. Used in constructing a front wrapper for selecting or counting  files where all files in an episode have at least one file matching the surrounded search condition 
    ///// </summary>
    ///// <param name="tableName"></param>
    ///// <param name="episodeNoteField"></param>
    //public static string CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(string tableName, string episodeNoteField, bool CountOnly)
    //{
    //    // using DataTable and Episode,
    //    // Select Complete form:  String.Format("Select * from DataTable WHERE SUBSTR(DataTable.{0}, 0, instr(DataTable.{0}, ':')) in (Select substr({0}, 0, instr({0}, ':')) From (", episodeNoteField);
    //    // Count Complete formstring frontWrapper = String.Format("Select  COUNT  ( * ) from DataTable WHERE SUBSTR(DataTable.{0}, 0, instr(DataTable.{0}, ':')) in (Select substr({0}, 0, instr({0}, ':')) From ", this.CustomSelection.EpisodeNoteField);
    //    // Line by line form:  
    //    // Count Form:  Select Count (*) from 
    //    // Select Form: Select * from 
    //    // DataTable WHERE SUBSTR(DataTable.{0}, 0,
    //    //                           instr(DataTable.{0}, ':'))
    //    // IN (Selectsubstr({0}, 0
    //    //                          instr({0}, ':'))
    //    // FROM 
    //    // Count form:   
    //    // Select form:  (
    //    string frontwrapper = CountOnly
    //        ? Sql.SelectCountStarFrom 
    //        : Sql.SelectStarFrom;
    //    frontwrapper += tableName + Sql.Where + Sql.Substr + Sql.OpenParenthesis + tableName + Sql.Dot + episodeNoteField + Sql.Comma + "0" + Sql.Comma
    //                        + Sql.Instr + Sql.OpenParenthesis + tableName + Sql.Dot + episodeNoteField + Sql.Comma + Sql.Quote(":") + Sql.CloseParenthesis + Sql.CloseParenthesis
    //                        + Sql.In + Sql.OpenParenthesis + Sql.Select + Sql.Substr + Sql.OpenParenthesis + episodeNoteField + Sql.Comma + "0" + Sql.Comma
    //                        + Sql.Instr + Sql.OpenParenthesis + episodeNoteField + Sql.Comma + Sql.Quote(":") + Sql.CloseParenthesis + Sql.CloseParenthesis
    //                        + Sql.From;
    //    frontwrapper += CountOnly
    //        ? string.Empty 
    //        : Sql.OpenParenthesis;
    //    return frontwrapper;
    //}       
    //}
}
