using System.Threading.Tasks;
using System.Windows;
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
        }
    }
}
