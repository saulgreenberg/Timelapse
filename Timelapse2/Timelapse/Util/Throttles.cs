using System;
using System.Windows;

namespace Timelapse.Util
{
    /// <summary>
    /// Throttles are used to slow down image rendering, as we may hit limits on some computers that causes image display to freeze or stutter.
    /// </summary>
    public class Throttles
    {
        #region Public Properties
        // The current setting for images rendered per second. Default is set to the maximum.
        public double DesiredImageRendersPerSecond { get; private set; }
        public TimeSpan DesiredIntervalBetweenRenders { get; private set; }
        public int RepeatedKeyAcceptanceInterval { get; private set; }
        #endregion

        #region Constructors
        public Throttles()
        {
            this.ResetToDefaults();
        }
        #endregion

        #region Public Methods - Set the Throttle

        /// <summary>
        /// Reset the image renedered values to their hard-coded defaults
        /// </summary>
        public void ResetToDefaults()
        {
            this.DesiredImageRendersPerSecond = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault;
        }

        /// <summary>
        /// Set desired image rendered values based upon the rendersPerSecond
        /// Also guarantees that the renders per second are kept within a hard-coded range
        /// </summary>
        /// <param name="rendersPerSecond"></param>
        public void SetDesiredImageRendersPerSecond(double rendersPerSecond)
        {
            // Ensure that the renders per second is within range. 
            // If not, and depending what it is set to, set it to either the lower or upper bound
            if (rendersPerSecond < Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound)
            {
                rendersPerSecond = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            }
            else if (rendersPerSecond > Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound)
            {
                rendersPerSecond = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
                // System.Diagnostics.Debug.Print("RendersPerSecond corrected as it was not within range");
            }

            this.DesiredImageRendersPerSecond = rendersPerSecond;
            this.DesiredIntervalBetweenRenders = TimeSpan.FromSeconds(1.0 / rendersPerSecond);
            this.RepeatedKeyAcceptanceInterval = (int)((SystemParameters.KeyboardSpeed + 0.5 * rendersPerSecond) / rendersPerSecond);
        }

        #endregion
    }
}
