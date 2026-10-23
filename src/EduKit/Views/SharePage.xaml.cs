using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class SharePage : UserControl
    {
        private readonly LanShareServer _server = new LanShareServer();
        private readonly ObservableCollection<string> _logs = new ObservableCollection<string>();

        public SharePage()
        {
            InitializeComponent();
            LogList.ItemsSource = _logs;
            _server.Logged += Server_Logged;
            Loaded += (s, e) =>
            {
                PortBox.Text = AppData.Settings.SharePort.ToString();
                UpdateStatus();
            };
            Unloaded += (s, e) =>
            {
                if (_server.IsRunning)
                {
                    _server.Stop();
                }
            };
        }

        private void Server_Logged(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _logs.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + message);
                while (_logs.Count > 300)
                {
                    _logs.RemoveAt(_logs.Count - 1);
                }
            }));
        }

        private void UpdateStatus()
        {
            if (_server.IsRunning)
            {
                StatusText.Text = "运行中 · 端口 " + _server.Port;
                StatusChip.Background = (System.Windows.Media.Brush)FindResource("SuccessSoft");
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("Success");
                ToggleButton.Content = "停止快传";
                ToggleButton.Style = (Style)FindResource("BtnSecondary");
                UrlList.ItemsSource = _server.GetUrls();
            }
            else
            {
                StatusText.Text = "未启动";
                StatusChip.Background = (System.Windows.Media.Brush)FindResource("BgHover");
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary");
                ToggleButton.Content = "启动快传";
                ToggleButton.Style = (Style)FindResource("BtnPrimary");
                UrlList.ItemsSource = null;
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_server.IsRunning)
            {
                _server.Stop();
                UpdateStatus();
                return;
            }

            int port;
            if (!int.TryParse(PortBox.Text.Trim(), out port) || port < 1024 || port > 65535)
            {
                Ui.Warn("端口请填写 1024 - 65535 之间的数字。");
                return;
            }

            AppData.Settings.SharePort = port;
            AppData.SaveSettings();

            _server.Start(port, AppData.ShareDir);
            UpdateStatus();
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is string url)
            {
                Ui.Copy(url);
                _logs.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  已复制地址：" + url);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppData.ShareDir);
                Native.Start("explorer.exe", "\"" + AppData.ShareDir + "\"");
            }
            catch (Exception ex)
            {
                Ui.Error("无法打开共享文件夹：" + ex.Message);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logs.Clear();
        }
    }
}
