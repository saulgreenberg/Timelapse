using System.Reflection;
using System.Windows.Controls;

namespace Timelapse.Controls
{
    /// <summary>
    /// A slider that provides a way to specify the auto tooltip text 
    /// The string currently held by AutoToolTipContent will be displayed. This should usually be set in the slider's ValueChanged callback so that changed values are shown
    /// Based on code supplied by Josh Smith
    /// </summary>
    public class AutoToolTipSlider : Slider
    {
        #region Public Properties and private variables
        // Gets/sets the string displayed in the auto tooltip's content.
        public string AutoToolTipContent
        {
            get;
            set
            {
                field = value;
                FormatAutoToolTipContent();
            }
        } = string.Empty;

        #endregion

        #region Constructor
        private ToolTip AutoToolTip
        {
            get
            {
                if (field == null)
                {
                    FieldInfo fieldInfo = typeof(Slider).GetField(
                        "_autoToolTip",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        field = fieldInfo.GetValue(this) as ToolTip;
                    }
                }
                return field;
            }
        }
        #endregion

        #region Private  methods
        private void FormatAutoToolTipContent()
        {
            if (AutoToolTipContent != null && AutoToolTip != null)
            {
                AutoToolTip.Content = AutoToolTipContent;
            }
        }
        #endregion
    }
}
