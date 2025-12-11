using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for SkipHiddenFiles.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class SkipHiddenFiles
    {
        #region Constructor, Loaded
        public SkipHiddenFiles(Window owner)
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
        #endregion

        #region Callbacks - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
