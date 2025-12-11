using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace TimelapseTemplateEditor
{
    internal static class EditorConstant
    {
        public const string MainWindowBaseTitle = "Timelapse Template Editor";  // The initial title shown in the window title bar
        public static TemplateEditorWindow templateEditorWindow;

        public static readonly SolidColorBrush NotEditableCellColor = Brushes.LightGray; // Color of non-editable data grid items 

        // reserved words that cannot be used as a data label
        public static readonly ReadOnlyCollection<string> ReservedSqlKeywords = new List<string>
        {
            "abort", "action", "add", "after", "all", "alter", "analyze", "and", "as", "asc", "attach", "autoincrement", "before", "begin", "between",
            "by", "cascade", "case", "cast", "check", "collate", "column", "commit", "conflict", "constraint", "create", "cross", "current_date",
            "current_time", "current_timestamp", "datetime", "database", "default", "deferrable", "deferred", "delete", "desc", "detach", "distinct", "drop",
            "each", "else", "end", "escape", "except", "exclusive", "exists", "explain", "fail", "for", "foreign", "from", "full", "glob", "group",
            "having", "id", "if", "ignore", "immediate", "in", "index", "indexed", "initially", "inner", "insert", "instead", "intersect", "into",
            "is", "isnull", "join", "key", "left", "like", "limit", "match", "natural", "no", "not", "nothing", "notnull", "null", "of", "offset", "on", "or",
            "order", "outer", "plan", "pragma", "primary", "query", "raise", "recursive", "references", "regexp", "reindex", "release", "rename",
            "replace", "restrict", "right", "rollback", "row", "savepoint", "select", "set", "table", "temp", "temporary", "then", "to", "transaction",
            "trigger", "union", "unique", "update", "using", "vacuum", "values", "view", "virtual", "when", "where", "with", "without",
            "date", "time", "datetime"
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
            public const string Export = "Export";
        }

        public class Standards
        {
            private const string ResourcePath = "pack://application:,,,/TemplateEditor/Resources/";
            private const string OverviewSuffix = "Overview.rtf";
            private const string TemplateSuffix = "Template.tdb";
            public const string AlbertaMetadataStandardsTitle = "Alberta Metadata Standards";
            public const string AlbertaMetadataStandards = "AlbertaMetadataStandards"; 
            public const string AlbertaMetadataStandardsOverview = ResourcePath + AlbertaMetadataStandards + OverviewSuffix;
            public const string AlbertaMetadataStandardsTemplate = ResourcePath + AlbertaMetadataStandards + TemplateSuffix;
            public const string CamtrapDPTitle = "CamtrapDP Standard";
            public const string CamtrapDP = "CamtrapDP";
            public const string CamtrapDPOverview = ResourcePath + CamtrapDP + OverviewSuffix;
            public const string CamtrapDPTemplate = ResourcePath + CamtrapDP + TemplateSuffix;
        }
    }
}