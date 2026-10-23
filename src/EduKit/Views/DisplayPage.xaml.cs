using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class DisplayPage : UserControl
    {
        private sealed class ModeItem
        {
            public string Title { get; set; }

            public string Detail { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public int Bits { get; set; }
        }

        private readonly DispatcherTimer _restoreTimer;
        private int _restoreSeconds;
        private Native.DevMode _originalMode;
        private bool _hasPending;

        public DisplayPage()
        {
            InitializeComponent();

            _restoreTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _restoreTimer.Tick += RestoreTimer_Tick;

            Loaded += (s, e) => Refresh();
            Unloaded += (s, e) =>
            {
                if (_hasPending)
                {
                    RevertInternal();
                }
            };
        }

        private void Refresh()
        {
            var mode = Native.GetCurrentMode();
            ResolutionText.Text = mode.dmPelsWidth + " × " + mode.dmPelsHeight;
            RefreshText.Text = mode.dmDisplayFrequency + " Hz";
            ColorText.Text = mode.dmBitsPerPel + " 位";
            MonitorText.Text = Native.MonitorCount + " 台";

            var items = new List<ModeItem>();
            foreach (var item in Native.GetAvailableModes())
            {
                items.Add(new ModeItem
                {
                    Title = item.dmPelsWidth + " × " + item.dmPelsHeight,
                    Detail = item.dmBitsPerPel + " 位 · " + item.dmDisplayFrequency + " Hz",
                    Width = item.dmPelsWidth,
                    Height = item.dmPelsHeight,
                    Bits = item.dmBitsPerPel
                });
            }

            ModeList.ItemsSource = items;
            foreach (ModeItem item in ModeList.Items)
            {
                if (item.Width == mode.dmPelsWidth && item.Height == mode.dmPelsHeight &&
                    item.Bits == mode.dmBitsPerPel)
                {
                    ModeList.SelectedItem = item;
                    break;
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Mode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string mode)
            {
                if (!Native.SetDisplayMode(mode))
                {
                    Ui.Warn("调用 DisplaySwitch 失败，请手动按 Win + P 切换投影模式。");
                }
            }
        }

        private void TurnOff_Click(object sender, RoutedEventArgs e)
        {
            Native.TurnOffMonitor();
        }

        private void OpenDisplaySettings_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("desk.cpl");
        }

        private void ScreenTest_Click(object sender, RoutedEventArgs e)
        {
            var window = new ScreenTestWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }

        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 只用于选择，不自动应用
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!(ModeList.SelectedItem is ModeItem item))
            {
                Ui.Warn("请先在列表中选择一个分辨率。");
                return;
            }

            if (!Ui.Confirm("即将切换到 " + item.Title + "（" + item.Bits + " 位），15 秒内可以还原。\r\n\r\n继续吗？"))
            {
                return;
            }

            _originalMode = Native.GetCurrentMode();
            var result = Native.ApplyDisplayMode(item.Width, item.Height, item.Bits);
            if (result != 0)
            {
                Ui.Warn("切换失败（错误码 " + result + "），当前显卡可能不支持该分辨率。");
                return;
            }

            _hasPending = true;
            _restoreSeconds = 15;
            PendingText.Text = "已切换，15 秒后自动还原";
            PendingPanel.Visibility = Visibility.Visible;
            _restoreTimer.Start();
        }

        private void RestoreTimer_Tick(object sender, EventArgs e)
        {
            _restoreSeconds--;
            if (_restoreSeconds <= 0)
            {
                _restoreTimer.Stop();
                RevertInternal();
                return;
            }

            PendingText.Text = "已切换，" + _restoreSeconds + " 秒后自动还原";
        }

        private void Keep_Click(object sender, RoutedEventArgs e)
        {
            _restoreTimer.Stop();
            _hasPending = false;
            PendingPanel.Visibility = Visibility.Collapsed;
            Refresh();
            Ui.Info("已保留当前分辨率。");
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            _restoreTimer.Stop();
            RevertInternal();
        }

        private void RevertInternal()
        {
            if (_hasPending)
            {
                Native.RestoreDisplayMode(_originalMode);
                _hasPending = false;
            }

            PendingPanel.Visibility = Visibility.Collapsed;
            Refresh();
        }
    }
}
