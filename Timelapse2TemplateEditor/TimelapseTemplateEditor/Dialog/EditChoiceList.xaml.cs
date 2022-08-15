using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.DataStructures;
using Timelapse.Dialog;

namespace Timelapse.Editor.Dialog
{
    public partial class EditChoiceList : Window
    {
        private static readonly string[] NewLineDelimiter = { Environment.NewLine };
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

        // Transform the list by trimming leading and trailing white space for each line, removing empty lines, and removing duplicate items
        private static string TrimLinesAndRemoveEmptyLines(string textlist)
        {
            List<string> trimmedchoices = new List<string>();
            string trimmedchoice;
            List<string> choices = new List<string>(textlist.Split(NewLineDelimiter, StringSplitOptions.RemoveEmptyEntries));

            foreach (string choice in choices)
            {
                trimmedchoice = choice.Trim();
                if (String.IsNullOrWhiteSpace(choice) == false && trimmedchoices.Contains(trimmedchoice) == false)
                {
                    trimmedchoices.Add(trimmedchoice);
                }
            }
            return string.Join(string.Join(String.Empty, NewLineDelimiter), trimmedchoices);
        }
    }
}
