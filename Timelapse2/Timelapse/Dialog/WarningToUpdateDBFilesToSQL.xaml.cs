using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for WarningToUpdateDBFilesToSQL.xaml
    /// </summary>
    public partial class WarningToUpdateDBFilesToSQL : Window
    {
        public bool DontShowAgain
        {
            get
            {
                return this.CheckBoxDontShowAgain.IsChecked == true;
            }
        }
        public WarningToUpdateDBFilesToSQL(Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
