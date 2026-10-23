using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class DutyPage : UserControl
    {
        private sealed class GroupView
        {
            public string Name { get; set; }

            public string Members { get; set; }

            public string CountText { get; set; }

            public bool IsCurrent { get; set; }
        }

        public DutyPage()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh();
        }

        private void Refresh()
        {
            var groups = ScheduleHelper.BuildDutyGroups();
            var current = ScheduleHelper.GetCurrentDutyIndex(groups);

            GroupCountText.Text = AppData.Settings.DutyGroupCount.ToString();
            CurrentText.Text = groups.Count == 0
                ? "名单为空，请先在「随机点名」里添加学生名单"
                : "本周轮到：" + groups[current].Name + "（第 " + AppData.CurrentWeek + " 周）";

            var views = new List<GroupView>();
            for (var i = 0; i < groups.Count; i++)
            {
                views.Add(new GroupView
                {
                    Name = groups[i].Name,
                    Members = string.Join("  ", groups[i].Members),
                    CountText = groups[i].Members.Count + " 人",
                    IsCurrent = i == current
                });
            }

            GroupList.ItemsSource = null;
            GroupList.ItemsSource = views;
        }

        private void MinusGroup_Click(object sender, RoutedEventArgs e)
        {
            AppData.Settings.DutyGroupCount = System.Math.Max(1, AppData.Settings.DutyGroupCount - 1);
            AppData.SaveSettings();
            Refresh();
        }

        private void PlusGroup_Click(object sender, RoutedEventArgs e)
        {
            AppData.Settings.DutyGroupCount = System.Math.Min(8, AppData.Settings.DutyGroupCount + 1);
            AppData.SaveSettings();
            Refresh();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            AppData.Settings.DutyOffset--;
            AppData.SaveSettings();
            Refresh();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            AppData.Settings.DutyOffset++;
            AppData.SaveSettings();
            Refresh();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }
    }
}
