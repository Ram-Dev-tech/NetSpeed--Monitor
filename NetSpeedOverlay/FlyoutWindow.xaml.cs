using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NetSpeedOverlay
{
    public partial class FlyoutWindow : Window
    {
        // --- Win32 Interop Constants ---
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_ROUND = 2;               // Rounded corners for flyouts
        private const int DWMSBT_TRANSIENTBACKDROP = 3;   // Acrylic backdrop

        // --- Win32 API Imports ---
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // --- Fields ---
        private DispatcherTimer _updateTimer;

        // --- Constructor ---
        public FlyoutWindow()
        {
            InitializeComponent();
        }

        // --- Window Initialization ---
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplyFluentDesign();
            UpdateInterfaceInfo();
        }

        private void ApplyFluentDesign()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;

                // Set Windows 11 Acrylic Backdrop
                int backdropValue = DWMSBT_TRANSIENTBACKDROP;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropValue, Marshal.SizeOf(typeof(int)));

                // Set Windows 11 Standard Rounded Corners (slightly larger than small corners, matches flyouts)
                int cornerValue = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerValue, Marshal.SizeOf(typeof(int)));
            }
            catch (Exception)
            {
                // Fallback gracefully on older Windows builds
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Start the update timer which runs only while the flyout is visible
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500); // Poll slightly faster for snappy response
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            // Perform initial update immediately
            UpdateStatsDisplay();
        }

        // --- Display Repositioning on Show ---
        public void ShowFlyout()
        {
            UpdateInterfaceInfo();

            // Place Flyout just above the System Tray / Bottom-Right corner of the working area
            double workAreaWidth = SystemParameters.WorkArea.Width;
            double workAreaHeight = SystemParameters.WorkArea.Height;
            double workAreaLeft = SystemParameters.WorkArea.Left;
            double workAreaTop = SystemParameters.WorkArea.Top;

            double margin = 12; // Gap from screen edge

            this.Left = workAreaLeft + workAreaWidth - this.Width - margin;
            this.Top = workAreaTop + workAreaHeight - this.Height - margin;

            this.Show();
            this.Activate(); // Make it active so clicking outside causes Deactivated to fire

            if (_updateTimer != null && !_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
        }

        // --- Auto-Hide when Focus Lost ---
        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
            if (_updateTimer != null)
            {
                _updateTimer.Stop(); // Stop polling when hidden to conserve CPU
            }
        }

        // --- Query Network Details ---
        private void UpdateInterfaceInfo()
        {
            string interfaceName = "en0";
            string ipAddress = "127.0.0.1";

            try
            {
                System.Net.NetworkInformation.NetworkInterface[] interfaces = 
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                for (int i = 0; i < interfaces.Length; i++)
                {
                    System.Net.NetworkInformation.NetworkInterface ni = interfaces[i];

                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                    {
                        interfaceName = ni.Name;
                        
                        System.Net.NetworkInformation.UnicastIPAddressInformationCollection ipAddresses = 
                            ni.GetIPProperties().UnicastAddresses;

                        for (int j = 0; j < ipAddresses.Count; j++)
                        {
                            System.Net.NetworkInformation.UnicastIPAddressInformation ip = ipAddresses[j];
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                ipAddress = ip.Address.ToString();
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback values on query exception
            }

            // Truncate interface name if it is too long (e.g. detailed Windows Ethernet controller descriptions)
            if (interfaceName.Length > 12)
            {
                interfaceName = interfaceName.Substring(0, 10) + "..";
            }

            TxtInterface.Text = string.Format("{0} • {1}", interfaceName, ipAddress);
        }

        // --- Real-time Updates ---
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatsDisplay();
        }

        private void UpdateStatsDisplay()
        {
            if (MainWindow.Instance == null || MainWindow.Instance.Monitor == null) return;

            SpeedMonitor monitor = MainWindow.Instance.Monitor;
            bool useBits = MainWindow.Instance.UseBits;

            // Update Speeds
            TxtDlSpeed.Text = MainWindow.FormatSpeed(monitor.CurrentDownloadSpeed, useBits);
            TxtUlSpeed.Text = MainWindow.FormatSpeed(monitor.CurrentUploadSpeed, useBits);

            // Update Session Totals
            TxtDlTotal.Text = string.Format("Total: {0}", FormatDataSize(monitor.TotalDownloadBytes));
            TxtUlTotal.Text = string.Format("Total: {0}", FormatDataSize(monitor.TotalUploadBytes));
        }

        private static string FormatDataSize(long bytes)
        {
            if (bytes < 1024)
            {
                return string.Format("{0} B", bytes);
            }
            double kb = bytes / 1024.0;
            if (kb < 1024)
            {
                return string.Format("{0:0.0} KB", kb);
            }
            double mb = kb / 1024.0;
            if (mb < 1024)
            {
                return string.Format("{0:0.0} MB", mb);
            }
            double gb = mb / 1024.0;
            return string.Format("{0:0.00} GB", gb);
        }

        // --- Button Click Actions ---

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || MainWindow.Instance == null) return;

            ContextMenu menu = new ContextMenu();
            int currentInterval = MainWindow.Instance.UpdateIntervalMs;

            MenuItem item1 = new MenuItem();
            item1.Header = "0.5 Seconds";
            item1.IsChecked = (currentInterval == 500);
            item1.Click += (s, ev) => MainWindow.Instance.SetUpdateInterval(500);
            menu.Items.Add(item1);

            MenuItem item2 = new MenuItem();
            item2.Header = "1.0 Second (Default)";
            item2.IsChecked = (currentInterval == 1000);
            item2.Click += (s, ev) => MainWindow.Instance.SetUpdateInterval(1000);
            menu.Items.Add(item2);

            MenuItem item3 = new MenuItem();
            item3.Header = "2.0 Seconds";
            item3.IsChecked = (currentInterval == 2000);
            item3.Click += (s, ev) => MainWindow.Instance.SetUpdateInterval(2000);
            menu.Items.Add(item3);

            MenuItem item4 = new MenuItem();
            item4.Header = "5.0 Seconds";
            item4.IsChecked = (currentInterval == 5000);
            item4.Click += (s, ev) => MainWindow.Instance.SetUpdateInterval(5000);
            menu.Items.Add(item4);

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void BtnUnits_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || MainWindow.Instance == null) return;

            ContextMenu menu = new ContextMenu();
            bool useBits = MainWindow.Instance.UseBits;

            MenuItem item1 = new MenuItem();
            item1.Header = "Bytes/s (KB/s, MB/s) [Default]";
            item1.IsChecked = !useBits;
            item1.Click += (s, ev) => {
                MainWindow.Instance.UseBits = false;
                UpdateStatsDisplay();
                MainWindow.Instance.ForceTimerTick();
            };
            menu.Items.Add(item1);

            MenuItem item2 = new MenuItem();
            item2.Header = "Bits/s (kbps, Mbps)";
            item2.IsChecked = useBits;
            item2.Click += (s, ev) => {
                MainWindow.Instance.UseBits = true;
                UpdateStatsDisplay();
                MainWindow.Instance.ForceTimerTick();
            };
            menu.Items.Add(item2);

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void BtnPosition_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || MainWindow.Instance == null) return;

            ContextMenu menu = new ContextMenu();
            bool isGhost = MainWindow.Instance.IsGhostMode;
            MainWindow.Position currentPos = MainWindow.Instance.CurrentPosition;

            MenuItem itemLock = new MenuItem();
            itemLock.Header = "Lock Position (Ghost)";
            itemLock.IsChecked = isGhost;
            itemLock.Click += (s, ev) => MainWindow.Instance.SetGhostMode(true);
            menu.Items.Add(itemLock);

            MenuItem itemUnlock = new MenuItem();
            itemUnlock.Header = "Unlock Position (Drag)";
            itemUnlock.IsChecked = !isGhost;
            itemUnlock.Click += (s, ev) => MainWindow.Instance.SetGhostMode(false);
            menu.Items.Add(itemUnlock);

            menu.Items.Add(new Separator());

            MenuItem taskbar = new MenuItem();
            taskbar.Header = "Snap: Taskbar (Default)";
            taskbar.IsChecked = (currentPos == MainWindow.Position.TaskbarRight);
            taskbar.Click += (s, ev) => MainWindow.Instance.SnapTo(MainWindow.Position.TaskbarRight);
            menu.Items.Add(taskbar);

            MenuItem br = new MenuItem();
            br.Header = "Snap: Desktop Bottom-Right";
            br.IsChecked = (currentPos == MainWindow.Position.BottomRight);
            br.Click += (s, ev) => MainWindow.Instance.SnapTo(MainWindow.Position.BottomRight);
            menu.Items.Add(br);

            MenuItem bl = new MenuItem();
            bl.Header = "Snap: Desktop Bottom-Left";
            bl.IsChecked = (currentPos == MainWindow.Position.BottomLeft);
            bl.Click += (s, ev) => MainWindow.Instance.SnapTo(MainWindow.Position.BottomLeft);
            menu.Items.Add(bl);

            MenuItem tr = new MenuItem();
            tr.Header = "Snap: Desktop Top-Right";
            tr.IsChecked = (currentPos == MainWindow.Position.TopRight);
            tr.Click += (s, ev) => MainWindow.Instance.SnapTo(MainWindow.Position.TopRight);
            menu.Items.Add(tr);

            MenuItem tl = new MenuItem();
            tl.Header = "Snap: Desktop Top-Left";
            tl.IsChecked = (currentPos == MainWindow.Position.TopLeft);
            tl.Click += (s, ev) => MainWindow.Instance.SnapTo(MainWindow.Position.TopLeft);
            menu.Items.Add(tl);

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null && MainWindow.Instance.Monitor != null)
            {
                MainWindow.Instance.Monitor.ResetTotals();
                UpdateStatsDisplay();
                MessageBox.Show("Session network statistics have been reset.", "NetSpeed Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            string message = "NetSpeed Monitor v1.0\r\n\r\n" +
                             "A Fluent Windows 11 Desktop Speed Overlay.\r\n" +
                             "Calculates real-time data rates across active adapters.\r\n\r\n" +
                             "Designed with Acrylic glass and native snap properties.";
            MessageBox.Show(message, "About NetSpeed Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnQuit_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.Close();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }
}
