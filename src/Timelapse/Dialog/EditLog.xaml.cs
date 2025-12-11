using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog lets the user edit text notes attached to this image set, ideally to keep a log of what is going on, if needed.
    /// The log is persisted.
    /// </summary>
    public partial class EditLog
    {
        #region Constructor, Loaded
        /// <summary>
        /// Raise a dialog that lets the user edit text given to it as a parameter  
        /// If the dialog returns true, the property LogContents will contain the modified text. 
        /// </summary>
        public EditLog(string text, Window owner)
        {
            InitializeComponent();
            Owner = owner;

            Log.Text = text;
            OkButton.IsEnabled = false;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region Callback - TextChanged
        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OkButton.IsEnabled = true;
        }
        #endregion

        #region Callback -Dialog Buttons
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