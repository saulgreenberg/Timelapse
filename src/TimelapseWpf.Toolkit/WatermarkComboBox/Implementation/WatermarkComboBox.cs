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
using System.Windows.Controls;
using System.Windows.Input;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit
{
  /// <summary>
  /// Enhanced ComboBox with watermark functionality and selection confirmation events.
  ///
  /// USAGE INSTRUCTIONS:
  /// ===================
  /// This control extends the standard ComboBox with a SelectionConfirmed event that fires
  /// for ALL user selection actions, making it a superset of SelectionChanged.
  ///
  /// SelectionConfirmed fires when:
  /// - User changes selection with arrow keys (immediate feedback)
  /// - User clicks an item in the dropdown
  /// - User presses Enter/Return/Tab
  /// - User re-selects the same item (SelectionChanged would NOT fire for this)
  ///
  /// KEY BENEFIT: You can subscribe ONLY to SelectionConfirmed and handle all selection
  /// scenarios with a single event handler, including cases where the value doesn't change.
  ///
  /// IMPORTANT: To prevent the event from firing during programmatic updates (data loading),
  /// set SuppressSelectionConfirmed = true before updating, then reset to false after.
  ///
  /// Example:
  ///   comboBox.SuppressSelectionConfirmed = true;
  ///   comboBox.SelectedIndex = 2;
  ///   comboBox.SuppressSelectionConfirmed = false;
  ///
  /// Subscribe to the event:
  ///   comboBox.SelectionConfirmed += MyHandler;
  ///
  ///   private void MyHandler(object sender, SelectionConfirmedEventArgs e)
  ///   {
  ///       // e.SelectedItem contains the current selection
  ///       // e.Source indicates how confirmation occurred:
  ///       //   "SelectionChanged" - arrow keys or programmatic change
  ///       //   "KeyPress" - Enter/Return/Tab
  ///       //   "DropDownClosed" - clicked item in dropdown
  ///   }
  /// </summary>
  public class WatermarkComboBox : ComboBox
  {
    #region Properties

    // The main display field is the control that actually holds and presents the content.
    private TextBlock _mainDisplayField;
    public TextBlock MainDisplayField
    {
      get
      {
        if (_mainDisplayField == null)
        {
          Border contentHost = (Border)this.Template.FindName("PART_Border", this);
          if (contentHost != null)
          {
            _mainDisplayField = TreeHelper.FindChild<TextBlock>(contentHost);
          }
        }

        return _mainDisplayField;
      }
    }


    #region Watermark

    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(nameof(Watermark), typeof(object), typeof(WatermarkComboBox), new UIPropertyMetadata(null));
    public object Watermark
    {
      get => GetValue(WatermarkProperty);
      set => SetValue(WatermarkProperty, value);
    }

    #endregion //Watermark

    #region WatermarkTemplate

    public static readonly DependencyProperty WatermarkTemplateProperty = DependencyProperty.Register(nameof(WatermarkTemplate), typeof(DataTemplate), typeof(WatermarkComboBox), new UIPropertyMetadata(null));
    public DataTemplate WatermarkTemplate
    {
      get => (DataTemplate)GetValue(WatermarkTemplateProperty);
      set => SetValue(WatermarkTemplateProperty, value);
    }

    #endregion //WatermarkTemplate

    #region ForceWatermark

    public static readonly DependencyProperty ForceWatermarkProperty = DependencyProperty.Register(nameof(ForceWatermark), typeof(bool), typeof(WatermarkComboBox), new UIPropertyMetadata(false));
    public bool ForceWatermark
    {
      get => (bool)GetValue(ForceWatermarkProperty);
      set => SetValue(ForceWatermarkProperty, value);
    }

    #endregion //ForceWatermark

    #region HideOnFocus

    public static readonly DependencyProperty HideOnFocusProperty = DependencyProperty.Register(nameof(HideOnFocus), typeof(bool), typeof(WatermarkComboBox), new UIPropertyMetadata(true));
    public bool HideOnFocus
    {
      get => (bool)GetValue(HideOnFocusProperty);
      set => SetValue(HideOnFocusProperty, value);
    }

    #endregion //HideOnFocus

    #region SuppressSelectionConfirmed

    /// <summary>
    /// Set to true to prevent SelectionConfirmed event from firing.
    /// Use this during programmatic updates to prevent unwanted event triggers.
    /// Equivalent to IsProgrammaticControlUpdate pattern used elsewhere in Timelapse.
    /// </summary>
    public bool SuppressSelectionConfirmed { get; set; } = false;

    #endregion //SuppressSelectionConfirmed

    #endregion //Properties

    #region SelectionConfirmed Event

    /// <summary>
    /// Event that fires whenever the user confirms a selection, even if the value hasn't changed.
    /// This is different from SelectionChanged which only fires when the value actually changes.
    /// </summary>
    public event EventHandler<SelectionConfirmedEventArgs> SelectionConfirmed;

    /// <summary>
    /// Raises the SelectionConfirmed event if not suppressed and there's a valid selection.
    /// </summary>
    /// <param name="source">Description of how the confirmation occurred (for debugging)</param>
    private void RaiseSelectionConfirmed(string source)
    {
      // Don't fire if suppressed (programmatic update) or no item selected
      if (SuppressSelectionConfirmed || SelectedItem == null)
      {
        return;
      }

      SelectionConfirmed?.Invoke(this, new()
      {
        SelectedItem = SelectedItem,
        Source = source
      });
    }

    #endregion //SelectionConfirmed Event

    #region Constructors

    static WatermarkComboBox()
    {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(WatermarkComboBox), new FrameworkPropertyMetadata(typeof(WatermarkComboBox)));
    }

    #endregion //Constructors

    #region Event Overrides for Selection Confirmation

    /// <summary>
    /// Override PreviewKeyDown to detect when user confirms selection with Enter/Return/Tab keys.
    /// IMPORTANT: Calls base.OnPreviewKeyDown() FIRST to ensure all existing handlers work correctly.
    /// This includes:
    /// - DataEntryChoice.ContentCtl_PreviewKeyDown (handles text search + Enter, blocks Left/Right)
    /// - TimelapseWindow.ContentCtl_PreviewKeyDown (focus management, moves focus to MarkableCanvas)
    /// - MetadataDataEntryPanel handlers
    ///
    /// We fire SelectionConfirmed AFTER these handlers run, and only if event wasn't handled.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
      // CRITICAL: Call base first to allow all attached PreviewKeyDown handlers to execute
      // This maintains compatibility with existing keyboard handling in:
      // - DataEntryChoice (Enter key for text search, Left/Right blocking)
      // - TimelapseWindow (focus management)
      // - MetadataDataEntryPanel (focus navigation)
      base.OnPreviewKeyDown(e);

      // After all other handlers have run, check if user pressed a confirmation key
      // Only fire if:
      // - Event wasn't already handled by another handler
      // - We're not suppressed (programmatic update)
     
      // - User pressed Enter, Return, or Tab
      //if (!e.Handled && !SuppressSelectionConfirmed &&
      //    (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab))
      //{
      //  RaiseSelectionConfirmed("KeyPress");
      //}

      // - User pressed Tab
      if (!e.Handled && !SuppressSelectionConfirmed &&
          e.Key == Key.Tab)
      {
        RaiseSelectionConfirmed("KeyPress");
      }
    }

    /// <summary>
    /// Override DropDownClosed to detect when user selects item by clicking in dropdown
    /// or confirms selection with Enter/Tab while dropdown is open.
    /// </summary>
    protected override void OnDropDownClosed(EventArgs e)
    {
      base.OnDropDownClosed(e);

      // User closed dropdown (by clicking an item or pressing Enter/Tab)
      if (SelectedItem != null)
      {
        RaiseSelectionConfirmed("DropDownClosed");
      }
    }

    /// <summary>
    /// Override OnSelectionChanged to also fire SelectionConfirmed when selection actually changes.
    /// This ensures SelectionConfirmed is a true superset of SelectionChanged:
    /// - Fires when selection changes (arrow keys with dropdown closed)
    /// - Also fires when same item is re-selected (via Enter/Tab/LostFocus with no change)
    ///
    /// Checks IsDropDownOpen to prevent double-firing when clicking dropdown items.
    /// If dropdown is open, OnDropDownClosed or OnPreviewKeyDown will handle the event.
    ///
    /// This allows consumers to subscribe only to SelectionConfirmed and get all selection events.
    /// </summary>
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
      base.OnSelectionChanged(e);

      // Fire SelectionConfirmed when selection actually changes with dropdown CLOSED
      // This captures arrow key navigation when dropdown is closed
      // Skip if dropdown is open - OnDropDownClosed or OnPreviewKeyDown will handle it
      if (e.AddedItems.Count > 0 && !IsDropDownOpen)
      {
        RaiseSelectionConfirmed("SelectionChanged");
      }
    }

    // <summary>
    // Override LostKeyboardFocus to detect when user confirms selection by clicking elsewhere or tabbing away.
    // Only fires if we're not already firing due to dropdown closing (prevents duplicates).
    //
    // NOTE: Other controls (DataEntryInteger, DataEntryDecimal, etc.) also use LostKeyboardFocus
    // for validation. This implementation is careful not to interfere with those.
    // </summary>
    //protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    //{
    //  base.OnLostKeyboardFocus(e);

    //  // Only fire if:
    //  // - We're not already firing due to dropdown closing (prevents duplicate)
    //  // - There's a valid selection
    //  // - We're not suppressed
    //  //
    //  // This captures cases where user tabs away or clicks elsewhere after making a selection
    //  if (!_isDropDownClosing && SelectedItem != null && !SuppressSelectionConfirmed)
    //  {
    //    RaiseSelectionConfirmed("LostFocus");
    //  }
    //}

    #endregion //Event Overrides
  }

  #region SelectionConfirmedEventArgs

  /// <summary>
  /// Event arguments for SelectionConfirmed event.
  /// Provides information about the confirmed selection.
  /// </summary>
  public class SelectionConfirmedEventArgs : EventArgs
  {
    /// <summary>
    /// The item that was confirmed (same as ComboBox.SelectedItem)
    /// </summary>
    public object SelectedItem { get; set; }

    /// <summary>
    /// Description of how the confirmation occurred.
    /// Useful for debugging. Values:
    /// - "KeyPress" - User pressed Enter, Return, or Tab
    /// - "DropDownClosed" - User clicked item in dropdown
    /// - "LostFocus" - User clicked elsewhere or tabbed away
    /// </summary>
    public string Source { get; set; }
  }

  #endregion //SelectionConfirmedEventArgs
}
