/*************************************************************************************
   
   Toolkit for WPF
   Copyright (C) 2007-2019 Xceed Software Inc.
   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/license/ms-pl-html

   Fork origin: https://github.com/dotnetprojects/WpfExtendedToolkit
   - based on: https://github.com/xceedsoftware/wpftoolkit, Version 3
   This fork: modified for use in Timelapse project
    by Saul Greenberg, 2025 onwards

  ***********************************************************************************/

using System;
using System.Windows;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Panels
{
  internal static class ScrollHelper
  {
    public static bool ScrollLeastAmount( Rect physViewRect, Rect itemRect, out Vector newPhysOffset )
    {
      bool scrollNeeded = false;

      newPhysOffset = new();

      if( physViewRect.Contains( itemRect ) == false )
      {
        // Check if child is inside the view horizontially.
        if( itemRect.Left > physViewRect.Left && itemRect.Right < physViewRect.Right ||
            DoubleHelper.AreVirtuallyEqual( itemRect.Left, physViewRect.Left ) )
        {
          newPhysOffset.X = itemRect.Left;
        }
        // Child is to the left of the view or is it bigger than the view
        else if( itemRect.Left < physViewRect.Left || itemRect.Width > physViewRect.Width )
        {
          newPhysOffset.X = itemRect.Left;
        }
        // Child is to the right of the view
        else
        {
          newPhysOffset.X = Math.Max( 0, physViewRect.Left + ( itemRect.Right - physViewRect.Right ) );
        }

        // Check if child is inside the view vertically.
        if( itemRect.Top > physViewRect.Top && itemRect.Bottom < physViewRect.Bottom ||
            DoubleHelper.AreVirtuallyEqual( itemRect.Top, physViewRect.Top ) )
        {
          newPhysOffset.Y = itemRect.Top;
        }
        // Child is the above the view or is it bigger than the view
        else if( itemRect.Top < physViewRect.Top || itemRect.Height > physViewRect.Height )
        {
          newPhysOffset.Y = itemRect.Top;
        }
        // Child is below the view
        else
        {
          newPhysOffset.Y = Math.Max( 0, physViewRect.Top + ( itemRect.Bottom - physViewRect.Bottom ) );
        }

        scrollNeeded = true;
      }

      return scrollNeeded;
    }
  }
}
