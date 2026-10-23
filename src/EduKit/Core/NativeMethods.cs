using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace EduKit.Core
{
    /// <summary>Win32 调用封装：电源、显示器、音量、截图、进程启动等。</summary>
    internal static class Native
    {
        public const int SmCxScreen = 0;
        public const int SmCyScreen = 1;
        public const int SmCMonitors = 80;
        public const int WmSysCommand = 0x0112;
        public const int ScMonitorPower = 0xF170;
        public const int HwndBroadcast = 0xFFFF;
        public const int EnumerateCurrentSettings = -1;
        public const int CdsUpdateRegistry = 0x00000001;

        public const byte VkMediaNextTrack = 0xB0;
        public const byte VkMediaPrevTrack = 0xB1;
        public const byte VkMediaStop = 0xB2;
        public const byte VkMediaPlayPause = 0xB3;
        public const byte VkVolumeMute = 0xAD;
        public const byte VkVolumeDown = 0xAE;
        public const byte VkVolumeUp = 0xAF;

        private const uint KeyeventfKeyup = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int ChangeDisplaySettings(ref DevMode devMode, int flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        public static int ScreenWidth => GetSystemMetrics(SmCxScreen);

        public static int ScreenHeight => GetSystemMetrics(SmCyScreen);

        public static int MonitorCount => Math.Max(1, GetSystemMetrics(SmCMonitors));

        /// <summary>关闭显示器（节能，不影响系统运行）。</summary>
        public static void TurnOffMonitor()
        {
            SendMessage((IntPtr)HwndBroadcast, WmSysCommand, (IntPtr)ScMonitorPower, (IntPtr)2);
        }

        public static bool LockScreen()
        {
            return LockWorkStation();
        }

        public static bool Sleep()
        {
            try
            {
                if (SetSuspendState(false, false, false))
                {
                    return true;
                }
            }
            catch
            {
                // fallback below
            }

            return Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
        }

        public static void Shutdown(int seconds, bool restart, bool logoff)
        {
            var flag = logoff ? "/l" : restart ? "/r" : "/s";
            Start("shutdown.exe", flag + " /t " + Math.Max(0, seconds));
        }

        public static void CancelShutdown()
        {
            Start("shutdown.exe", "/a");
        }

        public static void MediaKey(byte virtualKey)
        {
            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKey, 0, KeyeventfKeyup, UIntPtr.Zero);
        }

        /// <summary>切换显示模式：clone / extend / internal / external。</summary>
        public static bool SetDisplayMode(string mode)
        {
            var known = new[] { "clone", "extend", "internal", "external" };
            if (Array.IndexOf(known, mode) < 0)
            {
                return false;
            }

            return Start("DisplaySwitch.exe", "/" + mode);
        }

        public static bool Start(string fileName, string arguments = null)
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
                };

                if (arguments != null)
                {
                    info.Arguments = arguments;
                }

                Process.Start(info);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool StartQuiet(string fileName, string arguments)
        {
            try
            {
                Process.Start(new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>全屏截图，保存到“图片\EduKit”目录，返回文件路径。</summary>
        public static string CaptureScreen()
        {
            var width = Math.Max(1, ScreenWidth);
            var height = Math.Max(1, ScreenHeight);
            using (var bitmap = new Bitmap(width, height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));
                }

                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "EduKit");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                bitmap.Save(file, ImageFormat.Png);
                return file;
            }
        }

        public static string GetPrimaryDisplayText()
        {
            var mode = GetCurrentMode();
            if (mode.dmPelsWidth > 0)
            {
                return mode.dmPelsWidth + " × " + mode.dmPelsHeight + " @ " + mode.dmDisplayFrequency + " Hz";
            }

            return ScreenWidth + " × " + ScreenHeight;
        }

        public static DevMode GetCurrentMode()
        {
            var mode = new DevMode();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DevMode));
            EnumDisplaySettings(null, EnumerateCurrentSettings, ref mode);
            return mode;
        }

        /// <summary>返回主显示器可用的分辨率列表（去重，按像素降序）。</summary>
        public static System.Collections.Generic.List<DevMode> GetAvailableModes()
        {
            var list = new System.Collections.Generic.List<DevMode>();
            var seen = new System.Collections.Generic.HashSet<string>();
            var mode = new DevMode();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DevMode));

            for (var i = 0; EnumDisplaySettings(null, i, ref mode); i++)
            {
                if (mode.dmPelsWidth < 800 || mode.dmBitsPerPel < 16)
                {
                    continue;
                }

                var key = mode.dmPelsWidth + "x" + mode.dmPelsHeight + "x" + mode.dmBitsPerPel;
                if (seen.Add(key))
                {
                    var copy = mode;
                    list.Add(copy);
                }

                if (i > 500)
                {
                    break;
                }
            }

            list.Sort((a, b) =>
            {
                var result = b.dmPelsWidth.CompareTo(a.dmPelsWidth);
                return result != 0 ? result : b.dmPelsHeight.CompareTo(a.dmPelsHeight);
            });
            return list;
        }

        public static int ApplyDisplayMode(int width, int height, int bitsPerPel)
        {
            var mode = GetCurrentMode();
            if (mode.dmPelsWidth <= 0)
            {
                return -1;
            }

            mode.dmPelsWidth = width;
            mode.dmPelsHeight = height;
            mode.dmBitsPerPel = bitsPerPel;
            mode.dmFields = 0x00080000 /* DM_PELSWIDTH */ | 0x00100000 /* DM_PELSHEIGHT */ | 0x00040000 /* DM_BITSPERPEL */;

            return ChangeDisplaySettings(ref mode, CdsUpdateRegistry);
        }

        public static void RestoreDisplayMode(DevMode mode)
        {
            var copy = mode;
            ChangeDisplaySettings(ref copy, CdsUpdateRegistry);
        }

        /// <summary>格式化字节数为可读文本。</summary>
        public static string FormatBytes(double bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var index = 0;
            while (bytes >= 1024 && index < units.Length - 1)
            {
                bytes /= 1024;
                index++;
            }

            return bytes.ToString(bytes >= 100 || index == 0 ? "0" : "0.0") + " " + units[index];
        }

        public static bool IsWindows7OrLower()
        {
            return Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor <= 1;
        }

        public static void OpenDataFolder()
        {
            Start("explorer.exe", "\"" + AppData.Dir + "\"");
        }

        public static void OpenUrl(string url)
        {
            Start(url);
        }

        public static void OpenControlPanel(string page)
        {
            Start("control.exe", page);
        }

        public static void SetWindowForeground(Window window)
        {
            if (window == null)
            {
                return;
            }

            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            SetForegroundWindow(helper.Handle);
        }
    }
}
