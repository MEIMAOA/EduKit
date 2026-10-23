using System;
using System.IO;
using System.Management;

namespace EduKit.Core
{
    /// <summary>系统通用小工具：运行时长、临时文件统计与清理。</summary>
    public static class SystemInfoHelper
    {
        public static string GetUptime()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var boot = ManagementDateTimeConverter.ToDateTime(item["LastBootUpTime"]?.ToString() ?? string.Empty);
                        var span = DateTime.Now - boot;
                        if (span.TotalDays >= 1)
                        {
                            return (int)span.TotalDays + " 天 " + span.Hours + " 小时 " + span.Minutes + " 分钟";
                        }

                        return span.Hours + " 小时 " + span.Minutes + " 分钟";
                    }
                }
            }
            catch
            {
                // ignored
            }

            return string.Empty;
        }

        public static long GetTempFolderSize()
        {
            var dir = Path.GetTempPath();
            long total = 0;
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // 文件被占用时忽略
                    }
                }
            }
            catch
            {
                // ignored
            }

            return total;
        }

        public static long CleanTempFolder()
        {
            long removed = 0;
            var dir = Path.GetTempPath();
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        var size = info.Length;
                        info.Attributes = FileAttributes.Normal;
                        info.Delete();
                        removed += size;
                    }
                    catch
                    {
                        // 占用中的文件跳过
                    }
                }
            }
            catch
            {
                // ignored
            }

            return removed;
        }
    }
}