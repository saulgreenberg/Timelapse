using System.Windows;
using Timelapse.Util;

namespace Timelapse.Editor
{
    public static class EditorWindowExtensions
    {
        private static readonly DependencyProperty ChoiceListProperty =
            DependencyProperty.RegisterAttached("ChoiceList", typeof(string), typeof(EditorWindowExtensions), new PropertyMetadata(default(string)));

        public static void SetChoiceList(UIElement element, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(element, nameof(element));
            element.SetValue(ChoiceListProperty, value);
        }

        public static string GetChoiceList(UIElement element)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(element, nameof(element));
            return (string)element.GetValue(ChoiceListProperty);
        }
    }
}
