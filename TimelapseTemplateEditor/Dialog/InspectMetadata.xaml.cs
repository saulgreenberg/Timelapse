﻿using System;
using System.Windows;
using System.Windows.Input;
using Timelapse.Dialog;

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
            this.InitializeComponent();
            this.Owner = owner;
            this.FilePath = string.Empty;
        }

        // After the interface is loaded, try to adjust the position of the dialog box
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.MetadataGrid.HideDataLabelColumn = true;
            this.MetadataGrid.SelectedMetadata.CollectionChanged += this.SelectedMetadata_CollectionChanged;
        }

        #region UI Button Callbacks
        // When the user opens the file, get its metadata and display it in the datagrid
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            string filter = String.Format("Images and videos (*{0};*{1};*{2};*{3};*{4})|*{0};*{1};*{2};*{3};*{4}", Timelapse.Constant.File.JpgFileExtension, Timelapse.Constant.File.AviFileExtension, Timelapse.Constant.File.Mp4FileExtension, Timelapse.Constant.File.ASFFileExtension, Timelapse.Constant.File.MovFileExtension);
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog("Select a typical file to inspect", ".", filter, Timelapse.Constant.File.JpgFileExtension, out this.FilePath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.GetMetadataFromFile();
                this.MetadataGrid.viewModel.FilePath = this.FilePath;
                this.MetadataGrid.Refresh();
                Mouse.OverrideCursor = null;
            }
        }

        private void GetMetadataFromFile()
        {
            this.MetadataGrid.viewModel.FilePath = this.FilePath;
            this.MetadataGrid.Refresh();
        }
        #endregion

        #region Change Notifications
        private void SelectedMetadata_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // MAY NOT NEED THIS
        }
        #endregion

        #region Button callbacks
        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion
    }
}
