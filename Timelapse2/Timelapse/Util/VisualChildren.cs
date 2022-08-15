using System.Windows;
using System.Windows.Media;

namespace Timelapse.Util
{
    public static class VisualChildren
    {
        #region Public Static Methods - GetVisualChild - Various Forms
        // Get the visual child of the specified type
        // Invoke by, e.g., TextBlock tb = VisualChildren.GetVisualChild<TextBlock>(somePartentUIElement);
        // Code from: http://techiethings.blogspot.com/2010/05/get-wpf-datagrid-row-and-cell.html
        public static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default;
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

        // Similar to the above, except it also considers the name of the child.
        // Get the visual child of the specified type with the matching name
        // Invoke by, e.g., TextBlock tb = VisualChildren.GetVisualChild<TextBlock>(somePartentUIElement, name);
        public static T GetVisualChild<T>(DependencyObject parent, string childName)
           where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                if (!(child is T t))
                {
                    // recursively drill down the tree
                    foundChild = GetVisualChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    // If the child's name is set for search
                    if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = t;
                        break;
                    }
                }
                else
                {
                    // child element found.
                    foundChild = t;
                    break;
                }
            }
            return foundChild;
        }

        #endregion
    }
}
