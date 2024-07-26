using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPSources.xaml
    /// </summary>
    public partial class CamtrapDPSources : Window
    {
        #region Public properties
        public string JsonSourcesList { get; set; }

        // The main list of sources. It is
        // - initially populated by the initial jsonSourcesList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<Sources> SourcesList { get; set; }

        // The fields used to construct the EditList

        public Fields TitleField { get; set; } =
            new Fields("Title*",
                $"Title of the source (e.g. document or organization name).{Environment.NewLine}" +
                "• e.g., \"World Bank and OECD\"");

        public Fields EmailField { get; set; } =
            new Fields("Email",
                $"An email to the source.{Environment.NewLine}" +
                 "• e.g., \"bloggs@agouti.com\"");

        public Fields PathField { get; set; } =
            new Fields("Path",
                $"A fully qualified http URL pointing to a relevant location online for this contributor.{Environment.NewLine}" +
                 "• e.g., \"http://www.bloggs.com\"");

        public Fields VersionField { get; set; } =
            new Fields("Version",
                $"Version of the source.{Environment.NewLine}" +
                "• e.g., \"v3.21\"");
        #endregion

        #region Private variables
        private bool DontUpdate = false;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPSources(Window owner, string jsonSourcesList)
        {
            InitializeComponent();
            this.Owner = owner;
            this.JsonSourcesList = jsonSourcesList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonSourcesList))
            {
                // Make sure its a valid json array
                JsonSourcesList = "[]";
            }

            try
            {
                this.SourcesList = new ObservableCollection<Sources>(JsonConvert.DeserializeObject<List<Sources>>(JsonSourcesList));
            }
            catch (Exception)
            {
                // Maybe show an error dialog?
                DialogResult = false;
            }

            DataGrid_Refresh();
            if (this.dataGrid.Items.Count > 0)
            {
                this.dataGrid.SelectedIndex = 0;
                this.dataGridSelectedRow = this.dataGrid.SelectedIndex;
            }
        }
        #endregion

        #region DataGrid callbacks and helpers
        // Refresh the data grid to show the current itmes in the sources list
        private void DataGrid_Refresh()
        {
            this.DontUpdate = true;
            this.dataGrid.ItemsSource = null;
            this.dataGrid.Items.Clear();
            this.dataGrid.ItemsSource = this.SourcesList;
            this.DontUpdate = false;
            dataGrid.SelectedIndex = this.dataGridSelectedRow < this.dataGrid.Items.Count ? this.dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            this.dataGridSelectedRow = dataGrid.SelectedIndex;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.SourcesList.Count)
            {

                this.DeleteRow.IsEnabled = true;

                this.DontUpdate = true;
                Sources sources = this.SourcesList[dataGrid.SelectedIndex];
                this.DataFieldTitle.Text = sources.title;
                this.DataFieldEmail.Text = sources.email;
                this.DataFieldPath.Text = sources.path;
                this.DataFieldVersion.Text = sources.version;
                this.DontUpdate = false;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;

                this.DontUpdate = true;
                this.DataFieldTitle.Text = string.Empty;
                this.DataFieldEmail.Text = string.Empty;
                this.DataFieldPath.Text = string.Empty;
                this.DataFieldVersion.Text = string.Empty;
                this.DontUpdate = false;
            }
        }
        #endregion

        #region Button callbacks
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.SourcesList.Add(new Sources());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.SourcesList.Count)
            {
                this.SourcesList.RemoveAt(dataGrid.SelectedIndex);
                dataGrid.SelectedIndex = dataGridSelectedRow == -1 ? -1 : --dataGridSelectedRow;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Done_OnClick(object sender, RoutedEventArgs e)
        {
            this.JsonSerialize();
            DialogResult = true;
        }
        #endregion

        #region DataField callbacks
        // When a data field is edited, update the contributors list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.SourcesList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldTitle":
                        this.SourcesList[dataGrid.SelectedIndex].title = tb.Text;
                        break;
                    case "DataFieldEmail":
                        this.SourcesList[dataGrid.SelectedIndex].email = tb.Text;
                        break;
                    case "DataFieldPath":
                        this.SourcesList[dataGrid.SelectedIndex].path = tb.Text;
                        break;
                    case "DataFieldVersion":
                        this.SourcesList[dataGrid.SelectedIndex].version = tb.Text;
                        break;
                }
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
        }
        #endregion

        #region Helpers - Json Serializer
        private void JsonSerialize()
        {
            this.JsonSourcesList = JsonConvert.SerializeObject(this.SourcesList);
        }
        #endregion

        #region Sources class
        // A contributor has these fields, as defined in the CamtrapDP specification
        public class Sources
        {
            public string title { get; set; } = string.Empty;
            public string email { get; set; } = string.Empty;
            public string path { get; set; } = string.Empty;
            public string version { get; set; } = string.Empty;
        }
        #endregion

        #region Fields class
        //The EditFields
        public class Fields
        {
            public string Label { get; set; }
            public string Tooltip { get; set; }

            public Fields(string label, string tooltip)
            {
                this.Label = label;
                this.Tooltip = tooltip;
            }
        }
        #endregion
    }
}
