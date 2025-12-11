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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit
{
  [TemplatePart( Name = PART_DropDownButton, Type = typeof( ToggleButton ) )]
  [TemplatePart( Name = PART_ContentPresenter, Type = typeof( ContentPresenter ) )]
  [TemplatePart( Name = PART_Popup, Type = typeof( Popup ) )]
  public class DropDownButton : ContentControl, ICommandSource
  {
    private const string PART_DropDownButton = "PART_DropDownButton";
    private const string PART_ContentPresenter = "PART_ContentPresenter";
    private const string PART_Popup = "PART_Popup";

    #region Members 

    private ContentPresenter _contentPresenter;
    private Popup _popup;

    #endregion

    #region Constructors

    static DropDownButton()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( DropDownButton ), new FrameworkPropertyMetadata( typeof( DropDownButton ) ) );

      EventManager.RegisterClassHandler( typeof( DropDownButton ), AccessKeyManager.AccessKeyPressedEvent, new AccessKeyPressedEventHandler( OnAccessKeyPressed ) );
    }

    public DropDownButton()
    {
      Keyboard.AddKeyDownHandler( this, OnKeyDown );
      Mouse.AddPreviewMouseDownOutsideCapturedElementHandler( this, OnMouseDownOutsideCapturedElement );
    }

    #endregion //Constructors

    #region Properties

    private System.Windows.Controls.Primitives.ButtonBase _button;
    protected System.Windows.Controls.Primitives.ButtonBase Button
    {
      get => _button;
      set
      {
        if( _button != null )
          _button.Click -= DropDownButton_Click;

        _button = value;

        if( _button != null )
          _button.Click += DropDownButton_Click;
      }
    }

    #region DropDownContent

    public static readonly DependencyProperty DropDownContentProperty = DependencyProperty.Register( nameof(DropDownContent), typeof( object ), typeof( DropDownButton ), new UIPropertyMetadata( null, OnDropDownContentChanged ) );
    public object DropDownContent
    {
      get => GetValue( DropDownContentProperty );
      set => SetValue( DropDownContentProperty, value );
    }

    private static void OnDropDownContentChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DropDownButton dropDownButton )
        dropDownButton.OnDropDownContentChanged( e.OldValue, e.NewValue );
    }

    protected virtual void OnDropDownContentChanged( object oldValue, object newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //DropDownContent

    #region DropDownContentBackground

    public static readonly DependencyProperty DropDownContentBackgroundProperty = DependencyProperty.Register( nameof(DropDownContentBackground), typeof( Brush ), typeof( DropDownButton ), new UIPropertyMetadata( null ) );
    public Brush DropDownContentBackground
    {
      get => ( Brush )GetValue( DropDownContentBackgroundProperty );
      set => SetValue( DropDownContentBackgroundProperty, value );
    }

    #endregion //DropDownContentBackground

    #region DropDownPosition

    public static readonly DependencyProperty DropDownPositionProperty = DependencyProperty.Register( nameof(DropDownPosition), typeof( PlacementMode )
      , typeof( DropDownButton ), new UIPropertyMetadata( PlacementMode.Bottom ) );
    public PlacementMode DropDownPosition
    {
      get => (PlacementMode)GetValue( DropDownPositionProperty );
      set => SetValue( DropDownPositionProperty, value );
    }

    #endregion

    #region IsDefault

    public static readonly DependencyProperty IsDefaultProperty = DependencyProperty.Register( nameof(IsDefault), typeof( bool ), typeof( DropDownButton ), new UIPropertyMetadata( false, OnIsDefaultChanged ) );
    public bool IsDefault
    {
      get => ( bool )GetValue( IsDefaultProperty );
      set => SetValue( IsDefaultProperty, value );
    }

    private static void OnIsDefaultChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DropDownButton dropDownButton )
        dropDownButton.OnIsDefaultChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnIsDefaultChanged( bool oldValue, bool newValue )
    {
      if( newValue )
      {
        AccessKeyManager.Register( "\r", this );
      }
      else
      {
        AccessKeyManager.Unregister( "\r", this );
      }
    }

    #endregion //IsDefault

    #region IsOpen

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register( nameof(IsOpen), typeof( bool ), typeof( DropDownButton ), new UIPropertyMetadata( false, OnIsOpenChanged ) );
    public bool IsOpen
    {
      get => ( bool )GetValue( IsOpenProperty );
      set => SetValue( IsOpenProperty, value );
    }

    private static void OnIsOpenChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DropDownButton dropDownButton )
        dropDownButton.OnIsOpenChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnIsOpenChanged( bool oldValue, bool newValue )
    {
      RaiseRoutedEvent(newValue 
        ? DropDownButton.OpenedEvent 
        : DropDownButton.ClosedEvent);
    }

    #endregion //IsOpen

    #region MaxDropDownHeight

    public static readonly DependencyProperty MaxDropDownHeightProperty = DependencyProperty.Register( nameof(MaxDropDownHeight), typeof( double )
      , typeof( DropDownButton ), new UIPropertyMetadata( SystemParameters.PrimaryScreenHeight / 2.0, OnMaxDropDownHeightChanged ) );
    public double MaxDropDownHeight
    {
      get => (double)GetValue( MaxDropDownHeightProperty );
      set => SetValue( MaxDropDownHeightProperty, value );
    }

    private static void OnMaxDropDownHeightChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DropDownButton dropDownButton )
        dropDownButton.OnMaxDropDownHeightChanged( (double)e.OldValue, (double)e.NewValue );
    }

    protected virtual void OnMaxDropDownHeightChanged( double oldValue, double newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion

    #endregion //Properties

    #region Base Class Overrides

    public override void OnApplyTemplate()
    {
      base.OnApplyTemplate();
      this.Button = this.GetTemplateChild( PART_DropDownButton ) as ToggleButton;

      _contentPresenter = GetTemplateChild( PART_ContentPresenter ) as ContentPresenter;

      if( _popup != null )
        _popup.Opened -= Popup_Opened;

      _popup = GetTemplateChild( PART_Popup ) as Popup;

      if( _popup != null )
        _popup.Opened += Popup_Opened;
    }

    protected override void OnIsKeyboardFocusWithinChanged( DependencyPropertyChangedEventArgs e )
    {
      base.OnIsKeyboardFocusWithinChanged( e );
      if( !( bool )e.NewValue )
      {
        this.CloseDropDown( false );
      }
    }

    protected override void OnGotFocus( RoutedEventArgs e )
    {
      base.OnGotFocus( e );
      this.Button?.Focus();
    }

    protected override void OnAccessKey( AccessKeyEventArgs e )
    {
      if( e.IsMultiple )
      {
        base.OnAccessKey( e );
      }
      else
      {
        this.OnClick();
      }
    }

    #endregion //Base Class Overrides

    #region Events

    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent( "Click", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( DropDownButton ) );
    public event RoutedEventHandler Click
    {
      add => AddHandler( ClickEvent, value );
      remove => RemoveHandler( ClickEvent, value );
    }

    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent( "Opened", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( DropDownButton ) );
    public event RoutedEventHandler Opened
    {
      add => AddHandler( OpenedEvent, value );
      remove => RemoveHandler( OpenedEvent, value );
    }

    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent( "Closed", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( DropDownButton ) );
    public event RoutedEventHandler Closed
    {
      add => AddHandler( ClosedEvent, value );
      remove => RemoveHandler( ClosedEvent, value );
    }

    #endregion //Events

    #region Event Handlers

    private static void OnAccessKeyPressed( object sender, AccessKeyPressedEventArgs e )
    {
      if( !e.Handled && ( e.Scope == null ) && ( e.Target == null ) )
      {
        e.Target = sender as DropDownButton;
      }
    }

    private void OnKeyDown( object sender, KeyEventArgs e )
    {
      if( !IsOpen )
      {
        if( KeyboardUtilities.IsKeyModifyingPopupState( e ) )
        {
          IsOpen = true;
          // ContentPresenter items will get focus in Popup_Opened().
          e.Handled = true;
        }
      }
      else
      {
        if( KeyboardUtilities.IsKeyModifyingPopupState( e ) )
        {
          CloseDropDown( true );
          e.Handled = true;
        }
        else if( e.Key == Key.Escape )
        {
          CloseDropDown( true );
          e.Handled = true;
        }
      }
    }

    private void OnMouseDownOutsideCapturedElement( object sender, MouseButtonEventArgs e )
    {
      if( !this.IsMouseCaptureWithin )
      {
        this.CloseDropDown( true );
      }
    }

    private void DropDownButton_Click( object sender, RoutedEventArgs e )
    {
      OnClick();
    }

    void CanExecuteChanged( object sender, EventArgs e )
    {
      CanExecuteChanged();
    }

    private void Popup_Opened( object sender, EventArgs e )
    {
      // Set the focus on the content of the ContentPresenter.
      if( _contentPresenter != null )
      {
        _contentPresenter.MoveFocus(new(FocusNavigationDirection.First));
      }
    }

    #endregion //Event Handlers

    #region Methods

    private void CanExecuteChanged()
    {
      if( Command != null )
      {
        // If a RoutedCommand.
        if( Command is RoutedCommand command )
          IsEnabled = command.CanExecute( CommandParameter, CommandTarget );
        // If a not RoutedCommand.
        else
          IsEnabled = Command.CanExecute( CommandParameter );
      }
    }

    /// <summary>
    /// Closes the drop down.
    /// </summary>
    private void CloseDropDown( bool isFocusOnButton )
    {
      if( IsOpen )
      {
        IsOpen = false;
      }
      ReleaseMouseCapture();

      if( isFocusOnButton && (this.Button != null) )
      {
        Button.Focus();
      }
    }

    protected virtual void OnClick()
    {
      RaiseRoutedEvent( DropDownButton.ClickEvent );
      RaiseCommand();
    }

    /// <summary>
    /// Raises routed events.
    /// </summary>
    private void RaiseRoutedEvent( RoutedEvent routedEvent )
    {
      RoutedEventArgs args = new( routedEvent, this );
      RaiseEvent( args );
    }

    /// <summary>
    /// Raises the command's Execute event.
    /// </summary>
    private void RaiseCommand()
    {
      if( Command != null )
      {
        if( Command is not RoutedCommand routedCommand )
          Command.Execute( CommandParameter );
        else
          routedCommand.Execute( CommandParameter, CommandTarget );
      }
    }

    /// <summary>
    /// Unhooks a command from the Command property.
    /// </summary>
    /// <param name="oldCommand">The old command.</param>
    /// <param name="newCommand">The new command.</param>
    private void UnhookCommand( ICommand oldCommand, ICommand newCommand )
    {
      EventHandler handler = CanExecuteChanged;
      oldCommand.CanExecuteChanged -= handler;
    }

    /// <summary>
    /// Hooks up a command to the CanExecuteChnaged event handler.
    /// </summary>
    /// <param name="oldCommand">The old command.</param>
    /// <param name="newCommand">The new command.</param>
    private void HookUpCommand( ICommand oldCommand, ICommand newCommand )
    {
      EventHandler handler = CanExecuteChanged;
      canExecuteChangedHandler = handler;
      if( newCommand != null )
        newCommand.CanExecuteChanged += canExecuteChangedHandler;
    }

    #endregion //Methods

    #region ICommandSource Members

    // Keeps a copy of the CanExecuteChnaged handler so it doesn't get garbage collected.
    private EventHandler canExecuteChangedHandler;

    #region Command

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register( nameof(Command), typeof( ICommand ), typeof( DropDownButton ), new( null, OnCommandChanged ) );
    [TypeConverter( typeof( CommandConverter ) )]
    public ICommand Command
    {
      get => ( ICommand )GetValue( CommandProperty );
      set => SetValue( CommandProperty, value );
    }

    private static void OnCommandChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      if( d is DropDownButton dropDownButton )
        dropDownButton.OnCommandChanged( ( ICommand )e.OldValue, ( ICommand )e.NewValue );
    }

    protected virtual void OnCommandChanged( ICommand oldValue, ICommand newValue )
    {
      // If old command is not null, then we need to remove the handlers.
      if( oldValue != null )
        UnhookCommand( oldValue, newValue );

      HookUpCommand( oldValue, newValue );

      CanExecuteChanged(); //may need to call this when changing the command parameter or target.
    }

    #endregion //Command

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register( nameof(CommandParameter), typeof( object ), typeof( DropDownButton ), new( null ) );
    public object CommandParameter
    {
      get => GetValue( CommandParameterProperty );
      set => SetValue( CommandParameterProperty, value );
    }

    public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register( nameof(CommandTarget), typeof( IInputElement ), typeof( DropDownButton ), new( null ) );
    public IInputElement CommandTarget
    {
      get => ( IInputElement )GetValue( CommandTargetProperty );
      set => SetValue( CommandTargetProperty, value );
    }

    #endregion //ICommandSource Members
  }
}
