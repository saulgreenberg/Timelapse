using System.Windows;

namespace TimelapseTemplateEditor.EditorCode
{
    // This class tracks mouse state, which supports the drag/drop of controls
    public class MouseState
    {
        public UIElement dummyMouseDragSource = new();
        public bool isMouseDown;
        public bool isMouseDragging;
        public Point mouseDownStartPosition;
        public UIElement realMouseDragSource;
    }
}
