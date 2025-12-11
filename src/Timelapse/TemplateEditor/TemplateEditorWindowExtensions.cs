using System.Windows;
using Timelapse.Util;

namespace TimelapseTemplateEditor
{
    public static class TemplateEditorWindowExtensions
    {
        private static readonly DependencyProperty ChoiceListProperty =
            DependencyProperty.RegisterAttached("ChoiceList", typeof(string), typeof(TemplateEditorWindowExtensions), new(default(string)));

        public static void SetChoiceList(UIElement element, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(element, nameof(element));
            element.SetValue(ChoiceListProperty, value);
        }
    }
}
