// ReSharper disable UnusedMember.Global
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
        public const string Concatenate = " || ";
        public const string Descending = " DESC ";
        public const string BeginTransaction = " BEGIN TRANSACTION ";
        public const string Between = " BETWEEN ";
        public const string Cast = " CAST ";
        public const string Count = " Count ";
        public const string CountStar = Sql.Count + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis;
        public const string CreateIndex = " CREATE INDEX ";
        public const string CreateTable = " CREATE TABLE ";
        public const string CloseParenthesis = " ) ";
        public const string Comma = ", ";
        public const string DateTimeFunction = " DateTime ";
        public const string Default = " DEFAULT ";
        public const string DeleteFrom = "DELETE FROM ";
        public const string Do = " DO ";
        public const string DropIndex = " DROP INDEX ";
        public const string DropTable = " DROP TABLE ";
        public const string Else = " ELSE ";
        public const string End = " END ";
        public const string EndTransaction = " END TRANSACTION ";
        public const string Equal = " = ";
        public const string ForeignKey = " FOREIGN KEY ";
        public const string From = " FROM ";
        public const string Glob = " GLOB ";
        public const string GreaterThan = " > ";
        public const string HoursQuoted = "' hours'";
        public const string IfNotExists = " IF NOT EXISTS ";
        public const string IfExists = " IF EXISTS ";
        public const string In = " In ";
        public const string InsertInto = " INSERT INTO ";
        public const string Instr = " INSTR ";
        public const string IntegerType = " INTEGER ";
        public const string IsNull = " IS NULL ";
        public const string Like = " LIKE ";
        public const string Limit = " LIMIT ";
        public const string Max = " MAX ";
        public const string Name = " NAME ";
        public const string Not = " NOT ";
        public const string NotEqual = " <> ";
        public const string NotNull = " NOT NULL ";
        public const string Null = " NULL ";
        public const string NullAs = Null + " " + As;
        public const string Ok = "ok";
        public const string On = " ON ";
        public const string OnDeleteCascade = " ON Delete Cascade ";
        public const string OpenParenthesis = " ( ";
        public const string Or = " OR ";
        public const string OrderBy = " ORDER BY ";
        public const string Pragma = " PRAGMA ";
        public const string PragmaForeignKeysEquals = Sql.Pragma + " foreign_keys " + Sql.Equal;
        public const string PragmaTableInfo = Sql.Pragma + " TABLE_INFO ";
        public const string PragmaForeignKeysOff = PragmaForeignKeysEquals + " OFF ";
        public const string PragmaForeignKeysOn = PragmaForeignKeysEquals + " ON ";
        public const string PragmaQuickCheck = Sql.Pragma + " QUICK_CHECK ";
        public const string PrimaryKey = " PRIMARY KEY ";
        public const string References = " References ";
        public const string RenameTo = " RENAME TO ";
        public const string Replace = " REPLACE ";
        public const string Select = " SELECT ";
        public const string SelectDistinct = " SELECT DISTINCT ";
        public const string SelectOne = " SELECT 1 ";
        public const string SelectStar = Sql.Select + Sql.Star; // SELECT * "
        public const string SelectStarFrom = Sql.SelectStar + Sql.From; // SELECT * FROM "

        public const string SelectCount = " SELECT COUNT ";
        public const string SelectDistinctCount = " SELECT DISTINCT COUNT ";
        public const string SelectCountStarFrom = Sql.SelectCount + Sql.OpenParenthesis + Sql.Star + Sql.CloseParenthesis + Sql.From;
        public const string SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals = Sql.Select + Sql.Name + Sql.From + Sql.SqlMaster + Sql.Where + Sql.TypeEqualsTable + Sql.And + Sql.Name + Sql.Equal;
        public const string SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals = Sql.SelectCountStarFrom + Sql.SqlMaster + Sql.Where + Sql.TypeEqualsIndex + Sql.And + Sql.Name + Sql.Equal;
        public const string Semicolon = " ; ";
        public const string Set = " SET ";
        public const string SqlMaster = " sqlite_master ";
        public const string Star = "*";
        public const string Strftime = " Strftime ";
        public const string StringType = " STRING ";

        public const string Real = " REAL ";
        public const string Text = "TEXT";
        public const string Then = " THEN ";
        public const string Trim = " TRIM ";
        public const string True = " TRUE ";
        public const string TypeEquals = " TYPE " + Sql.Equal;
        public const string TypeEqualsTable = Sql.TypeEquals + " 'table' ";
        public const string TypeEqualsIndex = Sql.TypeEquals + " 'index' ";
        public const string Update = " UPDATE ";
        public const string Values = " VALUES ";
        public const string When = " WHEN ";
        public const string Where = " WHERE ";

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
}
