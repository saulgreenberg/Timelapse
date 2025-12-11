using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Control = System.Windows.Controls.Control;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Style = System.Windows.Style;

namespace Timelapse.ControlsMetadata
{
    // Two abstract classes are defined in this file:
    // - MetadataDataEntryControl defines Metadata-specific aspects (extends DataEntryControlBase)
    // - MetadataDataEntryControl<TContent, TLabel> defines the typed label and control
    //
    // Shared logic is in DataEntryControlBase (ControlsCore namespace)

    /// <summary>
    /// Metadata-specific base class.
    /// Extends DataEntryControlBase with Metadata-only features:
    /// - ControlType property
    /// - ParentPanel property
    /// - Tooltip property
    /// </summary>
    public abstract class MetadataDataEntryControl : DataEntryControlBase
    {
        #region Metadata-Specific Properties
        public string Tooltip { get; set; }
        public string ControlType { get; set; }
        public MetadataDataEntryPanel ParentPanel { get; set; }
        #endregion

        #region Constructor
        protected MetadataDataEntryControl(MetadataControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(control, nameof(control));
        }
        #endregion
    }

    /// <summary>
    /// Generic Metadata control with typed content and label.
    /// Contains Metadata-specific implementation details.
    /// </summary>
    public abstract class MetadataDataEntryControl<TContent, TLabel> : MetadataDataEntryControl
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        #region Properties
        // These properties are needed for concrete typed access, even though similar properties exist in base
        public TContent ContentControl { get; }
        public TLabel LabelControl { get; }

        /// <summary>Gets the control label's value</summary>
        public string Label => (string)LabelControl.Content;

        /// <summary>Gets or sets the width of the content control</summary>
        public int Width
        {
            get => (int)ContentControl.Width;
            set => ContentControl.Width = value;
        }

        /// <summary>Gets or sets whether this control is enabled or disabled</summary>
        public bool IsEnabled
        {
            get => Container.IsEnabled;
            set
            {
                ContentControl.IsEnabled = value;
                LabelControl.IsEnabled = value;
                Container.IsEnabled = value;
                ContentControl.Foreground = value ? Brushes.Black : Brushes.DimGray;
            }
        }

        /// <summary>Gets or sets a value indicating whether the control's content is user editable</summary>
        public override bool ContentReadOnly
        {
            get => GetContentControlReadOnly();
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    SetContentControlReadOnly(true);
                    ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    SetContentControlReadOnly(value);
                }
            }
        }

        /// <summary>
        /// Gets the readonly state of the content control.
        /// Override this if your control uses a different property name.
        /// </summary>
        protected virtual bool GetContentControlReadOnly()
        {
            // Most controls have an IsReadOnly property
            dynamic control = ContentControl;
            return control.IsReadOnly;
        }

        /// <summary>
        /// Sets the readonly state of the content control.
        /// Override this if your control uses a different property name or needs special handling.
        /// </summary>
        protected virtual void SetContentControlReadOnly(bool isReadOnly)
        {
            // Most controls have an IsReadOnly property
            dynamic control = ContentControl;
            control.IsReadOnly = isReadOnly;
        }
        #endregion

        #region Constructor
        protected MetadataDataEntryControl(MetadataControlRow control, DataEntryControls styleProvider, ControlContentStyleEnum? contentStyleName, ControlLabelStyleEnum labelStyleName, string tooltip)
            : base(control, styleProvider)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            Tooltip = tooltip;

            // Create content control
            ContentControl = new()
            {
                IsTabStop = true
            };
            if (contentStyleName.HasValue)
            {
                ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            ContentControl.IsEnabled = true;

            // use the content's tag to point back to this so event handlers can access the MetadataDataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), relies on this
            ContentControl.Tag = this;

            // Create the label (which is an actual label)
            LabelControl = new()
            {
                Content = control.Label,
                Style = (Style)styleProvider.FindResource(labelStyleName.ToString()),
                ToolTip = control.Tooltip,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Name = "ControlLabel",
            };

            // add the label and content to the stack panel
            Container.Children.Add(LabelControl);
            Container.Children.Add(ContentControl);
            //Container.PreviewKeyDown += Container_PreviewKeyDown;
            LabelControl.MouseDown += LabelControl_MouseDown;
        }
        #endregion

        #region Keyboard Event Handling and Focus
        // Metadata-specific keyboard handling
        // Shortcut keys are ignored when using controls in the MetadataEntryPanel
        //private void Container_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        //{
        //    if (IsCondition.IsKeyLeftRightArrow(keyEvent.Key))
        //    {
        //        // noop, as otherwise interpretted as tab
        //        keyEvent.Handled = true;
        //    }
        //}

        public override IInputElement Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with TimelapseWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, ContentControl);
            return ContentControl;
        }

        // If we click on the label, the content control becomes the focus
        private void LabelControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ContentControl.Focus();
            e.Handled = true;
        }
        #endregion

        #region Visual Effects
        // Flash the content area of the control (Metadata-specific implementation)
        private ScrollViewer MainDisplayField;
        public void FlashContentControl()
        {
            MainDisplayField ??= (ScrollViewer)ContentControl.Template.FindName("PART_ContentHost", ContentControl);
            if (MainDisplayField == null) return;
            MainDisplayField.Background = new SolidColorBrush(Colors.White);
            MainDisplayField.Background.BeginAnimation(SolidColorBrush.ColorProperty, GetColorAnimation());
        }

        // This is a standard color animation scheme that can be accessed by the other controls
        protected ColorAnimation GetColorAnimation()
        {
            return new()
            {
                From = Colors.LightCoral,
                AutoReverse = false,
                Duration = new(TimeSpan.FromSeconds(.1)),
                EasingFunction = new ExponentialEase
                {
                    EasingMode = EasingMode.EaseIn
                },
            };
        }
        #endregion
    }
}
