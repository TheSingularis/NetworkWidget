using System.Linq;
using System.Windows;

namespace NetworkWidget
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var window = new MainWindow();

            // Launched via the "Start with Windows" registry entry, which passes
            // this flag so the widget doesn't pop up over everything at login.
            bool startMinimized = e.Args.Any(a => a.Equals("--minimized", System.StringComparison.OrdinalIgnoreCase));
            if (!startMinimized)
            {
                window.Show();
            }
        }
    }
}
