using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Dialog;

namespace Timelapse
{
    public partial class TimelapseWindow
    {
        public async void HandleArgumentsOnOpen()
        {
            // Note: Timelapse allows  -viewOnly, -relativepath <relative path>, -templatepath <template path>, -templateeditor arguments to be combined.
            // It also allows a single argument containing a template or data file to be specified, e.g., by double clicking on a .tdb or .ddb file in Explorer
            if (Arguments.IsViewOnly)
            {
                // Timelapse was started with a -viewonly argument. Set it up to viewonly mode
                // Disable the copy previous values button, the data entry panels' context menu etc.
                // We don't have to do anything further, as viewonly mode is enforced within the main Timelapse code
                // TODO: we could also consider having the TemplateEditor open in viewonly mode too if the -viewonly argument was specified
                State.IsViewOnly = Arguments.IsViewOnly;
                DataEntryControls.ContextMenu = null;
                CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
            }

            // if (Arguments.ConstrainToRelativePath)
            // Nothing needs to be done with that arguement, as the relative path constraint is enforced within the main Timelapse code

            // Open the template editor window. If a template was specified, open that template in the template editor
            // Note: -viewonly and -relativePath arguments are ignored in template editor mode, but will be enforced
            //      if the user switches back to the main Timelapse window
            if (Arguments.IsOpenInTemplateEditor)
            {
                this.DoSwitchToTheTemplateEditor();

                // If a template was specified, load it into the template editor
                if (null != this.TimelapseTemplateEditor && false == string.IsNullOrEmpty(Arguments.TdbFile) && File.Exists(Arguments.TdbFile))
                {
                    this.TimelapseTemplateEditor.OpenTemplateFileInTemplateEditor(Arguments.TdbFile);
                }
                return;
            }

            if (false == string.IsNullOrEmpty(Arguments.DdbFile))
            {
                // Timelapse was opened with a .ddb file (e.g., by double clicking on the file in Explorer)

                // Get the folder containing the data file
                string folder = Path.GetDirectoryName(Arguments.DdbFile);
                if (string.IsNullOrEmpty(folder) || Directory.Exists(folder) == false)
                {
                    // Unlikely to get here, but if the folder doesn't exist, just open Timelapse with no file
                    return;
                }

                string ddbfile = Path.GetFileName(Arguments.DdbFile);

                // Check if there an accompanying template in the same folder
                string[] tdbFiles = Directory.GetFiles(folder, "*.tdb");
                if (tdbFiles.Length == 0)
                {
                    // No template found. Show a dialog and abort
                    Dialogs.ArgumentDataFileButNoTemplateDialog(this, ddbfile, folder);
                    return;
                }

                string selectedTemplate = string.Empty;
                if (tdbFiles.Length == 1)
                {
                    selectedTemplate = tdbFiles[0];
                }
                else if (tdbFiles.Length > 1)
                {
                    // More than one template. Ask the user which one to use
                    ChooseFromListOfTimelapseFiles chooseDatabaseFile = new(this, tdbFiles, Arguments.DdbFile);
                    bool? result = chooseDatabaseFile.ShowDialog();
                    if (result == true)
                    {
                        selectedTemplate = chooseDatabaseFile.SelectedFile;
                    }
                    else
                    {
                        // User cancelled  selection
                        return;
                    }
                }

                if (selectedTemplate != string.Empty)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    StatusBar.SetMessage("Loading images, please wait...");

                    Tuple<bool, string> results = await TryOpenTemplateAndBeginLoadFoldersAsync(selectedTemplate, Arguments.DdbFile).ConfigureAwait(true);
                    if (results.Item1 == false)
                    {
                        StatusBar.SetMessage("Loading images aborted.");
                        return;
                    }

                    StatusBar.SetMessage("Image set is now loaded.");
                    Mouse.OverrideCursor = null;
                }
            }

            // A template was supplied as a single argument 
            // Check to see if a template or data file was specified, i.e., via a template argument, by double clicking on a .tdb or .ddb file in Explorer
            else if (false == string.IsNullOrEmpty(Arguments.TdbFile))
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, Arguments.TdbFile))
                {
                    return;
                }
                if (File.Exists(Arguments.TdbFile))
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    StatusBar.SetMessage("Loading images, please wait...");
                    Tuple<bool, string> results = await TryOpenTemplateAndBeginLoadFoldersAsync(Arguments.TdbFile).ConfigureAwait(true);
                    if (results.Item1 == false)
                    {
                        // Abort as something went wrong opening the template and/or loading the database
                        return;
                    }

                    StatusBar.SetMessage("Image set is now loaded.");
                    Mouse.OverrideCursor = null;
                }
            }


            if (State.IsViewOnly)
            {
                Dialogs.OpeningMessageViewOnly(this);
            }

            if (Arguments.ConstrainToRelativePath)
            {
                // Tell user that its a constrained relative path,
                // Also, set the File menus so that users cannot close and reopen a new image set
                // This is to avoid confusion as to how the user may mis-interpret the argument state given another image set
                Dialogs.ArgumentRelativePathDialog(this, Arguments.RelativePath);
                MenuItemExit.Header = "Close image set and exit Timelapse";
                MenuFileCloseImageSet.Visibility = Visibility.Collapsed;
            }
        }

        // Open the template editor and close the Timelapse window
        // if a valid template file was specified, open it in the template editor
        // Note: -viewonly and -relativePath arguments are ignored in template editor mode
        private void HandleOpenInTemplateEditor(string templateFilePath)
        {
            this.MenuItemSwitchToTheTemplateEditor_Click(null, null);

            // If a template was specified, load it into the template editor
            if (null != this.TimelapseTemplateEditor && false == string.IsNullOrEmpty(templateFilePath) && File.Exists(templateFilePath))
            {
                this.TimelapseTemplateEditor.OpenTemplateFileInTemplateEditor(Arguments.TdbFile);
            }
        }
    }
}
