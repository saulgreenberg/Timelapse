using System.Windows;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Editor.Dialog
{
    public partial class EditChoiceList
    {
        private readonly UIElement PositionReference;
        public Choices Choices { get; private set; }

        public EditChoiceList(UIElement positionReference, Choices choices, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.PositionReference = positionReference;
            this.Choices = choices;

            this.TextBoxChoiceList.Text = this.Choices.GetAsTextboxList;
            this.IncludeEmptyChoiceCheckBox.IsChecked = this.Choices.IncludeEmptyChoice;
        }

        // Position the window so it appears as a popup with its bottom aligned to the top of its owner button
        // Add callbacks as needed
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Point topLeft = this.PositionReference.PointToScreen(new Point(0, 0));
            this.Top = topLeft.Y - this.ActualHeight;
            if (this.Top < 0)
            {
                this.Top = 0;
            }
            this.Left = topLeft.X;

            // On some older Windows versions the above positioning doesn't work as the list ends up to the right of the main window
            // Check to make sure it's in the main window, and if not, we try to position it there
            if (Application.Current != null)
            {
                double choiceRightSide = this.Left + this.ActualWidth;
                if (Application.Current.MainWindow == null)
                {
                    TracePrint.NullException(nameof(Application.Current.MainWindow));
                    return;
                }
                double mainWindowRightSide = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
                if (choiceRightSide > mainWindowRightSide)
                {
                    this.Left = mainWindowRightSide - this.ActualWidth - 100;
                }
            }
            Dialogs.TryFitDialogInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Choices = new Choices(this.TextBoxChoiceList.Text, this.IncludeEmptyChoiceCheckBox.IsChecked == true);
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
