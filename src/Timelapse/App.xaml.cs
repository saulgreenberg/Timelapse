using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Timelapse.DebuggingSupport;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for Timelapse
    /// </summary>
    public partial class App
    {
        // Diagnostics-only safety net: logs any exception that escapes a fire-and-forget
        // Task.Run whose result was never awaited/observed (e.g. DropSessionTempTables,
        // marker UpdateFileAsync calls). On this app's target framework, an unobserved task
        // exception does not crash the process by default, so this does not change failure
        // behavior — it only ensures such a failure leaves a trace in the log instead of
        // vanishing silently.
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppLog.Warning("Unobserved task exception (a fire-and-forget background task failed without anyone observing the result).", args.Exception);
                args.SetObserved();
            };

            // TimelapseWindow is expensive to construct (AvalonDock layout, many controls) and
            // does so synchronously on the UI thread, which blocks that thread's dispatcher for
            // the whole splash-visible period. A splash created on that same thread would freeze
            // (its DispatcherTimer/animations can't tick while the dispatcher isn't pumping), so
            // the splash gets its own thread and dispatcher instead, kept independent of the
            // main window's construction time.
            SplashWindow splash = null;
            using ManualResetEventSlim splashReady = new(false);
            Thread splashThread = new(() =>
            {
                splash = new SplashWindow();
                splash.Show();
                splashReady.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true
            };
            splashThread.SetApartmentState(ApartmentState.STA);
            splashThread.Start();
            splashReady.Wait();

            TimelapseWindow mainWindow = new();
            MainWindow = mainWindow;
            mainWindow.ContentRendered += MainWindow_FirstContentRendered;
            mainWindow.Show();

            void MainWindow_FirstContentRendered(object sender, EventArgs args)
            {
                mainWindow.ContentRendered -= MainWindow_FirstContentRendered;
                splash.Dispatcher.Invoke(() =>
                {
                    splash.Close();
                    splash.Dispatcher.InvokeShutdown();
                });
            }
        }
    }
}
