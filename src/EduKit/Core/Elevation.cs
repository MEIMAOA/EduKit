using System;
using System.Diagnostics;
using System.Security.Principal;

namespace EduKit.Core
{
    /// <summary>管理员权限检测与提权运行。</summary>
    public static class Elevation
    {
        public static bool IsAdministrator
        {
            get
            {
                try
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        var principal = new WindowsPrincipal(identity);
                        return principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>以管理员身份运行命令（会弹出 UAC 提示）。</summary>
        public static bool RunAsAdmin(string fileName, string arguments, out string error)
        {
            error = null;
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(info);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>以管理员身份运行 cmd 命令并等待窗口。</summary>
        public static bool RunCommandAsAdmin(string command, out string error)
        {
            return RunAsAdmin("cmd.exe", "/c " + command, out error);
        }
    }
}
