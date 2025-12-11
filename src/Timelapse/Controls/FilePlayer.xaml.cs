using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Enums;
using Timelapse.EventArguments;

namespace Timelapse.Controls
{
    /// <summary>
    /// FilePlayer contains a set of media controls representing how one can navigate through files by:
    /// - going to the first and last file
    /// - stepping forwards and backwards through files one at a time
    /// - playing forwards and backwards through files at two different speeds
    /// It does not actually do anything except raise events signifying the user's intentions.
    /// </summary>
    public partial class FilePlayer
    {
        #region Public Properties
        public DirectionEnum Direction { get; set; }
        public FilePlayerSelectionEnum Selection { get; set; }
        #endregion

        #region Event-related
        public delegate void FilePlayerChangedHandler(object sender, FilePlayerEventArgs e);
        public event FilePlayerChangedHandler FilePlayerChange;
        public void OnFilePlayerChange(object sender, FilePlayerEventArgs e)
        {
            // If there exist any subscribers call the event
            FilePlayerChange?.Invoke(this, e);
        }
        #endregion

        #region Constructor
        public FilePlayer()
        {
            InitializeComponent();
            SwitchFileMode(true);
        }
        #endregion

        #region Public methods
        public void Stop()
        {
            StopButton.IsChecked = true;
        }

        // Enable or Disable the backwards controls
        public void BackwardsControlsEnabled(bool isEnabled)
        {
            FirstFile.IsEnabled = isEnabled;
            PlayBackwardsFast.IsEnabled = isEnabled;
            PlayBackwardsSlow.IsEnabled = isEnabled;
            StepBackwards.IsEnabled = isEnabled;
            RowUp.IsEnabled = isEnabled;
            PageUp.IsEnabled = isEnabled;
        }

        // Enable or Disable the forward controls
        public void ForwardsControlsEnabled(bool isEnabled)
        {
            StepForward.IsEnabled = isEnabled;
            PlayForwardFast.IsEnabled = isEnabled;
            PlayForwardSlow.IsEnabled = isEnabled;
            LastFile.IsEnabled = isEnabled;
            RowDown.IsEnabled = isEnabled;
            PageDown.IsEnabled = isEnabled;
        }

        public void SwitchFileMode(bool isSingleMode)
        {
            RowDown.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;
            RowUp.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;
            PageDown.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;
            PageUp.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;

            PlayForwardSlow.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
            PlayForwardFast.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
            PlayBackwardsSlow.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
            PlayBackwardsFast.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region Button and Keypress Callbacks
        public void FilePlayer_Click(object sender, RoutedEventArgs e)
        {
            string tag = string.Empty;
            if (sender is RadioButton button)
            {
                tag = (string)button.Tag;
            }
            else if (sender is MenuItem menu)
            {
                tag = (string)menu.Tag;
            }

            switch (tag)
            {
                case "First":
                    Direction = DirectionEnum.Previous;
                    Selection = FilePlayerSelectionEnum.First;
                    break;
                case "PageUp":
                    Direction = DirectionEnum.Previous;
                    Selection = FilePlayerSelectionEnum.Page;
                    break;
                case "RowUp":
                    Direction = DirectionEnum.Previous;
                    Selection = FilePlayerSelectionEnum.Row;
                    break;
                case "PlayBackwardsFast":
                    Direction = DirectionEnum.Previous;
                    Selection = FilePlayerSelectionEnum.PlayFast;
                    break;
                case "PlayBackwardsSlow":
                    Direction = DirectionEnum.Previous;
                    Selection = FilePlayerSelectionEnum.PlaySlow;
                    break;
                case "StepBackwards":
                    Direction = DirectionEnum.Previous;
                    Selection = FilePlayerSelectionEnum.Step;
                    break;
                case "Stop":
                    Selection = FilePlayerSelectionEnum.Stop;
                    break;
                case "StepForward":
                    Direction = DirectionEnum.Next;
                    Selection = FilePlayerSelectionEnum.Step;
                    break;
                case "PlayForwardSlow":
                    Direction = DirectionEnum.Next;
                    Selection = FilePlayerSelectionEnum.PlaySlow;
                    break;
                case "PlayForwardFast":
                    Direction = DirectionEnum.Next;
                    Selection = FilePlayerSelectionEnum.PlayFast;
                    break;
                case "PageDown":
                    Direction = DirectionEnum.Next;
                    Selection = FilePlayerSelectionEnum.Page;
                    break;
                case "RowDown":
                    Direction = DirectionEnum.Next;
                    Selection = FilePlayerSelectionEnum.Row;
                    break;
                case "Last":
                    Direction = DirectionEnum.Next;
                    Selection = FilePlayerSelectionEnum.Last;
                    break;
                default:
                    Selection = FilePlayerSelectionEnum.Stop;
                    break;
            }
            // Raise the event
            OnFilePlayerChange(this, new(Direction, Selection));
        }

        private void FilePlayer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                Selection = FilePlayerSelectionEnum.Stop;
                OnFilePlayerChange(this, new(Direction, Selection));
                e.Handled = true;
            }
        }
        #endregion
    }
}
