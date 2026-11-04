using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Dialog;

namespace TimelapseTemplateEditor.Dialog
{
    /// <summary>
    /// Lets the user choose one of the taxa GBIF offers when there is no clear single match for a name.
    /// </summary>
    public partial class SelectGbifCandidate
    {
        public GbifCandidate SelectedCandidate { get; private set; }

        public SelectGbifCandidate(Window owner, string scientificName, List<GbifCandidate> candidates)
        {
            InitializeComponent();
            Owner = owner;
            Message.Text = $"GBIF lists these possibilities for '{scientificName}'. Select one to fill in the definition's fields with it.";
            CandidatesGrid.ItemsSource = candidates;
        }

        private void SelectGbifCandidate_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private void CandidatesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = CandidatesGrid.SelectedItem != null;
        }

        private void CandidatesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignore double clicks that aren't on a row, e.g., on a column header
            if (ItemsControl.ContainerFromElement(CandidatesGrid, e.OriginalSource as DependencyObject) is DataGridRow)
            {
                OkButton_OnClick(sender, e);
            }
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            SelectedCandidate = CandidatesGrid.SelectedItem as GbifCandidate;
            DialogResult = SelectedCandidate != null;
        }
    }
}
