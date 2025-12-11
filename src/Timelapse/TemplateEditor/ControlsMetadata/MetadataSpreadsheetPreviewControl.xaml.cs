using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using TimelapseTemplateEditor.EditorCode;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.ControlsMetadata
{
    /// <summary>
    /// Interaction logic for MetadataSpreadsheetPreviewControl.xaml
    /// </summary>
    public partial class MetadataSpreadsheetPreviewControl
    {
        public MetadataTabControl ParentTab { get; set; }
        #region Constructor
        public MetadataSpreadsheetPreviewControl()
        {
            InitializeComponent();
        }
        #endregion

        #region public Generate the Spreadsheet Preview
        // Generate the spreadsheet, adjusting the DateTime and UTCOffset visibility as needed
        public void GenerateSpreadsheet(int level)
        {
            if (null == Globals.TemplateDatabase?.MetadataControlsByLevel)
            {
                return;
            }
            if (false == Globals.TemplateDatabase.MetadataControlsByLevel.ContainsKey(ParentTab.Level)) return;
            List<MetadataControlRow> controlsInSpreadsheetOrder = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level].OrderBy(control => control.SpreadsheetOrder).ToList();
            SpreadsheetPreview.Columns.Clear();

            // Now generate the spreadsheet columns as needed. 
            foreach (MetadataControlRow control in controlsInSpreadsheetOrder)
            {
                DataGridTextColumn column = new();
                if (control.ExportToCSV == false)
                {
                    continue;
                }
                string dataLabel = control.DataLabel;
                if (string.IsNullOrEmpty(dataLabel))
                {
                    TracePrint.PrintMessage("GenerateSpreadsheet: Database constructors should guarantee data labels are not null.");
                }
                else
                {
                    column.Header = dataLabel;
                    SpreadsheetPreview.Columns.Add(column);
                }
            }
        }
        #endregion

        #region Callback: OnSpreadsheetOrderChanged
        private void OnSpreadsheetOrderChanged(object sender, DataGridColumnEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            Dictionary<string, long> spreadsheetOrderByDataLabel = [];
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                string dataLabelFromColumnHeader = column.Header.ToString();
                long newSpreadsheetOrder = column.DisplayIndex + 1;
                if (dataLabelFromColumnHeader != null)
                {
                    spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
                }
            }
            Globals.RootEditor.dataGridBeingUpdatedByCode = true;
            Globals.TemplateDatabase.UpdateMetadataControlDisplayOrder(ParentTab.Level, Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
            Globals.Root.MetadataUI.BindMetadataToDataGrid(ParentTab.Level);
            this.ParentTab.MetadataGridControl.DoLayoutUpdated(true);
            Globals.RootEditor.dataGridBeingUpdatedByCode = false;
        }
        #endregion
    }
}
