using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RelativePathEditor.xaml
    /// </summary>
    public partial class RelativePathEditor : Window
    {
        private readonly FileDatabase FileDatabase;
        public RelativePathEditor(Window owner, Timelapse.Database.FileDatabase fileDatabase)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.RelativePathControl.Initialize(this, this.FileDatabase, new List<Button> { SortButton, DoneButton });
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.RelativePathControl.WereEditsMade;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.RebuildTree(true);
        }

        private void RefreshCompletelyButton_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.Initialize(this, this.FileDatabase, new List<Button> { SortButton, DoneButton });
        }
    }
}
