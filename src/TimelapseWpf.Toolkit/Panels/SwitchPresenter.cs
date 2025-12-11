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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Panels
{
  public class SwitchPresenter : FrameworkElement
  {
    #region Constructors

    public SwitchPresenter()
    {
      this.AddVisualChild( _contentPresenter );

      this.Loaded += this.SwitchPresenter_Loaded;
      this.Unloaded += this.SwitchPresenter_Unloaded;
    }

    #endregion

    #region DelaySwitch Property

    // Using a DependencyProperty as the backing store for DelaySwitch.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty DelaySwitchProperty =
      DependencyProperty.Register( nameof(DelaySwitch), typeof( bool ), typeof( SwitchPresenter ), 
        new UIPropertyMetadata( false ) );

    public bool DelaySwitch
    {
      get => ( bool )this.GetValue( SwitchPresenter.DelaySwitchProperty );
      set => this.SetValue( SwitchPresenter.DelaySwitchProperty, value );
    }

    #endregion

    #region DelayPriority Property

    // Using a DependencyProperty as the backing store for DelayPriority.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty DelayPriorityProperty =
      DependencyProperty.Register( nameof(DelayPriority), typeof( DispatcherPriority ), typeof( SwitchPresenter ),
        new UIPropertyMetadata( DispatcherPriority.Background ) );

    public DispatcherPriority DelayPriority
    {
      get => ( DispatcherPriority )this.GetValue( SwitchPresenter.DelayPriorityProperty );
      set => this.SetValue( SwitchPresenter.DelayPriorityProperty, value );
    }

    #endregion

    #region SwitchParent Internal Property

    internal static readonly DependencyProperty SwitchParentProperty =
      DependencyProperty.Register( nameof(SwitchParent), typeof( SwitchPanel ), typeof( SwitchPresenter ),
        new FrameworkPropertyMetadata( null, 
          SwitchPresenter.OnSwitchParentChanged ) );

    internal SwitchPanel SwitchParent
    {
      get => ( SwitchPanel )this.GetValue( SwitchPresenter.SwitchParentProperty );
      set => this.SetValue( SwitchPresenter.SwitchParentProperty, value );
    }

    private static void OnSwitchParentChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( SwitchPresenter )d ).OnSwitchParentChanged( e );
    }

    protected virtual void OnSwitchParentChanged(DependencyPropertyChangedEventArgs e)
    {
      if (e.OldValue != null)
      {
        (e.OldValue as SwitchPanel)?.UnregisterPresenter(this, _switchRoot);
        _switchRoot = null;
        BindingOperations.ClearAllBindings(_contentPresenter);
      }

      if (e.NewValue is SwitchPanel switchPanel)
      {
        _contentPresenter.SetBinding(ContentPresenter.ContentProperty, new Binding());
        _switchRoot = switchPanel.RegisterPresenter(this);
      }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
      if (sender is not SwitchPresenter sp)
        return;

      if (sp._switchRoot == null)
      {
        sp.SwitchParent = VisualTreeHelperEx.FindAncestorByType(sp, typeof(SwitchPanel), false) as
          SwitchPanel;
      }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
      if (sender is SwitchPresenter switchPresenter)
      {
        switchPresenter.SwitchParent = null;
      }
    }

    #endregion

    #region VisualChildrenCount Protected Property

    protected override int VisualChildrenCount => 1;

    #endregion

    internal void RegisterID( string id, FrameworkElement element )
    {
      if( element == null )
        return;

      _knownIDs[ id ] = element;
    }

    internal void SwapTheTemplate( DataTemplate template, bool beginAnimation )
    {
      if( this.DelaySwitch )
      {
        _currentTemplate = template;

        this.Dispatcher.BeginInvoke( new Action<DelaySwitchParams>( this.OnSwapTemplate ),
          this.DelayPriority,
          new DelaySwitchParams()
          {
            Template = template,
            BeginAnimation = beginAnimation
          } );
      }
      else
      {
        this.DoSwapTemplate( template, beginAnimation );
      }
    }

    protected override Size MeasureOverride( Size constraint )
    {
      // if first pass, resolve SwitchParent
      if( !_isMeasured && _switchRoot == null )
      {
        SwitchPresenter.OnLoaded( this, null );
        _isMeasured = true;
      }

      _contentPresenter.Measure( constraint );
      return _contentPresenter.DesiredSize;
    }

    protected override Size ArrangeOverride( Size arrangeBounds )
    {
      _contentPresenter.Arrange( new( arrangeBounds ) );
      return arrangeBounds;
    }

    protected override Visual GetVisualChild( int index )
    {
      if( index != 0 )
        throw new ArgumentOutOfRangeException( nameof(index), index, "" );

      return _contentPresenter;
    }

    private void OnSwapTemplate( DelaySwitchParams data )
    {
      // If we are switching the templates fast the invokes will lag. So ignore old invokes.
      if( data.Template == _currentTemplate )
      {
        this.DoSwapTemplate( data.Template, data.BeginAnimation );
        _currentTemplate = null;
      }
    }

    private void DoSwapTemplate(DataTemplate template, bool beginAnimation)
    {
      // cache transforms for known ID'd elements in the current template
      Dictionary<string, Rect> knownLocations = null;
      if (beginAnimation && _knownIDs.Count > 0)
      {
        knownLocations = new();
        foreach (KeyValuePair<string, FrameworkElement> entry in _knownIDs)
        {
          GeneralTransform transform = entry.Value.TransformToAncestor(SwitchParent);
          if (transform is MatrixTransform matrixTransform)
          {
            Size size = entry.Value.RenderSize;
            Matrix m = matrixTransform.Matrix;
            Point[] points = [new(), new(size.Width, size.Height)];
            m.Transform(points);
            knownLocations[entry.Key] = new(points[0], points[1]);
          }
        }
      }

      // clear the known IDs because the new template will have all new IDs
      _knownIDs.Clear();

      // set and apply the new template
      _contentPresenter.ContentTemplate = template;
      if (template != null)
      {
        _contentPresenter.ApplyTemplate();
      }

      // determine locations of ID'd elements in new template
      // and begin animation to new location
      if (knownLocations != null && _knownIDs.Count > 0)
      {
        Dictionary<string, Rect> newLocations = null;
        RoutedEventHandler onLoaded = null;
        onLoaded = delegate (object sender, RoutedEventArgs _)
        {
          if (sender is not FrameworkElement element)
            return;

          element.Loaded -= onLoaded;
          string id = SwitchTemplate.GetID(element);
          if (knownLocations.TryGetValue(id, out var previousLocation))
          {
            // ensure that the new locations have been resolved
            newLocations ??= this.SwitchParent.ActiveLayout.GetNewLocationsBasedOnTargetPlacement(this,
     _switchRoot);

            if (VisualTreeHelper.GetParent(element) is UIElement parent)
            {
              GeneralTransform transform = SwitchParent.TransformToDescendant(parent);
              if (transform is MatrixTransform matrixTransform)
              {
                Point[] points = [previousLocation.TopLeft, previousLocation.BottomRight];
                Matrix m = matrixTransform.Matrix;
                m.Transform(points);
                Rect oldLocation = new(points[0], points[1]);
                Rect newLocation = newLocations[id];
                this.SwitchParent.ActiveLayout.BeginGrandchildAnimation(element, oldLocation,
    newLocation);
              }
            }
          }
        };

        foreach (KeyValuePair<string, FrameworkElement> entry in _knownIDs)
        {
          entry.Value.Loaded += onLoaded;
        }
      }
    }

    private void SwitchPresenter_Unloaded( object sender, RoutedEventArgs e )
    {
      this.SwitchParent = null;
    }

    private void SwitchPresenter_Loaded( object sender, RoutedEventArgs e )
    {
      if( _switchRoot == null )
      {
        this.SwitchParent = VisualTreeHelperEx.FindAncestorByType( this, typeof( SwitchPanel ), false ) as SwitchPanel;
      }
    }

    #region Private Fields

    // track our topmost ancestor that is the direct child of the SwitchPanel
    internal UIElement _switchRoot;
    internal Dictionary<string, FrameworkElement> _knownIDs = new();

    private readonly ContentPresenter _contentPresenter = new();
    private bool _isMeasured;

    private DataTemplate _currentTemplate;

    #endregion

    #region DelaySwitchParams Nested Type

    private struct DelaySwitchParams
    {
      public DataTemplate Template;
      public bool BeginAnimation;
    }

    #endregion
  }
}

