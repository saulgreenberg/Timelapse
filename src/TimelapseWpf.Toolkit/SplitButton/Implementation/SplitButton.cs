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

using System.Windows;
using System.Windows.Controls;

namespace TimelapseWpf.Toolkit
{
  [TemplatePart( Name = PART_ActionButton, Type = typeof( Button ) )]
  public class SplitButton : DropDownButton
  {
    private const string PART_ActionButton = "PART_ActionButton";

    #region Constructors

    static SplitButton()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( SplitButton ), new FrameworkPropertyMetadata( typeof( SplitButton ) ) );
    }

    #endregion //Constructors

    #region Base Class Overrides

    public override void OnApplyTemplate()
    {
      base.OnApplyTemplate();
      Button = GetTemplateChild( PART_ActionButton ) as Button;
    }


  #endregion //Base Class Overrides
  }
}
