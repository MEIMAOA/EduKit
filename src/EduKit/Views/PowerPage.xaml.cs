using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class PowerPage : UserControl
    {
        private readonly DispatcherTimer _timer;
        private DateTime? _target;
        private bool _isRestart;

        public PowerPage()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateStatus();
            _timer.Start();

            Loaded += (s, e) => UpdateStatus();
            Unloaded += (s, e) => _timer.Stop();
        }

        private void UpdateStatus()
        {
            if (_target == null)
            {
                StatusText.Text = "当前没有定时任务";
                return;
            }

            var remaining = _target.Value - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                _target = null;
                StatusText.Text = "定时任务已执行";
                return;
            }

            StatusText.Text = "将在 " + (int)remaining.TotalMinutes + " 分 " + remaining.Seconds + " 秒后" +
                              (_isRestart ? "重启" : "关机") + "（" + _target.Value.ToString("HH:mm:ss") + "）。";
        }

        private void Shutdown_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("确定要立即关机吗？\r\n建议先确认其他软件里没有未保存的内容。"))
            {
                return;
            }

            Native.Shutdown(5, false, false);
            StatusText.Text = "已发送关机命令（5 秒后关机）。";
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("确定要立即重启吗？\r\n建议先确认其他软件里没有未保存的内容。"))
            {
                return;
            }

            Native.Shutdown(5, true, false);
            StatusText.Text = "已发送重启命令（5 秒后重启）。";
        }

        private void Logoff_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("确定要注销当前用户吗？"))
            {
                return;
            }

            Native.Shutdown(0, false, true);
        }

        private void Sleep_Click(object sender, RoutedEventArgs e)
        {
            if (!Native.Sleep())
            {
                Ui.Warn("这台电脑不支持睡眠，可能已被电源策略禁用。");
            }
        }

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            Native.LockScreen();
        }

        private void MonitorOff_Click(object sender, RoutedEventArgs e)
        {
            Native.TurnOffMonitor();
        }

        private void MinutesPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string text)
            {
                MinutesBox.Text = text;
            }
        }

        private int GetMinutes()
        {
            int minutes;
            if (!int.TryParse(MinutesBox.Text.Trim(), out minutes) || minutes <= 0 || minutes > 24 * 60)
            {
                Ui.Warn("请输入 1 - 1440 之间的分钟数。");
                return -1;
            }

            return minutes;
        }

        private void ScheduleShutdown_Click(object sender, RoutedEventArgs e)
        {
            Schedule(false);
        }

        private void ScheduleRestart_Click(object sender, RoutedEventArgs e)
        {
            Schedule(true);
        }

        private void Schedule(bool restart)
        {
            var minutes = GetMinutes();
            if (minutes < 0)
            {
                return;
            }

            if (!Ui.Confirm("确定在 " + minutes + " 分钟后自动" + (restart ? "重启" : "关机") + "吗？"))
            {
                return;
            }

            Native.Shutdown(minutes * 60, restart, false);
            _isRestart = restart;
            _target = DateTime.Now.AddMinutes(minutes);
            UpdateStatus();
        }

        private void CancelSchedule_Click(object sender, RoutedEventArgs e)
        {
            Native.CancelShutdown();
            _target = null;
            StatusText.Text = "已取消定时任务。";
        }
    }
}
