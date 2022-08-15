using System.IO;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Timelapse.Util
{
    /// <summary>
    /// Helpers to implement drage and rop of template files
    /// </summary>
    public static class DragDropFile
    {
        #region Public Static
        public static bool IsTemplateFileDragging(DragEventArgs dragEvent, out string templateDatabasePath)
        {
            // Check the arguments for null 
            if (dragEvent != null && dragEvent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])dragEvent.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles != null && droppedFiles.Length == 1)
                {
                    templateDatabasePath = droppedFiles[0];
                    if (Path.GetExtension(templateDatabasePath) == Constant.File.TemplateDatabaseFileExtension)
                    {
                        return true;
                    }
                }
            }
            templateDatabasePath = null;
            return false;
        }

        public static void OnTemplateFilePreviewDrag(DragEventArgs dragEvent)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dragEvent, nameof(dragEvent));
            if (DragDropFile.IsTemplateFileDragging(dragEvent, out _))
            {
                dragEvent.Effects = DragDropEffects.All;
            }
            else
            {
                dragEvent.Effects = DragDropEffects.None;
            }
            dragEvent.Handled = true;
        }
        #endregion
    }
}
