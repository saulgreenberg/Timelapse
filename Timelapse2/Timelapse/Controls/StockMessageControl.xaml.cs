using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public partial class StockMessageControl : UserControl
    {
        #region Public Properties
        public MessageBoxImage Icon
        {
            get
            {
                return this.iconType;
            }
            set
            {
                // the MessageBoxImage enum contains duplicate values:
                // Hand = Stop = Error
                // Exclamation = Warning
                // Asterisk = Information
                switch (value)
                {
                    case MessageBoxImage.Question:
                        this.lblIconType.Content = "?";
                        this.iconType = MessageBoxImage.Question;
                        break;
                    case MessageBoxImage.Warning:
                        this.lblIconType.Content = "!";
                        this.iconType = MessageBoxImage.Warning;
                        break;
                    case MessageBoxImage.None:
                    case MessageBoxImage.Information:
                        this.lblIconType.Content = "i";
                        this.iconType = MessageBoxImage.Information;
                        break;
                    case MessageBoxImage.Error:
                        Run run = new Run
                        {
                            FontFamily = new FontFamily("Wingdings 2"),
                            Text = "\u004e"
                        };
                        // Create a symbol of a stopped hand
                        this.lblIconType.Content = run;
                        this.iconType = MessageBoxImage.Error;
                        break;
                    default:
                        this.lblIconType.Content = "?";
                        this.iconType = MessageBoxImage.Question; // Show a reasonable default in the unlikely case this happens
                        TracePrint.PrintMessage(String.Format("Unhandled icon type {0}.", this.Icon));
                        break;
                }
                this.iconType = value;
            }
        }

        public string Title
        {
            get
            {
                return this.TitleText.Text;
            }
            set
            {
                this.TitleText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string What
        {
            get
            {
                return this.WhatText.Text;
            }
            set
            {
                this.WhatText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Problem
        {
            get
            {
                return this.ProblemText.Text;
            }
            set
            {
                this.ProblemText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Reason
        {
            get
            {
                return this.ReasonText.Text;
            }
            set
            {
                this.ReasonText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Solution
        {
            get
            {
                return this.SolutionText.Text;
            }
            set
            {
                this.SolutionText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Result
        {
            get
            {
                return this.ResultText.Text;
            }
            set
            {
                this.ResultText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Hint
        {
            get
            {
                return this.HintText.Text;
            }
            set
            {
                this.HintText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Details
        {
            get
            {
                return this.DetailsText.Text;
            }
            set
            {
                this.DetailsText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public bool ShowExplanationVisibility
        {
            get
            {
                return this.HideText.Visibility == Visibility.Visible;
            }
            set
            {
                this.HideText.Visibility = (value == true) ? Visibility.Visible : Visibility.Collapsed;
                this.SetExplanationVisibility();
            }
        }
        #endregion

        #region Private Variables
        private MessageBoxImage iconType = MessageBoxImage.Information;
        #endregion

        #region Constructor
        public StockMessageControl()
        {
            this.InitializeComponent();
            this.SetExplanationVisibility();
        }
        #endregion

        #region Private methods
        private void SetExplanationVisibility()
        {
            GridLength zeroHeight = new GridLength(0.0);
            if (this.HideText.IsChecked == true)
            {
                this.MessageGrid.RowDefinitions[1].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[2].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[3].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[4].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[5].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[6].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[7].Height = zeroHeight;
                return;
            }

            GridLength autoHeight = new GridLength(1.0, GridUnitType.Auto);
            this.MessageGrid.RowDefinitions[1].Height = String.IsNullOrEmpty(this.Problem) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[2].Height = String.IsNullOrEmpty(this.What) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[3].Height = String.IsNullOrEmpty(this.Reason) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[4].Height = String.IsNullOrEmpty(this.Solution) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[5].Height = String.IsNullOrEmpty(this.Result) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[6].Height = String.IsNullOrEmpty(this.Hint) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[7].Height = String.IsNullOrEmpty(this.Details) ? zeroHeight : autoHeight;
        }

        // This will toggle the visibility of the explanation panel
        private void HideTextButton_StateChange(object sender, RoutedEventArgs e)
        {
            this.SetExplanationVisibility();
        }
        #endregion
    }
}
