using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class DashboardPage : UserControl
    {
        private readonly DispatcherTimer _clock;
        private bool _loading;

        public DashboardPage()
        {
            InitializeComponent();

            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) => UpdateClock();
            _clock.Start();

            Loaded += (s, e) =>
            {
                UpdateClock();
                RefreshContent();
            };
            Unloaded += (s, e) => _clock.Stop();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            ClockText.Text = now.ToString("HH:mm:ss");
            DateText.Text = now.ToString("yyyy年M月d日") + "  " + ScheduleHelper.DayNames[ScheduleHelper.TodayIndex];
            WeekText.Text = "第 " + AppData.CurrentWeek + " 周 · " + now.ToString("HH:mm");
            GreetingText.Text = now.Hour < 6 ? "夜深了，注意休息" :
                now.Hour < 11 ? "早上好，今天也要加油" :
                now.Hour < 14 ? "中午好，记得午休" :
                now.Hour < 18 ? "下午好，继续努力" : "晚上好，早点休息";

            var notice = AppData.Settings.NoticeText;
            NoticePreview.Text = string.IsNullOrWhiteSpace(notice) ? "公告：请保持教室安静" : "公告：" + notice;
        }

        private async void RefreshContent()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;

            try
            {
                // 今日课程
                var courses = ScheduleHelper.GetTodayCourses();
                if (courses.Count > 0)
                {
                    TodayCourses.ItemsSource = courses;
                    NoCourseText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TodayCourses.ItemsSource = null;
                    NoCourseText.Visibility = Visibility.Visible;
                }

                // 本周值日
                var groups = ScheduleHelper.BuildDutyGroups();
                if (groups.Count > 0)
                {
                    var index = ScheduleHelper.GetCurrentDutyIndex(groups);
                    DutyGroupText.Text = groups[index].Name;
                    DutyMembersText.Text = string.Join("  ", groups[index].Members);
                }
                else
                {
                    DutyGroupText.Text = "—";
                    DutyMembersText.Text = "名单为空，请到「随机点名」添加学生名单。";
                }

                // 系统概览（WMI 较慢，放到后台线程）
                var info = await Task.Run(() => SystemInfoHelper.GetHardwareInfo());
                var rows = new List<InfoRow>();
                foreach (var row in info)
                {
                    if (row.Label == "操作系统" || row.Label == "处理器" || row.Label == "物理内存" ||
                        row.Label == "已运行" || row.Label == "显示器")
                    {
                        rows.Add(row);
                    }
                }

                var ips = NetUtil.GetLocalIPv4();
                if (ips.Count > 0)
                {
                    rows.Add(new InfoRow { Label = "本机 IP", Value = string.Join("  ", ips) });
                }

                OverviewList.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                OverviewList.ItemsSource = new List<InfoRow>
                {
                    new InfoRow { Label = "读取失败", Value = ex.Message }
                };
            }
            finally
            {
                _loading = false;
            }
        }

        private void Timer_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("timer");
        }

        private void Random_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("random");
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("share");
        }

        private void Display_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("display");
        }

        private void Audio_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("audio");
        }

        private void Timetable_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("timetable");
        }

        private void Duty_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("duty");
        }

        private void System_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("system");
        }

        private void Notice_Click(object sender, RoutedEventArgs e)
        {
            var window = OverlayWindow.ForNotice(AppData.Settings.NoticeText, DateTime.Now.ToString("M月d日 HH:mm"));
            window.Show();
        }

        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = Native.CaptureScreen();
                if (Ui.Confirm("截图已保存：\r\n" + file + "\r\n\r\n要打开所在文件夹吗？"))
                {
                    Native.Start("explorer.exe", "/select,\"" + file + "\"");
                }
            }
            catch (Exception ex)
            {
                Ui.Error("截图失败：" + ex.Message);
            }
        }

        private void MonitorOff_Click(object sender, RoutedEventArgs e)
        {
            Native.TurnOffMonitor();
        }
    }
}
