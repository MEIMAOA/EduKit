using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace EduKit.Core
{
    public class CleanupItem
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public long Size { get; set; }

        public string SizeText => Native.FormatBytes(Size);

        public bool NeedsAdmin { get; set; }
    }

    /// <summary>系统垃圾与缓存清理（安全项优先，管理员项单独标记）。</summary>
    public static class SystemCleanup
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ShQueryRbInfo
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string rootPath, ref ShQueryRbInfo info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        private const int SherbNoConfirmation = 0x1;
        private const int SherbNoProgressUi = 0x2;
        private const int SherbNoSound = 0x4;

        public static List<CleanupItem> Scan()
        {
            var items = new List<CleanupItem>
            {
                new CleanupItem
                {
                    Name = "临时文件",
                    Description = "用户临时目录与 Windows Temp 中的缓存文件",
                    Size = GetTempSize()
                },
                new CleanupItem
                {
                    Name = "系统缓存",
                    Description = "缩略图缓存、错误报告、旧日志",
                    Size = GetCacheSize()
                },
                new CleanupItem
                {
                    Name = "回收站",
                    Description = "清空回收站中已删除的文件",
                    Size = GetRecycleBinSize()
                },
                new CleanupItem
                {
                    Name = "Windows 更新缓存",
                    Description = "已安装更新留下的安装包缓存（需要管理员权限）",
                    Size = GetWindowsUpdateCacheSize(),
                    NeedsAdmin = true
                }
            };

            return items;
        }

        public static long GetTempSize()
        {
            return SystemInfoHelper.GetTempFolderSize() + FolderSize(Path.GetTempPath());
        }

        public static long GetCacheSize()
        {
            long total = 0;
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            total += FolderSize(Path.Combine(local, "Microsoft", "Windows", "Explorer"));
            total += FolderSize(Path.Combine(local, "Microsoft", "Windows", "WER"));
            total += FolderSize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows", "WER"));
            return total;
        }

        public static long GetRecycleBinSize()
        {
            try
            {
                var info = new ShQueryRbInfo { cbSize = Marshal.SizeOf(typeof(ShQueryRbInfo)) };
                if (SHQueryRecycleBin(null, ref info) == 0)
                {
                    return info.i64Size;
                }
            }
            catch
            {
                // ignored
            }

            return 0;
        }

        public static long GetWindowsUpdateCacheSize()
        {
            return FolderSize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SoftwareDistribution", "Download"));
        }

        /// <summary>执行清理，返回释放的字节数。</summary>
        public static long Clean(CleanupItem item)
        {
            if (item == null)
            {
                return 0;
            }

            switch (item.Name)
            {
                case "临时文件":
                    return SystemInfoHelper.CleanTempFolder() + CleanFolder(Path.GetTempPath());
                case "系统缓存":
                    return CleanCache();
                case "回收站":
                    return EmptyRecycleBin();
                case "Windows 更新缓存":
                    return CleanFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "SoftwareDistribution", "Download"));
                default:
                    return 0;
            }
        }

        private static long CleanCache()
        {
            long freed = 0;
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            freed += CleanFolder(Path.Combine(local, "Microsoft", "Windows", "Explorer"));
            freed += CleanFolder(Path.Combine(local, "Microsoft", "Windows", "WER"));
            return freed;
        }

        private static long CleanFolder(string dir)
        {
            long freed = 0;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return 0;
            }

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
                        freed += size;
                    }
                    catch
                    {
                        // 占用中跳过
                    }
                }
            }
            catch
            {
                // ignored
            }

            return freed;
        }

        private static long FolderSize(string dir)
        {
            long total = 0;
            try
            {
                if (!Directory.Exists(dir))
                {
                    return 0;
                }

                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            catch
            {
                // ignored
            }

            return total;
        }

        private static long EmptyRecycleBin()
        {
            var before = GetRecycleBinSize();
            try
            {
                var result = SHEmptyRecycleBin(IntPtr.Zero, null,
                    SherbNoConfirmation | SherbNoProgressUi | SherbNoSound);
                if (result == 0)
                {
                    return before;
                }
            }
            catch
            {
                // ignored
            }

            return 0;
        }
    }
}
