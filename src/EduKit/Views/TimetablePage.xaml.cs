using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class TimetablePage : UserControl
    {
        private const int Days = 7;
        private readonly List<TextBox> _cells = new List<TextBox>();

        public TimetablePage()
        {
            InitializeComponent();
            Loaded += (s, e) => BuildGrid();
        }

        private void BuildGrid()
        {
            _cells.Clear();
            GridHost.Children.Clear();
            GridHost.ColumnDefinitions.Clear();
            GridHost.RowDefinitions.Clear();

            var periods = AppData.Timetable.PeriodNames;
            var today = ScheduleHelper.TodayIndex;

            GridHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            for (var d = 0; d < Days; d++)
            {
                GridHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 86 });
            }

            GridHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var p = 0; p < periods.Count; p++)
            {
                GridHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var corner = new Border
            {
                Background = (Brush)FindResource("BgCardAlt"),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(2),
                Padding = new Thickness(6, 8, 6, 8)
            };
            corner.Child = new TextBlock
            {
                Text = "节次 / 星期",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(corner, 0);
            Grid.SetColumn(corner, 0);
            GridHost.Children.Add(corner);

            for (var d = 0; d < Days; d++)
            {
                var isToday = d == today;
                var border = new Border
                {
                    Background = (Brush)FindResource(isToday ? "AccentSoft" : "BgCardAlt"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(2),
                    Padding = new Thickness(6, 8, 6, 8)
                };
                border.Child = new TextBlock
                {
                    Text = ScheduleHelper.DayNames[d],
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource(isToday ? "Accent" : "TextSecondary"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(border, 0);
                Grid.SetColumn(border, d + 1);
                GridHost.Children.Add(border);
            }

            for (var p = 0; p < periods.Count; p++)
            {
                var label = new Border
                {
                    Background = (Brush)FindResource("BgCardAlt"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(2),
                    Padding = new Thickness(6, 8, 6, 8)
                };
                label.Child = new TextBlock
                {
                    Text = periods[p],
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextSecondary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(label, p + 1);
                Grid.SetColumn(label, 0);
                GridHost.Children.Add(label);

                for (var d = 0; d < Days; d++)
                {
                    var box = new TextBox
                    {
                        Style = (Style)FindResource("InputBox"),
                        FontSize = 12,
                        Height = 38,
                        Margin = new Thickness(2),
                        Padding = new Thickness(6, 4, 6, 4),
                        VerticalContentAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        Text = ScheduleHelper.GetCourse(d, p),
                        Tag = d + ":" + p
                    };
                    box.TextChanged += Cell_TextChanged;
                    box.LostFocus += (s, e) => AppData.SaveTimetable();

                    Grid.SetRow(box, p + 1);
                    Grid.SetColumn(box, d + 1);
                    GridHost.Children.Add(box);
                    _cells.Add(box);
                }
            }

            TodayHint.Text = "今天是 " + ScheduleHelper.DayNames[today] + "，共 " + periods.Count + " 节课";
        }

        private void Cell_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox box) || !(box.Tag is string tag))
            {
                return;
            }

            var parts = tag.Split(':');
            int day;
            int period;
            if (parts.Length != 2 || !int.TryParse(parts[0], out day) || !int.TryParse(parts[1], out period))
            {
                return;
            }

            ScheduleHelper.SetCourse(day, period, box.Text.Trim());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppData.SaveTimetable();
            Ui.Info("课程表已保存。");
        }

        private void CopyToday_Click(object sender, RoutedEventArgs e)
        {
            var courses = ScheduleHelper.GetTodayCourses();
            if (courses.Count == 0)
            {
                Ui.Info("今天还没有填写课程。");
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("【" + DateTime.Now.ToString("M月d日") + " " + ScheduleHelper.DayNames[ScheduleHelper.TodayIndex] + "】");
            foreach (var course in courses)
            {
                builder.AppendLine(course.Key + "：" + course.Value);
            }

            Ui.Copy(builder.ToString().TrimEnd());
            Ui.Info("今日课程已复制到剪贴板，可以直接粘贴到班级群。");
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("确定清空整张课程表吗？"))
            {
                return;
            }

            AppData.Timetable.Cells.Clear();
            AppData.SaveTimetable();
            BuildGrid();
        }
    }
}
