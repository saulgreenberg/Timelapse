using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using TimelapseTemplateEditor.EditorCode;

namespace TimelapseTemplateEditor.Controls
{
    /// <summary>
    /// Interaction logic for TemplateEditRowsControlNew.xaml
    /// </summary>
    public partial class TemplateEditRowsControlNew
    {
        public TemplateEditRowsControlNew()
        {
            InitializeComponent();
        }

        #region Callbacks: Add / Remove row
        // Add a data row (i.e. a control definition) to the datagrid and thus the template.
        // - the row type is decided by the button tags.
        // - default values are set for the added row, and differ depending on type.
        private void MenuItemAddDataRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem button)
            {
                string controlType = button.Tag.ToString();
                bool result = true;
                if (EditorConstant.templateEditorWindow.standardType != string.Empty)
                {
                    result = true == Dialogs.ChangesToStandardWarning(EditorConstant.templateEditorWindow, "Adding controls", EditorConstant.templateEditorWindow.standardType);
                }
                if (result)
                {
                    Globals.Root.TemplateDoAddNewRow(controlType);
                }
            }
        }

        // Remove a data row from the datagrid and thus from the template
        // - this shifts up the ids on the remaining rows.
        // - required rows cannot be deleted, as the RemoveControl button will be unselectable.
        private void ButtonRemoveDataRow_Click(object sender, RoutedEventArgs e)
        {
            Globals.Root.TemplateDoRemoveSelectedRow();
            RemoveControlButton.IsEnabled = false;
        }
        #endregion

        #region Public DoAddNewRow
        // Add a data row (and thus define a new control) to the table
        public static void DoAddNewRow(string controlType)
        {
            Globals.Root.TemplateDoAddNewRow(controlType);
        }
        #endregion
    }
}
