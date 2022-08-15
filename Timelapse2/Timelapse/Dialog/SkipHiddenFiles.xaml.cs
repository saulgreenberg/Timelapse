using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for SkipHiddenFiles.xaml
    /// </summary>
    public partial class SkipHiddenFiles : Window
    {
        #region Constructor, Loaded
        public SkipHiddenFiles(Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region Callbacks - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
