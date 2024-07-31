using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPKeywords.xaml
    /// </summary>
    public partial class CamtrapDPKeywords : Window
    {
        #region Properties and Variables: JsonKeywordsList, KeywordsList, and Fields
        public string JsonKeywordsList { get; set; }

        // The main list of keywords. It is
        // - initially populated by the initial jsonKeywordsList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<string> KeywordsList { get; set; }

        // The fields used to construct the EditList

        public Fields KeywordField { get; set; } =
            new Fields("Keyword",
                $"A list of keywords to assist users searching for the package in catalogs.{Environment.NewLine}" +
                "• e.g., \"wolverine, wildlife management, conservation, population monitoring\"");
        #endregion

        #region Private variables
        private bool dontUpdate = false;
        private bool setFocus = true;
        private bool resetSelectedIndexToSavedIndex = false;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPKeywords(Window owner, string jsonKeywordsList)
        {
            InitializeComponent();
            this.Owner = owner;
            this.JsonKeywordsList = jsonKeywordsList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonKeywordsList))
            {
                // Make sure its a valid json array
                JsonKeywordsList = "[]";
            }

            try
            {
                this.KeywordsList = new ObservableCollection<string>(JsonConvert.DeserializeObject<List<string>>(JsonKeywordsList));
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
        // Refresh the data grid to show the current itmes in the keywords list
        private void DataGrid_Refresh()
        {
            this.dontUpdate = true;
            this.dataGrid.ItemsSource = null;
            this.dataGrid.Items.Clear();
            this.dataGrid.ItemsSource = this.KeywordsList;
            this.dontUpdate = false;
            dataGrid.SelectedIndex = this.dataGridSelectedRow < this.dataGrid.Items.Count ? this.dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.dontUpdate)
            {
                return;
            }

            if (resetSelectedIndexToSavedIndex)
            {
                this.dontUpdate = true;
                dataGrid.SelectedIndex = this.dataGridSelectedRow;
                this.dontUpdate = false;
            }
            else
            {
                this.dataGridSelectedRow = dataGrid.SelectedIndex;
            }

            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.dontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.KeywordsList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                string keywords = this.KeywordsList[dataGrid.SelectedIndex];
                this.DataFieldKeyword.Text = keywords;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;

                this.dontUpdate = true;
                this.DataFieldKeyword.Text = string.Empty;
            }
            this.dontUpdate = false;
            if (this.setFocus)
            {
                this.DataFieldKeyword.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.KeywordsList.Add(string.Empty);
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldKeyword.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.KeywordsList.Count)
            {
                this.dontUpdate = true;
                this.KeywordsList.RemoveAt(dataGrid.SelectedIndex);
                this.dontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    this.setFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    this.setFocus = true;
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
            if (this.dontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.KeywordsList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldKeyword":
                        resetSelectedIndexToSavedIndex = true;
                        this.KeywordsList[dataGrid.SelectedIndex] = tb.Text;
                        resetSelectedIndexToSavedIndex = false;
                        break;
                }
            }
            this.setFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedIndex = this.dataGridSelectedRow;
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.setFocus = true;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, remove it (to make for a cleaner json)
            // Note that we could put in a check for required fields here...
            List<string> keywordsListForExport = new List<string>();
            foreach (string keyword in this.KeywordsList)
            {
                if (false == string.IsNullOrWhiteSpace(keyword))
                {
                    keywordsListForExport.Add(keyword);
                }
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonKeywordsList = JsonConvert.SerializeObject(keywordsListForExport, settings);
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

