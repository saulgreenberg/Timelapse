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
using System.Windows.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TimelapseWpf.Toolkit.Core.Converters
{
    public class CenterTitleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Parameters: DesiredSize, WindowWidth, HeaderColumns
            double titleTextWidth = ((Size)values[0]).Width;
            double windowWidth = (double)values[1];

            ColumnDefinitionCollection headerColumns = (ColumnDefinitionCollection)values[2];
            double titleColWidth = headerColumns[2].ActualWidth;
            double buttonsColWidth = headerColumns[3].ActualWidth;


            // Result (1) Title is Centered across all HeaderColumns
            if ((titleTextWidth + buttonsColWidth * 2) < windowWidth)
                return 1;

            // Result (2) Title is Centered in HeaderColumns[2]
            if (titleTextWidth < titleColWidth)
                return 2;

            // Result (3) Title is Left-Aligned in HeaderColumns[2]
            return 3;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
