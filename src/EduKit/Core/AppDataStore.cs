using System;
using System.IO;
using Microsoft.Win32;

namespace EduKit.Core
{
    /// <summary>全局配置与数据目录（%APPDATA%\EduKit，不可写时自动降级）。</summary>
    public static class AppData
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string Dir { get; private set; }

        public static AppSettings Settings { get; private set; }

        private static string SettingsFile => Path.Combine(Dir, "settings.json");

        public static void Load()
        {
            Dir = ResolveDataDir();
            Settings = Json.Load<AppSettings>(SettingsFile) ?? new AppSettings();
            TryApplyAutoStart(Settings.StartWithWindows);
        }

        public static void SaveAll()
        {
            SaveSettings();
        }

        public static void SaveSettings()
        {
            Json.Save(SettingsFile, Settings);
        }

        /// <summary>恢复默认设置（保留图吧工具箱路径等本机信息）。</summary>
        public static void ResetSettings()
        {
            var tuba = Settings.TubaPath;
            Settings = new AppSettings { TubaPath = tuba };
            ThemeManager.Apply(Settings.DarkMode);
            SaveSettings();
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

        /// <summary>当前 ISO 周序号。</summary>
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
    }
}
