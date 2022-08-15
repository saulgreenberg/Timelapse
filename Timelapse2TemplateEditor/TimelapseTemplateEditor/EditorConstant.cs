using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Timelapse.Editor
{
    internal static class EditorConstant
    {
        public const string ApplicationName = "Timelapse.Editor";
        public const string MainWindowBaseTitle = "Timelapse Template Editor";  // The initial title shown in the window title bar

        public static readonly SolidColorBrush NotEditableCellColor = Brushes.LightGray; // Color of non-editable data grid items 
        public static readonly Uri LatestVersionAddress = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/timelapse_template_version.xml");

        // reserved words that cannot be used as a data label
        public static readonly ReadOnlyCollection<string> ReservedSqlKeywords = new List<string>()
        {
            "ABORT", "ACTION", "ADD", "AFTER", "ALL", "ALTER", "ANALYZE", "AND", "AS", "ASC", "ATTACH", "AUTOINCREMENT", "BEFORE", "BEGIN", "BETWEEN",
            "BY", "CASCADE", "CASE", "CAST", "CHECK", "COLLATE", "COLUMN", "COMMIT", "CONFLICT", "CONSTRAINT", "CREATE", "CROSS", "CURRENT_DATE",
            "CURRENT_TIME", "CURRENT_TIMESTAMP", "DATABASE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DELETE", "DESC", "DETACH", "DISTINCT", "DROP",
            "EACH", "ELSE", "END", "ESCAPE", "EXCEPT", "EXCLUSIVE", "EXISTS", "EXPLAIN", "FAIL", "FOR", "FOREIGN", "FROM", "FULL", "GLOB", "GROUP",
            "HAVING", "ID", "IF", "IGNORE", "IMMEDIATE", "IN", "INDEX", "INDEXED", "INITIALLY", "INNER", "INSERT", "INSTEAD", "INTERSECT", "INTO",
            "IS", "ISNULL", "JOIN", "KEY", "LEFT", "LIKE", "LIMIT", "MATCH", "NATURAL", "NO", "NOT", "NOTHING", "NOTNULL", "NULL", "OF", "OFFSET", "ON", "OR",
            "ORDER", "OUTER", "PLAN", "PRAGMA", "PRIMARY", "QUERY", "RAISE", "RECURSIVE", "REFERENCES", "REGEXP", "REINDEX", "RELEASE", "RENAME",
            "REPLACE", "RESTRICT", "RIGHT", "ROLLBACK", "ROW", "SAVEPOINT", "SELECT", "SET", "TABLE", "TEMP", "TEMPORARY", "THEN", "TO", "TRANSACTION",
            "TRIGGER", "UNION", "UNIQUE", "UPDATE", "USING", "VACUUM", "VALUES", "VIEW", "VIRTUAL", "WHEN", "WHERE", "WITH", "WITHOUT"
        }.AsReadOnly();

        // a few control values not needed in Constant.Control
        public static class ColumnHeader
        {
            // data grid column headers
            // these are human friendly forms of data labels
            // these constants are duplicated in MainWindow.xaml and must be kept in sync
            public const string ControlOrder = "Control\norder";
            public const string DataLabel = "Data Label";
            public const string DefaultValue = "Default Value";
            public const string ID = "ID";
            public const string Width = "Width";
            public const string SpreadsheetOrder = "Spreadsheet\norder";
        }

        public static class Registry
        {
            public static class EditorKey
            {
                // key containing the list of most recent template databases opened by the editor
                public const string MostRecentlyUsedTemplates = "MostRecentlyUsedTemplates";
            }
        }
    }
}