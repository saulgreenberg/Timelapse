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
using System.Windows.Input;

namespace TimelapseWpf.Toolkit
{
  /// <summary>
  /// TimePicker with enhanced watermark support including ForceWatermark and HideOnFocus properties.
  /// Inherits Watermark and WatermarkTemplate properties from TimePicker base class.
  /// </summary>
  public class WatermarkTimePicker : TimePicker
  {
    #region Properties

    #region ForceWatermark

    /// <summary>
    /// Gets or sets a value indicating whether to force the watermark to display even when Value is not null.
    /// When true, watermark is shown regardless of the Value property (subject to HideOnFocus behavior).
    /// When false (default), watermark is only shown when Value is null.
    /// </summary>
    public static readonly DependencyProperty ForceWatermarkProperty = DependencyProperty.Register( nameof(ForceWatermark), typeof( bool ), typeof( WatermarkTimePicker ), new UIPropertyMetadata( false, OnWatermarkPropertyChanged ) );
    public bool ForceWatermark
    {
      get => ( bool )GetValue( ForceWatermarkProperty );
      set => SetValue( ForceWatermarkProperty, value );
    }

    #endregion //ForceWatermark

    #region HideOnFocus

    /// <summary>
    /// Gets or sets a value indicating whether the watermark should hide when the control receives focus.
    /// When true (default), watermark hides when control is focused (standard behavior).
    /// When false, watermark remains visible even when control is focused.
    /// </summary>
    public static readonly DependencyProperty HideOnFocusProperty = DependencyProperty.Register( nameof(HideOnFocus), typeof( bool ), typeof( WatermarkTimePicker ), new UIPropertyMetadata( true, OnWatermarkPropertyChanged ) );
    public bool HideOnFocus
    {
      get => ( bool )GetValue( HideOnFocusProperty );
      set => SetValue( HideOnFocusProperty, value );
    }

    #endregion //HideOnFocus

    #region IsWatermarkVisible

    /// <summary>
    /// Read-only DependencyPropertyKey for IsWatermarkVisible property.
    /// </summary>
    private static readonly DependencyPropertyKey IsWatermarkVisiblePropertyKey = DependencyProperty.RegisterReadOnly(
      nameof(IsWatermarkVisible),
      typeof( bool ),
      typeof( WatermarkTimePicker ),
      new( false ) );

    /// <summary>
    /// Gets a value indicating whether the watermark is currently visible.
    /// This property is automatically updated based on Value, ForceWatermark, HideOnFocus, IsOpen, and focus state.
    /// </summary>
    public static readonly DependencyProperty IsWatermarkVisibleProperty = IsWatermarkVisiblePropertyKey.DependencyProperty;

    public bool IsWatermarkVisible
    {
      get => ( bool )GetValue( IsWatermarkVisibleProperty );
      private set => SetValue( IsWatermarkVisiblePropertyKey, value );
    }

    #endregion //IsWatermarkVisible

    #endregion //Properties

    #region Constructors

    /// <summary>
    /// Static constructor to override the default style key for WatermarkTimePicker.
    /// </summary>
    static WatermarkTimePicker()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( WatermarkTimePicker ), new FrameworkPropertyMetadata( typeof( WatermarkTimePicker ) ) );

      // Add property changed callback for Value property (inherited from base)
      ValueProperty.OverrideMetadata( typeof( WatermarkTimePicker ), new FrameworkPropertyMetadata( null, OnWatermarkPropertyChanged ) );

      // Add property changed callback for IsOpen property (inherited from DateTimePickerBase)
      IsOpenProperty.OverrideMetadata( typeof( WatermarkTimePicker ), new FrameworkPropertyMetadata( false, OnWatermarkPropertyChanged ) );
    }

    /// <summary>
    /// Instance constructor
    /// </summary>
    public WatermarkTimePicker()
    {
      // Subscribe to focus events to update IsWatermarkVisible
      this.IsKeyboardFocusWithinChanged += OnIsKeyboardFocusWithinChanged;

      // Initialize IsWatermarkVisible based on initial state
      UpdateIsWatermarkVisible();
    }

    #endregion //Constructors

    #region Base Class Overrides

    /// <summary>
    /// Override to ensure the internal TextBox receives keyboard focus when control gets focus.
    /// This allows keyboard navigation (left/right arrows) to work immediately.
    /// </summary>
    protected override void OnGotFocus( RoutedEventArgs e )
    {
      base.OnGotFocus( e );
      FocusTextBox();
    }

    #endregion //Base Class Overrides

    #region Methods

    /// <summary>
    /// Property changed callback for properties that affect watermark visibility.
    /// </summary>
    private static void OnWatermarkPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      if( d is WatermarkTimePicker picker )
      {
        picker.UpdateIsWatermarkVisible();
      }
    }

    /// <summary>
    /// Event handler for IsKeyboardFocusWithinChanged - updates watermark visibility when focus changes.
    /// </summary>
    private void OnIsKeyboardFocusWithinChanged( object sender, DependencyPropertyChangedEventArgs e )
    {
      UpdateIsWatermarkVisible();
      // Note: TextBox focusing is handled by OnGotFocus to avoid performance issues during programmatic updates
    }

    /// <summary>
    /// Focuses the internal PART_TextBox to enable keyboard navigation.
    /// </summary>
    private void FocusTextBox()
    {
      // Delay focusing the TextBox until the next dispatcher cycle to ensure template is applied
      this.Dispatcher.BeginInvoke( new System.Action( () =>
      {
        // Only focus if we still have focus and TextBox doesn't already have it
        if( this.IsKeyboardFocusWithin )
        {
          if( this.Template?.FindName( "PART_TextBox", this ) is System.Windows.Controls.TextBox { IsKeyboardFocused: false } textBox )
          {
            textBox.Focus();
            Keyboard.Focus( textBox );
          }
        }
      } ), System.Windows.Threading.DispatcherPriority.Input );
    }

    /// <summary>
    /// Updates the IsWatermarkVisible property based on current state.
    /// Logic matches the XAML triggers:
    /// Watermark is visible when (Value is null OR ForceWatermark is true)
    /// AND (HideOnFocus is false OR control does not have keyboard focus)
    /// AND (dropdown is not open).
    /// </summary>
    private void UpdateIsWatermarkVisible()
    {
      // Check if watermark should be shown based on Value or ForceWatermark
      bool shouldShowWatermark = !this.Value.HasValue || this.ForceWatermark;

      // Check focus condition
      bool focusAllowsWatermark = !this.HideOnFocus || !this.IsKeyboardFocusWithin;

      // Check if dropdown is closed (watermark should hide when dropdown is open - Option A)
      bool dropdownAllowsWatermark = !this.IsOpen;

      // Update the read-only property
      this.IsWatermarkVisible = shouldShowWatermark && focusAllowsWatermark && dropdownAllowsWatermark;
    }

    #endregion //Methods
  }
}
