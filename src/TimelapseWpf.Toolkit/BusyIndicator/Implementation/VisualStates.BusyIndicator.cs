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

namespace TimelapseWpf.Toolkit
{
  internal static partial class VisualStates
  {
    /// <summary>
    /// Busyness group name.
    /// </summary>
    public const string GroupBusyStatus = "BusyStatusStates";

    /// <summary>
    /// Busy state for BusyIndicator.
    /// </summary>
    public const string StateBusy = "Busy";

    /// <summary>
    /// Idle state for BusyIndicator.
    /// </summary>
    public const string StateIdle = "Idle";

    /// <summary>
    /// BusyDisplay group.
    /// </summary>
    public const string GroupVisibility = "VisibilityStates";

    /// <summary>
    /// Visible state name for BusyIndicator.
    /// </summary>
    public const string StateVisible = "Visible";

    /// <summary>
    /// Hidden state name for BusyIndicator.
    /// </summary>
    public const string StateHidden = "Hidden";
  }
}
