using System.Windows;
using Timelapse.Dialog;
using ConstantFile = Timelapse.Constant.File;

namespace Timelapse.Standards
{
    /// <summary>
    /// Explains what a species list file is and how to create one, and asks the user to locate one.
    /// A file with a problem is reported here, so the user can select another one.
    /// </summary>
    public partial class CamtrapDPPopulateFromSpeciesList
    {
        public CamtrapDPSpeciesList SpeciesList { get; private set; }
        public bool ReplaceExistingDefinitions => true == ReplaceDefinitions.IsChecked;

        public CamtrapDPPopulateFromSpeciesList(Window owner, int existingDefinitionsCount)
        {
            InitializeComponent();
            Owner = owner;
            if (existingDefinitionsCount > 0)
            {
                ExistingDefinitionsMessage.Text = $"The datapackage taxonomic information list already has {existingDefinitionsCount} definition(s):";
                ExistingDefinitionsPanel.Visibility = Visibility.Visible;
            }
        }

        private void SelectFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a species list file (json)",
                    string.Empty,
                    "Json files (*.json)|*.json",
                    ConstantFile.JsonFileExtension,
                    out string filePath,
                    this))
            {
                UseFile(filePath);
            }
        }

        private void UseFile(string filePath)
        {
            CamtrapDPSpeciesList speciesList = CamtrapDPSpeciesList.Read(filePath);
            if (speciesList.Problem != null)
            {
                Dialogs.CamtrapDPSpeciesListProblemDialog(this, filePath, speciesList.Problem);
                return;
            }
            SpeciesList = speciesList;
            DialogResult = true;
        }
    }
}
