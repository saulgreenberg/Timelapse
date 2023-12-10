using System.Windows;
using System.Windows.Controls;
using TimelapseTemplateEditor.EditorCode;

namespace TimelapseTemplateEditor.Controls
{
    /// <summary>
    /// Callbacks to add or remove rows from the template data table
    /// </summary>
    public partial class TemplateEditRowsControl
    {
        public TemplateEditRowsControl()
        {
            InitializeComponent();
        }

        // Add a data row (i.e. a control definition) to the datagrid and thus the template.
        // - the row type is decided by the button tags.
        // - default values are set for the added row, and differ depending on type.
        private void ButtonAddDataRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string controlType = button.Tag.ToString();
                Globals.Root.TemplateDoAddNewRow(controlType);
            }
        }

        // Remove a data row from the datagrid and thus from the template
        // - this shifts up the ids on the remaining rows.
        // - required rows cannot be deleted, as the RemoveControl button will be unselectable.
        private void ButtonRemoveDataRow_Click(object sender, RoutedEventArgs e)
        {
            Globals.Root.TemplateDoRemoveSelectedRow();
        }
    }
}
