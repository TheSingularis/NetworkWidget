using System.Windows;

namespace NetworkWidget
{
    public partial class UpdateProgressWindow : Window
    {
        private bool _closed;

        public UpdateProgressWindow()
        {
            InitializeComponent();

            SourceInitialized += (_, _) => DwmHelper.ApplyDarkRoundedStyling(this);
            Closed += (_, _) => _closed = true;
        }

        public void SetStatus(string text)
        {
            if (_closed) return;
            txtStatus.Text = text;
        }

        // Shows/updates a determinate progress bar for the download phase specifically
        // (0-100); the spinner keeps running throughout regardless, as a generic
        // "still working" indicator for the checking/installing phases either side of it.
        public void SetProgress(int percent)
        {
            if (_closed) return;
            progressTrack.Visibility = Visibility.Visible;
            progressFill.Width = progressTrack.ActualWidth * System.Math.Clamp(percent, 0, 100) / 100.0;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
