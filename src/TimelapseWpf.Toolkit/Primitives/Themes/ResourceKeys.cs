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

namespace TimelapseWpf.Toolkit.Themes
{
  public static class ResourceKeys
  {
    #region Brush Keys

    public static readonly ComponentResourceKey ControlNormalBackgroundKey = new( typeof( ResourceKeys ), "ControlNormalBackgroundKey" );
    public static readonly ComponentResourceKey ControlDisabledBackgroundKey = new( typeof( ResourceKeys ), "ControlDisabledBackgroundKey" );
    public static readonly ComponentResourceKey ControlNormalBorderKey = new( typeof( ResourceKeys ), "ControlNormalBorderKey" );
    public static readonly ComponentResourceKey ControlMouseOverBorderKey = new( typeof( ResourceKeys ), "ControlMouseOverBorderKey" );
    public static readonly ComponentResourceKey ControlSelectedBorderKey = new(typeof(ResourceKeys), "ControlSelectedBorderKey");
    public static readonly ComponentResourceKey ControlFocusedBorderKey = new( typeof( ResourceKeys ), "ControlFocusedBorderKey" );

    public static readonly ComponentResourceKey ButtonNormalOuterBorderKey = new( typeof( ResourceKeys ), "ButtonNormalOuterBorderKey" );
    public static readonly ComponentResourceKey ButtonNormalInnerBorderKey = new( typeof( ResourceKeys ), "ButtonNormalInnerBorderKey" );
    public static readonly ComponentResourceKey ButtonNormalBackgroundKey = new( typeof( ResourceKeys ), "ButtonNormalBackgroundKey" );

    public static readonly ComponentResourceKey ButtonMouseOverBackgroundKey = new( typeof( ResourceKeys ), "ButtonMouseOverBackgroundKey" );
    public static readonly ComponentResourceKey ButtonMouseOverOuterBorderKey = new( typeof( ResourceKeys ), "ButtonMouseOverOuterBorderKey" );
    public static readonly ComponentResourceKey ButtonMouseOverInnerBorderKey = new( typeof( ResourceKeys ), "ButtonMouseOverInnerBorderKey" );

    public static readonly ComponentResourceKey ButtonPressedOuterBorderKey = new( typeof( ResourceKeys ), "ButtonPressedOuterBorderKey" );
    public static readonly ComponentResourceKey ButtonPressedInnerBorderKey = new( typeof( ResourceKeys ), "ButtonPressedInnerBorderKey" );
    public static readonly ComponentResourceKey ButtonPressedBackgroundKey = new( typeof( ResourceKeys ), "ButtonPressedBackgroundKey" );

    public static readonly ComponentResourceKey ButtonFocusedOuterBorderKey = new( typeof( ResourceKeys ), "ButtonFocusedOuterBorderKey" );
    public static readonly ComponentResourceKey ButtonFocusedInnerBorderKey = new( typeof( ResourceKeys ), "ButtonFocusedInnerBorderKey" );
    public static readonly ComponentResourceKey ButtonFocusedBackgroundKey = new( typeof( ResourceKeys ), "ButtonFocusedBackgroundKey" );

    public static readonly ComponentResourceKey ButtonDisabledOuterBorderKey = new( typeof( ResourceKeys ), "ButtonDisabledOuterBorderKey" );
    public static readonly ComponentResourceKey ButtonInnerBorderDisabledKey = new( typeof( ResourceKeys ), "ButtonInnerBorderDisabledKey" );

    #endregion //Brush Keys

    public static readonly ComponentResourceKey GlyphNormalForegroundKey = new( typeof( ResourceKeys ), "GlyphNormalForegroundKey" );
    public static readonly ComponentResourceKey GlyphDisabledForegroundKey = new( typeof( ResourceKeys ), "GlyphDisabledForegroundKey" );

    public static readonly ComponentResourceKey SpinButtonCornerRadiusKey = new( typeof( ResourceKeys ), "SpinButtonCornerRadiusKey" );

    public static readonly ComponentResourceKey SpinnerButtonStyleKey = new( typeof( ResourceKeys ), "SpinnerButtonStyleKey" );

  }
}
