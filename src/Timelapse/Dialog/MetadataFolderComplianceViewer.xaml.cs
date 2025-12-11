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
using Timelapse.Util;

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
        public MetadataFolderComplianceViewer(TimelapseWindow owner, FileDatabase fileDatabase, List<string> addedRelativePathList, DataTableBackedList<MetadataInfoRow> metadataInfo, bool showOptionToLoadImages):base(owner)
        {
            InitializeComponent();
            Owner = owner;
            FileDatabase = fileDatabase;
            MetadataInfo = metadataInfo;
            AddedRelativePathList = addedRelativePathList;
            this.MessageDivergence.Solution += showOptionToLoadImages
                ? "[li][b]Cancel[/b]: to abort loading images. You can then reorganize your folders and images to match your metadata structure.[li][b]Okay[/b]   to continue loading images anyways."
                : " You may then want to consider reorganizing your folders and images to match your metadata structure.";
            if (showOptionToLoadImages == false)
            {
                this.OkButton.Visibility = Visibility.Collapsed;
                this.CancelButton.Content = "Close";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(MessageNoDivergence);
            FormattedDialogHelper.SetupStaticReferenceResolver(MessageDivergence);
            this.MessageNoDivergence.BuildContentFromProperties();
            this.MessageDivergence.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            TextBlock tb1 = GenerateHierarchicalList(MetadataInfo);
            Grid.SetRow(tb1, 2);
            Grid.SetColumn(tb1, 0);
            ExpectedFolderLevelsGrid.Children.Add(tb1);

            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Set up a progress handler for long-running atomic operation
            InitalizeProgressHandler(BusyCancelIndicator);
            Mouse.OverrideCursor = Cursors.Wait;
            BusyCancelIndicator.Reset(true);

            bool result = await MetadataComplianceControl.AsyncInitialize(this, FileDatabase, AddedRelativePathList, ProgressHandler, GlobalReferences.CancelTokenSource);

            BusyCancelIndicator.Reset(false);
            Mouse.OverrideCursor = null;
            if (result == false)
            {
                // Abort this, likely due to a user's progress cancel event
                DialogResult = false;
            }
        }
        #endregion

        #region Texblocks

        // Creates a textblock outlining the TDB  hierarchy that also indicates changes from the DDB hierarchy
        private static TextBlock GenerateHierarchicalList(DataTableBackedList<MetadataInfoRow> MetadataInfo)
        {
            int levels = MetadataInfo.RowCount;

            TextBlock tb = new();
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
            Mouse.OverrideCursor = null;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Mouse.OverrideCursor = null;
        }
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            MetadataComplianceControl.ExpandTreeView(true);
        }

        private void ContractAll_Click(object sender, RoutedEventArgs e)
        {
            MetadataComplianceControl.ExpandTreeView(false);
        }
        #endregion

    }
}
