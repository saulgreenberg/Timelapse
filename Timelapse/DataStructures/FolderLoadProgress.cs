using System.Windows.Media.Imaging;

namespace Timelapse.DataStructures
{
    // FolderLoadProgess is used to pass data to a background progress report during Image Set Loading  
    internal class FolderLoadProgress
    {
        public BitmapSource BitmapSource { get; set; }
        public int CurrentFile { get; set; }
        public string CurrentFileName { get; set; }
        public int TotalFiles { get; set; }
        public int CurrentPass { get; set; }
        public int TotalPasses { get; set; }

        public FolderLoadProgress(int totalFiles)
        {
            BitmapSource = null;
            CurrentFile = 0;
            CurrentFileName = null;
            TotalFiles = totalFiles;
            CurrentPass = 0;
            TotalPasses = 0;
        }
    }
}
