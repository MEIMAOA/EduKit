using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class RandomPage : UserControl
    {
        private readonly Random _random = new Random();
        private readonly DispatcherTimer _animation;
        private readonly List<string> _picked = new List<string>();
        private int _count = 1;
        private bool _spinning;
        private DateTime _spinEnd;

        public RandomPage()
        {
            InitializeComponent();

            _animation = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _animation.Tick += Animation_Tick;

            Loaded += (s, e) => ReloadRoster();
        }

        private List<string> Names => AppData.Roster.Names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        private void ReloadRoster()
        {
            RosterBox.Text = string.Join(Environment.NewLine, AppData.Roster.Names);
            RosterCount.Text = "共 " + Names.Count + " 人";
            CountChip.Text = "共 " + Names.Count + " 人";
            HistoryList.ItemsSource = null;
            HistoryList.ItemsSource = AppData.Roster.History;
            UpdateModeChip();
        }

        private void UpdateModeChip()
        {
            ModeChip.Text = NoRepeatSwitch.IsChecked == true ? "不重复模式" : "全部同学";
        }

        private void SaveRoster_Click(object sender, RoutedEventArgs e)
        {
            var names = RosterBox.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct()
                .ToList();

            if (names.Count == 0)
            {
                Ui.Warn("名单不能为空。");
                return;
            }

            AppData.Roster.Names.Clear();
            AppData.Roster.Names.AddRange(names);
            AppData.SaveRoster();
            _picked.Clear();
            ReloadRoster();
        }

        private void Pick_Click(object sender, RoutedEventArgs e)
        {
            if (_spinning)
            {
                return;
            }

            var pool = Names;
            if (pool.Count == 0)
            {
                Ui.Warn("名单为空，请先在右侧添加学生名字。");
                return;
            }

            if (NoRepeatSwitch.IsChecked == true)
            {
                pool = pool.Where(n => !_picked.Contains(n)).ToList();
                if (pool.Count == 0)
                {
                    _picked.Clear();
                    pool = Names;
                    HistoryList.ItemsSource = null;
                }
            }

            _count = Math.Max(1, Math.Min(_count, pool.Count));
            _spinning = true;
            _spinEnd = DateTime.Now.AddMilliseconds(1100);
            PickButton.IsEnabled = false;
            _animation.Start();
            UpdateModeChip();
        }

        private void Animation_Tick(object sender, EventArgs e)
        {
            var pool = Names;
            if (pool.Count == 0)
            {
                _animation.Stop();
                return;
            }

            if (DateTime.Now >= _spinEnd)
            {
                _animation.Stop();
                FinishPick(pool);
                return;
            }

            ResultText.Text = pool[_random.Next(pool.Count)];
        }

        private void FinishPick(List<string> pool)
        {
            _spinning = false;
            PickButton.IsEnabled = true;

            var picking = new List<string>();
            var available = new List<string>(pool);
            for (var i = 0; i < _count && available.Count > 0; i++)
            {
                var index = _random.Next(available.Count);
                var name = available[index];
                available.RemoveAt(index);
                picking.Add(name);
                if (NoRepeatSwitch.IsChecked == true && !_picked.Contains(name))
                {
                    _picked.Add(name);
                }
            }

            ResultText.Text = string.Join("  ", picking);
            if (_count == 1)
            {
                ResultText.Text = picking.FirstOrDefault() ?? "—";
            }

            AppData.Roster.History.Insert(0, DateTime.Now.ToString("HH:mm") + "  " + string.Join("、", picking));
            while (AppData.Roster.History.Count > 100)
            {
                AppData.Roster.History.RemoveAt(AppData.Roster.History.Count - 1);
            }

            HistoryList.ItemsSource = null;
            HistoryList.ItemsSource = AppData.Roster.History;
            AppData.SaveRoster();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _animation.Stop();
            _spinning = false;
            PickButton.IsEnabled = true;
            _picked.Clear();
            ResultText.Text = "已重置，点击开始抽取";
            UpdateModeChip();
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            AppData.Roster.History.Clear();
            HistoryList.ItemsSource = null;
            AppData.SaveRoster();
        }

        private void Minus_Click(object sender, RoutedEventArgs e)
        {
            _count = Math.Max(1, _count - 1);
            CountText.Text = _count.ToString();
        }

        private void Plus_Click(object sender, RoutedEventArgs e)
        {
            _count = Math.Min(10, _count + 1);
            CountText.Text = _count.ToString();
        }
    }
}
