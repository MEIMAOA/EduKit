using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class SystemPage : UserControl
    {
        private bool _loading;

        public SystemPage()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshAll();
        }

        private async void RefreshAll()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            InfoList.ItemsSource = null;
            DiskList.ItemsSource = null;
            TempSizeText.Text = "正在统计临时文件…";

            try
            {
                var info = await Task.Run(() => SystemInfoHelper.GetHardwareInfo());
                InfoList.ItemsSource = info;

                var disks = await Task.Run(() => SystemInfoHelper.GetDisks());
                DiskList.ItemsSource = disks;

                var tempSize = await Task.Run(() => SystemInfoHelper.GetTempFolderSize());
                TempSizeText.Text = "当前临时文件约 " + Native.FormatBytes(tempSize);
            }
            catch (Exception ex)
            {
                Ui.Warn("读取系统信息失败：" + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private async void CleanTemp_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("将删除当前用户的临时文件夹内容（正在被占用的文件会自动跳过）。\r\n\r\n继续吗？"))
            {
                return;
            }

            TempSizeText.Text = "正在清理…";
            var removed = await Task.Run(() => SystemInfoHelper.CleanTempFolder());
            TempSizeText.Text = "已清理 " + Native.FormatBytes(removed) + " 临时文件";
            await Task.Delay(100);
            RefreshAll();
        }

        private void RestartExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("重启资源管理器会让桌面和任务栏闪一下，正在运行的程序不受影响。\r\n\r\n继续吗？"))
            {
                return;
            }

            try
            {
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                Task.Delay(600).ContinueWith(_ => Native.Start("explorer.exe"));
                Ui.Info("已重启资源管理器。");
            }
            catch (Exception ex)
            {
                Ui.Error("重启资源管理器失败：" + ex.Message);
            }
        }

        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = Native.CaptureScreen();
                if (Ui.Confirm("截图已保存到：\r\n" + file + "\r\n\r\n要打开所在文件夹吗？"))
                {
                    Native.Start("explorer.exe", "/select,\"" + file + "\"");
                }
            }
            catch (Exception ex)
            {
                Ui.Error("截图失败：" + ex.Message);
            }
        }

        private void OpenData_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenDataFolder();
        }

        private void TaskManager_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("taskmgr.exe");
        }

        private void ControlPanel_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("control.exe");
        }

        private void DeviceManager_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("devmgmt.msc");
        }

        private void SystemInfo_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("msinfo32.exe");
        }
    }
}
