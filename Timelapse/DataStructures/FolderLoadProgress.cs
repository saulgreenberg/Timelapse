using System.Windows.Media.Imaging;

namespace Timelapse.DataStructures
{
    // FolderLoadProgess is used to pass data to a background progress report during Image Set Loading  
    internal class FolderLoadProgress(int totalFiles)
    {
        public BitmapSource BitmapSource { get; set; } = null;
        public int CurrentFile { get; set; } = 0;
        public string CurrentFileName { get; set; } = null;
        public int TotalFiles { get; set; } = totalFiles;
        public int CurrentPass { get; set; } = 0;
        public int TotalPasses { get; set; } = 0;
    }
}
