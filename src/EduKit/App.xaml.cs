using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EduKit.Core;

namespace EduKit
{
    public partial class App : Application
    {
        private static Mutex _mutex;

        public static TimerEngine Timer { get; private set; }

        public static MainWindow Shell { get; private set; }

        public static string Version => "1.0.0";

        public static string ProjectUrl => "https://github.com/MEIMAOA/EduKit";

        /// <summary>从任意页面跳转到指定模块。</summary>
        public static void Navigate(string key)
        {
            Shell?.Go(key);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, "EduKit.SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("「电教委工具箱」已经在运行了。", "EduKit", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, args) => Log(args.ExceptionObject as Exception);

            AppData.Load();
            ThemeManager.Apply(AppData.Settings.DarkMode);
            Timer = new TimerEngine();

            if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--render", StringComparison.OrdinalIgnoreCase))
            {
                ScreenshotRenderer.Run(e.Args[1]);
                return;
            }

            if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                SelfTest.Run(e.Args[1]);
                return;
            }

            Shell = new MainWindow();
            Shell.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log(e.Exception);
            MessageBox.Show("程序遇到一个错误，已记录到日志：\r\n" + e.Exception.Message,
                "EduKit", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        private static void Log(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            var text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex + Environment.NewLine;

            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "EduKit-error.log"), text);
            }
            catch
            {
                // ignored
            }

            try
            {
                File.AppendAllText(Path.Combine(AppData.Dir, "error.log"), text);
            }
            catch
            {
                // ignored
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                AppData.SaveAll();
            }
            catch
            {
                // ignored
            }

            base.OnExit(e);
        }
    }
}
