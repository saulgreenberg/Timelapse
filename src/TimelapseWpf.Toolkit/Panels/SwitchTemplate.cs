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

using System.Threading;
using System.Windows;
using System.Windows.Threading;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Panels
{
  public static class SwitchTemplate
  {
    #region ID Attached Property

    public static readonly DependencyProperty IDProperty =
      DependencyProperty.RegisterAttached("ID", typeof(string), typeof(SwitchTemplate),
        new FrameworkPropertyMetadata(null,
          SwitchTemplate.OnIDChanged));

    public static string GetID(DependencyObject d)
    {
      return (string)d.GetValue(SwitchTemplate.IDProperty);
    }

    public static void SetID(DependencyObject d, string value)
    {
      d.SetValue(SwitchTemplate.IDProperty, value);
    }

    private static void OnIDChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if ((e.NewValue == null) || !(d is UIElement))
        return;

      SwitchPresenter parentPresenter = VisualTreeHelperEx.FindAncestorByType<SwitchPresenter>(d);
      if (parentPresenter != null)
      {
        parentPresenter.RegisterID(e.NewValue as string, d as FrameworkElement);
      }
      else
      {
        d.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            (ThreadStart)delegate
            {
              parentPresenter = VisualTreeHelperEx.FindAncestorByType<SwitchPresenter>(d);
              if (parentPresenter != null)
              {
                parentPresenter.RegisterID(e.NewValue as string, d as FrameworkElement);
              }
            });
      }
    }

    #endregion
  }
}
