using System;
using System.Threading.Tasks;
using System.Windows;

namespace NetworkWidget
{
    public partial class App : System.Windows.Application
    {
        public App()
        {
            // Must run before anything else - this is how Velopack intercepts its
            // own install/update/uninstall lifecycle command-line invocations and
            // exits immediately when appropriate, rather than launching the app.
            Velopack.VelopackApp.Build().Run();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var progress = new UpdateProgressWindow();
            progress.Show();

            // Capped rather than fully awaited: if GitHub is slow or unreachable, the
            // widget should still start promptly rather than hang on startup waiting
            // for a routine update check. The check itself keeps running in the
            // background either way (AppUpdater applies-and-restarts on its own if it
            // finds something after this window has already moved on).
            var checkTask = AppUpdater.CheckAndApplyAsync(status => progress.SetStatus(status));
            await Task.WhenAny(checkTask, Task.Delay(TimeSpan.FromSeconds(5)));

            progress.SetStatus("Starting widget...");
            await Task.Delay(300);

            var window = new MainWindow();

            // "Start minimized" is a persisted preference (set from the Settings pane),
            // not a launch flag, so it applies whether the app was auto-started or opened
            // by hand.
            if (!AppSettings.Load().StartMinimized)
            {
                window.Show();
            }

            progress.Close();
        }
    }
}
