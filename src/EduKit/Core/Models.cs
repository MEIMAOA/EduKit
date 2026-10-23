using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EduKit.Core
{
    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)] public bool DarkMode { get; set; }

        [DataMember(Order = 2)] public bool StartWithWindows { get; set; }

        [DataMember(Order = 3)] public int SharePort { get; set; } = 8899;

        [DataMember(Order = 4)] public string ShareFolder { get; set; } = string.Empty;

        [DataMember(Order = 5)] public string LastPage { get; set; } = "dashboard";

        [DataMember(Order = 6)] public bool TimerSound { get; set; } = true;

        [DataMember(Order = 7)] public int DutyOffset { get; set; }

        [DataMember(Order = 8)] public bool AutoStartTimer { get; set; }

        [DataMember(Order = 9)] public string NoticeText { get; set; } = "请保持安静，认真听讲。";

        [DataMember(Order = 10)] public int DutyGroupCount { get; set; } = 4;
    }

    [DataContract]
    public class RosterData
    {
        [DataMember(Order = 1)] public List<string> Names { get; set; } = new List<string>();

        [DataMember(Order = 2)] public List<string> History { get; set; } = new List<string>();
    }

    [DataContract]
    public class TimetableCell
    {
        [DataMember(Order = 1)] public int Day { get; set; }

        [DataMember(Order = 2)] public int Period { get; set; }

        [DataMember(Order = 3)] public string Text { get; set; } = string.Empty;
    }

    [DataContract]
    public class TimetableData
    {
        [DataMember(Order = 1)] public List<TimetableCell> Cells { get; set; } = new List<TimetableCell>();

        [DataMember(Order = 2)] public List<string> PeriodNames { get; set; } = new List<string>();
    }

    [DataContract]
    public class LaunchItem
    {
        [DataMember(Order = 1)] public string Name { get; set; } = string.Empty;

        [DataMember(Order = 2)] public string Target { get; set; } = string.Empty;

        [DataMember(Order = 3)] public string Arguments { get; set; } = string.Empty;

        [DataMember(Order = 4)] public string Category { get; set; } = "常用";

        [DataMember(Order = 5)] public bool BuiltIn { get; set; }
    }

    [DataContract]
    public class LauncherData
    {
        [DataMember(Order = 1)] public List<LaunchItem> Items { get; set; } = new List<LaunchItem>();
    }

    [DataContract]
    public class DutyGroup
    {
        [DataMember(Order = 1)] public string Name { get; set; } = string.Empty;

        [DataMember(Order = 2)] public List<string> Members { get; set; } = new List<string>();
    }
}
