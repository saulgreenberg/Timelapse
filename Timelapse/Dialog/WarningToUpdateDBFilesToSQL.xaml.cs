using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for WarningToUpdateDBFilesToSQL.xaml
    /// </summary>
    public partial class WarningToUpdateDBFilesToSQL
    {
        public bool DontShowAgain => CheckBoxDontShowAgain.IsChecked == true;

        public WarningToUpdateDBFilesToSQL(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
