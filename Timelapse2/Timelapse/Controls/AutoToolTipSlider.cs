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
        private string autoToolTipContent = string.Empty;
        public string AutoToolTipContent
        {
            get
            {
                return this.autoToolTipContent;
            }
            set
            {
                this.autoToolTipContent = value;
                this.FormatAutoToolTipContent();
            }
        }

        private ToolTip autoToolTip;
        #endregion

        #region Constructor
        private ToolTip AutoToolTip
        {
            get
            {
                if (this.autoToolTip == null)
                {
                    FieldInfo field = typeof(Slider).GetField(
                        "_autoToolTip",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    this.autoToolTip = field.GetValue(this) as ToolTip;
                }
                return this.autoToolTip;
            }
        }
        #endregion

        #region Private  methods
        private void FormatAutoToolTipContent()
        {
            if (this.AutoToolTipContent != null && this.AutoToolTip != null)
            {
                this.AutoToolTip.Content = this.AutoToolTipContent;
            }
        }
        #endregion
    }
}
