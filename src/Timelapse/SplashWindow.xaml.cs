using System;
using System.Windows.Threading;

namespace Timelapse
{
    // Lightweight window shown immediately at startup while the (heavier) main TimelapseWindow
    // is constructed and loaded. Closed by App.OnStartup once the main window has rendered.
    public partial class SplashWindow
    {
        private readonly DispatcherTimer dotsTimer;
        private int dotCount = 1;

        public SplashWindow()
        {
            InitializeComponent();

            dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            dotsTimer.Tick += (_, _) =>
            {
                dotCount = dotCount % 3 + 1;
                DotsText.Text = new string('.', dotCount);
            };
            dotsTimer.Start();
            Closed += (_, _) => dotsTimer.Stop();
        }
    }
}
