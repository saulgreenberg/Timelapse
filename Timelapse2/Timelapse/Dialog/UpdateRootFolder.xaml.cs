using System.Windows;
namespace Timelapse.Dialog
{
    /// <summary>
    /// Ask the user if he/she wants to update the root folder names in the database to match the name of the actual root folder where the template, data and images currently reside
    /// </summary>
    public partial class UpdateRootFolder : Window
    {
        #region Private Variables
        private readonly string dbfoldername;
        private readonly string actualFolderName;
        #endregion

        #region Constructor, Loaded
        public UpdateRootFolder(Window owner, string dbfoldername, string actualFolderName)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.dbfoldername = dbfoldername;
            this.actualFolderName = actualFolderName;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.Message.What = "The name of your root folder, which is saved in the Timelapse database, has changed from " + System.Environment.NewLine;
            this.Message.What += this.dbfoldername + " to " + this.actualFolderName + "." + System.Environment.NewLine;
            this.Message.What += "You may want to update the folder name if that folder reflects where those files are normally located.";
            this.Message.Solution = "Clicking Update will update the saved root folder location from '" + this.dbfoldername + "' to '" + this.actualFolderName + "'.";
            this.Message.Hint = "Your root folder is the name of the folder containing your template, data, and image files (perhaps in their own sub-folders). " +
                "The folder name is recorded solely for your records, where you have the option to save it as a column in your CSV file. It is otherwise unused by Timelapse.";
        }
        #endregion

        #region Callbacks -Dialog Butotns
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion
    }
}
