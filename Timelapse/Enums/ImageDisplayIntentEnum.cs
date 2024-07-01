namespace Timelapse.Enums
{
    // Possible ways that an image is expected to be rendered
    public enum ImageDisplayIntentEnum
    {
        Ephemeral,           // Indicates the user is displaying images for a very short time, e.g. image previes shown during load, or when navigating images quickly (e.g., arrow keys, slider)
        Persistent           // Indicates the image will likely be on display for more than a brief moment.
    }
}
