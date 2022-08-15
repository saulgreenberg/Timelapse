namespace Timelapse.Controls
{
    internal static class BusyCancelIndicatorExtensions
    {
        //Set the busy indicator and set it to various intial states depending upon what it is being invoked for. 
        // If its busy, it sets it to the various specific messages. 
        // If its not busy, it sets it to some neutral messages
        // Note: I'm not really sure if setting the messages are even necessary, as that is usually overwritten in the progress handler

        // File selection - initial busy indicator state
        public static void EnableForSelection(this BusyCancelIndicator busyCancelIndicator, bool isBusy)
        {
            busyCancelIndicator.IsBusy = isBusy;
            busyCancelIndicator.Message = isBusy ? "Selecting Files from the database. Please wait..." : "Please wait...";
            busyCancelIndicator.CancelButtonIsEnabled = false;
            busyCancelIndicator.CancelButtonText = isBusy ? "Querying the database" : "Cancel";
        }

        // Merging - initial busy indicator state
        public static void EnableForMerging(this BusyCancelIndicator busyCancelIndicator, bool isBusy)
        {
            busyCancelIndicator.IsBusy = isBusy;
            busyCancelIndicator.Message = isBusy ? "Merging databases. Please wait..." : "Please wait...";
            busyCancelIndicator.CancelButtonIsEnabled = false;
            busyCancelIndicator.CancelButtonText = isBusy ? "Merging databases..." : "Cancel";
        }

    }
}
