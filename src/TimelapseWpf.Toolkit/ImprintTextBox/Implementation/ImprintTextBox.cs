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
  /// <summary>
  /// A TextBox control that can display an imprint (overlay text/content) when ShowImprint is true.
  /// The imprint is shown only when ShowImprint is true AND the control does not have keyboard focus.
  /// When the imprint is displayed, clicking on it or navigating to it will give the control keyboard focus.
  /// </summary>

  // These properties change the behavior of the imprint textbox
  // 1. AlwaysInvokeTextChanged Property
  //- Forces TextChanged to fire even when text value doesn't change
  //  - Uses temporary empty string trick
  //  - Includes cursor position preservation in SetText() method
  //2. ClearOnFirstKeystrokeWhenShowingImprint Property (Main Feature)
  //- Keeps imprint visible when control has focus(instead of hiding it)
  //- On first keystroke: Selects all text, hides imprint, lets typed character replace selection
  //  - On Delete/Backspace: Clears text and hides imprint
  //  - Natural typing experience - TextChanged fires with new values
  public class ImprintTextBox : TextBox
  {
    #region Properties
    public TextBox MainDisplayField => this;

    #region ShowImprint

    public static readonly DependencyProperty ShowImprintProperty = DependencyProperty.Register(
      nameof(ShowImprint),
      typeof(bool),
      typeof(ImprintTextBox),
      new UIPropertyMetadata(false, OnShowImprintChanged));

    /// <summary>
    /// Gets or sets whether the imprint should be displayed.
    /// When true, the imprint is shown as an overlay on the textbox (only when the control lacks keyboard focus).
    /// When false, the imprint is hidden.
    /// Clicking on the visible imprint will give the control keyboard focus and hide the imprint.
    /// </summary>
    public bool ShowImprint
    {
      get => (bool)GetValue(ShowImprintProperty);
      set => SetValue(ShowImprintProperty, value);
    }

    private static void OnShowImprintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if (d is ImprintTextBox control)
      {
        control.UpdateImprintVisibility();
      }
    }

    #endregion //ShowImprint

    #region Imprint

    public static readonly DependencyProperty ImprintProperty = DependencyProperty.Register(
      nameof(Imprint),
      typeof(object),
      typeof(ImprintTextBox),
      new UIPropertyMetadata(null));

    /// <summary>
    /// Gets or sets the imprint content to display.
    /// Can be a string or any object (will be rendered using ImprintTemplate if provided).
    /// </summary>
    public object Imprint
    {
      get => GetValue(ImprintProperty);
      set => SetValue(ImprintProperty, value);
    }

    #endregion //Imprint

    #region ImprintTemplate

    public static readonly DependencyProperty ImprintTemplateProperty = DependencyProperty.Register(
      nameof(ImprintTemplate),
      typeof(DataTemplate),
      typeof(ImprintTextBox),
      new UIPropertyMetadata(null));

    /// <summary>
    /// Gets or sets the template used to display the imprint.
    /// If not set, a default template will be used.
    /// </summary>
    public DataTemplate ImprintTemplate
    {
      get => (DataTemplate)GetValue(ImprintTemplateProperty);
      set => SetValue(ImprintTemplateProperty, value);
    }

    #endregion //ImprintTemplate

    #region AlwaysInvokeTextChanged

    public static readonly DependencyProperty AlwaysInvokeTextChangedProperty = DependencyProperty.Register(
      nameof(AlwaysInvokeTextChanged),
      typeof(bool),
      typeof(ImprintTextBox),
      new UIPropertyMetadata(false));

    /// <summary>
    /// Gets or sets whether the TextChanged event should always be invoked when SetText is called,
    /// even if the new text value is the same as the current text value.
    /// When true, SetText will temporarily change the text to empty then back to the desired value
    /// to force the TextChanged event to fire.
    /// Default is false.
    /// </summary>
    public bool AlwaysInvokeTextChanged
    {
      get => (bool)GetValue(AlwaysInvokeTextChangedProperty);
      set => SetValue(AlwaysInvokeTextChangedProperty, value);
    }

    #endregion //AlwaysInvokeTextChanged

    #region ClearOnFirstKeystrokeWhenShowingImprint

    public static readonly DependencyProperty ClearOnFirstKeystrokeWhenShowingImprintProperty = DependencyProperty.Register(
      nameof(ClearOnFirstKeystrokeWhenShowingImprint),
      typeof(bool),
      typeof(ImprintTextBox),
      new UIPropertyMetadata(false));

    /// <summary>
    /// Gets or sets whether the imprint should remain visible when focused and be cleared on first keystroke.
    /// When true:
    /// - Imprint stays visible when control has focus (appears as selected/highlighted)
    /// - On first keystroke (or Delete/Backspace), text is cleared, imprint hides, and typed character is inserted
    /// - TextChanged fires naturally with the new text
    /// Default is false.
    /// </summary>
    public bool ClearOnFirstKeystrokeWhenShowingImprint
    {
      get => (bool)GetValue(ClearOnFirstKeystrokeWhenShowingImprintProperty);
      set => SetValue(ClearOnFirstKeystrokeWhenShowingImprintProperty, value);
    }

    #endregion //ClearOnFirstKeystrokeWhenShowingImprint

    #endregion //Properties

    #region Public Methods

    /// <summary>
    /// Sets the text value, optionally forcing the TextChanged event to fire even if the value hasn't changed.
    /// If AlwaysInvokeTextChanged is true and the new value equals the current Text value,
    /// the text will be temporarily set to empty string then to the desired value,
    /// guaranteeing that TextChanged fires.
    /// Preserves cursor position after the text change.
    /// </summary>
    /// <param name="value">The text value to set</param>
    public void SetText(string value)
    {
      // Save cursor position before any text changes
      int savedCaretIndex = CaretIndex;

      // If AlwaysInvokeTextChanged is true and the text isn't actually changing,
      // force TextChanged to fire by temporarily setting to empty
      if (AlwaysInvokeTextChanged && Text == value)
      {
        Text = string.Empty;
      }

      // Set the desired value (TextChanged will fire if value is different from current)
      Text = value;

      // Restore cursor position after text changes
      // Ensure we don't set cursor beyond the new text length
      CaretIndex = System.Math.Min(savedCaretIndex, (int)value?.Length);
    }

    #endregion //Public Methods

    #region Constructors

    static ImprintTextBox()
    {
      DefaultStyleKeyProperty.OverrideMetadata(
        typeof(ImprintTextBox),
        new FrameworkPropertyMetadata(typeof(ImprintTextBox)));
    }

    public ImprintTextBox()
    {
      this.GotFocus += ImprintTextBox_GotFocus;
      this.LostFocus += ImprintTextBox_LostFocus;
      this.GotKeyboardFocus += ImprintTextBox_GotKeyboardFocus;
      this.LostKeyboardFocus += ImprintTextBox_LostKeyboardFocus;

      // Block text input when imprint is visible
      this.PreviewTextInput += ImprintTextBox_PreviewTextInput;
      this.PreviewKeyDown += ImprintTextBox_PreviewKeyDown;

      // Handle imprint-to-text transition when AlwaysInvokeTextChanged is enabled
      this.TextChanged += ImprintTextBox_TextChanged_Internal;
    }

    #endregion //Constructors

    #region Fields

    private System.Windows.FrameworkElement _imprintHost;
    private bool _hasFocus;
    private bool _wasShowingImprint;
    private bool _isHandlingImprintTransition;
    private object _originalImprint;  // Store original imprint content when showing cursor
    private System.Windows.Threading.DispatcherTimer _cursorBlinkTimer;  // For cursor blinking animation
    private bool _cursorVisible = true;  // Track cursor blink state

    #endregion //Fields

    #region Overrides

    public override void OnApplyTemplate()
    {
      base.OnApplyTemplate();

      // Find the PART_ImprintHost element in the template
      _imprintHost = this.Template?.FindName("PART_ImprintHost", this) as System.Windows.FrameworkElement;
      UpdateImprintVisibility();
    }

    #endregion //Overrides

    #region Event Handlers

    private void ImprintTextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
      _hasFocus = true;
      UpdateImprintVisibility();
    }

    private void ImprintTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
      _hasFocus = false;
      UpdateImprintVisibility();
    }

    private void ImprintTextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
      _hasFocus = true;
      UpdateImprintVisibility();

      // Start blinking cursor when focused with imprint showing
      if (ShowImprint && ClearOnFirstKeystrokeWhenShowingImprint)
      {
        StartBlinkingCursor();
      }
    }

    private void ImprintTextBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
      _hasFocus = false;
      StopBlinkingCursor();
      UpdateImprintVisibility();
    }

    private void ImprintTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
      // New behavior: On first keystroke while imprint showing, select all and allow replacement
      // Check ShowImprint property directly (more reliable than checking visibility)
      if (ShowImprint && ClearOnFirstKeystrokeWhenShowingImprint && _hasFocus)
      {
        // Stop cursor blinking and restore original imprint
        StopBlinkingCursor();

        // Select all text so the typed character will replace it
        SelectAll();
        // Hide the imprint
        ShowImprint = false;
        // Allow the keystroke to proceed - it will replace the selected text
        // Don't set e.Handled, let it process normally
        return;
      }

      // Original behavior: Block text input when imprint is visible
      if (_imprintHost is { Visibility: System.Windows.Visibility.Visible })
      {
        e.Handled = true;
      }
    }

    private void ImprintTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
      // New behavior: When ClearOnFirstKeystrokeWhenShowingImprint is enabled, don't block keys
      // Let PreviewTextInput handle text input, but handle Delete/Backspace here
      if (ShowImprint && ClearOnFirstKeystrokeWhenShowingImprint && _hasFocus)
      {
        if (e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back)
        {
          // Stop cursor blinking and restore original imprint
          StopBlinkingCursor();

          // Clear text and hide imprint
          Text = string.Empty;
          ShowImprint = false;
          e.Handled = true; // Prevent default delete/backspace behavior
          return;
        }
        else
        {
          // For all other keys (including text input keys), don't block them
          // Let them proceed to PreviewTextInput where they'll be handled
          return; // Don't block, let the key proceed
        }
      }

      // Original behavior: Block key input when imprint is visible (for keys like Delete, Backspace, etc.)
      if (_imprintHost is { Visibility: System.Windows.Visibility.Visible })
      {
        // Allow navigation keys but block input keys
        if (e.Key != System.Windows.Input.Key.Tab &&
            e.Key != System.Windows.Input.Key.Left &&
            e.Key != System.Windows.Input.Key.Right &&
            e.Key != System.Windows.Input.Key.Up &&
            e.Key != System.Windows.Input.Key.Down &&
            e.Key != System.Windows.Input.Key.Home &&
            e.Key != System.Windows.Input.Key.End)
        {
          e.Handled = true;
        }
      }
    }

    private void ImprintTextBox_TextChanged_Internal(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
      // Prevent re-entry while handling the imprint transition
      // SetText triggers TextChanged multiple times (empty -> value), so we need to guard against recursion
      if (_isHandlingImprintTransition)
      {
        return;
      }

      // Detect transition from imprint state to user-entered text
      // This ensures TextChanged fires properly even if the new text matches a previous value
      if (_wasShowingImprint && !string.IsNullOrEmpty(Text) && AlwaysInvokeTextChanged)
      {
        // Set re-entry guard to prevent infinite loop during SetText calls
        _isHandlingImprintTransition = true;
        _wasShowingImprint = false;

        // Force TextChanged to fire again by using SetText
        // This ensures any external handlers process the transition properly
        // Note: SetText preserves cursor position internally
        SetText(Text);

        // Clear re-entry guard after SetText completes
        _isHandlingImprintTransition = false;
      }
      else
      {
        // Only update the tracked state if we didn't trigger the transition
        // Track whether imprint is currently showing for the next TextChanged event
        _wasShowingImprint = ShowImprint;
      }
    }

    private void UpdateImprintVisibility()
    {
      if (_imprintHost == null)
      {
        return;
      }

      // Show imprint when ShowImprint is true AND either:
      // 1. Control doesn't have focus (original behavior), OR
      // 2. Control has focus AND ClearOnFirstKeystrokeWhenShowingImprint is enabled (new behavior - keeps imprint visible when focused)
      bool shouldShowImprint = ShowImprint && (!_hasFocus || ClearOnFirstKeystrokeWhenShowingImprint);

      _imprintHost.Visibility = shouldShowImprint ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

      // Note: We block text input via PreviewTextInput/PreviewKeyDown handlers instead of using IsReadOnly
      // to avoid focus loss issues when changing IsReadOnly
    }

    private void StartBlinkingCursor()
    {
      // Save the original imprint so we can restore it later
      _originalImprint = Imprint;

      // Create and start the cursor blink timer
      if (_cursorBlinkTimer == null)
      {
        _cursorBlinkTimer = new()
        {
          Interval = System.TimeSpan.FromMilliseconds(500)
        };
        _cursorBlinkTimer.Tick += CursorBlink_Tick;
      }

      // Start with cursor visible
      _cursorVisible = true;
      Imprint = "|";
      _cursorBlinkTimer.Start();
    }

    private void StopBlinkingCursor()
    {
      // Stop the timer if it exists
      if (_cursorBlinkTimer != null)
      {
        _cursorBlinkTimer.Stop();
      }

      // Restore the original imprint
      if (_originalImprint != null)
      {
        Imprint = _originalImprint;
        _originalImprint = null;
      }
    }

    private void CursorBlink_Tick(object sender, System.EventArgs e)
    {
      // Toggle cursor visibility
      _cursorVisible = !_cursorVisible;
      Imprint = _cursorVisible ? "|" : " ";
    }

    #endregion //Event Handlers
  }
}
