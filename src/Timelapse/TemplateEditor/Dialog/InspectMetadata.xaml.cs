using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Dialog;
using Timelapse.Util;

namespace TimelapseTemplateEditor.Dialog
{
    /// <summary>
    /// This dialog displays a list of metadata found in a selected file. 
    /// </summary>
    // Note: There are lots of commonalities between this dialog and DialogPopulate, but its not clear if it's worth the effort of factoring the two.
    public partial class InspectMetadata
    {
        #region Private variables
        private string FilePath;
        #endregion

        public InspectMetadata(Window owner)
        {
            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Dialog);
            Owner = owner;
            FilePath = string.Empty;
        }

        // After the interface is loaded, try to adjust the position of the dialog box
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            MetadataGrid.HideDataLabelColumn = true;
            MetadataGrid.SelectedMetadata.CollectionChanged += SelectedMetadata_CollectionChanged;
            this.Dialog.BuildContentFromProperties();
        }

        #region UI Button Callbacks
        // When the user opens the file, get its metadata and display it in the datagrid
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            string filter = String.Format("Images and videos (*{0};*{1};*{2};*{3};*{4})|*{0};*{1};*{2};*{3};*{4}", File.JpgFileExtension, File.AviFileExtension, File.Mp4FileExtension, File.ASFFileExtension, File.MovFileExtension);
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog("Select a typical file to inspect", ".", filter, File.JpgFileExtension, out FilePath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                GetMetadataFromFile();
                MetadataGrid.viewModel.FilePath = FilePath;
                MetadataGrid.Refresh();
                Mouse.OverrideCursor = null;
            }
        }

        private void GetMetadataFromFile()
        {
            MetadataGrid.viewModel.FilePath = FilePath;
            MetadataGrid.Refresh();
        }
        #endregion

        #region Change Notifications
        private void SelectedMetadata_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // MAY NOT NEED THIS
        }
        #endregion

        #region Button callbacks
        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion
    }
}
