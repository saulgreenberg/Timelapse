using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;
using TimelapseWpf.Toolkit;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RandomSampleSelection.xaml
    /// </summary>
    public partial class RandomSampleSelection
    {
        public int SampleSize { get; set; }

        private readonly int MaxSampleSize;

        private bool DontPropagate;
        public RandomSampleSelection(Window owner, int maxSampleSize)
        {
            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            MaxSampleSize = maxSampleSize;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            RandomSlider.Maximum = MaxSampleSize - 1;
            RandomSlider.ValueChanged += RandomSlider_ValueChanged;
            RandomSlider.Value = MaxSampleSize >= 100 ? 100 : MaxSampleSize;

            UpDownRandom.Maximum = MaxSampleSize - 1;
            UpDownRandom.Value = Convert.ToInt32(RandomSlider.Value);
            UpDownRandom.ValueChanged += UpDownRandom_ValueChanged;
        }

        private void UpDownRandom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is IntegerUpDown integerUpDown && false == DontPropagate)
            {
                RandomSlider.Value = Convert.ToDouble(integerUpDown.Value);
            }
        }

        #region Callback -Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SampleSize = Convert.ToInt32(RandomSlider.Value);
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion

        private void RandomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                SampleSize = Convert.ToInt32(slider.Value);
                TBFilesSelected.Text = $" / {MaxSampleSize} files will be sampled";
                DontPropagate = true;
                UpDownRandom.Value = Convert.ToInt32(slider.Value);
                DontPropagate = false;
            }
        }
    }
}
