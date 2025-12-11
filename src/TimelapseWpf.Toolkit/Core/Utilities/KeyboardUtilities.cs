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

using System.Windows.Input;

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal class KeyboardUtilities
  {
    internal static bool IsKeyModifyingPopupState( KeyEventArgs e )
    {
      return ( ( ( ( Keyboard.Modifiers & ModifierKeys.Alt ) == ModifierKeys.Alt ) && ( ( e.SystemKey == Key.Down ) || ( e.SystemKey == Key.Up ) ) )
            || ( e.Key == Key.F4 ) );
    }
  }
}
