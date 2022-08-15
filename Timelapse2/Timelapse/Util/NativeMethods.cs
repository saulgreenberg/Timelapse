using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Timelapse.Util
{
    internal class NativeMethods
    {
        #region Public Methods - Cursor Position
        /// <summary>
        /// Get the cursor position. This purportedly corrects a WPF problem... not sure if its really needed.
        /// </summary>
        /// <param name="relativeTo"></param>
        /// <returns>Point</returns>
        public static Point GetCursorPos(Visual relativeTo)
        {
            Win32Point w32Mouse = new Win32Point();
            NativeMethods.GetCursorPos(ref w32Mouse);

            // Check if the presentation source is actually there as otherwise relativeTo will return an error
            // This happens when the relativeTo  is deleted when we are still trying to get the magnifying glass position.
            if (PresentationSource.FromVisual(relativeTo) == null)
            {
                return new Point(0, 0);
            }
            return relativeTo.PointFromScreen(new Point(w32Mouse.X, w32Mouse.Y));
        }
        #endregion

        #region Private aspects
        // Conversions between Pixels and device-independent pixels
        // Note that this depends on the DPI settings of the display. 
        // Typical dpi settings are 96dpi (which means the two are equivalent), but this is not always the case.
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hDc, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;
        #endregion

        #region Public Methos - Transform Pixels
        /// <summary>
        /// Given size units in normal pixels, translate them into device independent pixels (the out parameters)
        /// </summary>
        /// <param name="widthInPixels"></param>
        /// <param name="heightInPixels"></param>
        /// <param name="widthInDeviceIndependentPixels"></param>
        /// <param name="heightInDeviceIndependentPixels"></param>
        public static void TransformPixelsToDeviceIndependentPixels(int widthInPixels,
                                      int heightInPixels,
                                      out double widthInDeviceIndependentPixels,
                                      out double heightInDeviceIndependentPixels)
        {
            IntPtr hDc = GetDC(IntPtr.Zero);
            if (hDc != IntPtr.Zero)
            {
                int dpiX = GetDeviceCaps(hDc, LOGPIXELSX);
                int dpiY = GetDeviceCaps(hDc, LOGPIXELSY);

                _ = ReleaseDC(IntPtr.Zero, hDc);

                widthInDeviceIndependentPixels = 96 * widthInPixels / (double)dpiX;
                heightInDeviceIndependentPixels = 96 * heightInPixels / (double)dpiY;
            }
            else
            {
                // This is very unlikely. 
                // As a workaround, we just return the original pixel size. While this may not be the correct size (depending on the actual dpi), 
                // it will not crash the program and at least maintains the correct aspect ration
                widthInDeviceIndependentPixels = widthInPixels;
                heightInDeviceIndependentPixels = heightInPixels;
                TracePrint.PrintMessage("In TransformPixelsToDeviceIndependentPixels: Failed to get DC.");
            }
        }
        #endregion

        #region Private aspects
        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref Win32Point pt);
        #endregion

        #region Unused: TransformDeviceIndependentPixelsToPixels
        // UNUSED - BUT LETS KEEP IT FOR NOW.
        // Transforms device independent units(1/96 of an inch) to pixels
        // <param name = "widthInDeviceIndependentPixels" > a device independent unit value X</param>
        // <param name = "heightInDeviceIndependentPixels" > a device independent unit value Y</param>
        // <param name = "widthInPixels" > returns the X value in pixels</param>
        // <param name = "heightInPixels" > returns the Y value in pixels</param>
        // public static void TransformDeviceIndependentPixelsToPixels(double widthInDeviceIndependentPixels,
        //                              double heightInDeviceIndependentPixels,
        //                              out int widthInPixels,
        //                              out int heightInPixels)
        // {
        //    IntPtr hDc = GetDC(IntPtr.Zero);
        //    if (hDc != IntPtr.Zero)
        //    {
        //        int dpiX = GetDeviceCaps(hDc, LOGPIXELSX);
        //        int dpiY = GetDeviceCaps(hDc, LOGPIXELSY);
        //        ReleaseDC(IntPtr.Zero, hDc);
        //        widthInPixels = (int)(((double)dpiX / 96) * widthInDeviceIndependentPixels);
        //        heightInPixels = (int)(((double)dpiY / 96) * heightInDeviceIndependentPixels);
        //    }
        //    else
        //    {
        //        // This failure is unlikely. 
        //        // But just in case... we just return the original pixel size. While this may not be the correct size (depending on the actual dpi), 
        //        // it will not crash the program and at least maintains the correct aspect ration
        //        widthInPixels = Convert.ToInt32(widthInDeviceIndependentPixels);
        //        heightInPixels = Convert.ToInt32(heightInDeviceIndependentPixels);
        //        TraceDebug.PrintFailure("In TransformPixelsToDeviceIndependentPixels: Failed to get DC.");
        //    }
        // }
        #endregion
    }
}
