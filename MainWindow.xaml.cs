using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ManagedNativeWifi;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace NetworkWidget
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer = new();
        private WinForms.NotifyIcon? _trayIcon;
        private bool _exiting;
        private string? _selectedAdapterId;

        private sealed class AdapterOption
        {
            public required string Id { get; init; }
            public required string Name { get; init; }
            public override string ToString() => Name;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Must happen once the native HWND exists (SourceInitialized), before the
            // window is shown, so it never flashes square corners or a light system border.
            SourceInitialized += (_, _) => DwmHelper.ApplyDarkRoundedStyling(this);

            // With SizeToContent="Height", ActualHeight isn't final until after layout,
            // so position once the window has been laid out rather than at SourceInitialized.
            Loaded += (_, _) => PositionNearCursor();

            SetupTrayIcon();

            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += (_, _) => RefreshNetworkInfo();
            _timer.Start();

            RefreshNetworkInfo();
        }

        // Screen.WorkingArea is in physical pixels; Window.Left/Top are DPI-independent
        // units, so on a scaled monitor the two must not be mixed directly or the window
        // ends up placed outside every monitor's bounds while still reporting as visible.
        private void PositionNearCursor()
        {
            var area = WinForms.Screen.FromPoint(WinForms.Cursor.Position).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = area.Right / dpi.DpiScaleX - ActualWidth - 20;
            Top = area.Bottom / dpi.DpiScaleY - ActualHeight - 20;
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

        // -------------------------------------------------------------
        // Tray icon
        // -------------------------------------------------------------

        private void SetupTrayIcon()
        {
            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule!.FileName!),
                Text = "Network Widget",
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Show/Hide", null, (_, _) => ToggleVisibility());
            menu.Items.Add("Refresh Now", null, (_, _) => RefreshNetworkInfo());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => ExitApp());

            _trayIcon.ContextMenuStrip = menu;

            // NotifyIcon.Click fires for every mouse button, which would fight with the
            // right-click context menu; MouseClick exposes which button was actually used.
            _trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    ToggleVisibility();
                }
            };
        }

        private SettingsWindow? _settingsWindow;

        private void OpenSettings()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
                Activate();
            }
        }

        private void ExitApp()
        {
            _exiting = true;
            _timer.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!_exiting)
            {
                e.Cancel = true;
                Hide();
            }
        }

        // -------------------------------------------------------------
        // Network info gathering
        // -------------------------------------------------------------

        private void RefreshNetworkInfo()
        {
            try
            {
                UpdateAdapterList();
                UpdateIpInfo();
                UpdateWifiInfo();
                txtUpdated.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Text = $"Error refreshing: {ex.Message}";
            }
        }

        private static System.Collections.Generic.IEnumerable<NetworkInterface> GetActiveInterfaces() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            // Hyper-V virtual switches ("vEthernet (...)") clutter the
                            // adapter list without being anything a user would pick.
                            && !n.Name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase));

        private static int AdapterTypePriority(NetworkInterface nic) => nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => 0,
            NetworkInterfaceType.Wireless80211 => 1,
            _ => 2,
        };

        // Keeps the dropdown in sync with whatever adapters are currently up, without
        // clobbering the user's selection on every 5s tick unless it's no longer valid.
        private void UpdateAdapterList()
        {
            var options = GetActiveInterfaces()
                .Select(n => new AdapterOption { Id = n.Id, Name = n.Name })
                .ToList();

            if (options.Count == 0)
            {
                cmbAdapter.ItemsSource = null;
                _selectedAdapterId = null;
                return;
            }

            if (_selectedAdapterId == null || !options.Any(o => o.Id == _selectedAdapterId))
            {
                // Prefer whichever of Ethernet/WiFi actually has a working route, wired
                // over wireless - matches how Windows itself deprioritizes WiFi once a
                // cable is plugged in, so this naturally tracks "the one really in use"
                // rather than whatever GetAllNetworkInterfaces() happens to list first.
                var defaultNic = GetActiveInterfaces()
                    .Where(n => n.GetIPProperties().GatewayAddresses
                        .Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    .OrderBy(AdapterTypePriority)
                    .FirstOrDefault();
                _selectedAdapterId = defaultNic?.Id ?? options[0].Id;
            }

            cmbAdapter.ItemsSource = options;
            cmbAdapter.SelectedItem = options.FirstOrDefault(o => o.Id == _selectedAdapterId);
        }

        private void cmbAdapter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbAdapter.SelectedItem is AdapterOption opt)
            {
                _selectedAdapterId = opt.Id;
                UpdateIpInfo();
            }
        }

        private void UpdateIpInfo()
        {
            var nic = GetActiveInterfaces().FirstOrDefault(n => n.Id == _selectedAdapterId);

            if (nic == null)
            {
                txtIPv4.Text = "-";
                txtSubnet.Text = "-";
                txtGateway.Text = "-";
                txtDNS.Text = "-";
                txtMAC.Text = "-";
                return;
            }

            var props = nic.GetIPProperties();
            var v4 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            var gateway = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            var dnsServers = props.DnsAddresses
                .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(d => d.ToString());

            txtIPv4.Text = v4?.Address.ToString() ?? "-";
            txtSubnet.Text = $"/{v4?.PrefixLength ?? 0}";
            txtGateway.Text = gateway?.Address.ToString() ?? "-";
            txtDNS.Text = string.Join(", ", dnsServers);
            txtMAC.Text = FormatMac(nic.GetPhysicalAddress().ToString());
        }

        private static string FormatMac(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length != 12) return raw;
            return string.Join(":", Enumerable.Range(0, 6).Select(i => raw.Substring(i * 2, 2)));
        }

        private void UpdateWifiInfo()
        {
            var output = RunCommand("netsh", "wlan show interfaces");
            var props = ParseNetshBlock(output);

            if (props.TryGetValue("State", out var state) && state.Equals("connected", StringComparison.OrdinalIgnoreCase))
            {
                txtSSID.Text = props.GetValueOrDefault("SSID", "-");
                txtSignal.Text = props.GetValueOrDefault("Signal", "-");
                txtRSSI.Text = GetRssiText();
                txtChannel.Text = props.GetValueOrDefault("Channel", "-");
                txtRadio.Text = props.GetValueOrDefault("Radio type", "-");
            }
            else
            {
                txtSSID.Text = "not connected";
                txtSignal.Text = "-";
                txtRSSI.Text = "-";
                txtChannel.Text = "-";
                txtRadio.Text = "-";
            }
        }

        // netsh only exposes signal strength as a %; the real dBm figure comes from the
        // WLAN BSS list via the Native Wifi API, so query that directly instead of
        // approximating dBm from the percentage.
        private static string GetRssiText()
        {
            try
            {
                var iface = NativeWifi.EnumerateInterfaces()
                    .FirstOrDefault(i => i.State == InterfaceState.Connected);
                if (iface == null) return "-";

                var (result, rssi) = NativeWifi.GetRssi(iface.Id);
                return result == ActionResult.Success ? $"{rssi} dBm" : "-";
            }
            catch
            {
                return "-";
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> ParseNetshBlock(string raw)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var line in raw.Split('\n'))
            {
                var m = Regex.Match(line, @"^\s+(?<name>[^:]+):\s?(?<value>.*)$");
                if (m.Success)
                {
                    var key = m.Groups["name"].Value.Trim();
                    var val = m.Groups["value"].Value.Trim();
                    if (!result.ContainsKey(key)) result[key] = val;
                }
            }
            return result;
        }

        private static string RunCommand(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            string output = proc!.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output;
        }

        // -------------------------------------------------------------
        // Release & Renew (elevated via a pre-registered Scheduled Task,
        // so only the one-time task registration prompts for UAC - not
        // every click)
        // -------------------------------------------------------------

        private const string RenewTaskName = "NetworkWidget_ReleaseRenew";
        private static readonly string RenewLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "netwidget_renew.log");
        private static readonly string RenewScriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "netwidget_renew_task.bat");

        private async void btnRenew_Click(object sender, RoutedEventArgs e)
        {
            var adapterName = (cmbAdapter.SelectedItem as AdapterOption)?.Name;
            if (adapterName == null)
            {
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Text = "No adapter selected";
                return;
            }

            txtStatus.Foreground = System.Windows.Media.Brushes.Orange;
            txtStatus.Text = "Releasing and renewing...";
            btnRenew.IsEnabled = false;

            try
            {
                // All of this (elevation prompt, process launches, and the poll loop
                // below) is blocking I/O - it must run off the UI thread or the whole
                // window stops pumping messages and appears to freeze/blank out.
                //
                // The script's content (which adapter it targets) is rewritten on every
                // click - only the scheduled task itself is a one-time, admin-gated setup.
                bool registered = await System.Threading.Tasks.Task.Run(() =>
                {
                    WriteRenewScript(adapterName);
                    return EnsureRenewTaskRegistered();
                });
                if (!registered)
                {
                    txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                    txtStatus.Text = "Setup cancelled";
                    btnRenew.IsEnabled = true;
                    return;
                }

                await System.Threading.Tasks.Task.Run(() =>
                {
                    RunCommand("schtasks", $"/run /tn \"{RenewTaskName}\"");

                    // schtasks /run queues the task and returns immediately, so poll
                    // until it's no longer "Running" before refreshing (max ~15s).
                    for (int i = 0; i < 30; i++)
                    {
                        System.Threading.Thread.Sleep(500);
                        var status = RunCommand("schtasks", $"/query /tn \"{RenewTaskName}\" /fo LIST");
                        if (!status.Contains("Running", StringComparison.OrdinalIgnoreCase)) break;
                    }
                });

                txtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                txtStatus.Text = "Renewed successfully";
            }
            catch (Exception ex)
            {
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Text = $"Failed: {ex.Message}";
            }

            btnRenew.IsEnabled = true;
            RefreshNetworkInfo();
        }

        private static bool RenewTaskExists()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{RenewTaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc!.WaitForExit();
            return proc.ExitCode == 0;
        }

        // Redirection/&&/quoting doesn't survive being embedded inline in schtasks' /tr
        // value, so the release/renew logic lives in a script file instead and /tr just
        // points at that single, stable path - only the file's content changes per click.
        private static void WriteRenewScript(string adapterName)
        {
            System.IO.File.WriteAllText(RenewScriptPath,
                "@echo off\r\n" +
                $"ipconfig /release \"{adapterName}\" > \"{RenewLogPath}\" 2>&1\r\n" +
                $"ipconfig /renew \"{adapterName}\" >> \"{RenewLogPath}\" 2>&1\r\n");
        }

        private static bool EnsureRenewTaskRegistered()
        {
            if (RenewTaskExists()) return true;

            // /sc ONCE with a start date far in the past registers the task without
            // it ever firing on its own; it only runs when triggered via /run. The
            // doubled inner quotes around the path are schtasks' documented syntax
            // for a /tr target whose path may contain spaces.
            var createArgs = $"/create /tn \"{RenewTaskName}\" /tr \"\\\"{RenewScriptPath}\\\"\" /sc ONCE /sd 01/01/2020 /st 00:00 /rl HIGHEST /f";

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = createArgs,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using var proc = Process.Start(psi);
                proc!.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // UAC prompt was cancelled
                return false;
            }
        }
    }
}
