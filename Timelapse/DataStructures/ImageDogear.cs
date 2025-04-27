using Timelapse.ControlsDataEntry;

namespace Timelapse.DataStructures
{
    public class ImageDogear
    {
        private int DogearedImageIndex;
        private int LastSeenImageIndex;
        private readonly DataEntryHandler DataHandler;
        public ImageDogear(DataEntryHandler datahandler)
        {
            this.LastSeenImageIndex = Constant.DatabaseValues.InvalidRow;
            this.DogearedImageIndex = Constant.DatabaseValues.InvalidRow;
            this.DataHandler = datahandler;
        }

        public bool TrySetDogearToCurrentImage()
        {
            if (this.DataHandler?.ImageCache?.CurrentRow == null)
            {
                return false;
            }
            this.DogearedImageIndex = this.DataHandler.ImageCache.CurrentRow;
            this.LastSeenImageIndex = Constant.DatabaseValues.InvalidRow;
            return true;
        }

        // If we are on the dogeared image, return the last seen image
        // otherwise return the dogeared image
        public int TryGetDogearOrPreviouslySeenImageIndex()
        {
            if (this.DogearedImageIndex == Constant.DatabaseValues.InvalidRow || this.DataHandler?.ImageCache == null || this.DataHandler?.FileDatabase == null)
            {
                // Can't go to the dogeared image as it doesn't exist, so nothing to do
                return Constant.DatabaseValues.InvalidRow;
            }

            if (this.DogearedImageIndex == this.DataHandler.ImageCache.CurrentRow)
            {
                // As we are already on the dogeared image,try to return the last seen image instead (which may be an invalid row)
                return this.LastSeenImageIndex;
            }

            // Try to return to the dogeared image
            this.LastSeenImageIndex = this.DataHandler.ImageCache.CurrentRow;
            return this.DogearedImageIndex;
        }

        public bool IsDogearTheCurrentImage()
        {
            return this.DogearedImageIndex == this.DataHandler.ImageCache.CurrentRow;
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
