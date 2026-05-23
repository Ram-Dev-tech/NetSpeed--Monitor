using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace NetSpeedOverlay
{
    public struct SpeedResult
    {
        public double Download;
        public double Upload;

        public SpeedResult(double dl, double ul)
        {
            Download = dl;
            Upload = ul;
        }
    }

    public partial class MainWindow : Window
    {
        // --- Win32 Structures and Constants ---
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_ROUNDSMALL = 3;         // Small rounded corners (Windows 11)
        private const int DWMSBT_TRANSIENTBACKDROP = 3;   // Acrylic backdrop (Windows 11)

        // --- Win32 API Imports ---

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        // --- Fields and Public Properties ---

        public enum Position
        {
            TaskbarRight,  // Default: Embedded inside the taskbar next to system tray
            BottomRight,   // Desktop bottom-right
            BottomLeft,    // Desktop bottom-left
            TopRight,      // Desktop top-right
            TopLeft        // Desktop top-left
        }

        public static MainWindow Instance { get; private set; }

        private bool _isGhostMode = true;
        private Position _currentPosition = Position.TaskbarRight;

        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private IntPtr _hIcon = IntPtr.Zero;

        private DispatcherTimer _timer;
        private SpeedMonitor _monitor;
        private FlyoutWindow _flyoutWindow;

        // Shared Configuration
        public bool UseBits = false;
        public int UpdateIntervalMs = 1000;

        public bool IsGhostMode { get { return _isGhostMode; } }
        public Position CurrentPosition { get { return _currentPosition; } }
        public SpeedMonitor Monitor { get { return _monitor; } }

        // --- Constructor ---

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
        }

        // --- Lifecycle and Interop Initialization ---

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplyFluentDesign();

            // Set WS_EX_TOOLWINDOW so the app doesn't show up in the Alt-Tab menu
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);

            // Set up snapping and layout (Embedded in Taskbar by default)
            SnapTo(Position.TaskbarRight);

            // Initialize System Tray
            InitializeTray();

            // Enable Ghost mode by default (click-through)
            SetGhostMode(true);

            // Start Monitoring
            StartMonitoring();

            // Auto-reposition if display resolution or taskbar configuration changes
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        private void ApplyFluentDesign()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;

                // Set Windows 11 Acrylic Backdrop
                int backdropValue = DWMSBT_TRANSIENTBACKDROP;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropValue, Marshal.SizeOf(typeof(int)));

                // Set Windows 11 Small Rounded Corners
                int cornerValue = DWMWCP_ROUNDSMALL;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerValue, Marshal.SizeOf(typeof(int)));
            }
            catch (Exception)
            {
                // Fallback gracefully on older OS versions where DWM attributes are not supported
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_isGhostMode)
            {
                SnapTo(_currentPosition);
            }
        }

        // --- Ghost Mode Control ---

        public void SetGhostMode(bool ghost)
        {
            _isGhostMode = ghost;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (ghost)
            {
                // Add WS_EX_TRANSPARENT style to make it click-through
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                
                // Set borders to native, subtle glass border
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                // Remove WS_EX_TRANSPARENT style to enable clicks (interaction/dragging)
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                
                // Glow the border to visually indicate interactive drag mode
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x60, 0xCD, 0xFF));
            }
        }

        // --- Positioning and Snapping ---

        public void SnapTo(Position pos)
        {
            _currentPosition = pos;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double workAreaWidth = SystemParameters.WorkArea.Width;
            double workAreaHeight = SystemParameters.WorkArea.Height;
            double workAreaLeft = SystemParameters.WorkArea.Left;
            double workAreaTop = SystemParameters.WorkArea.Top;

            double taskbarHeight = screenHeight - workAreaHeight;
            double margin = 12; // Gap from screen edges

            switch (pos)
            {
                case Position.TaskbarRight:
                    // Center vertically in the taskbar, place next to tray icons (approx ~210px offset from right)
                    if (taskbarHeight > 0 && workAreaTop == 0) // Taskbar is at the bottom
                    {
                        this.Left = screenWidth - this.Width - 210;
                        this.Top = workAreaHeight + (taskbarHeight - this.Height) / 2;
                    }
                    else if (taskbarHeight > 0 && workAreaTop > 0) // Taskbar is at the top
                    {
                        this.Left = screenWidth - this.Width - 210;
                        this.Top = (taskbarHeight - this.Height) / 2;
                    }
                    else // Auto-hidden or side-aligned
                    {
                        this.Left = workAreaLeft + workAreaWidth - this.Width - margin;
                        this.Top = workAreaTop + workAreaHeight - this.Height - margin;
                    }
                    break;

                case Position.BottomRight:
                    this.Left = workAreaLeft + workAreaWidth - this.Width - margin;
                    this.Top = workAreaTop + workAreaHeight - this.Height - margin;
                    break;
                case Position.BottomLeft:
                    this.Left = workAreaLeft + margin;
                    this.Top = workAreaTop + workAreaHeight - this.Height - margin;
                    break;
                case Position.TopRight:
                    this.Left = workAreaLeft + workAreaWidth - this.Width - margin;
                    this.Top = workAreaTop + margin;
                    break;
                case Position.TopLeft:
                    this.Left = workAreaLeft + margin;
                    this.Top = workAreaTop + margin;
                    break;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isGhostMode && e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // --- Network Speeds Monitor Core ---

        private void StartMonitoring()
        {
            _monitor = new SpeedMonitor();
            _monitor.Update(); // Seed baseline statistics

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            SpeedResult result = _monitor.Update();
            TxtDownload.Text = FormatPillSpeed(result.Download, UseBits);
            TxtUpload.Text = FormatPillSpeed(result.Upload, UseBits);
        }

        public void SetUpdateInterval(int ms)
        {
            UpdateIntervalMs = ms;
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(ms);
            }
        }

        public void ForceTimerTick()
        {
            Timer_Tick(null, null);
        }

        // --- Custom Speed Formatting Helpers ---

        public static string FormatPillSpeed(double bytesPerSecond, bool useBits)
        {
            double val = useBits ? (bytesPerSecond * 8.0) : bytesPerSecond;
            string suffix = useBits ? "b" : "";
            string kChar = useBits ? "k" : "K";
            string mChar = useBits ? "m" : "M";

            if (val < 1024)
            {
                return string.Format("{0:0}B", val);
            }
            double kb = val / 1024.0;
            if (kb < 1024)
            {
                return string.Format("{0:0}{1}{2}", kb, kChar, suffix);
            }
            double mb = kb / 1024.0;
            if (mb < 1024)
            {
                return string.Format("{0:0.0}{1}{2}", mb, mChar, suffix);
            }
            double gb = mb / 1024.0;
            return string.Format("{0:0.0}{1}{2}", gb, useBits ? "g" : "G", suffix);
        }

        public static string FormatSpeed(double bytesPerSecond, bool useBits)
        {
            if (useBits)
            {
                double bits = bytesPerSecond * 8.0;
                if (bits < 1024)
                {
                    return string.Format("{0:0} bps", bits);
                }
                double kbps = bits / 1024.0;
                if (kbps < 1024)
                {
                    return string.Format("{0:0.0} kbps", kbps);
                }
                double mbps = kbps / 1024.0;
                return string.Format("{0:0.1} Mbps", mbps);
            }
            else
            {
                if (bytesPerSecond < 1024)
                {
                    return string.Format("{0:0} B/s", bytesPerSecond);
                }
                double kb = bytesPerSecond / 1024.0;
                if (kb < 1024)
                {
                    return string.Format("{0:0.0} KB/s", kb);
                }
                double mb = kb / 1024.0;
                return string.Format("{0:0.1} MB/s", mb);
            }
        }

        // --- System Tray Operations ---

        private void InitializeTray()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = "NetSpeed Monitor";
            _notifyIcon.Visible = true;

            // Create and set the dynamic tray icon
            _notifyIcon.Icon = CreateDynamicIcon();

            // Handle tray clicks to toggle FlyoutWindow
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private System.Drawing.Icon CreateDynamicIcon()
        {
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(16, 16))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.Transparent);

                    using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(96, 205, 255), 1.5f))
                    {
                        g.DrawArc(pen, 1, 1, 13, 13, 135, 270);
                    }
                    
                    using (System.Drawing.Pen needle = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 158, 100), 1.5f))
                    {
                        g.DrawLine(needle, 7, 7, 11, 3);
                    }

                    _hIcon = bitmap.GetHicon();
                    return System.Drawing.Icon.FromHandle(_hIcon);
                }
            }
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_flyoutWindow == null)
            {
                _flyoutWindow = new FlyoutWindow();
            }

            if (_flyoutWindow.IsVisible)
            {
                _flyoutWindow.Hide();
            }
            else
            {
                _flyoutWindow.ShowFlyout();
            }
        }

        // --- Cleanup ---

        private void ShutdownApp()
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            if (_flyoutWindow != null)
            {
                _flyoutWindow.Close();
                _flyoutWindow = null;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            ShutdownApp();
            base.OnClosed(e);
        }
    }

    // --- Core Network Calculations Support ---

    public class SpeedMonitor
    {
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastTime = DateTime.MinValue;

        public double CurrentDownloadSpeed { get; private set; }
        public double CurrentUploadSpeed { get; private set; }
        public long TotalDownloadBytes { get; private set; }
        public long TotalUploadBytes { get; private set; }

        public void ResetTotals()
        {
            TotalDownloadBytes = 0;
            TotalUploadBytes = 0;
            _lastTime = DateTime.MinValue;
        }

        public SpeedResult Update()
        {
            long currentBytesReceived = 0;
            long currentBytesSent = 0;
            DateTime currentTime = DateTime.Now;

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
                        System.Net.NetworkInformation.IPInterfaceStatistics stats = ni.GetIPStatistics();
                        currentBytesReceived += stats.BytesReceived;
                        currentBytesSent += stats.BytesSent;
                    }
                }
            }
            catch (Exception)
            {
            }

            double dlSpeed = 0;
            double ulSpeed = 0;

            if (_lastTime != DateTime.MinValue)
            {
                double elapsedSeconds = (currentTime - _lastTime).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    long diffReceived = currentBytesReceived - _lastBytesReceived;
                    long diffSent = currentBytesSent - _lastBytesSent;

                    if (diffReceived >= 0)
                    {
                        dlSpeed = diffReceived / elapsedSeconds;
                        TotalDownloadBytes += diffReceived;
                    }
                    if (diffSent >= 0)
                    {
                        ulSpeed = diffSent / elapsedSeconds;
                        TotalUploadBytes += diffSent;
                    }
                }
            }

            _lastBytesReceived = currentBytesReceived;
            _lastBytesSent = currentBytesSent;
            _lastTime = currentTime;

            CurrentDownloadSpeed = dlSpeed;
            CurrentUploadSpeed = ulSpeed;

            return new SpeedResult(dlSpeed, ulSpeed);
        }
    }
}
