using System;
using System.Threading;
using System.Windows;

namespace NetSpeedOverlay
{
    public partial class App : Application
    {
        private static Mutex _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            const string appName = "NetSpeedOverlayUniqueMutexName";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("NetSpeed Monitor is already running.", "NetSpeed Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true;
            Application.Current.Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                LogException(ex);
            }
        }

        private void LogException(Exception ex)
        {
            try
            {
                string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                string content = string.Format("[{0}] Exception: {1}\r\nStack Trace:\r\n{2}\r\n\r\n", 
                    DateTime.Now.ToString(), ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    content += string.Format("Inner Exception: {0}\r\nInner Stack Trace:\r\n{1}\r\n\r\n", 
                        ex.InnerException.Message, ex.InnerException.StackTrace);
                }
                System.IO.File.AppendAllText(logPath, content);
            }
            catch
            {
                // Ignore errors during logging
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (Exception)
                {
                    // Mutex was not acquired or already released
                }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
