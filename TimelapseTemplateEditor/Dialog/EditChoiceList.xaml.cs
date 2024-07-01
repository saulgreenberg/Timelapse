using System.Windows;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;

namespace TimelapseTemplateEditor.Dialog
{
    public partial class EditChoiceList
    {
        private readonly UIElement PositionReference;
        public Choices Choices { get; private set; }

        public EditChoiceList(UIElement positionReference, Choices choices, bool showEmptyChoiceOption, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            PositionReference = positionReference;
            Choices = choices;

            TextBoxChoiceList.Text = Choices.GetAsTextboxList;
            IncludeEmptyChoiceCheckBox.Visibility = showEmptyChoiceOption ? Visibility.Visible : Visibility.Collapsed;
            IncludeEmptyChoiceCheckBox.IsChecked = Choices.IncludeEmptyChoice;
        }

        // Position the window so it appears as a popup with its bottom aligned to the top of its owner button
        // Add callbacks as needed
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Point topLeft = PositionReference.PointToScreen(new Point(0, 0));
            Top = topLeft.Y - ActualHeight;
            if (Top < 0)
            {
                Top = 0;
            }
            Left = topLeft.X;

            // On some older Windows versions the above positioning doesn't work as the list ends up to the right of the main window
            // Check to make sure it's in the main window, and if not, we try to position it there
            if (Application.Current != null)
            {
                double choiceRightSide = Left + ActualWidth;
                if (Application.Current.MainWindow == null)
                {
                    TracePrint.NullException(nameof(Application.Current.MainWindow));
                    return;
                }
                double mainWindowRightSide = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
                if (choiceRightSide > mainWindowRightSide)
                {
                    Left = mainWindowRightSide - ActualWidth - 100;
                }
            }
            Dialogs.TryFitDialogInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Choices = new Choices(TextBoxChoiceList.Text, IncludeEmptyChoiceCheckBox.IsChecked == true);
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
