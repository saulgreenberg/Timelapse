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

namespace TimelapseWpf.Toolkit
{
  public static class WizardCommands
  {
    public static RoutedCommand Cancel { get; } = new();

    public static RoutedCommand Finish { get; } = new();

    public static RoutedCommand Help { get; } = new();

    public static RoutedCommand NextPage { get; } = new();

    public static RoutedCommand PreviousPage { get; } = new();

    public static RoutedCommand SelectPage { get; } = new();
  }
}
