using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Timelapse.DebuggingSupport;

namespace Timelapse.Controls
{
    public partial class StockMessageControl
    {
        #region Public Properties
        public MessageBoxImage Icon
        {
            get => iconType;
            set
            {
                // the MessageBoxImage enum contains duplicate values:
                // Hand = Stop = Error
                // Exclamation = Warning
                // Asterisk = Information
                switch (value)
                {
                    case MessageBoxImage.Question:
                        lblIconType.Content = "?";
                        iconType = MessageBoxImage.Question;
                        break;
                    case MessageBoxImage.Warning:
                        lblIconType.Content = "!";
                        iconType = MessageBoxImage.Warning;
                        break;
                    case MessageBoxImage.None:
                    case MessageBoxImage.Information:
                        lblIconType.Content = "i";
                        iconType = MessageBoxImage.Information;
                        break;
                    case MessageBoxImage.Error:
                        Run run = new Run
                        {
                            FontFamily = new FontFamily("Wingdings 2"),
                            Text = "\u004e"
                        };
                        // Create a symbol of a stopped hand
                        lblIconType.Content = run;
                        iconType = MessageBoxImage.Error;
                        break;
                    default:
                        lblIconType.Content = "?";
                        iconType = MessageBoxImage.Question; // Show a reasonable default in the unlikely case this happens
                        TracePrint.PrintMessage($"Unhandled icon type {Icon}.");
                        break;
                }
                iconType = value;
            }
        }

        public string Title
        {
            get => TitleText.Text;
            set
            {
                TitleText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string What
        {
            get => WhatText.Text;
            set
            {
                WhatText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string Problem
        {
            get => ProblemText.Text;
            set
            {
                ProblemText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string Reason
        {
            get => ReasonText.Text;
            set
            {
                ReasonText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string Solution
        {
            get => SolutionText.Text;
            set
            {
                SolutionText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string Result
        {
            get => ResultText.Text;
            set
            {
                ResultText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string Hint
        {
            get => HintText.Text;
            set
            {
                HintText.Text = value;
                SetExplanationVisibility();
            }
        }

        public string Details
        {
            get => DetailsText.Text;
            set
            {
                DetailsText.Text = value;
                SetExplanationVisibility();
            }
        }

        public bool ShowExplanationVisibility
        {
            get => HideText.Visibility == Visibility.Visible;
            set
            {
                HideText.Visibility = value 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
                SetExplanationVisibility();
            }
        }
        #endregion

        #region Private Variables
        private MessageBoxImage iconType = MessageBoxImage.Information;
        #endregion

        #region Constructor
        public StockMessageControl()
        {
            InitializeComponent();
            SetExplanationVisibility();
        }
        #endregion

        #region Private methods
        private void SetExplanationVisibility()
        {
            GridLength zeroHeight = new GridLength(0.0);
            if (HideText.IsChecked == true)
            {
                MessageGrid.RowDefinitions[1].Height = zeroHeight;
                MessageGrid.RowDefinitions[2].Height = zeroHeight;
                MessageGrid.RowDefinitions[3].Height = zeroHeight;
                MessageGrid.RowDefinitions[4].Height = zeroHeight;
                MessageGrid.RowDefinitions[5].Height = zeroHeight;
                MessageGrid.RowDefinitions[6].Height = zeroHeight;
                MessageGrid.RowDefinitions[7].Height = zeroHeight;
                return;
            }

            GridLength autoHeight = new GridLength(1.0, GridUnitType.Auto);
            MessageGrid.RowDefinitions[1].Height = string.IsNullOrEmpty(Problem) ? zeroHeight : autoHeight;
            MessageGrid.RowDefinitions[2].Height = string.IsNullOrEmpty(What) ? zeroHeight : autoHeight;
            MessageGrid.RowDefinitions[3].Height = string.IsNullOrEmpty(Reason) ? zeroHeight : autoHeight;
            MessageGrid.RowDefinitions[4].Height = string.IsNullOrEmpty(Solution) ? zeroHeight : autoHeight;
            MessageGrid.RowDefinitions[5].Height = string.IsNullOrEmpty(Result) ? zeroHeight : autoHeight;
            MessageGrid.RowDefinitions[6].Height = string.IsNullOrEmpty(Hint) ? zeroHeight : autoHeight;
            MessageGrid.RowDefinitions[7].Height = string.IsNullOrEmpty(Details) ? zeroHeight : autoHeight;
        }

        // This will toggle the visibility of the explanation panel
        private void HideTextButton_StateChange(object sender, RoutedEventArgs e)
        {
            SetExplanationVisibility();
        }
        #endregion
    }
}
