using Timelapse.Database;
using TimelapseTemplateEditor.Controls;

namespace TimelapseTemplateEditor.EditorCode
{
    // Various globals that we can access for convenience,
    // mostly so UI callbacks can invoke the main program methods and other controls 
    public static class Globals
    {
        // Globals to allow access to variables in the main program
        public static TemplateEditorWindow Root;
        public static TemplateEditorWindow RootEditor => EditorConstant.templateEditorWindow;
        public static CommonDatabase TemplateDatabase => EditorConstant.templateEditorWindow?.templateDatabase;
        public static TemplateDataGridControl TemplateDataGridControl => TemplateUI?.TemplateDataGridControl;
        public static TemplateUIControl TemplateUI => EditorConstant.templateEditorWindow.TemplateUI;
        public static TemplateDataEntryPreviewPanel TemplateDataEntryPreviewPanelControl => TemplateUI.TemplateDataEntryPreviewPanel;
        public static TemplateSpreadsheetPreviewControl TemplateSpreadsheet => TemplateUI.TemplateSpreadsheetPreviewControl;
        public static MouseState MouseState => EditorConstant.templateEditorWindow.mouseState;
    }
}