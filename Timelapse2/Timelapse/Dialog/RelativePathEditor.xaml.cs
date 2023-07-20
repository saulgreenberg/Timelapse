using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using Timelapse.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RelativePathEditor.xaml
    /// </summary>
    public partial class RelativePathEditor 
    {
        private readonly FileDatabase FileDatabase;
        public RelativePathEditor(Window owner, Timelapse.Database.FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase = fileDatabase;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);
            this.BusyCancelIndicator.IsBusy = true; ;
            var relativePaths = await ProgressWrapper(() => AsyncGetRelativePath("foo"), this.ProgressHandler);
            var physicalFolders = await ProgressWrapper(() => AsyncGetAllFoldersExceptBackupAndDeletedFolders(FileDatabase.FolderPath, FileDatabase.FolderPath), this.ProgressHandler);


            // Need to get alll files/folders in the async task loop!
            this.RelativePathControl.Initialize(this, this.FileDatabase, relativePaths, physicalFolders);
            this.BusyCancelIndicator.IsBusy = false;
        }

        public async Task<List<string>> AsyncGetRelativePath(string someArgumentCurrentlyUnused)
        {
            //await Task.Delay(TimeSpan.FromSeconds(1));
            return await Task.Run(() => this.FileDatabase.GetRelativePaths());
        }


        public async Task<List<string>> AsyncGetAllFoldersExceptBackupAndDeletedFolders(string rootFolderPath, string rootFolderPrefix)
        {
            List<string> physicalFolders = new List<string>();
            //await Task.Delay(TimeSpan.FromSeconds(1));
            return await Task.Run(() => FilesFolders.GetAllFoldersExceptBackupAndDeletedFolders(rootFolderPath, physicalFolders, rootFolderPrefix));
            // await this.RelativePathControl.Initialize(this, this.FileDatabase);
            //await Task.Delay(TimeSpan.FromSeconds(5));
            //return new List<string>();
        }

        static int i = 0;
        public async Task<T> ProgressWrapper<T>(Func<Task<T>> method, System.IProgress<ProgressBarArguments> progress)
        {
            Task<T> task = method();
            while (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
            {
                await Task.WhenAny(task, Task.Delay(250)); // original was 500
                progress.Report(new ProgressBarArguments(i,
                    $"Please wait", true, true));
                i++;
            }
            return await task;
        }


        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.RelativePathControl.WereEditsMade;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.RebuildTreeAndNodes(true);
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.ExpandTreeView(true);
        }
        private void ContractAll_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.ExpandTreeView(false);
        }

        // Used for debugging
        //private void RefreshCompletelyButton_Click(object sender, RoutedEventArgs e)
        //{
        //    this.RelativePathControl.Initialize(this, this.FileDatabase);
        //}

    }
}
