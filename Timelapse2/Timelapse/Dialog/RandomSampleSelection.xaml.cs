using System;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RandomSampleSelection.xaml
    /// </summary>
    public partial class RandomSampleSelection : Window
    {
        public int SampleSize { get; set; }

        private readonly int MaxSampleSize;

        private bool DontPropagate;
        public RandomSampleSelection(Window owner, int maxSampleSize)
        {
            InitializeComponent();
            this.Owner = owner;
            this.MaxSampleSize = maxSampleSize;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.RandomSlider.Maximum = this.MaxSampleSize - 1;
            this.RandomSlider.ValueChanged += this.RandomSlider_ValueChanged;
            this.RandomSlider.Value = this.MaxSampleSize >= 100 ? 100 : this.MaxSampleSize;

            this.UpDownRandom.Maximum = this.MaxSampleSize - 1;
            this.UpDownRandom.Value = Convert.ToInt32(this.RandomSlider.Value);
            this.UpDownRandom.ValueChanged += this.UpDownRandom_ValueChanged;
        }

        private void UpDownRandom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is IntegerUpDown integerUpDown && false == this.DontPropagate)
            {
                this.RandomSlider.Value = Convert.ToDouble(integerUpDown.Value);
            }
        }

        #region Callback -Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.SampleSize = Convert.ToInt32(this.RandomSlider.Value);
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        private void RandomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                this.SampleSize = Convert.ToInt32(slider.Value);
                this.TBFilesSelected.Text = String.Format(" / {0} files will be sampled", this.MaxSampleSize);
                this.DontPropagate = true;
                this.UpDownRandom.Value = Convert.ToInt32(slider.Value);
                this.DontPropagate = false;
            }
        }
    }
}
