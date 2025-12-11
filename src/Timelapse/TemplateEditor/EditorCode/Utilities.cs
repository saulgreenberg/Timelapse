using System.Windows;
using System.Windows.Media;

namespace TimelapseTemplateEditor.EditorCode
{
    public class Utilities
    {
        // Given a UI element and a Type T, search the UI element's parents until T is found
        // e.g., a stackpanel T that encloses a UIElement button.
        public static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                if (parent is T correctlyTyped)
                {
                    return correctlyTyped;
                }
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }
    }
}
