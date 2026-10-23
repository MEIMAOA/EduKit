using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class DashboardPage : UserControl
    {
        private bool _loading;

        public DashboardPage()
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
            try
            {
                MachineText.Text = await Task.Run(() => HardwareDetails.GetMachineText());
                CpuText.Text = await Task.Run(() => HardwareDetails.GetCpuName());
                MemoryText.Text = await Task.Run(() => HardwareDetails.GetMemoryText());
                GpuText.Text = await Task.Run(() => HardwareDetails.GetGpuName());
                OsText.Text = await Task.Run(() => HardwareDetails.GetOsText());
                UptimeText.Text = "已运行 " + (await Task.Run(() => HardwareDetails.GetUptimeText()) ?? "");

                var disks = await Task.Run(() => HardwareDetails.GetDisks());
                DiskList.ItemsSource = disks;

                var devices = await Task.Run(() => HardwareDetails.GetProblemDevices());
                DeviceList.ItemsSource = devices;
                NoDeviceText.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                var admin = Elevation.IsAdministrator;
                AdminText.Text = admin ? "管理员权限" : "普通权限";
                AdminChip.Background = (System.Windows.Media.Brush)FindResource(admin ? "SuccessSoft" : "BgHover");
                AdminText.Foreground = (System.Windows.Media.Brush)FindResource(admin ? "Success" : "TextSecondary");

                var tuba = await Task.Run(() => TubaToolbox.Detect());
                if (string.IsNullOrEmpty(tuba))
                {
                    TubaChipText.Text = "图吧工具箱未检测到";
                    TubaPathText.Text = "未找到图吧工具箱。可以到「图吧工具箱」页面选择安装目录，或从官网下载安装。";
                    TubaChip.Background = (System.Windows.Media.Brush)FindResource("BgHover");
                    TubaChipText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary");
                }
                else
                {
                    TubaChipText.Text = "图吧工具箱已就绪";
                    TubaPathText.Text = tuba;
                    TubaChip.Background = (System.Windows.Media.Brush)FindResource("SuccessSoft");
                    TubaChipText.Foreground = (System.Windows.Media.Brush)FindResource("Success");
                }
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
            Refresh();
        }

        private void OpenTuba_Click(object sender, RoutedEventArgs e)
        {
            var path = TubaToolbox.Detect();
            if (string.IsNullOrEmpty(path))
            {
                Ui.Info("没有检测到图吧工具箱，可以到「图吧工具箱」页面选择安装目录或打开官网下载。");
                App.Navigate("tuba");
                return;
            }

            Native.Start("explorer.exe", "\"" + path + "\"");
        }

        private void ManageTuba_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("tuba");
        }

        private void DeviceManager_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("devmgmt.msc");
        }

        private void Hardware_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("hardware");
        }

        private void Processes_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("processes");
        }

        private void Startup_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("startup");
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("system");
        }

        private void Network_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("network");
        }

        private void Display_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("display");
        }

        private void Power_Click(object sender, RoutedEventArgs e)
        {
            App.Navigate("power");
        }
    }
}