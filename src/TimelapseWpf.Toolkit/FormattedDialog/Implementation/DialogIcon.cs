using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TimelapseWpf.Toolkit
{
    public enum DialogIconType
    {
        None,
        Error,
        Information,
        Warning,
        Question
    }

    public static class DialogIconFactory
    {
        public static FrameworkElement CreateIcon(DialogIconType iconType, double size = 32)
        {
            return iconType switch
            {
                DialogIconType.Error => CreateErrorIcon(size),
                DialogIconType.Information => CreateInformationIcon(size),
                DialogIconType.Warning => CreateWarningIcon(size),
                DialogIconType.Question => CreateQuestionIcon(size),
                _ => null
            };
        }

        private static System.Windows.Controls.Canvas CreateErrorIcon(double size)
        {
            var canvas = new System.Windows.Controls.Canvas
            {
                Width = size,
                Height = size
            };

            // Red circle background
            var circle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.Red,
                Stroke = Brushes.DarkRed,
                StrokeThickness = 2
            };
            canvas.Children.Add(circle);

            // White X
            var line1 = new Line
            {
                X1 = size * 0.25,
                Y1 = size * 0.25,
                X2 = size * 0.75,
                Y2 = size * 0.75,
                Stroke = Brushes.White,
                StrokeThickness = 3
            };
            canvas.Children.Add(line1);

            var line2 = new Line
            {
                X1 = size * 0.75,
                Y1 = size * 0.25,
                X2 = size * 0.25,
                Y2 = size * 0.75,
                Stroke = Brushes.White,
                StrokeThickness = 3
            };
            canvas.Children.Add(line2);

            return canvas;
        }

        private static System.Windows.Controls.Canvas CreateInformationIcon(double size)
        {
            var canvas = new System.Windows.Controls.Canvas
            {
                Width = size,
                Height = size
            };

            // Blue circle background
            var circle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.DodgerBlue,
                Stroke = Brushes.DarkBlue,
                StrokeThickness = 2
            };
            canvas.Children.Add(circle);

            // White "i"
            var dot = new Ellipse
            {
                Width = size * 0.15,
                Height = size * 0.15,
                Fill = Brushes.White
            };
            System.Windows.Controls.Canvas.SetLeft(dot, size * 0.425);
            System.Windows.Controls.Canvas.SetTop(dot, size * 0.2);
            canvas.Children.Add(dot);

            var line = new Rectangle
            {
                Width = size * 0.1,
                Height = size * 0.4,
                Fill = Brushes.White
            };
            System.Windows.Controls.Canvas.SetLeft(line, size * 0.45);
            System.Windows.Controls.Canvas.SetTop(line, size * 0.4);
            canvas.Children.Add(line);

            return canvas;
        }

        private static System.Windows.Controls.Canvas CreateWarningIcon(double size)
        {
            var canvas = new System.Windows.Controls.Canvas
            {
                Width = size,
                Height = size
            };

            // Yellow triangle background
            var triangle = new Polygon
            {
                Fill = Brushes.Gold,
                Stroke = Brushes.Orange,
                StrokeThickness = 2,
                Points =
                [
                    new(size * 0.5, size * 0.1),
                    new(size * 0.1, size * 0.9),
                    new(size * 0.9, size * 0.9)
                ]
            };
            canvas.Children.Add(triangle);

            // Black exclamation mark
            var line = new Rectangle
            {
                Width = size * 0.08,
                Height = size * 0.35,
                Fill = Brushes.Black
            };
            System.Windows.Controls.Canvas.SetLeft(line, size * 0.46);
            System.Windows.Controls.Canvas.SetTop(line, size * 0.25);
            canvas.Children.Add(line);

            var dot = new Ellipse
            {
                Width = size * 0.1,
                Height = size * 0.1,
                Fill = Brushes.Black
            };
            System.Windows.Controls.Canvas.SetLeft(dot, size * 0.45);
            System.Windows.Controls.Canvas.SetTop(dot, size * 0.7);
            canvas.Children.Add(dot);

            return canvas;
        }

        private static System.Windows.Controls.Canvas CreateQuestionIcon(double size)
        {
            var canvas = new System.Windows.Controls.Canvas
            {
                Width = size,
                Height = size
            };

            // Green circle background
            var circle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.MediumSeaGreen,
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 2
            };
            canvas.Children.Add(circle);

            // White question mark using TextBlock for better rendering
            var questionMark = new System.Windows.Controls.TextBlock
            {
                Text = "?",
                FontSize = size * 0.6,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Canvas.SetLeft(questionMark, size * 0.32);
            System.Windows.Controls.Canvas.SetTop(questionMark, size * 0.15);
            canvas.Children.Add(questionMark);

            return canvas;
        }
    }
}
