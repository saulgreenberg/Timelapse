using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog is shown when the user has an old version of the database files
    /// We don't use it anymore (as we are now many timelapse versions later), but
    /// its worth keeping in case we need to show it again (perhaps in modified format) in the future.
    /// </summary>
    
    // ReSharper disable once UnusedMember.Global
    public partial class WarningToUpdateDBFilesToSQL
    {
        public WarningToUpdateDBFilesToSQL(Window owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
