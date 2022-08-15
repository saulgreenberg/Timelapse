using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for the mismatched templates dialog.
    /// </summary>
    public partial class TemplateSynchronization : Window
    {
        #region Public Properties
        public bool UseNewTemplate { get; private set; }
        #endregion

        #region Constructor, Loaded
        public TemplateSynchronization(List<string> errors, List<string> warnings, Window owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(errors, nameof(errors));
            ThrowIf.IsNullArgument(warnings, nameof(warnings));

            this.InitializeComponent();
            this.Owner = owner;

            string messageUsingNewTemplate = Environment.NewLine + " o 'Open using New Template':   uses the new template, but will have the issues below.";
            string messageUsingOldTemplate = Environment.NewLine + " o Open using Old Template:'   ignores the new template, where it uses the original version of your template.";
            string messageExitTimelapse = Environment.NewLine + " o Exit Timelapse:'   closes Timelapse.";
            string messageWarningLabel = String.Empty;

            if (warnings.Count > 0)
            {
                messageWarningLabel = (errors.Count > 0) ? "Additional warnings" : "Warnings";
            }

            if (errors.Count == 0 && warnings.Count > 0)
            {
                // Warning dialog only. Show all buttons
                this.Message.Title = "Your template has minor incompatiblities with your data file";
                this.Message.Problem = "The fields defined in your .tdb template file differ somewhat from the origin template used to create your data. " + Environment.NewLine +
                                        "While you can still use your new template, check the warnings below.";
                this.Message.Solution += messageUsingNewTemplate;
                this.Message.Solution += messageUsingOldTemplate;
                this.Message.Solution += messageExitTimelapse;
            }

            if (errors.Count > 0)
            {
                // Errors and warnings dialog. Don't show Use New Template button as the new template is not valid
                this.Message.Title = "Your template is not compatible with your data file";
                this.Message.Problem = "The fields defined in your .tdb template file conflict with those originally used to create this data." + Environment.NewLine +
                                       "You won't be able to use your new template due to the errors below.";
                this.Message.Solution += messageUsingOldTemplate;
                this.Message.Solution += messageExitTimelapse;

                // We should not allow the new template to be selected if there is an unrecoverable error
                this.ButtonUseNewTemplate.Visibility = Visibility.Collapsed;
                this.ButtonUseNewTemplate.IsDefault = false;
                this.ButtonUseOldTemplate.IsDefault = true;

                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = "Errors" });

                foreach (string error in errors)
                {
                    this.TextBlockDetails.Inlines.Add(Environment.NewLine);
                    this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Normal, Text = error });
                }
                if (warnings.Count > 0)
                {
                    this.TextBlockDetails.Inlines.Add(Environment.NewLine);
                }
            }

            if (warnings.Count > 0)
            {
                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = messageWarningLabel });
                foreach (string warning in warnings)
                {
                    this.TextBlockDetails.Inlines.Add(Environment.NewLine);
                    this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Normal, Text = warning });
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region UI Callbacks
        private void ExitTimelapse_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OpenUsingOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.UseNewTemplate = false;
            this.DialogResult = true;
        }

        private void OpenUsingNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.UseNewTemplate = true;
            this.DialogResult = true;
        }
        #endregion
    }
}
