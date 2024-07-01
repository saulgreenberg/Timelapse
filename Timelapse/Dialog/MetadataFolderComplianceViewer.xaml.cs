using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MetadataFolderComplianceViewer.xaml
    /// </summary>
    public partial class MetadataFolderComplianceViewer
    {
        private readonly DataTableBackedList<MetadataInfoRow> MetadataInfo;
        private readonly FileDatabase FileDatabase;
        private readonly List<string> AddedRelativePathList;
        #region Constructor, loaded
        public MetadataFolderComplianceViewer(TimelapseWindow owner, FileDatabase fileDatabase, List<string> addedRelativePathList, DataTableBackedList<MetadataInfoRow> metadataInfo):base(owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase = fileDatabase;
            this.MetadataInfo = metadataInfo;
            this.AddedRelativePathList = addedRelativePathList;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            TextBlock tb1 = GenerateHierarchicalList(this.MetadataInfo);
            Grid.SetRow(tb1, 2);
            Grid.SetColumn(tb1, 0);
            this.ExpectedFolderLevelsGrid.Children.Add(tb1);

            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Set up a progress handler for long-running atomic operation
            this.InitalizeProgressHandler(this.BusyCancelIndicator);
            Mouse.OverrideCursor = Cursors.Wait;
            this.BusyCancelIndicator.Reset(true);

            bool result = await this.MetadataComplianceControl.AsyncInitialize(this, this.FileDatabase, this.AddedRelativePathList, this.ProgressHandler, GlobalReferences.CancelTokenSource);

            this.BusyCancelIndicator.Reset(false);
            Mouse.OverrideCursor = null;
            if (result == false)
            {
                // Abort this, likely due to a user's progress cancel event
                this.DialogResult = false;
            }
        }
        #endregion

        #region Texblocks

        // Creates a textblock outlining the TDB  hierarchy that also indicates changes from the DDB hierarchy
        private static TextBlock GenerateHierarchicalList(DataTableBackedList<MetadataInfoRow> MetadataInfo)
        {
            int levels = MetadataInfo.RowCount;

            TextBlock tb = new TextBlock();
            if (levels == 0)
            {
                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Crimson,
                    FontSize = 12,
                    Text = "No levels defined",
                });
                return tb;
            }

            string indent = "     ";
            for (int i = 0; i < levels; i++)
            {
                string tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(MetadataInfo[i].Level, MetadataInfo[i].Alias);
                string levelText = $"{indent}{MetadataInfo[i].Level}: {tempAlias}";
                string levelSuffix = $"{(i < levels - 1 ? Environment.NewLine : string.Empty)}";
                if (i == 0)
                {
                    levelSuffix = "     ...corresponds to your root folder of this image set" + levelSuffix;
                }

                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Medium,
                    FontSize = 12,
                    Text = levelText
                });
                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Italic,
                    FontSize = 12,
                    Foreground = Brushes.Blue,
                    Text = levelSuffix
                });
                indent += "    ";
            }

            tb.Inlines.Add(new Run
            {
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Normal,
                FontSize = 12,
                Text = $"{Environment.NewLine}{indent}\u2022 images should be located here..."
            });
            
            return tb;
        }
        #endregion

        #region Button Callbacks
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            this.MetadataComplianceControl.ExpandTreeView(true);
        }

        private void ContractAll_Click(object sender, RoutedEventArgs e)
        {
            this.MetadataComplianceControl.ExpandTreeView(false);
        }
        #endregion

    }
}
