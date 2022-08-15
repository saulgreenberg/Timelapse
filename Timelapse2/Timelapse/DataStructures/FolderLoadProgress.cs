using System.Windows.Media.Imaging;

namespace Timelapse.Images
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
            this.BitmapSource = null;
            this.CurrentFile = 0;
            this.CurrentFileName = null;
            this.TotalFiles = totalFiles;
            this.CurrentPass = 0;
            this.TotalPasses = 0;
        }
    }
}
