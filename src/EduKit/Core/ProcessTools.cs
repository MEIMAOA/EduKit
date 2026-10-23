using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Win32;

namespace EduKit.Core
{
    public class ProcessRow
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Memory { get; set; }

        public string Company { get; set; }

        public string Path { get; set; }
    }

    public class StartupItem
    {
        public string Name { get; set; }

        public string Command { get; set; }

        public string Source { get; set; }

        public bool IsRegistry { get; set; }

        public string RegistryKey { get; set; }

        public bool IsMachine { get; set; }
    }

    public class ServiceInfo
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Status { get; set; }

        public string StartType { get; set; }

        public bool IsRunning { get; set; }
    }

    /// <summary>进程 / 启动项 / 服务管理。</summary>
    public static class ProcessTools
    {
        public static List<ProcessRow> GetProcesses()
        {
            var list = new List<ProcessRow>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var row = new ProcessRow
                    {
                        Id = process.Id,
                        Name = process.ProcessName + ".exe",
                        Memory = Native.FormatBytes(process.WorkingSet64)
                    };

                    try
                    {
                        row.Path = process.MainModule?.FileName ?? string.Empty;
                    }
                    catch
                    {
                        row.Path = string.Empty;
                    }

                    try
                    {
                        row.Company = process.MainModule?.FileVersionInfo?.CompanyName ?? string.Empty;
                    }
                    catch
                    {
                        row.Company = string.Empty;
                    }

                    list.Add(row);
                }
                catch
                {
                    // 受保护进程跳过
                }
                finally
                {
                    process.Dispose();
                }
            }

            return list.OrderByDescending(p => p.Memory.Length).ThenBy(p => p.Name).ToList();
        }

        public static bool KillProcess(int id, out string error)
        {
            error = null;
            try
            {
                using (var process = Process.GetProcessById(id))
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool OpenFileLocation(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return false;
                }

                return Native.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch
            {
                return false;
            }
        }

        public static List<StartupItem> GetStartupItems()
        {
            var list = new List<StartupItem>();
            ReadRunKey(list, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM 启动项");
            ReadRunKey(list, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM 启动项(32 位)");
            ReadRunKey(list, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU 启动项");

            foreach (var folder in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                         Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
                     })
            {
                try
                {
                    if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    {
                        continue;
                    }

                    foreach (var file in Directory.GetFiles(folder))
                    {
                        list.Add(new StartupItem
                        {
                            Name = Path.GetFileName(file),
                            Command = file,
                            Source = folder == Environment.GetFolderPath(Environment.SpecialFolder.Startup)
                                ? "当前用户启动文件夹"
                                : "公共启动文件夹",
                            IsRegistry = false
                        });
                    }
                }
                catch
                {
                    // ignored
                }
            }

            return list;
        }

        public static bool DeleteStartupItem(StartupItem item, out string error)
        {
            error = null;
            try
            {
                if (!item.IsRegistry)
                {
                    File.Delete(item.Command);
                    return true;
                }

                var hive = item.IsMachine ? Registry.LocalMachine : Registry.CurrentUser;
                using (var key = hive.OpenSubKey(item.RegistryKey, true))
                {
                    key?.DeleteValue(item.Name, false);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static List<ServiceInfo> GetServices()
        {
            var list = new List<ServiceInfo>();
            try
            {
                foreach (var service in ServiceController.GetServices())
                {
                    try
                    {
                        list.Add(new ServiceInfo
                        {
                            Name = service.ServiceName,
                            DisplayName = string.IsNullOrWhiteSpace(service.DisplayName) ? service.ServiceName : service.DisplayName,
                            Status = service.Status.ToString(),
                            StartType = GetStartType(service.ServiceName),
                            IsRunning = service.Status == ServiceControllerStatus.Running
                        });
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        service.Dispose();
                    }
                }
            }
            catch
            {
                // ignored
            }

            return list.OrderByDescending(s => s.IsRunning).ThenBy(s => s.DisplayName).ToList();
        }

        public static bool ControlService(string name, string action, out string error)
        {
            error = null;
            try
            {
                using (var service = new ServiceController(name))
                {
                    var timeout = TimeSpan.FromSeconds(15);
                    switch (action)
                    {
                        case "start":
                            if (service.Status != ServiceControllerStatus.Running)
                            {
                                service.Start();
                                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            }

                            break;
                        case "stop":
                            if (service.Status != ServiceControllerStatus.Stopped && service.CanStop)
                            {
                                service.Stop();
                                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            }

                            break;
                        case "restart":
                            if (service.CanStop)
                            {
                                service.Stop();
                                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            }

                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            break;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string GetStartType(string serviceName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName))
                {
                    var value = key?.GetValue("Start");
                    if (value == null)
                    {
                        return "未知";
                    }

                    switch (Convert.ToInt32(value))
                    {
                        case 0: return "引导";
                        case 1: return "系统";
                        case 2: return "自动";
                        case 3: return "手动";
                        case 4: return "已禁用";
                        default: return "未知";
                    }
                }
            }
            catch
            {
                return "未知";
            }
        }

        private static void ReadRunKey(List<StartupItem> list, RegistryKey root, string path, string source)
        {
            try
            {
                using (var key = root.OpenSubKey(path, false))
                {
                    if (key == null)
                    {
                        return;
                    }

                    foreach (var name in key.GetValueNames())
                    {
                        list.Add(new StartupItem
                        {
                            Name = name,
                            Command = key.GetValue(name)?.ToString() ?? string.Empty,
                            Source = source,
                            IsRegistry = true,
                            RegistryKey = path,
                            IsMachine = ReferenceEquals(root, Registry.LocalMachine)
                        });
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
