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
using System.Windows.Media;
using System;
using System.Windows.Input;

namespace TimelapseWpf.Toolkit
{
  public class WizardPage : ContentControl
  {
    #region Properties

    public static readonly DependencyProperty BackButtonVisibilityProperty = DependencyProperty.Register( nameof(BackButtonVisibility), typeof( WizardPageButtonVisibility ), typeof( WizardPage ), new UIPropertyMetadata( WizardPageButtonVisibility.Inherit ) );
    public WizardPageButtonVisibility BackButtonVisibility
    {
      get => ( WizardPageButtonVisibility )GetValue( BackButtonVisibilityProperty );
      set => SetValue( BackButtonVisibilityProperty, value );
    }

    public static readonly DependencyProperty CanCancelProperty = DependencyProperty.Register( nameof(CanCancel), typeof( bool? ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public bool? CanCancel
    {
      get => ( bool? )GetValue( CanCancelProperty );
      set => SetValue( CanCancelProperty, value );
    }

    public static readonly DependencyProperty CancelButtonVisibilityProperty = DependencyProperty.Register( nameof(CancelButtonVisibility), typeof( WizardPageButtonVisibility ), typeof( WizardPage ), new UIPropertyMetadata( WizardPageButtonVisibility.Inherit ) );
    public WizardPageButtonVisibility CancelButtonVisibility
    {
      get => ( WizardPageButtonVisibility )GetValue( CancelButtonVisibilityProperty );
      set => SetValue( CancelButtonVisibilityProperty, value );
    }

    public static readonly DependencyProperty CanFinishProperty = DependencyProperty.Register( nameof(CanFinish), typeof( bool? ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public bool? CanFinish
    {
      get => ( bool? )GetValue( CanFinishProperty );
      set => SetValue( CanFinishProperty, value );
    }

    public static readonly DependencyProperty CanHelpProperty = DependencyProperty.Register( nameof(CanHelp), typeof( bool? ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public bool? CanHelp
    {
      get => ( bool? )GetValue( CanHelpProperty );
      set => SetValue( CanHelpProperty, value );
    }

    public static readonly DependencyProperty CanSelectNextPageProperty = DependencyProperty.Register( nameof(CanSelectNextPage), typeof( bool? ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public bool? CanSelectNextPage
    {
      get => ( bool? )GetValue( CanSelectNextPageProperty );
      set => SetValue( CanSelectNextPageProperty, value );
    }

    public static readonly DependencyProperty CanSelectPreviousPageProperty = DependencyProperty.Register( nameof(CanSelectPreviousPage), typeof( bool? ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public bool? CanSelectPreviousPage
    {
      get => ( bool? )GetValue( CanSelectPreviousPageProperty );
      set => SetValue( CanSelectPreviousPageProperty, value );
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register( nameof(Description), typeof( string ), typeof( WizardPage ) );
    public string Description
    {
      get => ( string )base.GetValue( DescriptionProperty );
      set => base.SetValue( DescriptionProperty, value );
    }

    public static readonly DependencyProperty ExteriorPanelBackgroundProperty = DependencyProperty.Register( nameof(ExteriorPanelBackground), typeof( Brush ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public Brush ExteriorPanelBackground
    {
      get => ( Brush )GetValue( ExteriorPanelBackgroundProperty );
      set => SetValue( ExteriorPanelBackgroundProperty, value );
    }

    public static readonly DependencyProperty ExteriorPanelContentProperty = DependencyProperty.Register( nameof(ExteriorPanelContent), typeof( object ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public object ExteriorPanelContent
    {
      get => GetValue( ExteriorPanelContentProperty );
      set => SetValue( ExteriorPanelContentProperty, value );
    }

    public static readonly DependencyProperty FinishButtonVisibilityProperty = DependencyProperty.Register( nameof(FinishButtonVisibility), typeof( WizardPageButtonVisibility ), typeof( WizardPage ), new UIPropertyMetadata( WizardPageButtonVisibility.Inherit ) );
    public WizardPageButtonVisibility FinishButtonVisibility
    {
      get => ( WizardPageButtonVisibility )GetValue( FinishButtonVisibilityProperty );
      set => SetValue( FinishButtonVisibilityProperty, value );
    }

    public static readonly DependencyProperty HeaderBackgroundProperty = DependencyProperty.Register( nameof(HeaderBackground), typeof( Brush ), typeof( WizardPage ), new UIPropertyMetadata( Brushes.White ) );
    public Brush HeaderBackground
    {
      get => ( Brush )GetValue( HeaderBackgroundProperty );
      set => SetValue( HeaderBackgroundProperty, value );
    }

    public static readonly DependencyProperty HeaderImageProperty = DependencyProperty.Register( nameof(HeaderImage), typeof( ImageSource ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public ImageSource HeaderImage
    {
      get => ( ImageSource )GetValue( HeaderImageProperty );
      set => SetValue( HeaderImageProperty, value );
    }

    public static readonly DependencyProperty HelpButtonVisibilityProperty = DependencyProperty.Register( nameof(HelpButtonVisibility), typeof( WizardPageButtonVisibility ), typeof( WizardPage ), new UIPropertyMetadata( WizardPageButtonVisibility.Inherit ) );
    public WizardPageButtonVisibility HelpButtonVisibility
    {
      get => ( WizardPageButtonVisibility )GetValue( HelpButtonVisibilityProperty );
      set => SetValue( HelpButtonVisibilityProperty, value );
    }

    public static readonly DependencyProperty NextButtonVisibilityProperty = DependencyProperty.Register( nameof(NextButtonVisibility), typeof( WizardPageButtonVisibility ), typeof( WizardPage ), new UIPropertyMetadata( WizardPageButtonVisibility.Inherit ) );
    public WizardPageButtonVisibility NextButtonVisibility
    {
      get => ( WizardPageButtonVisibility )GetValue( NextButtonVisibilityProperty );
      set => SetValue( NextButtonVisibilityProperty, value );
    }

    public static readonly DependencyProperty NextPageProperty = DependencyProperty.Register( nameof(NextPage), typeof( WizardPage ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public WizardPage NextPage
    {
      get => ( WizardPage )GetValue( NextPageProperty );
      set => SetValue( NextPageProperty, value );
    }

    public static readonly DependencyProperty PageTypeProperty = DependencyProperty.Register( nameof(PageType), typeof( WizardPageType ), typeof( WizardPage ), new UIPropertyMetadata( WizardPageType.Exterior ) );
    public WizardPageType PageType
    {
      get => ( WizardPageType )GetValue( PageTypeProperty );
      set => SetValue( PageTypeProperty, value );
    }

    public static readonly DependencyProperty PreviousPageProperty = DependencyProperty.Register( nameof(PreviousPage), typeof( WizardPage ), typeof( WizardPage ), new UIPropertyMetadata( null ) );
    public WizardPage PreviousPage
    {
      get => ( WizardPage )GetValue( PreviousPageProperty );
      set => SetValue( PreviousPageProperty, value );
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register( nameof(Title), typeof( string ), typeof( WizardPage ) );
    public string Title
    {
      get => ( string )base.GetValue( TitleProperty );
      set => base.SetValue( TitleProperty, value );
    }

    #endregion //Properties

    #region Constructors

    static WizardPage()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( WizardPage ), new FrameworkPropertyMetadata( typeof( WizardPage ) ) );
    }

    public WizardPage()
    {
      this.Loaded += WizardPage_Loaded;
      this.Unloaded += WizardPage_Unloaded;
    }

    void WizardPage_Unloaded( object sender, RoutedEventArgs e )
    {
      base.RaiseEvent( new( WizardPage.LeaveEvent, this ) );
    }

    void WizardPage_Loaded( object sender, RoutedEventArgs e )
    {
      if( this.IsVisible )
      {
        base.RaiseEvent( new( WizardPage.EnterEvent, this ) );
      }
    }

    #endregion //Constructors

    #region Overrides

    protected override void OnPropertyChanged( DependencyPropertyChangedEventArgs e )
    {
      base.OnPropertyChanged( e );

      if( ( e.Property.Name == "CanSelectNextPage" ) || ( e.Property.Name == "CanHelp" ) || ( e.Property.Name == "CanFinish" )
        || ( e.Property.Name == "CanCancel" ) || ( e.Property.Name == "CanSelectPreviousPage" ) )
      {
        CommandManager.InvalidateRequerySuggested();
      }
    }




    #endregion

    #region Events

    #region Enter Event

    public static readonly RoutedEvent EnterEvent = EventManager.RegisterRoutedEvent( "Enter", RoutingStrategy.Bubble, typeof( EventHandler ), typeof( WizardPage ) );
    public event RoutedEventHandler Enter
    {
      add => AddHandler( EnterEvent, value );
      remove => RemoveHandler( EnterEvent, value );
    }

    #endregion //Enter Event

    #region Leave Event

    public static readonly RoutedEvent LeaveEvent = EventManager.RegisterRoutedEvent( "Leave", RoutingStrategy.Bubble, typeof( EventHandler ), typeof( WizardPage ) );
    public event RoutedEventHandler Leave
    {
      add => AddHandler( LeaveEvent, value );
      remove => RemoveHandler( LeaveEvent, value );
    }

    #endregion //Leave Event

    #endregion  //Events
  }
}
