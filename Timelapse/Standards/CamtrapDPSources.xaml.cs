using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPSOurces.xaml
    /// </summary>
    public partial class CamtrapDPSources : Window
    {
        #region Properties and Variables: JsonSourcesList, SourcesList, and Fields
        public string JsonSourcesList { get; set; }

        // The main list of sources. It is
        // - initially populated by the initial jsonSourcesList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<Standards.sources> SourcesList { get; set; }

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
        private bool SetFocus = true;
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
                this.SourcesList = new ObservableCollection<Standards.sources>(JsonConvert.DeserializeObject<List<Standards.sources>>(JsonSourcesList));
            }
            catch (Exception)
            {
                // Maybe show an error dialog?
                DialogResult = false;
            }

            DataGrid_Refresh();
            if (this.dataGrid.Items.Count == 0)
            {
                this.NewRow_OnClick(null, null);
            }
            if (this.dataGrid.Items.Count > 0)
            {
                this.dataGrid.SelectedIndex = 0;
                this.dataGridSelectedRow = this.dataGrid.SelectedIndex;
            }
        }
        #endregion

        #region Callbacks and helpers: DataGrid
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
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.SourcesList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                Standards.sources sources = this.SourcesList[dataGrid.SelectedIndex];
                this.DataFieldTitle.Text = sources.title;
                this.DataFieldEmail.Text = sources.email;
                this.DataFieldPath.Text = sources.path;
                this.DataFieldVersion.Text = sources.version;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;

                this.DontUpdate = true;
                this.DataFieldTitle.Text = string.Empty;
                this.DataFieldEmail.Text = string.Empty;
                this.DataFieldPath.Text = string.Empty;
                this.DataFieldVersion.Text = string.Empty;
            }
            this.DontUpdate = false;
            if (this.SetFocus)
            {
                this.DataFieldTitle.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.SourcesList.Add(new Standards.sources());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldTitle.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.SourcesList.Count)
            {
                this.DontUpdate = true;
                this.SourcesList.RemoveAt(dataGrid.SelectedIndex);
                this.DontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    this.SetFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    this.SetFocus = true;
                }
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

        #region Callbacks: DataField edits
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
            this.SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.SetFocus = true;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, set it to null (to make for a cleaner json)
            // If a taxonomic object is all empty, skip it/
            // Note that we could put in a check for required fields here...
            List<Standards.sources> sourcesListForExport = new List<Standards.sources>();
            foreach (Standards.sources source in this.SourcesList)
            {
                PropertyInfo[] properties = typeof(Standards.sources).GetProperties();
                bool allNull = true;
                Standards.sources newSource = new Standards.sources();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(source) != null && !string.IsNullOrWhiteSpace(property.GetValue(source).ToString()))
                    {
                        allNull = false;
                        property.SetValue(newSource, property.GetValue(source));
                    }
                    else
                    {
                        property.SetValue(newSource, null);
                    }
                }
                if (!allNull)
                {
                    sourcesListForExport.Add(newSource);
                }
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonSourcesList = JsonConvert.SerializeObject(sourcesListForExport, settings);
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
