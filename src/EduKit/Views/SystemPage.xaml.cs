using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class SystemPage : UserControl
    {
        private bool _loading;

        public SystemPage()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh();
        }

        private async void Refresh()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            CleanHint.Text = "正在统计缓存大小…";
            try
            {
                var admin = Elevation.IsAdministrator;
                AdminText.Text = admin ? "管理员权限" : "普通权限";
                AdminChip.Background = (Brush)FindResource(admin ? "SuccessSoft" : "BgHover");
                AdminText.Foreground = (Brush)FindResource(admin ? "Success" : "TextSecondary");

                DiskList.ItemsSource = await Task.Run(() => HardwareDetails.GetDisks());
                var items = await Task.Run(() => SystemCleanup.Scan());
                CleanList.ItemsSource = items;
                var total = items.Sum(i => i.Size);
                CleanHint.Text = "共可清理约 " + Native.FormatBytes(total) + "（Windows 更新缓存需要管理员权限）";
            }
            catch (Exception ex)
            {
                Ui.Warn("读取维护信息失败：" + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private async void CleanSafe_Click(object sender, RoutedEventArgs e)
        {
            var items = (CleanList.ItemsSource as System.Collections.Generic.List<CleanupItem>);
            if (items == null || items.Count == 0)
            {
                Refresh();
                return;
            }

            var targets = items.Where(i => !i.NeedsAdmin).ToList();
            var total = targets.Sum(i => i.Size);
            if (!Ui.Confirm("将清理临时文件、系统缓存和回收站，共约 " + Native.FormatBytes(total) + "。\r\n\r\n继续吗？"))
            {
                return;
            }

            await CleanAsync(targets);
        }

        private async void CleanAll_Click(object sender, RoutedEventArgs e)
        {
            var items = (CleanList.ItemsSource as System.Collections.Generic.List<CleanupItem>);
            if (items == null || items.Count == 0)
            {
                Refresh();
                return;
            }

            var admin = Elevation.IsAdministrator;
            var elevateItem = items.FirstOrDefault(i => i.NeedsAdmin);
            if (elevateItem != null && !admin)
            {
                if (!Ui.Confirm("Windows 更新缓存需要管理员权限，将打开 UAC 提权窗口清理。\r\n\r\n继续吗？"))
                {
                    return;
                }

                string error;
                var ok = Elevation.RunAsAdmin("cmd.exe",
                    "/c del /f /s /q \"%windir%\\SoftwareDistribution\\Download\\*.*\" & pause", out error);
                if (!ok)
                {
                    Ui.Warn("提权失败：" + error);
                }

                await CleanAsync(items.Where(i => !i.NeedsAdmin).ToList());
                Refresh();
                return;
            }

            if (!Ui.Confirm("将清理所有缓存项，共约 " + Native.FormatBytes(items.Sum(i => i.Size)) + "。\r\n\r\n继续吗？"))
            {
                return;
            }

            await CleanAsync(items);
        }

        private async Task CleanAsync(System.Collections.Generic.List<CleanupItem> items)
        {
            CleanHint.Text = "正在清理…";
            var freed = await Task.Run(() =>
            {
                long total = 0;
                foreach (var item in items)
                {
                    total += SystemCleanup.Clean(item);
                }

                return total;
            });

            CleanHint.Text = "已释放 " + Native.FormatBytes(freed);
            Refresh();
        }

        private void DiskCleanup_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("cleanmgr.exe");
        }

        private void Sfc_Click(object sender, RoutedEventArgs e)
        {
            RunElevated("sfc /scannow", "系统文件检查");
        }

        private void Dism_Click(object sender, RoutedEventArgs e)
        {
            RunElevated("DISM /Online /Cleanup-Image /RestoreHealth", "DISM 修复");
        }

        private void ResetNetwork_Click(object sender, RoutedEventArgs e)
        {
            RunElevated("netsh winsock reset & netsh int ip reset & ipconfig /flushdns", "网络重置");
        }

        private void FlushDns_Click(object sender, RoutedEventArgs e)
        {
            Native.StartQuiet("ipconfig.exe", "/flushdns");
            Ui.Info("DNS 缓存已刷新。");
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("rstrui.exe");
        }

        private void EventViewer_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("eventvwr.msc");
        }

        private void Minidump_Click(object sender, RoutedEventArgs e)
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
            Native.Start("explorer.exe", System.IO.Directory.Exists(dir) ? "\"" + dir + "\"" : "\"" + Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\"");
        }

        private void DeviceManager_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("devmgmt.msc");
        }

        private void Services_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("services.msc");
        }

        private void Msinfo_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("msinfo32.exe");
        }

        private void Regedit_Click(object sender, RoutedEventArgs e) { Native.Start("regedit.exe"); }
        private void Taskschd_Click(object sender, RoutedEventArgs e) { Native.Start("taskschd.msc"); }
        private void Gpedit_Click(object sender, RoutedEventArgs e) { Native.Start("gpedit.msc"); }
        private void EnvVars_Click(object sender, RoutedEventArgs e) { Native.Start("SystemPropertiesAdvanced.exe"); }
        private void PerfOptions_Click(object sender, RoutedEventArgs e) { Native.Start("SystemPropertiesPerformance.exe"); }
        private void Accounts_Click(object sender, RoutedEventArgs e) { Native.Start("netplwiz.exe"); }
        private void PowerOptions_Click(object sender, RoutedEventArgs e) { Native.Start("powercfg.cpl"); }

        private void Balanced_Click(object sender, RoutedEventArgs e)
        {
            Native.StartQuiet("powercfg.exe", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            Ui.Info("已切换到「平衡」电源计划。");
        }

        private void HighPerf_Click(object sender, RoutedEventArgs e)
        {
            Native.StartQuiet("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            Ui.Info("已切换到「高性能」电源计划。");
        }

        private async void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await Task.Run(() =>
                {
                    var builder = new System.Text.StringBuilder();
                    builder.AppendLine("SysKit 系统报告 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    builder.AppendLine();
                    foreach (var row in HardwareDetails.GetHardwareInfo())
                    {
                        builder.AppendLine(row.Label + "：" + row.Value);
                    }

                    builder.AppendLine();
                    builder.AppendLine("【磁盘】");
                    foreach (var disk in HardwareDetails.GetDisks())
                    {
                        builder.AppendLine(disk.Name + "  " + disk.Label + "  " + disk.Model + "  " + disk.SizeText +
                                           " / " + disk.FreeText);
                    }

                    builder.AppendLine();
                    builder.AppendLine("【有问题的设备】");
                    foreach (var device in HardwareDetails.GetProblemDevices())
                    {
                        builder.AppendLine(device.Name + " -> " + device.Error);
                    }

                    var path = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "SysKit系统报告-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                    System.IO.File.WriteAllText(path, builder.ToString(), System.Text.Encoding.UTF8);
                    return path;
                });

                if (Ui.Confirm("系统报告已导出到：\r\n" + file + "\r\n\r\n要立即打开吗？"))
                {
                    Native.Start(file);
                }
            }
            catch (Exception ex)
            {
                Ui.Error("导出失败：" + ex.Message);
            }
        }

        private void BatteryReport_Click(object sender, RoutedEventArgs e)
        {
            var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "battery-report.html");
            Native.StartQuiet("powercfg.exe", "/batteryreport /output \"" + output + "\"");
            System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (System.IO.File.Exists(output))
                {
                    Native.Start(output);
                }
                else
                {
                    Ui.Info("电池报告生成失败（台式机可能没有电池）。");
                }
            })));
        }

        private void RestorePoint_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("将在当前时间创建系统还原点（需要管理员权限且系统保护已开启）。\r\n\r\n继续吗？"))
            {
                return;
            }

            try
            {
                var scope = new System.Management.ManagementScope("root\\default");
                var path = new System.Management.ManagementPath("SystemRestore");
                using (var classObj = new System.Management.ManagementClass(scope, path, null))
                {
                    var parameters = classObj.GetMethodParameters("CreateRestorePoint");
                    parameters["Description"] = "SysKit 创建还原点";
                    parameters["RestorePointType"] = 0;
                    parameters["EventType"] = 100;
                    classObj.InvokeMethod("CreateRestorePoint", parameters, null);
                }

                Ui.Info("还原点创建请求已提交（如系统保护未开启会静默失败）。");
            }
            catch (Exception ex)
            {
                Ui.Error("创建还原点失败：" + ex.Message);
            }
        }

        private void RunElevated(string command, string title)
        {
            string error;
            if (!Elevation.RunAsAdmin("cmd.exe", "/k " + command, out error))
            {
                Ui.Warn(title + " 需要管理员权限，提权失败：" + error);
            }
        }
    }
}