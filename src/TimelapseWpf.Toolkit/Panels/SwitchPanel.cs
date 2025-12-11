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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TimelapseWpf.Toolkit.Media.Animation;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Panels
{
  public class SwitchPanel : PanelBase, IScrollInfo
  {
    #region Static Fields

    private static readonly Vector ZeroVector = new();

    #endregion

    #region Constructors

    public SwitchPanel()
    {
      this.SetLayouts([]);
      this.Loaded += this.OnLoaded;
    }

    #endregion

    #region AreLayoutSwitchesAnimated Property

    public static readonly DependencyProperty AreLayoutSwitchesAnimatedProperty =
      DependencyProperty.Register( nameof(AreLayoutSwitchesAnimated), typeof( bool ), typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( true ) );

    public bool AreLayoutSwitchesAnimated
    {
      get => ( bool )this.GetValue( SwitchPanel.AreLayoutSwitchesAnimatedProperty );
      set => this.SetValue( SwitchPanel.AreLayoutSwitchesAnimatedProperty, value );
    }

    #endregion

    #region ActiveLayout Property

    private static readonly DependencyPropertyKey ActiveLayoutPropertyKey =
      DependencyProperty.RegisterReadOnly( "ActiveLayout", typeof( AnimationPanel ), typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsMeasure,
          SwitchPanel.OnActiveLayoutChanged ) );

    public static readonly DependencyProperty ActiveLayoutProperty = SwitchPanel.ActiveLayoutPropertyKey.DependencyProperty;

    public AnimationPanel ActiveLayout => ( AnimationPanel )this.GetValue( SwitchPanel.ActiveLayoutProperty );

    protected void SetActiveLayout( AnimationPanel value )
    {
      this.SetValue( SwitchPanel.ActiveLayoutPropertyKey, value );
    }

    private static void OnActiveLayoutChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( SwitchPanel )d ).OnActiveLayoutChanged( e );
    }

    protected virtual void OnActiveLayoutChanged( DependencyPropertyChangedEventArgs e )
    {
      if( _currentLayoutPanel != null )
      {
        _currentLayoutPanel.DeactivateLayout();
      }
      _currentLayoutPanel = e.NewValue as AnimationPanel;
      if( _currentLayoutPanel != null )
      {
        if( _currentLayoutPanel is IScrollInfo { ScrollOwner: not null } info )
        {
          info.ScrollOwner.InvalidateScrollInfo();
        }

        _currentLayoutPanel.ActivateLayout();
      }

      this.RaiseActiveLayoutChangedEvent();
      this.Dispatcher.BeginInvoke(
        DispatcherPriority.Normal,
        ( ThreadStart )UpdateSwitchTemplate );
    }

    #endregion

    #region ActiveLayoutIndex Property

    public static readonly DependencyProperty ActiveLayoutIndexProperty =
      DependencyProperty.Register( nameof(ActiveLayoutIndex), typeof( int ), typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( -1,
          SwitchPanel.OnActiveLayoutIndexChanged, SwitchPanel.CoerceActiveLayoutIndexValue ) );

    public int ActiveLayoutIndex
    {
      get => ( int )this.GetValue( SwitchPanel.ActiveLayoutIndexProperty );
      set => this.SetValue( SwitchPanel.ActiveLayoutIndexProperty, value );
    }

    private static void OnActiveLayoutIndexChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( SwitchPanel )d ).OnActiveLayoutIndexChanged( e );
    }

    protected virtual void OnActiveLayoutIndexChanged( DependencyPropertyChangedEventArgs e )
    {
      this.SetActiveLayout( ( this.Layouts.Count == 0 ) ? null : this.Layouts[ ActiveLayoutIndex ] );
    }

    private static object CoerceActiveLayoutIndexValue(DependencyObject d, object value)
    {
      if (d is not SwitchPanel switchPanel)
        return value;

      int panelCount = switchPanel.Layouts.Count;
      int result = (int)value;

      if (result < 0 && panelCount > 0)
      {
        result = 0;
      }
      else if (result >= panelCount)
      {
        result = panelCount - 1;
      }

      return result;
    }

    #endregion

    #region ActiveSwitchTemplate Property

    private static readonly DependencyPropertyKey ActiveSwitchTemplatePropertyKey =
      DependencyProperty.RegisterReadOnly( "ActiveSwitchTemplate", typeof( DataTemplate ), typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( null, SwitchPanel.OnActiveSwitchTemplateChanged ) );

    public static readonly DependencyProperty ActiveSwitchTemplateProperty = SwitchPanel.ActiveSwitchTemplatePropertyKey.DependencyProperty;

    public DataTemplate ActiveSwitchTemplate => ( DataTemplate )this.GetValue( SwitchPanel.ActiveSwitchTemplateProperty );

    protected void SetActiveSwitchTemplate( DataTemplate value )
    {
      this.SetValue( SwitchPanel.ActiveSwitchTemplatePropertyKey, value );
    }

    private static void OnActiveSwitchTemplateChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( SwitchPanel )d ).OnActiveSwitchTemplateChanged( e );
    }

    protected virtual void OnActiveSwitchTemplateChanged( DependencyPropertyChangedEventArgs e )
    {
      if( _presenters.Count > 0 )
      {
        DataTemplate template = e.NewValue as DataTemplate;
        List<UIElement> currentChildren = new( this.InternalChildren.Count );
        foreach( UIElement child in this.InternalChildren )
        {
          if( child == null )
            continue;

          currentChildren.Add( child );
        }

        foreach( SwitchPresenter presenter in _presenters )
        {
          if( presenter._switchRoot != null && currentChildren.Contains( presenter._switchRoot ) )
          {
            presenter.SwapTheTemplate( template, this.AreLayoutSwitchesAnimated );
          }
        }
      }
    }

    #endregion

    #region DefaultAnimationRate Property

    public static readonly DependencyProperty DefaultAnimationRateProperty =
      AnimationPanel.DefaultAnimationRateProperty.AddOwner( typeof( SwitchPanel ) );

    public AnimationRate DefaultAnimationRate
    {
      get => ( AnimationRate )this.GetValue( SwitchPanel.DefaultAnimationRateProperty );
      set => this.SetValue( SwitchPanel.DefaultAnimationRateProperty, value );
    }

    #endregion

    #region DefaultAnimator Property

    public static readonly DependencyProperty DefaultAnimatorProperty =
      AnimationPanel.DefaultAnimatorProperty.AddOwner( typeof( SwitchPanel ) );

    public IterativeAnimator DefaultAnimator
    {
      get => ( IterativeAnimator )this.GetValue( SwitchPanel.DefaultAnimatorProperty );
      set => this.SetValue( SwitchPanel.DefaultAnimatorProperty, value );
    }

    #endregion

    #region EnterAnimationRate Property

    public static readonly DependencyProperty EnterAnimationRateProperty =
      AnimationPanel.EnterAnimationRateProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( AnimationRate.Default ) );

    public AnimationRate EnterAnimationRate
    {
      get => ( AnimationRate )this.GetValue( SwitchPanel.EnterAnimationRateProperty );
      set => this.SetValue( SwitchPanel.EnterAnimationRateProperty, value );
    }

    #endregion

    #region EnterAnimator Property

    public static readonly DependencyProperty EnterAnimatorProperty =
      AnimationPanel.EnterAnimatorProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( IterativeAnimator.Default ) );

    public IterativeAnimator EnterAnimator
    {
      get => ( IterativeAnimator )this.GetValue( SwitchPanel.EnterAnimatorProperty );
      set => this.SetValue( SwitchPanel.EnterAnimatorProperty, value );
    }

    #endregion

    #region ExitAnimationRate Property

    public static readonly DependencyProperty ExitAnimationRateProperty =
      AnimationPanel.ExitAnimationRateProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( AnimationRate.Default ) );

    public AnimationRate ExitAnimationRate
    {
      get => ( AnimationRate )this.GetValue( SwitchPanel.ExitAnimationRateProperty );
      set => this.SetValue( SwitchPanel.ExitAnimationRateProperty, value );
    }

    #endregion

    #region ExitAnimator Property

    public static readonly DependencyProperty ExitAnimatorProperty =
      AnimationPanel.ExitAnimatorProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( IterativeAnimator.Default ) );

    public IterativeAnimator ExitAnimator
    {
      get => ( IterativeAnimator )this.GetValue( SwitchPanel.ExitAnimatorProperty );
      set => this.SetValue( SwitchPanel.ExitAnimatorProperty, value );
    }

    #endregion

    #region LayoutAnimationRate Property

    public static readonly DependencyProperty LayoutAnimationRateProperty =
      AnimationPanel.LayoutAnimationRateProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( AnimationRate.Default ) );

    public AnimationRate LayoutAnimationRate
    {
      get => ( AnimationRate )this.GetValue( SwitchPanel.LayoutAnimationRateProperty );
      set => this.SetValue( SwitchPanel.LayoutAnimationRateProperty, value );
    }

    #endregion

    #region LayoutAnimator Property

    public static readonly DependencyProperty LayoutAnimatorProperty =
      AnimationPanel.LayoutAnimatorProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( IterativeAnimator.Default ) );

    public IterativeAnimator LayoutAnimator
    {
      get => ( IterativeAnimator )this.GetValue( SwitchPanel.LayoutAnimatorProperty );
      set => this.SetValue( SwitchPanel.LayoutAnimatorProperty, value );
    }

    #endregion

    #region Layouts Property

    private static readonly DependencyPropertyKey LayoutsPropertyKey =
      DependencyProperty.RegisterReadOnly( "Layouts", typeof( ObservableCollection<AnimationPanel> ), typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsArrange,
          SwitchPanel.OnLayoutsChanged ) );

    public static readonly DependencyProperty LayoutsProperty = SwitchPanel.LayoutsPropertyKey.DependencyProperty;

    public ObservableCollection<AnimationPanel> Layouts => ( ObservableCollection<AnimationPanel> )this.GetValue( SwitchPanel.LayoutsProperty );

    protected void SetLayouts( ObservableCollection<AnimationPanel> value )
    {
      this.SetValue( SwitchPanel.LayoutsPropertyKey, value );
    }

    private static void OnLayoutsChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( SwitchPanel )d ).OnLayoutsChanged( e );
    }

    protected virtual void OnLayoutsChanged(DependencyPropertyChangedEventArgs e)
    {
      if (e.NewValue != null)
      {
        if (e.NewValue is ObservableCollection<AnimationPanel> newCollection)
        {
          newCollection.CollectionChanged += LayoutsCollectionChanged;
        }
      }
      if (e.OldValue != null)
      {
        if (e.OldValue is ObservableCollection<AnimationPanel> oldCollection)
        {
          oldCollection.CollectionChanged -= LayoutsCollectionChanged;
        }
      }
    }


    #endregion

    #region SwitchAnimationRate Property

    public static readonly DependencyProperty SwitchAnimationRateProperty =
      AnimationPanel.SwitchAnimationRateProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( AnimationRate.Default ) );

    public AnimationRate SwitchAnimationRate
    {
      get => ( AnimationRate )this.GetValue( SwitchPanel.SwitchAnimationRateProperty );
      set => this.SetValue( SwitchPanel.SwitchAnimationRateProperty, value );
    }

    #endregion

    #region SwitchAnimator Property

    public static readonly DependencyProperty SwitchAnimatorProperty =
      AnimationPanel.SwitchAnimatorProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( IterativeAnimator.Default ) );

    public IterativeAnimator SwitchAnimator
    {
      get => ( IterativeAnimator )this.GetValue( SwitchPanel.SwitchAnimatorProperty );
      set => this.SetValue( SwitchPanel.SwitchAnimatorProperty, value );
    }

    #endregion

    #region SwitchTemplate Property

    public static readonly DependencyProperty SwitchTemplateProperty =
      DependencyProperty.Register( nameof(SwitchTemplate), typeof( DataTemplate ), typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( null,
          SwitchPanel.OnSwitchTemplateChanged ) );

    public DataTemplate SwitchTemplate
    {
      get => ( DataTemplate )this.GetValue( SwitchPanel.SwitchTemplateProperty );
      set => this.SetValue( SwitchPanel.SwitchTemplateProperty, value );
    }

    private static void OnSwitchTemplateChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( SwitchPanel )d ).OnSwitchTemplateChanged( e );
    }

    protected virtual void OnSwitchTemplateChanged( DependencyPropertyChangedEventArgs e )
    {
      this.UpdateSwitchTemplate();
    }

    #endregion

    #region TemplateAnimationRate Property

    public static readonly DependencyProperty TemplateAnimationRateProperty =
      AnimationPanel.TemplateAnimationRateProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( AnimationRate.Default ) );

    public AnimationRate TemplateAnimationRate
    {
      get => ( AnimationRate )this.GetValue( SwitchPanel.TemplateAnimationRateProperty );
      set => this.SetValue( SwitchPanel.TemplateAnimationRateProperty, value );
    }

    #endregion

    #region TemplateAnimator Property

    public static readonly DependencyProperty TemplateAnimatorProperty =
      AnimationPanel.TemplateAnimatorProperty.AddOwner( typeof( SwitchPanel ),
        new FrameworkPropertyMetadata( IterativeAnimator.Default ) );

    public IterativeAnimator TemplateAnimator
    {
      get => ( IterativeAnimator )this.GetValue( SwitchPanel.TemplateAnimatorProperty );
      set => this.SetValue( SwitchPanel.TemplateAnimatorProperty, value );
    }

    #endregion

    #region VisualChildrenCount Protected Property

    protected override int VisualChildrenCount
    {
      get
      {
        int result;

        if( this.HasLoaded && _currentLayoutPanel != null )
        {
          result = _currentLayoutPanel.VisualChildrenCountInternal;
        }
        else
        {
          result = base.VisualChildrenCount;
        }

        return result;
      }
    }

    #endregion

    #region ExitingChildren Internal Property

    internal List<UIElement> ExitingChildren { get; } = [];

    #endregion

    #region ChildrenInternal Internal Property

    internal UIElementCollection ChildrenInternal => this.InternalChildren;

    #endregion

    #region HasLoaded Internal Property

    internal bool HasLoaded
    {
      get => _cacheBits[ ( int )CacheBits.HasLoaded ];
      set => _cacheBits[ ( int )CacheBits.HasLoaded ] = value;
    }

    #endregion

    #region IsScrollingPhysically Private Property

    private bool IsScrollingPhysically
    {
      get
      {
        bool isScrollingPhys = false;

        if( _scrollOwner != null )
        {
          isScrollingPhys = true;

          if( this.ActiveLayout is IScrollInfo )
          {
            isScrollingPhys = ( ( IScrollInfo )this.ActiveLayout ).ScrollOwner == null;
          }
        }

        return isScrollingPhys;
      }
    }

    #endregion

    #region ActiveLayoutChanged Event

    public static readonly RoutedEvent ActiveLayoutChangedEvent =
      EventManager.RegisterRoutedEvent( "ActiveLayoutChanged", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( SwitchPanel ) );

    public event RoutedEventHandler ActiveLayoutChanged
    {
      add => this.AddHandler( SwitchPanel.ActiveLayoutChangedEvent, value );
      remove => this.RemoveHandler( SwitchPanel.ActiveLayoutChangedEvent, value );
    }

    protected RoutedEventArgs RaiseActiveLayoutChangedEvent()
    {
      return SwitchPanel.RaiseActiveLayoutChangedEvent( this );
    }

    internal static RoutedEventArgs RaiseActiveLayoutChangedEvent( UIElement target )
    {
      if( target == null )
        return null;

      RoutedEventArgs args = new()
      {
        RoutedEvent = SwitchPanel.ActiveLayoutChangedEvent
      };
      RoutedEventHelper.RaiseEvent( target, args );
      return args;
    }

    #endregion

    #region SwitchAnimationBegun Event

    public static readonly RoutedEvent SwitchAnimationBegunEvent =
      EventManager.RegisterRoutedEvent( "SwitchAnimationBegun", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( SwitchPanel ) );

    public event RoutedEventHandler SwitchAnimationBegun
    {
      add => this.AddHandler( SwitchPanel.SwitchAnimationBegunEvent, value );
      remove => this.RemoveHandler( SwitchPanel.SwitchAnimationBegunEvent, value );
    }

    protected RoutedEventArgs RaiseSwitchAnimationBegunEvent()
    {
      return SwitchPanel.RaiseSwitchAnimationBegunEvent( this );
    }

    private static RoutedEventArgs RaiseSwitchAnimationBegunEvent( UIElement target )
    {
      if( target == null )
        return null;

      RoutedEventArgs args = new()
      {
        RoutedEvent = SwitchPanel.SwitchAnimationBegunEvent
      };
      RoutedEventHelper.RaiseEvent( target, args );
      return args;
    }

    #endregion

    #region SwitchAnimationCompleted Event

    public static readonly RoutedEvent SwitchAnimationCompletedEvent =
      EventManager.RegisterRoutedEvent( "SwitchAnimationCompleted", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( SwitchPanel ) );

    public event RoutedEventHandler SwitchAnimationCompleted
    {
      add => this.AddHandler( SwitchPanel.SwitchAnimationCompletedEvent, value );
      remove => this.RemoveHandler( SwitchPanel.SwitchAnimationCompletedEvent, value );
    }

    protected RoutedEventArgs RaiseSwitchAnimationCompletedEvent()
    {
      return SwitchPanel.RaiseSwitchAnimationCompletedEvent( this );
    }

    private static RoutedEventArgs RaiseSwitchAnimationCompletedEvent( UIElement target )
    {
      if( target == null )
        return null;

      RoutedEventArgs args = new()
      {
        RoutedEvent = SwitchPanel.SwitchAnimationCompletedEvent
      };
      RoutedEventHelper.RaiseEvent( target, args );
      return args;
    }

    #endregion

    protected override Size MeasureOverride( Size availableSize )
    {
      AnimationPanel layout = ( Layouts.Count == 0 ) ? _defaultLayoutCanvas : this.ActiveLayout;
      Size measureSize = layout.MeasureChildrenCore( this.InternalChildren, availableSize );

      if( this.IsScrollingPhysically )
      {
        Size viewport = availableSize;
        Size extent = measureSize;

        //
        // Make sure our offset works with the new size of the panel.  We don't want to show
        // any whitespace if the user scrolled all the way down and then increased the size of the panel.
        //
        Vector newOffset = new(
          Math.Max( 0, Math.Min( _offset.X, extent.Width - viewport.Width ) ),
          Math.Max( 0, Math.Min( _offset.Y, extent.Height - viewport.Height ) ) );

        this.SetScrollingData( viewport, extent, newOffset );
      }

      return measureSize;
    }

    protected override Size ArrangeOverride( Size finalSize )
    {
      AnimationPanel layout = ( Layouts.Count == 0 ) 
        ? _defaultLayoutCanvas 
        : this.ActiveLayout;

      layout.PhysicalScrollOffset = this.IsScrollingPhysically ? _offset : ZeroVector;

      return layout.ArrangeChildrenCore( InternalChildren, finalSize );
    }

    protected override Visual GetVisualChild( int index )
    {
      if( this.HasLoaded && _currentLayoutPanel != null )
        return _currentLayoutPanel.GetVisualChildInternal( index );

      return base.GetVisualChild( index );
    }

    protected override void OnVisualChildrenChanged( DependencyObject visualAdded, DependencyObject visualRemoved )
    {
      // The OnNotifyVisualChildAdded/Removed methods get called for all animation panels within a 
      // SwitchPanel.Layouts collection, regardless of whether they are the active layout for the SwitchPanel.
      if( visualAdded is UIElement added )
      {
        // do not issue notification for a child that is exiting
        if( _currentLayoutPanel == null || !_currentLayoutPanel.IsRemovingInternalChild )
        {
          foreach( AnimationPanel panel in Layouts )
          {
            panel.OnNotifyVisualChildAddedInternal( added );
          }
        }
      }
      else if( visualRemoved is UIElement element )
      {
        foreach( AnimationPanel panel in Layouts )
        {
          panel.OnNotifyVisualChildRemovedInternal( element );
        }
      }

      if( _currentLayoutPanel != null )
      {
        _currentLayoutPanel.OnSwitchParentVisualChildrenChanged( visualAdded, visualRemoved );
      }
      else
      {
        base.OnVisualChildrenChanged( visualAdded, visualRemoved );
      }
    }

    internal void AddVisualChildInternal( Visual child )
    {
      this.AddVisualChild( child );
    }

    internal void BeginLayoutSwitch()
    {
      this.RaiseSwitchAnimationBegunEvent();
    }

    internal void EndLayoutSwitch()
    {
      this.RaiseSwitchAnimationCompletedEvent();
    }

    internal Visual GetVisualChildInternal( int index )
    {
      // called from AnimationPanel to access base class method
      return base.GetVisualChild( index );
    }

    internal void OnVisualChildrenChangedInternal( DependencyObject visualAdded, DependencyObject visualRemoved )
    {
      base.OnVisualChildrenChanged( visualAdded, visualRemoved );
    }

    internal UIElement RegisterPresenter( SwitchPresenter presenter )
    {
      var result = AnimationPanel.FindAncestorChildOfAnimationPanel( presenter, out _ );
      if( result != null )
      {
        _presenters.Add( presenter );
        presenter.SwapTheTemplate( ActiveSwitchTemplate, false );
      }
      return result;
    }

    internal void RemoveVisualChildInternal( Visual child )
    {
      this.RemoveVisualChild( child );
    }

    internal void UnregisterPresenter( SwitchPresenter presenter, DependencyObject container )
    {
      if( container != null )
      {
        _presenters.Remove( presenter );
        presenter.SwapTheTemplate( null, false );
      }
    }

    internal void UpdateSwitchTemplate()
    {
      this.SetActiveSwitchTemplate( ( this.ActiveLayout == null ) || ( this.ActiveLayout.SwitchTemplate == null )
          ? this.SwitchTemplate : this.ActiveLayout.SwitchTemplate );
    }

    private void OnLoaded( object sender, RoutedEventArgs e )
    {
      this.HasLoaded = true;

      // invalidate arrange to give enter animations a chance to run
      this.InvalidateArrange();
    }

    private void LayoutsCollectionChanged( object sender, NotifyCollectionChangedEventArgs e )
    {
      if( e.Action != NotifyCollectionChangedAction.Move )
      {
        if( e.NewItems != null )
        {
          foreach( AnimationPanel panel in e.NewItems )
          {
            this.AddLogicalChild( panel );
            panel.SetSwitchParent( this );

            if( panel is IScrollInfo info )
            {
              info.ScrollOwner = this.ScrollOwner;
            }

            if( this.IsLoaded )
            {
              foreach( UIElement child in this.InternalChildren )
              {
                if( child == null )
                  continue;

                panel.OnNotifyVisualChildAddedInternal( child );
              }
            }
          }
        }

        if( e.OldItems != null )
        {
          foreach( AnimationPanel panel in e.OldItems )
          {
            if( IsLoaded )
            {
              foreach( UIElement child in this.InternalChildren )
              {
                if( child == null )
                  continue;

                panel.OnNotifyVisualChildRemovedInternal( child );
              }
            }

            this.RemoveLogicalChild( panel );
            panel.SetSwitchParent( null );

            if( panel is IScrollInfo info )
            {
              info.ScrollOwner = null;
            }
          }
        }
      }

      // ensure valid ActiveLayoutIndex value
      this.CoerceValue( SwitchPanel.ActiveLayoutIndexProperty );
      this.SetActiveLayout( ( this.Layouts.Count == 0 ) ? null : this.Layouts[ ActiveLayoutIndex ] );
    }

    private void ResetScrollInfo()
    {
      _offset = new();
      _viewport = _extent = new( 0, 0 );
    }

    private void OnScrollChange()
    {
      if( this.ScrollOwner != null )
      {
        this.ScrollOwner.InvalidateScrollInfo();
      }
    }

    private void SetScrollingData( Size viewport, Size extent, Vector offset )
    {
      _offset = offset;

      if( DoubleHelper.AreVirtuallyEqual( viewport, _viewport ) == false || DoubleHelper.AreVirtuallyEqual( extent, _extent ) == false ||
          DoubleHelper.AreVirtuallyEqual( offset, _computedOffset ) == false )
      {
        _viewport = viewport;
        _extent = extent;
        _computedOffset = offset;
        this.OnScrollChange();
      }
    }

    private double ValidateInputOffset( double offset, string parameterName )
    {
      if( double.IsNaN( offset ) )
        throw new ArgumentOutOfRangeException( parameterName );

      return Math.Max( 0d, offset );
    }

    private int FindChildFromVisual( Visual vis )
    {
      int index = -1;

      DependencyObject parent = vis;
      DependencyObject child;

      do
      {
        child = parent;
        parent = VisualTreeHelper.GetParent( child );
      }
      while( parent != null && parent != this );

      if( parent == this )
      {
        index = this.Children.IndexOf( ( UIElement )child );
      }

      return index;
    }

    #region IScrollInfo Members

    public bool CanHorizontallyScroll
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
          return ( ( IScrollInfo )this.ActiveLayout ).CanHorizontallyScroll;

        return _allowHorizontal;
      }
      set
      {
        if( this.ActiveLayout is IScrollInfo )
        {
          ( ( IScrollInfo )this.ActiveLayout ).CanHorizontallyScroll = value;
        }
        else
        {
          _allowHorizontal = value;
        }
      }
    }

    public bool CanVerticallyScroll
    {
      get
      {
        if (this.ActiveLayout is IScrollInfo)
          return ((IScrollInfo)this.ActiveLayout).CanVerticallyScroll;

        return _allowVertical;
      }
      set
      {
        if( this.ActiveLayout is IScrollInfo )
        {
          ( ( IScrollInfo )this.ActiveLayout ).CanVerticallyScroll = value;
        }
        else
        {
          _allowVertical = value;
        }
      }
    }

    public double ExtentHeight
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
          return ( ( IScrollInfo )this.ActiveLayout ).ExtentHeight;

        return _extent.Height;
      }
    }

    public double ExtentWidth
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
          return ( ( IScrollInfo )this.ActiveLayout ).ExtentWidth;

        return _extent.Width;
      }
    }

    public double HorizontalOffset
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
          return ( ( IScrollInfo )this.ActiveLayout ).HorizontalOffset;

        return _offset.X;
      }
    }

    public void LineDown()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).LineDown();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset + 1d );
      }
    }

    public void LineLeft()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).LineLeft();
      }
      else
      {
        this.SetHorizontalOffset( this.VerticalOffset - 1d );
      }
    }

    public void LineRight()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).LineRight();
      }
      else
      {
        this.SetHorizontalOffset( this.VerticalOffset + 1d );
      }
    }

    public void LineUp()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).LineUp();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset + 1d );
      }
    }

    public Rect MakeVisible( Visual visual, Rect rectangle )
    {
      if( this.ActiveLayout is IScrollInfo )
        return ( ( IScrollInfo )this.ActiveLayout ).MakeVisible( visual, rectangle );

      if( ( rectangle.IsEmpty || ( visual == null ) ) || ( ( visual == this ) || !this.IsAncestorOf( visual ) ) )
        return Rect.Empty;

      rectangle = visual.TransformToAncestor( this ).TransformBounds( rectangle );

      if( this.IsScrollingPhysically == false )
        return rectangle;

      //
      // Make sure we can find the child...
      //
      int index = this.FindChildFromVisual( visual );

      if( index == -1 )
        throw new ArgumentException( "visual" );

      //
      // Since our _Offset pushes the items down we need to correct it here to 
      // give a true rectangle of the child.
      //
      Rect itemRect = rectangle;
      itemRect.Offset( _offset );

      Rect viewRect = new( new( _offset.X, _offset.Y ), _viewport );

      if( ScrollHelper.ScrollLeastAmount( viewRect, itemRect, out var newPhysOffset ) )
      {
        this.SetHorizontalOffset( newPhysOffset.X );
        this.SetVerticalOffset( newPhysOffset.Y );
      }

      return rectangle;
    }

    public void MouseWheelDown()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )ActiveLayout ).MouseWheelDown();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset + SystemParameters.WheelScrollLines );
      }
    }

    public void MouseWheelLeft()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).MouseWheelLeft();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset - 3d );
      }
    }

    public void MouseWheelRight()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).MouseWheelRight();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset + 3d );
      }
    }

    public void MouseWheelUp()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).MouseWheelUp();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset - SystemParameters.WheelScrollLines );
      }
    }

    public void PageDown()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).PageDown();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset + this.ViewportHeight );
      }
    }

    public void PageLeft()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).PageLeft();
      }
      else
      {
        this.SetHorizontalOffset( this.HorizontalOffset - this.ViewportWidth );
      }
    }

    public void PageRight()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).PageRight();
      }
      else
      {
        this.SetHorizontalOffset( this.HorizontalOffset + this.ViewportWidth );
      }
    }

    public void PageUp()
    {
      if( this.ActiveLayout is IScrollInfo )
      {
        ( ( IScrollInfo )this.ActiveLayout ).PageUp();
      }
      else
      {
        this.SetVerticalOffset( this.VerticalOffset - this.ViewportHeight );
      }
    }

    public ScrollViewer ScrollOwner
    {
      get => _scrollOwner;
      set
      {
        foreach( AnimationPanel layout in this.Layouts )
        {
          if( layout is IScrollInfo info )
          {
            info.ScrollOwner = value;
          }
        }

        if( _scrollOwner != value )
        {
          _scrollOwner = value;

          this.ResetScrollInfo();
        }
      }
    }

    public void SetHorizontalOffset( double offset )
    {
      offset = this.ValidateInputOffset( offset, "HorizontalOffset" );

      offset = Math.Min( offset, this.ExtentWidth - this.ViewportWidth );

      if( !DoubleHelper.AreVirtuallyEqual( offset, _offset.X ) )
      {
        _offset.X = offset;
        base.InvalidateMeasure();
      }
    }

    public void SetVerticalOffset( double offset )
    {
      offset = this.ValidateInputOffset( offset, "VerticalOffset" );

      offset = Math.Min( offset, this.ExtentHeight - this.ViewportHeight );

      if( !DoubleHelper.AreVirtuallyEqual( offset, _offset.Y ) )
      {
        _offset.Y = offset;
        base.InvalidateMeasure();
      }
    }

    public double VerticalOffset
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
        {
          return ( ( IScrollInfo )this.ActiveLayout ).VerticalOffset;
        }

        return _offset.Y;
      }
    }

    public double ViewportHeight
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
        {
          return ( ( IScrollInfo )this.ActiveLayout ).ViewportHeight;
        }

        return _viewport.Height;
      }
    }

    public double ViewportWidth
    {
      get
      {
        if( this.ActiveLayout is IScrollInfo )
        {
          return ( ( IScrollInfo )this.ActiveLayout ).ViewportWidth;
        }

        return _viewport.Width;
      }
    }

    #endregion

    #region Private Fields

    internal AnimationPanel _currentLayoutPanel;

    private readonly AnimationPanel _defaultLayoutCanvas = new TimelapseWpf.Toolkit.Panels.WrapPanel();
    private readonly Collection<SwitchPresenter> _presenters = [];

    private BitVector32 _cacheBits = new( 0 );

    private bool _allowHorizontal;
    private bool _allowVertical;
    private Vector _computedOffset = new( 0.0, 0.0 );
    private Size _extent = new( 0, 0 );
    private Vector _offset = new( 0, 0 );
    private ScrollViewer _scrollOwner;
    private Size _viewport;

    #endregion

    #region CacheBits Nested Type

    private enum CacheBits
    {
      HasLoaded = 0x00000001,
    }

    #endregion
  }
}
