using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class HardwarePage : UserControl
    {
        private System.Collections.Generic.List<InfoRow> _info = new System.Collections.Generic.List<InfoRow>();
        private bool _loading;

        public HardwarePage()
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
            SummaryText.Text = "正在读取硬件信息…";
            try
            {
                _info = await Task.Run(() => HardwareDetails.GetHardwareInfo());
                InfoList.ItemsSource = _info;
                DiskList.ItemsSource = await Task.Run(() => HardwareDetails.GetDisks());

                var devices = await Task.Run(() => HardwareDetails.GetProblemDevices());
                DeviceList.ItemsSource = devices;
                NoDeviceText.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                SummaryText.Text = HardwareDetails.GetOsText() + " · " + Environment.MachineName +
                                   " · 共 " + _info.Count + " 项硬件信息";
            }
            catch (Exception ex)
            {
                SummaryText.Text = "读取失败：" + ex.Message;
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

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            var builder = new StringBuilder();
            builder.AppendLine("EduKit 硬件信息 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            foreach (var row in _info)
            {
                builder.AppendLine(row.Label + "：" + row.Value);
            }

            Ui.Copy(builder.ToString());
            Ui.Info("硬件信息已复制到剪贴板。");
        }
    }
}