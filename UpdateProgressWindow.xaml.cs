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
