using System.Windows.Controls;
using TimelapseTemplateEditor.EditorCode;

namespace TimelapseTemplateEditor.ControlsMetadata
{
    /// <summary>
    /// Interaction logic for MetadataTabControl.xaml
    /// </summary>
    public partial class MetadataTabControl
    {
        #region Properties
        // The level associated with this data grid, set when the tab control is created.
        public int Level { get; set; }

        public readonly string Guid = System.Guid.NewGuid().ToString();

        // A user-defined pseudonym for the level (with supplied default)
        private string levelAlias;
        public string LevelAlias
        {
            get => levelAlias;
            //get => string.IsNullOrEmpty(levelAlias)
            //        ? "$\"Level-{Level}\""
            //        : levelAlias;
            set
            {
                if (LevelAlias != value)
                {
                    levelAlias = value;
                    Globals.TemplateDatabase.UpsertMetadataInfoTableRow(Level, alias: value);
                }
                // As a side effect, it always sets the tab header
                TabItem tabItem = (TabItem)Parent;
                if (tabItem.Header is TextBlock tb)
                {
                    tb.Text = MetadataUIControl.CreateTemporaryAliasIfNeeded(Level, levelAlias);
                       // $"{levelAlias} ({Level})";
                }
            }
        }
        #endregion

        #region Constructor
        public MetadataTabControl(int level, string alias) 
        {
            InitializeComponent();
            Level = level;
            if (!string.IsNullOrWhiteSpace(alias)) levelAlias = alias;
            MetadataEditRowControls.ParentTab = this;
            MetadataDataEntryPreviewPanel.ParentTab = this;
            MetadataSpreadsheetPreviewControl.ParentTab = this;
            MetadataGridControl.ParentTab = this;
        }
        #endregion
    }
}
