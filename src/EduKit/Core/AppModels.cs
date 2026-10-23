using System.Runtime.Serialization;

namespace EduKit.Core
{
    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)] public bool DarkMode { get; set; }

        [DataMember(Order = 2)] public bool StartWithWindows { get; set; }

        [DataMember(Order = 3)] public string LastPage { get; set; } = "dashboard";

        /// <summary>用户手动指定的图吧工具箱目录（为空时自动搜索）。</summary>
        [DataMember(Order = 4)] public string TubaPath { get; set; } = string.Empty;
    }
}
