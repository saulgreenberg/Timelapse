using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Util;

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

        // As multichoice uses a comma separated list, and as we do allow conversions between fixed choice to multichoice, don't allow commas 
        // Note that we *could* allow commas in fixed choices by testing to see if showEmptyChoiceOption is true (only for FixedChoices)
        // although we don't do that here.
        private void TextBoxChoiceList_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.OemComma)
            {
                if (sender is TextBox tb)
                {
                    FlashContentControl(tb);
                    e.Handled = true;
                }
            }
        }

        // Flash the textbox
        public void FlashContentControl(TextBox tb)
        {
            if (tb != null)
            {
                tb.Background = new SolidColorBrush(Colors.White);
                tb.Background.BeginAnimation(
                    SolidColorBrush.ColorProperty,
                    new ColorAnimation
                    {
                        From = Colors.LightCoral,
                        AutoReverse = false,
                        Duration = TimeSpan.FromSeconds(.1),
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseIn
                        },
                    });
            }
        }
    }
}
