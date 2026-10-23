using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace EduKit.Core
{
    /// <summary>全局数据与配置（%APPDATA%\EduKit）。</summary>
    public static class AppData
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string Dir { get; private set; }

        public static AppSettings Settings { get; private set; }

        public static RosterData Roster { get; private set; }

        public static TimetableData Timetable { get; private set; }

        public static LauncherData Launcher { get; private set; }

        private static string SettingsFile => Path.Combine(Dir, "settings.json");

        private static string RosterFile => Path.Combine(Dir, "roster.json");

        private static string TimetableFile => Path.Combine(Dir, "timetable.json");

        private static string LauncherFile => Path.Combine(Dir, "launcher.json");

        public static string ShareDir
        {
            get
            {
                var dir = Settings?.ShareFolder;
                if (string.IsNullOrWhiteSpace(dir))
                {
                    dir = Path.Combine(Dir, "Share");
                }

                return dir;
            }
        }

        /// <summary>
        /// 选择可写的数据目录：优先 %APPDATA%，其次 %LOCALAPPDATA%，最后程序目录（便携模式）。
        /// 某些受管控的电脑会限制对用户配置目录的写入，此时自动降级，保证程序可用。
        /// </summary>
        private static string ResolveDataDir()
        {
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty);
            var candidates = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(exeDir) && File.Exists(Path.Combine(exeDir, "portable.txt")))
            {
                candidates.Add(Path.Combine(exeDir, "EduKitData"));
            }

            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EduKit"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EduKit"));

            if (!string.IsNullOrEmpty(exeDir))
            {
                candidates.Add(Path.Combine(exeDir, "EduKitData"));
            }

            candidates.Add(Path.Combine(Path.GetTempPath(), "EduKit"));

            foreach (var dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var probe = Path.Combine(dir, ".write-test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return dir;
                }
                catch
                {
                    // 尝试下一个位置
                }
            }

            return Path.Combine(Path.GetTempPath(), "EduKit");
        }

        public static bool IsDataDirWritable
        {
            get
            {
                try
                {
                    var probe = Path.Combine(Dir, ".write-test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Load()
        {
            Dir = ResolveDataDir();

            Settings = Json.Load<AppSettings>(SettingsFile) ?? new AppSettings();
            Roster = Json.Load<RosterData>(RosterFile) ?? new RosterData();
            Timetable = Json.Load<TimetableData>(TimetableFile) ?? new TimetableData();
            Launcher = Json.Load<LauncherData>(LauncherFile) ?? new LauncherData();

            if (Timetable.PeriodNames == null)
            {
                Timetable.PeriodNames = new List<string>();
            }

            if (Timetable.PeriodNames.Count == 0)
            {
                for (var i = 1; i <= 8; i++)
                {
                    Timetable.PeriodNames.Add("第" + i + "节");
                }
            }

            if (Timetable.Cells == null)
            {
                Timetable.Cells = new List<TimetableCell>();
            }

            if (Roster.Names == null)
            {
                Roster.Names = new List<string>();
            }

            if (Roster.Names.Count == 0)
            {
                Roster.Names.AddRange(new[] { "张三", "李四", "王五", "赵六", "钱七", "孙八", "周九", "吴十" });
            }

            if (Launcher.Items == null || Launcher.Items.Count == 0)
            {
                SeedLauncher();
            }

            TryApplyAutoStart(Settings.StartWithWindows);

            try
            {
                Directory.CreateDirectory(ShareDir);
            }
            catch
            {
                // ignored
            }
        }

        public static void SaveAll()
        {
            SaveSettings();
            Json.Save(RosterFile, Roster);
            Json.Save(TimetableFile, Timetable);
            Json.Save(LauncherFile, Launcher);
        }

        public static void SaveSettings()
        {
            Json.Save(SettingsFile, Settings);
        }

        /// <summary>恢复默认设置（保留端口、共享目录和值日分组数等与本机相关的配置）。</summary>
        public static void ResetSettings()
        {
            var port = Settings.SharePort;
            var folder = Settings.ShareFolder;
            var groups = Settings.DutyGroupCount;
            var notice = Settings.NoticeText;

            Settings = new AppSettings
            {
                SharePort = port,
                ShareFolder = folder,
                DutyGroupCount = groups,
                NoticeText = notice
            };

            ThemeManager.Apply(Settings.DarkMode);
            SaveSettings();
        }

        public static void SaveRoster()
        {
            Json.Save(RosterFile, Roster);
        }

        public static void SaveTimetable()
        {
            Json.Save(TimetableFile, Timetable);
        }

        public static void SaveLauncher()
        {
            Json.Save(LauncherFile, Launcher);
        }

        /// <summary>当前 ISO 周序号，用于值日小组轮换（net462 无 ISOWeek，手写实现）。</summary>
        public static int CurrentWeek
        {
            get
            {
                var today = DateTime.Today;
                var weekday = (int)today.DayOfWeek;
                if (weekday == 0)
                {
                    weekday = 7;
                }

                var thursday = today.AddDays(4 - weekday);
                var firstThursday = new DateTime(thursday.Year, 1, 1);
                var firstWeekday = (int)firstThursday.DayOfWeek;
                if (firstWeekday == 0)
                {
                    firstWeekday = 7;
                }

                firstThursday = firstThursday.AddDays(4 - firstWeekday);
                return 1 + (thursday - firstThursday).Days / 7;
            }
        }

        public static void TryApplyAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null)
                    {
                        return;
                    }

                    if (enable)
                    {
                        var exe = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                        if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                        {
                            key.SetValue("EduKit", "\"" + exe + "\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue("EduKit", false);
                    }
                }
            }
            catch
            {
                // 无权限时忽略
            }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key?.GetValue("EduKit") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void SeedLauncher()
        {
            Launcher.Items = new List<LaunchItem>
            {
                new LaunchItem { Name = "控制面板", Target = "control.exe", Category = "系统工具", BuiltIn = true },
                new LaunchItem { Name = "设备管理器", Target = "devmgmt.msc", Category = "系统工具", BuiltIn = true },
                new LaunchItem { Name = "任务管理器", Target = "taskmgr.exe", Category = "系统工具", BuiltIn = true },
                new LaunchItem { Name = "服务", Target = "services.msc", Category = "系统工具", BuiltIn = true },
                new LaunchItem { Name = "网络连接", Target = "ncpa.cpl", Category = "网络", BuiltIn = true },
                new LaunchItem { Name = "网络和共享中心", Target = "control.exe", Arguments = "/name Microsoft.NetworkAndSharingCenter", Category = "网络", BuiltIn = true },
                new LaunchItem { Name = "声音设置", Target = "mmsys.cpl", Category = "声音与显示", BuiltIn = true },
                new LaunchItem { Name = "显示设置", Target = "desk.cpl", Category = "声音与显示", BuiltIn = true },
                new LaunchItem { Name = "系统信息", Target = "msinfo32.exe", Category = "系统工具", BuiltIn = true },
                new LaunchItem { Name = "命令提示符", Target = "cmd.exe", Category = "常用", BuiltIn = true },
                new LaunchItem { Name = "计算器", Target = "calc.exe", Category = "常用", BuiltIn = true },
                new LaunchItem { Name = "记事本", Target = "notepad.exe", Category = "常用", BuiltIn = true },
                new LaunchItem { Name = "画图", Target = "mspaint.exe", Category = "常用", BuiltIn = true },
                new LaunchItem { Name = "截图工具", Target = "snippingtool.exe", Category = "常用", BuiltIn = true },
                new LaunchItem { Name = "计算机", Target = "explorer.exe", Category = "常用", BuiltIn = true },
                new LaunchItem { Name = "回收站", Target = "shell:RecycleBinFolder", Category = "常用", BuiltIn = true }
            };
        }
    }
}
