using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32;

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

        public string Model { get; set; }

        public string SizeText { get; set; }

        public string FreeText { get; set; }

        public double FreePercent { get; set; }

        public string Health { get; set; }

        public bool IsHealthy { get; set; }
    }

    public class DeviceProblem
    {
        public string Name { get; set; }

        public string Class { get; set; }

        public string Error { get; set; }
    }

    /// <summary>专业硬件信息读取（WMI / 注册表 / Win32），全部只读，不需要管理员权限。</summary>
    public static class HardwareDetails
    {
        /// <summary>按分组返回硬件信息：处理器 / 内存 / 主板 / 显示 / 存储 / 系统。</summary>
        public static List<InfoRow> GetHardwareInfo()
        {
            var rows = new List<InfoRow>();

            // 系统
            TryQuery("SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate FROM Win32_OperatingSystem", item =>
            {
                Add(rows, "操作系统", Text(item["Caption"]));
                Add(rows, "系统版本", Text(item["Version"]) + " (Build " + Text(item["BuildNumber"]) + ")");
                Add(rows, "系统架构", Text(item["OSArchitecture"]));
                var install = ToDate(item["InstallDate"]);
                if (install != null)
                {
                    Add(rows, "安装日期", install.Value.ToString("yyyy-MM-dd"));
                }
            });

            Add(rows, "计算机名", Environment.MachineName);
            Add(rows, "当前用户", Environment.UserName);
            Add(rows, "系统目录", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            Add(rows, "启动模式", DetectBootMode());

            // 处理器
            TryQuery("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, SocketDesignation, Manufacturer FROM Win32_Processor", item =>
            {
                Add(rows, "处理器", Text(item["Name"]).Trim());
                Add(rows, "核心 / 线程", Text(item["NumberOfCores"]) + " 核 / " + Text(item["NumberOfLogicalProcessors"]) + " 线程");
                Add(rows, "主频", Text(item["MaxClockSpeed"]) + " MHz");
                Add(rows, "三级缓存", Text(item["L3CacheSize"]) + " KB");
                Add(rows, "插槽", Text(item["SocketDesignation"]));
            });

            // 内存条
            TryQuery("SELECT Capacity, Speed, Manufacturer, PartNumber, DeviceLocator, MemoryType, SMBIOSMemoryType FROM Win32_PhysicalMemory", item =>
            {
                var capacity = ToDouble(item["Capacity"]);
                Add(rows, "内存条 " + Text(item["DeviceLocator"]),
                    Native.FormatBytes(capacity) + " · " + Text(item["Speed"]) + " MHz · " +
                    Text(item["Manufacturer"]).Trim() + " " + Text(item["PartNumber"]).Trim());
            });

            // 主板 / BIOS
            TryQuery("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard", item =>
            {
                Add(rows, "主板", (Text(item["Manufacturer"]) + " " + Text(item["Product"])).Trim());
                Add(rows, "主板序列号", Text(item["SerialNumber"]));
            });
            TryQuery("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", item =>
            {
                Add(rows, "BIOS", Text(item["Manufacturer"]) + " " + Text(item["SMBIOSBIOSVersion"]));
                var date = ToDate(item["ReleaseDate"]);
                if (date != null)
                {
                    Add(rows, "BIOS 日期", date.Value.ToString("yyyy-MM-dd"));
                }
            });

            // 显卡
            TryQuery("SELECT Name, DriverVersion, DriverDate, AdapterRAM, VideoModeDescription FROM Win32_VideoController", item =>
            {
                Add(rows, "显卡", Text(item["Name"]));
                Add(rows, "显卡驱动", Text(item["DriverVersion"]));
                var date = ToDate(item["DriverDate"]);
                if (date != null)
                {
                    Add(rows, "驱动日期", date.Value.ToString("yyyy-MM-dd"));
                }
            });

            // 电池（笔记本）
            TryQuery("SELECT Name, EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery", item =>
            {
                Add(rows, "电池", Text(item["Name"]) + "（剩余 " + Text(item["EstimatedChargeRemaining"]) + "%）");
            });

            // 显示器
            Add(rows, "显示器", Native.GetPrimaryDisplayText() + "，共 " + Native.MonitorCount + " 台");
            Add(rows, "运行时长", SystemInfoHelper.GetUptime());

            // 激活状态
            var activation = GetActivationStatus();
            Add(rows, "系统激活", activation);

            return rows;
        }

        /// <summary>物理磁盘列表（含健康状态）。</summary>
        public static List<DiskInfo> GetDisks()
        {
            var list = new List<DiskInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT DeviceID, VolumeName, Size, FreeSpace, DriveType FROM Win32_LogicalDisk WHERE DriveType = 3"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var size = ToDouble(item["Size"]);
                        var free = ToDouble(item["FreeSpace"]);
                        if (size <= 0)
                        {
                            continue;
                        }

                        list.Add(new DiskInfo
                        {
                            Name = Text(item["DeviceID"]),
                            Label = string.IsNullOrWhiteSpace(Text(item["VolumeName"])) ? "本地磁盘" : Text(item["VolumeName"]),
                            SizeText = "共 " + Native.FormatBytes(size),
                            FreeText = "可用 " + Native.FormatBytes(free),
                            FreePercent = free / size * 100,
                            Health = "正常",
                            IsHealthy = true
                        });
                    }
                }
            }
            catch
            {
                // ignored
            }

            // 磁盘型号与健康
            try
            {
                var models = new List<string>();
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Model, Size, MediaType, InterfaceType, Status FROM Win32_DiskDrive"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var model = Text(item["Model"]).Trim();
                        var interfaceType = Text(item["InterfaceType"]);
                        var status = Text(item["Status"]);
                        var healthy = string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(status);
                        var text = model + "（" + interfaceType + "，" + Native.FormatBytes(ToDouble(item["Size"])) + "）";
                        if (!healthy)
                        {
                            text += " · 状态：" + status;
                        }

                        models.Add(text);
                    }
                }

                for (var i = 0; i < list.Count && i < models.Count; i++)
                {
                    list[i].Model = models[i];
                }
            }
            catch
            {
                // ignored
            }

            return list;
        }

        /// <summary>有问题的设备（驱动异常 / 资源冲突）。</summary>
        public static List<DeviceProblem> GetProblemDevices()
        {
            var list = new List<DeviceProblem>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Name, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
                {
                    foreach (var item in searcher.Get())
                    {
                        list.Add(new DeviceProblem
                        {
                            Name = Text(item["Name"]),
                            Class = Text(item["PNPClass"]),
                            Error = ErrorText(Text(item["ConfigManagerErrorCode"]))
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

        public static string GetActivationStatus()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND LicenseStatus = 1"))
                {
                    foreach (var item in searcher.Get())
                    {
                        return "已激活（密钥 " + Text(item["PartialProductKey"]) + "）";
                    }
                }

                return "未知（可能未激活或使用 KMS）";
            }
            catch
            {
                return "无法读取";
            }
        }

        public static string GetSystemSummary()
        {
            var parts = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        parts.Add(Text(item["Caption"]));
                        break;
                    }
                }
            }
            catch
            {
                // ignored
            }

            parts.Add(Environment.MachineName);
            return string.Join(" · ", parts);
        }

        public static string GetCpuName()
        {
            var result = string.Empty;
            TryQuery("SELECT Name FROM Win32_Processor", item => result = Text(item["Name"]).Trim());
            return string.IsNullOrEmpty(result) ? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") : result;
        }

        public static string GetGpuName()
        {
            var result = string.Empty;
            TryQuery("SELECT Name FROM Win32_VideoController", item =>
            {
                if (string.IsNullOrEmpty(result))
                {
                    result = Text(item["Name"]);
                }
            });
            return result;
        }

        public static string GetMemoryText()
        {
            var text = string.Empty;
            TryQuery("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem", item =>
            {
                double total;
                double free;
                if (double.TryParse(Text(item["TotalVisibleMemorySize"]), out total) &&
                    double.TryParse(Text(item["FreePhysicalMemory"]), out free))
                {
                    text = Native.FormatBytes(total * 1024) + "（可用 " + Native.FormatBytes(free * 1024) + "）";
                }
            });
            return text;
        }

        public static string GetOsText()
        {
            var caption = string.Empty;
            var build = string.Empty;
            TryQuery("SELECT Caption, BuildNumber FROM Win32_OperatingSystem", item =>
            {
                caption = Text(item["Caption"]);
                build = Text(item["BuildNumber"]);
            });
            return caption + (string.IsNullOrEmpty(build) ? "" : " · Build " + build);
        }

        public static string GetMachineText()
        {
            return Environment.MachineName + " · " + Environment.UserName;
        }

        public static string GetUptimeText()
        {
            return SystemInfoHelper.GetUptime();
        }

        private static string DetectBootMode()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control"))
                {
                    var value = key?.GetValue("PEFirmwareType");
                    if (value != null)
                    {
                        return Convert.ToInt32(value) == 2 ? "UEFI" : "Legacy BIOS";
                    }
                }
            }
            catch
            {
                // ignored
            }

            return "Legacy BIOS";
        }

        private static void TryQuery(string wql, Action<ManagementObject> handler)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(wql))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        handler((ManagementObject)item);
                    }
                }
            }
            catch
            {
                // WMI 不可用时跳过
            }
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

        private static DateTime? ToDate(object value)
        {
            try
            {
                var text = Text(value);
                if (text.Length >= 8)
                {
                    return ManagementDateTimeConverter.ToDateTime(text);
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static string ErrorText(string code)
        {
            switch (code)
            {
                case "10": return "设备无法启动";
                case "12": return "资源冲突";
                case "18": return "需要重新安装驱动程序";
                case "24": return "设备未安装";
                case "28": return "缺少驱动程序";
                case "31": return "无法加载驱动程序";
                case "43": return "驱动报告设备故障";
                default: return "错误代码 " + code;
            }
        }
    }
}
