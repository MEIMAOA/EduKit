using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace EduKit.Core
{
    public class TubaTool
    {
        public string Name { get; set; }

        public string Category { get; set; }

        public string Path { get; set; }
    }

    /// <summary>
    /// 图吧工具箱（图拉丁吧工具箱）本地集成：自动查找安装目录、按分类列出其中的工具并启动。
    /// 本项目不打包、不重新分发图吧工具箱的任何二进制文件，仅在用户本机已安装时提供入口。
    /// </summary>
    public static class TubaToolbox
    {
        public const string OfficialSite = "https://www.tbtool.cn/";

        private static readonly string[] FolderNames = { "图吧工具箱", "图拉丁吧工具箱", "图吧工具", "TuBaTool", "TB工具箱" };

        private static readonly string[] IgnoredExeKeywords =
        {
            "unins", "setup", "install", "update", "crash", "report", "helper", "vcredist",
            "dxwebsetup", "bcdboot", "bootsect", "uninstall"
        };

        private static readonly Dictionary<string, string> KnownTools = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "cpuz", "CPU-Z" }, { "cpu-z", "CPU-Z" }, { "cpu_z", "CPU-Z" },
            { "gpuz", "GPU-Z" }, { "gpu-z", "GPU-Z" },
            { "aida64", "AIDA64" }, { "hwinfo", "HWiNFO" },
            { "crystaldiskinfo", "CrystalDiskInfo" }, { "crystaldiskmark", "CrystalDiskMark" },
            { "diskgenius", "DiskGenius" }, { "hdtune", "HD Tune" }, { "asssd", "AS SSD" }, { "as ssd", "AS SSD" },
            { "memtest", "MemTest" }, { "prime95", "Prime95" }, { "furmark", "FurMark" },
            { "superpi", "SuperPI" }, { "cinebench", "Cinebench" }, { "3dmark", "3DMark" },
            { "7zip", "7-Zip" }, { "7-zip", "7-Zip" }, { "winrar", "WinRAR" },
            { "dism++", "Dism++" }, { "everything", "Everything" }, { "batteryinfoview", "BatteryInfoView" },
            { "bluescreenview", "BlueScreenView" }, { "directx repair", "DirectX Repair" },
            { "aida", "AIDA64" }, { "cpuid", "CPUID" }, { "throttlestop", "ThrottleStop" },
            { "screentest", "屏幕测试" }, { "屏幕测试", "屏幕测试" }, { "键盘检测", "键盘检测" },
            { "keyboardtest", "键盘检测" }, { "mouse", "鼠标测试" }, { "显示器", "显示器工具" },
            { "ssd", "SSD 工具" }, { "benchmark", "性能测试" }, { "考机", "烤机测试" }, { "烤机", "烤机测试" }
        };

        public static string Detect()
        {
            var configured = AppData.Settings?.TubaPath;
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            {
                return configured;
            }

            var candidates = new List<string>();

            foreach (var drive in Directory.GetLogicalDrives())
            {
                candidates.Add(Path.Combine(drive, "图吧工具箱"));
                candidates.Add(Path.Combine(drive, "图拉丁吧工具箱"));
                candidates.Add(Path.Combine(drive, "TuBaTool"));
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (var name in FolderNames)
            {
                candidates.Add(Path.Combine(programFiles, name));
                candidates.Add(Path.Combine(programFilesX86, name));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), name));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", name));
            }

            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir))
                {
                    return dir;
                }
            }

            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var sub in new[]
                         {
                             @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                             @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                         })
                {
                    try
                    {
                        using (var root = hive.OpenSubKey(sub))
                        {
                            if (root == null)
                            {
                                continue;
                            }

                            foreach (var name in root.GetSubKeyNames())
                            {
                                using (var key = root.OpenSubKey(name))
                                {
                                    var display = key?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                                    if (!display.Contains("图吧") && !display.Contains("图拉丁") &&
                                        !display.ToLowerInvariant().Contains("tubatool"))
                                    {
                                        continue;
                                    }

                                    var location = key.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location))
                                    {
                                        return location;
                                    }

                                    var icon = key.GetValue("DisplayIcon")?.ToString();
                                    if (!string.IsNullOrWhiteSpace(icon))
                                    {
                                        var dir = Path.GetDirectoryName(icon.Trim('"').Split(',')[0]);
                                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                                        {
                                            return dir;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return null;
        }

        /// <summary>扫描目录（图吧工具箱的版本目录 / tools 目录），按分类列出工具。</summary>
        public static List<TubaTool> Scan(string root)
        {
            var tools = new List<TubaTool>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return tools;
            }

            try
            {
                var toolsDirs = new List<string>();
                var direct = Path.Combine(root, "tools");
                if (Directory.Exists(direct))
                {
                    toolsDirs.Add(direct);
                }

                foreach (var versionDir in Directory.GetDirectories(root))
                {
                    var toolsDir = Path.Combine(versionDir, "tools");
                    if (Directory.Exists(toolsDir))
                    {
                        toolsDirs.Add(toolsDir);
                    }
                }

                // 多个版本时只取名称最大的（最新版本），避免重复
                toolsDirs = toolsDirs.OrderByDescending(d => d).Take(1).ToList();
                if (toolsDirs.Count == 0)
                {
                    return ScanPlain(root);
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var toolsDir in toolsDirs)
                {
                    foreach (var categoryDir in Directory.GetDirectories(toolsDir))
                    {
                        var category = ShortCategory(Path.GetFileName(categoryDir));
                        AddToolsFromFolder(tools, seen, categoryDir, category, Path.GetFileName(categoryDir));

                        foreach (var toolDir in Directory.GetDirectories(categoryDir))
                        {
                            AddToolsFromFolder(tools, seen, toolDir, category, Path.GetFileName(toolDir));
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return tools
                .GroupBy(t => t.Name + "|" + t.Category)
                .Select(g => g.First())
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Name)
                .Take(400)
                .ToList();
        }

        private static void AddToolsFromFolder(List<TubaTool> tools, HashSet<string> seen, string dir,
            string category, string folderName)
        {
            try
            {
                foreach (var exe in Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileNameWithoutExtension(exe);
                    var lower = fileName.ToLowerInvariant();
                    if (IgnoredExeKeywords.Any(k => lower.Contains(k)))
                    {
                        continue;
                    }

                    if (!seen.Add(exe))
                    {
                        continue;
                    }

                    tools.Add(new TubaTool
                    {
                        Name = ResolveName(folderName, fileName),
                        Category = category,
                        Path = exe
                    });
                }
            }
            catch
            {
                // ignored
            }
        }

        private static string ResolveName(string folderName, string fileName)
        {
            foreach (var pair in KnownTools)
            {
                if (folderName.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pair.Value;
                }
            }

            return fileName;
        }

        private static string ShortCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "其他工具";
            }

            return category.Length > 8 ? category.Substring(0, 8) : category;
        }

        private static List<TubaTool> ScanPlain(string root)
        {
            var tools = new List<TubaTool>();
            try
            {
                foreach (var exe in Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories).Take(200))
                {
                    var fileName = Path.GetFileNameWithoutExtension(exe);
                    var lower = fileName.ToLowerInvariant();
                    if (IgnoredExeKeywords.Any(k => lower.Contains(k)))
                    {
                        continue;
                    }

                    tools.Add(new TubaTool
                    {
                        Name = ResolveName(Path.GetFileName(Path.GetDirectoryName(exe)), fileName),
                        Category = "工具",
                        Path = exe
                    });
                }
            }
            catch
            {
                // ignored
            }

            return tools;
        }

        public static bool Launch(TubaTool tool, out string error)
        {
            error = null;
            try
            {
                if (tool == null || !File.Exists(tool.Path))
                {
                    error = "文件不存在";
                    return false;
                }

                Native.Start(tool.Path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}