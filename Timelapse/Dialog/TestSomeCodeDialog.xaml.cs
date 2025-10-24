using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {
        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {

        }

        private void Ml_OnTextChanged(object sender, MultiLineTextChangedEventArgs e)
        {
            if (e.NewText != null && this.TextBox != null)
            {
               this.TextBox.Text = e.NewText;
            }
        }

        private void Ml_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not MultiLineText ml) return;
            ml.BorderBrush = System.Windows.Media.Brushes.DarkOrange;
        }

        private void Ml_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not MultiLineText ml) return;
            ml.BorderBrush = System.Windows.Media.Brushes.LightBlue;
        }

        private void TB_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox ml) return;
            ml.BorderBrush = System.Windows.Media.Brushes.DarkOrange;
        }

        private void TB_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox ml) return;
            ml.BorderBrush = System.Windows.Media.Brushes.LightBlue;
        }

        private void UIElement_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.BorderBrush = System.Windows.Media.Brushes.DarkOrange;
            }
            else if (sender is MultiLineText ml)
            {
                ml.BorderBrush = System.Windows.Media.Brushes.DarkOrange;
            }
        }

        private void UIElement_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.BorderBrush = System.Windows.Media.Brushes.LightBlue;
            }
            else if (sender is MultiLineText ml)
            {
                ml.BorderBrush = System.Windows.Media.Brushes.LightBlue;
            }
        }
    }
}
