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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit
{
    public class MagnifierAdorner : Adorner
    {
        #region Members

        private readonly Magnifier _magnifier;
        private Point _currentMousePosition;
        private double _currentZoomFactor;

        #endregion

        #region Constructors

        public MagnifierAdorner(UIElement element, Magnifier magnifier)
          : base(element)
        {
            _magnifier = magnifier;
            _currentZoomFactor = _magnifier.ZoomFactor;
            UpdateViewBox();
            AddVisualChild(_magnifier);

            Loaded += (_, _) => InputManager.Current.PostProcessInput += OnProcessInput;
            Unloaded += (_, _) => InputManager.Current.PostProcessInput -= OnProcessInput;
        }


        #endregion

        #region Private/Internal methods

        private void OnProcessInput(object sender, ProcessInputEventArgs e)
        {
            if (!IsLoaded)
            {
                // Avoids System.ComponentModel.Win32Exception in PresentationCore.dll when the window unloaded and the mouse moves.
                return;
            }

            Point pt;
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                pt = Mouse.GetPosition(this);

                if (DoubleHelper.AreVirtuallyEqual(_currentMousePosition, pt) && DoubleHelper.AreVirtuallyEqual(_magnifier.ZoomFactor, _currentZoomFactor))
                    return;

                if (_magnifier.IsFrozen)
                    return;

                _currentMousePosition = pt;
                _currentZoomFactor = _magnifier.ZoomFactor;
                UpdateViewBox();
                InvalidateArrange();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        internal void UpdateViewBox()
        {
            try
            {
                var viewBoxLocation = CalculateViewBoxLocation();
                _magnifier.ViewBox = new(viewBoxLocation, _magnifier.ViewBox.Size);
            }
            catch
            {
                // ignore
            }
        }

        private Point CalculateViewBoxLocation()
        {
            double left = 0;
            double top = 0;

            this.Dispatcher.BeginInvoke(new Action(() =>
             {
                 Point adorner = Mouse.GetPosition(this);
                 Point element = Mouse.GetPosition(AdornedElement);

                 var offsetX = element.X - adorner.X;
                 var offsetY = element.Y - adorner.Y;

                 //An element will use the offset from its parent (StackPanel, Grid, etc.) to be rendered.
                 //When this element is put in a VisualBrush, the element will draw with that offset applied. 
                 //To fix this: we add that parent offset to Magnifier location.
                 Vector parentOffsetVector = VisualTreeHelper.GetOffset(_magnifier.Target);
                 Point parentOffset = new(parentOffsetVector.X, parentOffsetVector.Y);

                 left = _currentMousePosition.X - ((_magnifier.ViewBox.Width / 2) + offsetX) + parentOffset.X;
                 top = _currentMousePosition.Y - ((_magnifier.ViewBox.Height / 2) + offsetY) + parentOffset.Y;

             }), System.Windows.Threading.DispatcherPriority.Background);
            return new(left, top);
        }

        #endregion

        #region Overrides

        protected override Visual GetVisualChild(int index)
        {
            return _magnifier;
        }

        protected override int VisualChildrenCount => 1;

        protected override Size MeasureOverride(Size constraint)
        {
            _magnifier.Measure(constraint);
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double x = _currentMousePosition.X - (_magnifier.Width / 2);
            double y = _currentMousePosition.Y - (_magnifier.Height / 2);
            _magnifier.Arrange(new(x, y, _magnifier.Width, _magnifier.Height));
            return base.ArrangeOverride(finalSize);
        }

        #endregion
    }
}
