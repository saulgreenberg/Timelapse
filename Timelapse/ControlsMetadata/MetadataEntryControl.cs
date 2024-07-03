using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Control = System.Windows.Controls.Control;
using DateTimePicker = Xceed.Wpf.Toolkit.DateTimePicker;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Style = System.Windows.Style;

namespace Timelapse.ControlsMetadata
{
    /// Two abstact classes are defined in this file:
    /// - MetadataDataEntryControl defines the base aspects of the control portion of a data entry control
    /// - MetadataDataEntryControl TContent, TLabel defines the label and actual control presented on the display

    /// <summary>
    /// Abstract class that defines the base aspects of a data entry control
    /// </summary>
    public abstract class MetadataDataEntryControl
    {
        #region MetadataDataEntryControl Properties

        /// <summary>Gets the position of the content control</summary>
        public abstract UIElement GetContentControl { get; }

        public abstract bool IsContentControlEnabled { get; }

        /// <summary>Gets the value of the control</summary>
        public abstract string Content { get; }

        /// <summary>Gets or sets a value indicating whether the control's content is user editable</summary>
        public abstract bool ContentReadOnly { get; set; }

        public string Tooltip { get; set; }

        /// <summary>Gets the container that holds the control.</summary>
        public StackPanel Container { get; }

        /// <summary>Gets the data label which corresponds to this control.</summary>
        public string DataLabel { get; }

        public string ControlType { get; set; }

        public MetadataDataEntryPanel ParentPanel { get; set; }

        public abstract IInputElement Focus(DependencyObject focusScope);

        #endregion

        #region Base constructor for all data entry controls
        protected MetadataDataEntryControl(MetadataControlRow control, DataEntryControls styleProvider)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            // populate properties from database definition of control
            // this.Content and Tooltip can't be set, however, as the caller hasn't instantiated the content control yet
            this.DataLabel = control.DataLabel;

            // Create the stack panel
            this.Container = new StackPanel();
            Style style = styleProvider.FindResource(Constant.ControlStyle.StackPanelContainerStyle) as Style;
            this.Container.Style = style;

            // use the containers's tag to point back to this so event handlers can access the DataEntryControl
            // this is needed by callbacks such as DataEntryHandler.Container_PreviewMouseRightButtonDown() and TimelapseWindow.CounterControl_MouseLeave()
            this.Container.Tag = this;
        }
        #endregion

        #region Abstract methods
        public abstract void SetContentAndTooltip(string value);

        #endregion
    }

    //    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "StyleCop limitation.")]
    /// <summary> A generic control comprises a stack panel containing 
    /// - a control containing at least a descriptive label 
    /// - another control for displaying / entering data at a given width
    /// </summary>
    public abstract class MetadataDataEntryControl<TContent, TLabel> : MetadataDataEntryControl
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        #region DataEntryControl<TContent, TLabel> Properties
        public TContent ContentControl { get; }

        /// <summary>Gets the control label's value</summary>
        public string Label => (string)this.LabelControl.Content;

        public TLabel LabelControl { get; }

        /// <summary>Gets or sets the width of the content control</summary>
        public int Width
        {
            get => (int)this.ContentControl.Width;
            set => this.ContentControl.Width = value;
        }

        // Sets or gets whether this control is enabled or disabled</summary>
        public bool IsEnabled
        {
            get => this.Container.IsEnabled;
            set
            {
                this.ContentControl.IsEnabled = value;
                this.LabelControl.IsEnabled = value;
                this.Container.IsEnabled = value;
                this.ContentControl.Foreground = value ? Brushes.Black : Brushes.DimGray;
            }
        }
        #endregion

        #region Base constructor for a DataEntryControl<,>
        protected MetadataDataEntryControl(MetadataControlRow control, DataEntryControls styleProvider, ControlContentStyleEnum? contentStyleName, ControlLabelStyleEnum labelStyleName, string tooltip) :
            base(control, styleProvider)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));
            
            this.Tooltip = tooltip;
            this.ContentControl = new TContent()
            {
                IsTabStop = true
            };
            if (contentStyleName.HasValue)
            {
                this.ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            // this.ContentReadOnly = false;
            this.ContentControl.IsEnabled = true;

            // use the content's tag to point back to this so event handlers can access the NetadataDataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), relies on this
            this.ContentControl.Tag = this;

            // Create the label (which is an actual label)
            this.LabelControl = new TLabel()
            {
                Content = control.Label,
                Style = (Style)styleProvider.FindResource(labelStyleName.ToString()),
                ToolTip = control.Tooltip,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Name = "ControlLabel",
            };

            // add the label and content to the stack panel
            this.Container.Children.Add(this.LabelControl);
            this.Container.Children.Add(this.ContentControl);
            this.Container.PreviewKeyDown += this.Container_PreviewKeyDown;
            this.LabelControl.MouseDown += LabelControl_MouseDown;
        }
        #endregion

        #region Callbacks: PreviewKeyDown and various Focus
        // We want to capture the Shift/Arrow key presses so we can navigate images. However, both the UTCOffset and the DateTime picker consume 
        // those PreviewKeyDown event. As a workaround, we attach a preview keydown to the container and take action on that.
        private void Container_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            // We are only interested in interpretting the Shift/Arrow key for the following controls
            // The DataEntryChoice and DataEntryFlags do their own previewKeyDown processing to acheive a similar effect
            if (this.ContentControl is DateTimePicker ||
                this.ContentControl is IntegerUpDown ||
                this.ContentControl is AutocompleteTextBox)
            {
                // Use the SHIFT right/left pageUp/PageDownkey to go to the next/previous image for datetimepicker
                // The right/left arrow keys normally navigate through text characters when the text is enabled.
                // which restricts the use the arrow keys to cycle through images.
                // As a work-around, we use the arrow keys for cycling through the image when:
                // - it is a read-only note (as we don't have to navigate text)
                // - the SHIFT key is held down when its not a read only note
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                if ((this.ContentReadOnly || Keyboard.Modifiers == ModifierKeys.Shift) && (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown))
                {
                    keyEvent.Handled = true;
                    GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
                }
            }
        }

        public override IInputElement Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with TimelapseWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, this.ContentControl);
            return this.ContentControl;
        }

        // If we click on the label, the content control becomes the focus
        private void LabelControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.ContentControl.Focus();
            e.Handled = true;
        }
        #endregion

        #region Visual effects
        // Flash the content area of the control
        public void FlashContentControl()
        {
            ScrollViewer contentHost = (ScrollViewer)this.ContentControl.Template.FindName("PART_ContentHost", this.ContentControl);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
            }
        }

        // This is a standard color animation scheme that can be accessed by the other controls
        protected ColorAnimation GetColorAnimation()
        {
            return new ColorAnimation()
            {
                From = Colors.LightCoral,
                AutoReverse = false,
                Duration = new Duration(TimeSpan.FromSeconds(.1)),
                EasingFunction = new ExponentialEase()
                {
                    EasingMode = EasingMode.EaseIn
                },
            };
        }
        #endregion
    }


}
