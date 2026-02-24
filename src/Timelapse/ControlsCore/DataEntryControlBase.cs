using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Unified base class for both DataEntry and Metadata controls.
    /// Contains all shared logic (95%+ of implementation).
    /// Mode-specific behavior is handled via virtual methods that derived classes can override.
    /// </summary>
    public abstract class DataEntryControlBase
    {
        #region Properties - Shared by all control types
        /// <summary>Gets the content control element</summary>
        public abstract UIElement GetContentControl { get; }

        /// <summary>Gets whether the content control is enabled</summary>
        public abstract bool IsContentControlEnabled { get; }

        /// <summary>Gets the value of the control</summary>
        public abstract string Content { get; }

        /// <summary>Gets or sets whether the control's content is user editable</summary>
        public abstract bool ContentReadOnly { get; set; }

        /// <summary>Gets the container that holds the control</summary>
        public StackPanel Container { get; }

        /// <summary>Gets the data label which corresponds to this control</summary>
        public string DataLabel { get; }

        /// <summary>Focus the control</summary>
        public abstract IInputElement Focus(DependencyObject focusScope);

        /// <summary>Popup preview (used by DataEntry mode, optional for Metadata)</summary>
        protected Popup PopupPreview { get; set; }
        #endregion

        #region Constructor
        protected DataEntryControlBase(CommonControlRow control, DataEntryControls styleProvider)
        {
            // Check arguments
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            // Store data label
            DataLabel = control.DataLabel;

            // Create the stack panel container
            Container = new StackPanel();
            Style style = styleProvider.FindResource(ControlStyle.StackPanelContainerStyle) as Style;
            Container.Style = style;

            // Use container's tag to point back to this for event handlers
            Container.Tag = this;
        }
        #endregion

        #region Abstract Methods - Must be implemented by derived classes
        /// <summary>Set the content and tooltip of the control</summary>
        public abstract void SetContentAndTooltip(string value);
        #endregion

        #region Virtual Methods - Can be overridden by mode-specific implementations
        // Technical note. Resharper normally flags these as unused. However, that is disabled for the following reasons.
        // Executive summary: ReSharper's analysis is technically accurate but architecturally short-sighted here. The warnings are safe to suppress. 
        // 1. The new abstract pattern depends on them. DataEntryControl uses new specifically because the base already has virtual versions     
        // (your own comment says so: // Use 'new' keyword because base class has virtual versions). The design intent is: the base provides safe
        //  no-op defaults for the whole hierarchy; DataEntryControl then overrides that contract to make them mandatory for the DataEntry       
        // branch. Delete the base virtuals and you'd need to change new abstract → abstract everywhere in DataEntryControl, and the comment     
        // would become misleading.
        // 2. They serve as the no-op default for the Metadata hierarchy. MetadataDataEntryControl doesn't override any of these four methods    
        // (the FlashContentControl() in MetadataEntryControl.cs has a different signature — no parameters). So if any code were to call these   
        // methods through a DataEntryControlBase reference on a metadata control, it safely does nothing, which is the correct behaviour.       
        // 3. They express intent at the base contract level. They declare that these behaviours exist across the whole hierarchy, even if the   
        // default is a no-op. Removing them would silently narrow the base class contract.

        /// <summary>Flash the background of the content control (DataEntry mode typically implements this)</summary>
        // ReSharper disable once UnusedMember.Global
        public virtual void FlashContentControl(FlashEnum flashEnum) { }

        /// <summary>Show a preview of what the control value would be (DataEntry mode typically implements this)</summary>
        // ReSharper disable once UnusedMember.Global
        public virtual void ShowPreviewControlValue(string value) { }

        /// <summary>Hide the preview control value (DataEntry mode typically implements this)</summary>
        // ReSharper disable once UnusedMember.Global
        public virtual void HidePreviewControlValue() { }

        /// <summary>Flash the preview control value (DataEntry mode typically implements this)</summary>
        // ReSharper disable once UnusedMember.Global
        public virtual void FlashPreviewControlValue() { }
        #endregion
    }
}
