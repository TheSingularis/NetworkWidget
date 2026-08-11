using System.Windows;

namespace NetworkWidget
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var window = new MainWindow();

            // "Start minimized" is a persisted preference (set from the Settings pane),
            // not a launch flag, so it applies whether the app was auto-started or opened
            // by hand.
            if (!AppSettings.Load().StartMinimized)
            {
                window.Show();
            }
        }
    }
}
