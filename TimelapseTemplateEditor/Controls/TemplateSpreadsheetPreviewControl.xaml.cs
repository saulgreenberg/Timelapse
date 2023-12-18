using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using TimelapseTemplateEditor.EditorCode;
using Constant=Timelapse.Constant;

namespace TimelapseTemplateEditor.Controls
{
    /// <summary>
    /// Interaction logic for TemplateSpreadsheetPreviewControl.xaml
    /// </summary>
    public partial class TemplateSpreadsheetPreviewControl 
    {
        public TemplateSpreadsheetPreviewControl()
        {
            InitializeComponent();
        }

        #region public Generate the Spreadsheet Preview
        // Generate the spreadsheet, adjusting the DateTime and UTCOffset visibility as needed
        public void GenerateSpreadsheet()
        {
            List<ControlRow> controlsInSpreadsheetOrder = Globals.TemplateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();
            Globals.TemplateUI.TemplateSpreadsheetPreviewControl.SpreadsheetPreview.Columns.Clear();

            // Now generate the spreadsheet columns as needed. 
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                DataGridTextColumn column = new DataGridTextColumn();
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
            Dictionary<string, long> spreadsheetOrderByDataLabel = new Dictionary<string, long>();
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                string dataLabelFromColumnHeader = column.Header.ToString();
                long newSpreadsheetOrder = column.DisplayIndex + 1;
                spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
            }
            Globals.RootEditor.dataGridBeingUpdatedByCode = true;
            Globals.TemplateDatabase.UpdateControlDisplayOrder(Constant.Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
            Globals.RootEditor.dataGridBeingUpdatedByCode = false;
        }
        #endregion

    }
}
