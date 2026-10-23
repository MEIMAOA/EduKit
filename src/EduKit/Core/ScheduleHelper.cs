using System;
using System.Collections.Generic;
using System.Linq;

namespace EduKit.Core
{
    /// <summary>课程表 / 值日小组的公共逻辑。</summary>
    public static class ScheduleHelper
    {
        public static readonly string[] DayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        /// <summary>今天对应的列索引（周一 = 0）。</summary>
        public static int TodayIndex => ((int)DateTime.Today.DayOfWeek + 6) % 7;

        public static TimetableCell FindCell(int day, int period)
        {
            return AppData.Timetable.Cells.FirstOrDefault(c => c.Day == day && c.Period == period);
        }

        public static string GetCourse(int day, int period)
        {
            return FindCell(day, period)?.Text ?? string.Empty;
        }

        public static void SetCourse(int day, int period, string text)
        {
            var cell = FindCell(day, period);
            if (cell == null)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                cell = new TimetableCell { Day = day, Period = period };
                AppData.Timetable.Cells.Add(cell);
            }

            cell.Text = text ?? string.Empty;
        }

        /// <summary>今日课程（只返回有内容的节次）。</summary>
        public static List<KeyValuePair<string, string>> GetTodayCourses()
        {
            var result = new List<KeyValuePair<string, string>>();
            var data = AppData.Timetable;
            for (var period = 0; period < data.PeriodNames.Count; period++)
            {
                var text = GetCourse(TodayIndex, period);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var name = period < data.PeriodNames.Count ? data.PeriodNames[period] : "第" + (period + 1) + "节";
                    result.Add(new KeyValuePair<string, string>(name, text));
                }
            }

            return result;
        }

        /// <summary>把名单按顺序平均分成若干小组。</summary>
        public static List<DutyGroup> BuildDutyGroups(int groupCount)
        {
            var names = AppData.Roster.Names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            groupCount = Math.Max(1, Math.Min(groupCount, Math.Max(1, names.Count)));

            var groups = new List<DutyGroup>();
            for (var i = 0; i < groupCount; i++)
            {
                groups.Add(new DutyGroup { Name = "第 " + (i + 1) + " 组" });
            }

            for (var i = 0; i < names.Count; i++)
            {
                groups[i % groupCount].Members.Add(names[i]);
            }

            return groups;
        }

        public static List<DutyGroup> BuildDutyGroups()
        {
            return BuildDutyGroups(AppData.Settings.DutyGroupCount);
        }

        /// <summary>本周轮到的小组索引（结合 ISO 周号与手动偏移）。</summary>
        public static int GetCurrentDutyIndex(List<DutyGroup> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                return 0;
            }

            var offset = AppData.Settings.DutyOffset;
            var index = (AppData.CurrentWeek + offset) % groups.Count;
            return index < 0 ? index + groups.Count : index;
        }
    }
}
