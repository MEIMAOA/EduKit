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