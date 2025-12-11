using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using TimelapseTemplateEditor.EditorCode;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.Controls
{
    /// <summary>
    /// Interaction logic for TemplateSpreadsheetPreviewControl.xaml
    /// </summary>
    public partial class TemplateSpreadsheetPreviewControl 
    {
        #region Constructor
        public TemplateSpreadsheetPreviewControl()
        {
            InitializeComponent();
        }
        #endregion

        #region public Generate the Spreadsheet Preview
        // Generate the spreadsheet, adjusting the DateTime visibility as needed
        public void GenerateSpreadsheet()
        {
            List<ControlRow> controlsInSpreadsheetOrder = Globals.TemplateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();
            Globals.TemplateUI.TemplateSpreadsheetPreviewControl.SpreadsheetPreview.Columns.Clear();

            // Now generate the spreadsheet columns as needed. 
            foreach (ControlRow control in controlsInSpreadsheetOrder)
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
                    Globals.TemplateUI.TemplateSpreadsheetPreviewControl.SpreadsheetPreview.Columns.Add(column);
                }
            }
        }
        #endregion

        #region Private callbacks
        // When the spreadsheet order is changed by the user, update the spreadsheet order and the database
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
            Globals.TemplateDatabase.UpdateControlDisplayOrder(Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
            Globals.TemplateDataGridControl.DoLayoutUpdated(true);
            Globals.RootEditor.dataGridBeingUpdatedByCode = false;
        }
        #endregion

    }
}
