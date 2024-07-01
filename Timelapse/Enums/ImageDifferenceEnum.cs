namespace Timelapse.Enums
{
    // These are used for image differencing
    // If a person toggles between the current image and its two differenced images, those images are stored
    // in a 'cache' so they can be redisplayed more quickly (vs. re-reading it from a file or regenerating it)
    public enum ImageDifferenceEnum
    {
        Previous,  // image illustrates the difference between the current image with the previous image 
        Unaltered, // the original unaltered image
        Next,      // image illustrates the difference between the current image with the next image 
        Combined   // image illustrates the difference between the current, previous and next image combined
    }
}
