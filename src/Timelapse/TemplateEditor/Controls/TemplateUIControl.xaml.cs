using System.Windows;

namespace TimelapseTemplateEditor.Controls
{
    /// <summary>
    /// Interaction logic for TemplateUIControl.xaml
    /// </summary>
    public partial class TemplateUIControl
    {
        // Must match the MinHeight of DisplayGridRow in XAML.
        // Ensures the data grid + button panel are always fully visible.
        private const double DataGridRowMinHeight = 260;

        public TemplateUIControl()
        {
            InitializeComponent();
            Loaded += UserControl_Loaded;
            GridContainingPrimaryUI.SizeChanged += GridContainingPrimaryUI_SizeChanged;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDataEntryPanelMaxHeight();
        }

        private void GridContainingPrimaryUI_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateDataEntryPanelMaxHeight();
        }

        // Dynamically caps TemplateDataEntryPreviewPanel so that:
        //   - TemplateDataGridControl / TemplateEditRowsControlNew always have at least DataGridRowMinHeight
        //   - TemplateSpreadsheetPreviewControl always has its full natural height
        //   - TemplateDataEntryPreviewPanel uses all remaining space (inner ScrollViewer scrolls if content overflows)
        private void UpdateDataEntryPanelMaxHeight()
        {
            double gridHeight = GridContainingPrimaryUI.ActualHeight;
            if (gridHeight <= 0)
                return;

            // Measure the spreadsheet at infinite height to get its natural (desired) size,
            // independent of where it currently sits in the layout.
            TemplateSpreadsheetPreviewControl.Measure(
                new Size(GridContainingPrimaryUI.ActualWidth, double.PositiveInfinity));
            double spreadsheetHeight = TemplateSpreadsheetPreviewControl.DesiredSize.Height
                                       + TemplateSpreadsheetPreviewControl.Margin.Top;

            double panelMargin = TemplateDataEntryPreviewPanel.Margin.Top;
            double maxHeight = gridHeight - DataGridRowMinHeight - spreadsheetHeight - panelMargin;

            // Never collapse the panel entirely; keep at least 80px so some controls are visible.
            TemplateDataEntryPreviewPanel.MaxHeight = maxHeight > 80 ? maxHeight : 80;
        }
    }
}
