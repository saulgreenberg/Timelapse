using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.DataStructures;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {

        #region Constructor and Initialization

        public TestSomeCodeDialog(Window owner) 
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(TestMessage);
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            //Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.TestMessage.BuildContentFromProperties();

            ComboBoxItem cbi;
            ComboBoxItem cbi2;
            List<string> choices = ["a", "b", "sss", "s2"];
            foreach (string choice in choices)
            {
                cbi = new()
                {
                    Content = choice
                };
                CB.Items.Add(cbi);

                cbi = new()
                {
                    Content = choice
                };
                CBX.Items.Add(cbi);
            }


            // put empty choice / separator at the beginning of the control

            cbi = new()
            {
                Content = string.Empty
            };
            Separator separator = new Separator();
            TextSearch.SetText(separator, "\x0001");
            CB.Items.Insert(0, separator);
            CB.Items.Insert(0, cbi);
        }
        #endregion
    }
}


