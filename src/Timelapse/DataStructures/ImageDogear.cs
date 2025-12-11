using Timelapse.ControlsDataEntry;

namespace Timelapse.DataStructures
{
    public class ImageDogear(DataEntryHandler datahandler)
    {
        private int DogearedImageIndex = Constant.DatabaseValues.InvalidRow;
        private int LastSeenImageIndex = Constant.DatabaseValues.InvalidRow;

        public bool TrySetDogearToCurrentImage()
        {
            if (datahandler?.ImageCache?.CurrentRow == null)
            {
                return false;
            }
            this.DogearedImageIndex = datahandler.ImageCache.CurrentRow;
            this.LastSeenImageIndex = Constant.DatabaseValues.InvalidRow;
            return true;
        }

        // If we are on the dogeared image, return the last seen image
        // otherwise return the dogeared image
        public int TryGetDogearOrPreviouslySeenImageIndex()
        {
            if (this.DogearedImageIndex == Constant.DatabaseValues.InvalidRow || datahandler?.ImageCache == null || datahandler?.FileDatabase == null)
            {
                // Can't go to the dogeared image as it doesn't exist, so nothing to do
                return Constant.DatabaseValues.InvalidRow;
            }

            if (this.DogearedImageIndex == datahandler.ImageCache.CurrentRow)
            {
                // As we are already on the dogeared image,try to return the last seen image instead (which may be an invalid row)
                return this.LastSeenImageIndex;
            }

            // Try to return to the dogeared image
            this.LastSeenImageIndex = datahandler.ImageCache.CurrentRow;
            return this.DogearedImageIndex;
        }

        public bool IsDogearTheCurrentImage()
        {
            return this.DogearedImageIndex == datahandler.ImageCache.CurrentRow;
        }

        public bool DogearExists()
        {
            return this.DogearedImageIndex != Constant.DatabaseValues.InvalidRow;
        }

        public bool LastSeenImageExists()
        {
            return this.LastSeenImageIndex != Constant.DatabaseValues.InvalidRow;
        }

    }
}
