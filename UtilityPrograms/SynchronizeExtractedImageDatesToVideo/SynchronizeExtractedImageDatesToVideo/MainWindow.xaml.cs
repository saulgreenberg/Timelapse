using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;

namespace SynchronizeExtractedImageDatesToVideo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> VideoFiles = new List<string>();
        private List<string> JPGFiles = new List<string>();
        private string initialPath = @"C:";
        private string[] VideoSuffixes = { "*.AVI" }; // The video files of interest
        private double incrementAmount = 0.5;

        public MainWindow()
        {
            InitializeComponent();
        }

        private bool TryGetVideoPath(string initialPath, out string videoPath)
        {
            // Default the template selection dialog to the most recently opened database
            if (TryGetFolderFromUser(
                         "Select the folder containing the videos and extracted images",
                         out string videoFilesPath) == false)
            {
                videoPath = String.Empty;
                return false;
            }

            videoPath = videoFilesPath;
            if (String.IsNullOrEmpty(videoPath))
            {
                return false;
            }
            return true;
        }

        public static bool TryGetFolderFromUser(string title, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            FolderBrowserDialog openFolderDialog = new FolderBrowserDialog
            {
                Description = title,
                ShowNewFolderButton = false
            };
            DialogResult result = openFolderDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                selectedFilePath = openFolderDialog.SelectedPath;
                return true;
            }
            selectedFilePath = null;
            return false;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetVideoPath(initialPath, out string videoPath) == false)
            {
                return;
            }

            // reset everything
            this.FeedbackListBox.Items.Clear();
            JPGFiles.Clear();

            VideoFiles.AddRange(System.IO.Directory.GetFiles(videoPath, "*.AVI"));
            if (VideoFiles.Count == 0)
            {
                this.FeedbackListBox.Items.Add("No video files (ending in .AVI) found.");
                return;
            }

            this.Title = "Processing " + VideoFiles.Count.ToString() + " videos in " + videoPath;
            this.OpenButton.IsEnabled = false;
            System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;

            JPGFiles.AddRange(System.IO.Directory.GetFiles(videoPath, "*.JPG"));

            if (JPGFiles.Count == 0)
            {
                this.FeedbackListBox.Items.Add("    No JPG files found matching this video");
            }

            foreach (string videoFile in VideoFiles)
            {
                DateTime modified = File.GetLastWriteTime(videoFile);
                this.FeedbackListBox.Items.Add ("Processing " + Path.GetFileName(videoFile) + " (" + modified.ToLongDateString() + " " + modified.ToLongTimeString() + ")");

                string prefix = Path.Combine(Path.GetDirectoryName(videoFile), Path.GetFileNameWithoutExtension(videoFile));
                foreach(string jpgFile in JPGFiles)
                {
                    if (jpgFile.StartsWith(prefix))
                    {
                        // File.SetLastWriteTime(jpgFile, modified);
                        await System.Threading.Tasks.Task.Run( () => ResetFileDate(jpgFile, modified));
                        this.FeedbackListBox.Items.Add("     " + Path.GetFileName(jpgFile) + " -> " + modified.ToLongDateString() + " " + modified.ToLongTimeString());
                        this.FeedbackListBox.ScrollIntoView(this.FeedbackListBox.Items[this.FeedbackListBox.Items.Count - 1]);
                        modified = modified + TimeSpan.FromMilliseconds(this.IncrementSlider.Value * 1000);
                    }
                }
            }
            System.Windows.Forms.Cursor.Current = Cursors.Default;
            this.OpenButton.IsEnabled = true;
        }
        private void ResetFileDate(string jpgFile, DateTime modified)
        {
            File.SetLastWriteTime(jpgFile, modified);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.IncrementLabel.Content = this.IncrementSlider.Value.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.IncrementSlider.Value = this.incrementAmount;
        }
    }
 
}
