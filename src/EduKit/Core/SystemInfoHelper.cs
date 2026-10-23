using System;
using System.Collections.Generic;
using System.Management;

namespace EduKit.Core
{
    public class InfoRow
    {
        public string Label { get; set; }

        public string Value { get; set; }
    }

    public class DiskInfo
    {
        public string Name { get; set; }

        public string Label { get; set; }

        public string SizeText { get; set; }

        public string FreeText { get; set; }

        public double FreePercent { get; set; }

        public string BarText { get; set; }
    }

    /// <summary>通过 WMI 读取硬件与系统信息（Windows 7 可用）。</summary>
    public static class SystemInfoHelper
    {
        public static List<InfoRow> GetHardwareInfo()
        {
            var rows = new List<InfoRow>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Caption, Version, OSArchitecture, TotalVisibleMemorySize, FreePhysicalMemory, LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var caption = Text(item["Caption"]);
                        var version = Text(item["Version"]);
                        var architecture = Text(item["OSArchitecture"]);
                        Add(rows, "操作系统", caption + (string.IsNullOrEmpty(architecture) ? "" : " (" + architecture + ")"));
                        Add(rows, "系统版本", version);
                        Add(rows, "启动时间", FormatDateTime(item["LastBootUpTime"]));

                        double totalKb;
                        double freeKb;
                        if (double.TryParse(Text(item["TotalVisibleMemorySize"]), out totalKb) &&
                            double.TryParse(Text(item["FreePhysicalMemory"]), out freeKb))
                        {
                            Add(rows, "物理内存", Native.FormatBytes(totalKb * 1024) + "（可用 " + Native.FormatBytes(freeKb * 1024) + "）");
                        }
                    }
                }
            }
            catch
            {
                // WMI 不可用时忽略
            }

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var text = Text(item["Name"]).Trim();
                        var cores = Text(item["NumberOfCores"]);
                        var threads = Text(item["NumberOfLogicalProcessors"]);
                        Add(rows, "处理器", text + "（" + cores + " 核 / " + threads + " 线程）");
                        break;
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Manufacturer, Model, Name, Domain, UserName FROM Win32_ComputerSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        Add(rows, "计算机名", Text(item["Name"]));
                        Add(rows, "品牌型号", (Text(item["Manufacturer"]) + " " + Text(item["Model"])).Trim());
                        Add(rows, "当前用户", Text(item["UserName"]));
                        break;
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
                {
                    var first = true;
                    foreach (var item in searcher.Get())
                    {
                        if (first)
                        {
                            Add(rows, "显卡", Text(item["Name"]) + "（驱动 " + Text(item["DriverVersion"]) + "）");
                            first = false;
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (var item in searcher.Get())
                    {
                        Add(rows, "主板", (Text(item["Manufacturer"]) + " " + Text(item["Product"])).Trim());
                        break;
                    }
                }
            }
            catch
            {
                // ignored
            }

            var uptime = GetUptime();
            if (!string.IsNullOrEmpty(uptime))
            {
                Add(rows, "已运行", uptime);
            }

            Add(rows, "显示器", Native.GetPrimaryDisplayText() + "，共 " + Native.MonitorCount + " 台");
            Add(rows, "系统目录", Environment.GetFolderPath(Environment.SpecialFolder.Windows));

            return rows;
        }

        public static string GetUptime()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var boot = ManagementDateTimeConverter.ToDateTime(Text(item["LastBootUpTime"]));
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

        public static List<DiskInfo> GetDisks()
        {
            var list = new List<DiskInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT DeviceID, VolumeName, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var size = ToDouble(item["Size"]);
                        var free = ToDouble(item["FreeSpace"]);
                        if (size <= 0)
                        {
                            continue;
                        }

                        var percent = free / size * 100;
                        list.Add(new DiskInfo
                        {
                            Name = Text(item["DeviceID"]),
                            Label = string.IsNullOrWhiteSpace(Text(item["VolumeName"])) ? "本地磁盘" : Text(item["VolumeName"]),
                            SizeText = "共 " + Native.FormatBytes(size),
                            FreeText = "可用 " + Native.FormatBytes(free),
                            FreePercent = percent,
                            BarText = percent.ToString("0") + "% 可用"
                        });
                    }
                }
            }
            catch
            {
                // ignored
            }

            return list;
        }

        public static long GetTempFolderSize()
        {
            var dir = System.IO.Path.GetTempPath();
            long total = 0;
            try
            {
                foreach (var file in System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories))
                {
                    try
                    {
                        total += new System.IO.FileInfo(file).Length;
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
            var dir = System.IO.Path.GetTempPath();
            try
            {
                foreach (var file in System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new System.IO.FileInfo(file);
                        var size = info.Length;
                        info.Attributes = System.IO.FileAttributes.Normal;
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

        private static void Add(List<InfoRow> rows, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(new InfoRow { Label = label, Value = value });
            }
        }

        private static string Text(object value)
        {
            return value?.ToString() ?? string.Empty;
        }

        private static double ToDouble(object value)
        {
            double parsed;
            return double.TryParse(Text(value), out parsed) ? parsed : 0;
        }

        private static string FormatDateTime(object value)
        {
            try
            {
                return ManagementDateTimeConverter.ToDateTime(Text(value)).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
