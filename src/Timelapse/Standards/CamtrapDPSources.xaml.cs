using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPSOurces.xaml
    /// </summary>
    public partial class CamtrapDPSources 
    {
        #region Properties and Variables: JsonSourcesList, SourcesList, and Fields
        public string JsonSourcesList { get; set; }

        // The main list of sources. It is
        // - initially populated by the initial jsonSourcesList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<sources> SourcesList { get; set; }

        // The fields used to construct the EditList

        public Fields TitleField { get; set; } =
            new("Title*",
                $"Title of the source (e.g. document or organization name).{Environment.NewLine}" +
                "• e.g., \"World Bank and OECD\"");

        public Fields EmailField { get; set; } =
            new("Email",
                $"An email to the source.{Environment.NewLine}" +
                 "• e.g., \"bloggs@agouti.com\"");

        public Fields PathField { get; set; } =
            new("Path",
                $"A fully qualified http URL pointing to a relevant location online for this contributor.{Environment.NewLine}" +
                 "• e.g., \"http://www.bloggs.com\"");

        public Fields VersionField { get; set; } =
            new("Version",
                $"Version of the source.{Environment.NewLine}" +
                "• e.g., \"v3.21\"");
        #endregion

        #region Private variables
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPSources(Window owner, string jsonSourcesList)
        {
            InitializeComponent();
            Owner = owner;
            JsonSourcesList = jsonSourcesList;
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
                SourcesList = new(JsonConvert.DeserializeObject<List<sources>>(JsonSourcesList));
            }
            catch (Exception)
            {
                // Maybe show an error dialog?
                DialogResult = false;
            }

            DataGrid_Refresh();
            if (dataGrid.Items.Count == 0)
            {
                NewRow_OnClick(null, null);
            }
            if (dataGrid.Items.Count > 0)
            {
                dataGrid.SelectedIndex = 0;
                dataGridSelectedRow = dataGrid.SelectedIndex;
            }
        }
        #endregion

        #region Callbacks and helpers: DataGrid
        // Refresh the data grid to show the current itmes in the sources list
        private void DataGrid_Refresh()
        {
            DontUpdate = true;
            dataGrid.ItemsSource = null;
            dataGrid.Items.Clear();
            dataGrid.ItemsSource = SourcesList;
            DontUpdate = false;
            dataGrid.SelectedIndex = dataGridSelectedRow < dataGrid.Items.Count ? dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < SourcesList.Count)
            {
                DeleteRow.IsEnabled = true;
                sources sources = SourcesList[dataGrid.SelectedIndex];
                DataFieldTitle.Text = sources.title;
                DataFieldEmail.Text = sources.email;
                DataFieldPath.Text = sources.path;
                DataFieldVersion.Text = sources.version;
            }
            else
            {
                DeleteRow.IsEnabled = false;

                DontUpdate = true;
                DataFieldTitle.Text = string.Empty;
                DataFieldEmail.Text = string.Empty;
                DataFieldPath.Text = string.Empty;
                DataFieldVersion.Text = string.Empty;
            }
            DontUpdate = false;
            if (SetFocus)
            {
                DataFieldTitle.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            SourcesList.Add(new());
            dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DataFieldTitle.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < SourcesList.Count)
            {
                DontUpdate = true;
                SourcesList.RemoveAt(dataGrid.SelectedIndex);
                DontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    SetFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    SetFocus = true;
                }
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Done_OnClick(object sender, RoutedEventArgs e)
        {
            JsonSerialize();
            DialogResult = true;
        }
        #endregion

        #region Callbacks: DataField edits
        // When a data field is edited, update the contributors list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < SourcesList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldTitle":
                        SourcesList[dataGrid.SelectedIndex].title = tb.Text;
                        break;
                    case "DataFieldEmail":
                        SourcesList[dataGrid.SelectedIndex].email = tb.Text;
                        break;
                    case "DataFieldPath":
                        SourcesList[dataGrid.SelectedIndex].path = tb.Text;
                        break;
                    case "DataFieldVersion":
                        SourcesList[dataGrid.SelectedIndex].version = tb.Text;
                        break;
                }
            }
            SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
            SetFocus = true;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, set it to null (to make for a cleaner json)
            // If a taxonomic object is all empty, skip it/
            // Note that we could put in a check for required fields here...
            List<sources> sourcesListForExport = [];
            foreach (sources source in SourcesList)
            {
                PropertyInfo[] properties = typeof(sources).GetProperties();
                bool allNull = true;
                sources newSource = new();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(source) != null && !string.IsNullOrWhiteSpace(property.GetValue(source)?.ToString()))
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

            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            JsonSourcesList = JsonConvert.SerializeObject(sourcesListForExport, settings);
        }
        #endregion

        #region Fields class
        //The EditFields
        public class Fields(string label, string tooltip)
        {
            public string Label { get; set; } = label;
            public string Tooltip { get; set; } = tooltip;
        }
        #endregion
    }
}
