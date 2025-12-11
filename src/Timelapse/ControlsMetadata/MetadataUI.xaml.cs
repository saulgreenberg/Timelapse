using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using FontStyle = System.Windows.FontStyle;
using TabControl = System.Windows.Controls.TabControl;

namespace Timelapse.ControlsMetadata
{
    /// <summary>
    /// Interaction logic for MetadataUI.xaml
    /// </summary>
    public partial class MetadataUI
    {
        #region Public properties
        private string relativePathToCurrentImage;
        public string RelativePathToCurrentImage
        {
            get => relativePathToCurrentImage;
            set
            {
                if (null == relativePathToCurrentImage || relativePathToCurrentImage != value)
                {
                    relativePathToCurrentImage = value;
                    SetPanelsRelativePathToCurrentFolder(TabControl.Items, value);
                }
            }
        }

        public bool IsActive
        {
            set => IsEnabled = value;
        }

        #endregion

        #region Private variables
        private FileDatabase FileDatabase => GlobalReferences.MainWindow?.DataHandler?.FileDatabase;
        #endregion

        #region Constructor
        public MetadataUI()
        {
            InitializeComponent();
        }
        #endregion

        #region Public InitializeFolderMetadataTabs
        public void InitalizeFolderMetadataTabs()
        {
            // clear any existing metadata tabs (except the instructions)
            ClearMetadataTabsExceptInstructions(TabControl);

            if (null == FileDatabase?.FileTable)
            {
                // No image set is open
                NoteNoMetadataTemplate.Visibility = Visibility.Collapsed;
                NoteNoImageSetOpen.Visibility = Visibility.Visible;
                return;
            }

            if (null == FileDatabase?.MetadataControlsByLevel || FileDatabase.MetadataControlsByLevel.Count == 0)
            {
                // No metadata controls defined, just abort.
                NoteNoMetadataTemplate.Visibility = Visibility.Visible;
                NoteNoImageSetOpen.Visibility = Visibility.Collapsed;
                return;
            }
            // Good to go... Hide the warning as we no longer need it
            NoteNoMetadataTemplate.Visibility = Visibility.Collapsed;
            NoteNoImageSetOpen.Visibility = Visibility.Collapsed;

            int i = 1;
            // Create a tab for each level
            foreach (MetadataInfoRow row in GlobalReferences.MainWindow.DataHandler.FileDatabase.MetadataInfo)
            {
                // Set the fonts and color for the header. Panels that are not defined in the template show their headers in gray and italicized.
                bool levelPresent = FileDatabase.MetadataTablesIsLevelPresent(i);
                
                FontStyle fontStyle = levelPresent 
                ? FontStyles.Normal
                : FontStyles.Italic;
                
                Brush fontColor = levelPresent
                    ? Brushes.Black
                    : Brushes.Gray;

                string tabHeader = CreateTemporaryAliasIfNeeded(row.Level, row.Alias);

                TabItem tabItem = new()
                {
                    Header = new TextBlock
                    {
                        Padding = new(5),
                        Text = tabHeader,
                        FontStyle = fontStyle,
                        Foreground = fontColor
                    },
                    IsTabStop = false,
                };

                TabControl.Items.Insert(i++, tabItem);

                // Add a MetadataDataEntryPanel to each tab
                MetadataDataEntryPanel folderMetadataDataEntryPanel = new(row.Level, tabItem, tabHeader);
                tabItem.Content = folderMetadataDataEntryPanel;
            }
            if (TabControl.Items.Count > 1)
            {
                // Set the initial tab to the first level tab, if one exists
                TabControl.SelectedIndex = 1;
            }
        }
        #endregion

        public void ResetNavigationButtonsForMetadataTabs()
        {
            foreach (TabItem tabItem in TabControl.Items)
            {
                if (tabItem.Content is MetadataDataEntryPanel panel)
                {
                    panel.NavigationButtonsShowHide();
                }
            }
        }

        #region Public SetEnableState
        // Clear any existing metadata tabs (except the instructions, which should always be the first tab)
        public void SetEnableState(bool isEnabled)
        {
            TabControl.IsEnabled = isEnabled;
        }
        #endregion

        #region Callbacks: OnSelectionChanged to set Tab Appearance
        // Bold the header of the currently selected tab
        private void MetadataTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl { SelectedItem: TabItem } tabControl)
            {
                // Bold only the elected tab's header
                foreach (TabItem tb in TabControl.Items)
                {
                    if (tb.Header is TextBlock tblock)
                    {
                        tblock.FontWeight = tb == tabControl.SelectedItem
                            ? tblock.FontWeight = FontWeights.Bold
                            : tblock.FontWeight = FontWeights.Normal;
                    }
                }
            }
        }
        #endregion

        #region Private utilities: SetPanelsRelativePathToCurrentFolder, ClearMetadataTabsExceptInstructions
        // Set the relative path for all panels to the current image
        private static void SetPanelsRelativePathToCurrentFolder(ItemCollection tabControlItems, string relativePathToCurrentImage)
        {
            foreach (TabItem tabItem in tabControlItems)
            {
                if (tabItem.Content is MetadataDataEntryPanel panel)
                {
                    panel.RelativePathToCurrentFolder = relativePathToCurrentImage;
                }
            }
        }

        // Clear any existing metadata tabs (except the instructions, which should always be the first tab)
        private static void ClearMetadataTabsExceptInstructions(TabControl tabControl)
        {
            if (null == tabControl?.Items || tabControl.Items.Count == 1)
            {
                return;
            }

            for (int i = tabControl.Items.Count - 1; i > 0; i--)
            {
                tabControl.Items.RemoveAt(i);
            }
        }
        #endregion

        #region Utilities
        public static string CreateTemporaryAliasIfNeeded(int level, string alias)
        {
            return string.IsNullOrEmpty(alias)
                ? $"Level-{level}"
                : alias;
        }
        #endregion
    }
}
