using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class StartupPage : UserControl
    {
        private List<StartupItem> _startupItems = new List<StartupItem>();
        private List<ServiceInfo> _services = new List<ServiceInfo>();
        private bool _busy;

        public StartupPage()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshAll();
        }

        private async void RefreshAll()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            try
            {
                _startupItems = await Task.Run(() => ProcessTools.GetStartupItems());
                StartupList.ItemsSource = _startupItems;

                _services = await Task.Run(() => ProcessTools.GetServices());
                ServiceList.ItemsSource = _services;

                ServiceHint.Text = "共 " + _services.Count + " 个服务，启动 / 停止通常需要管理员权限。";
            }
            catch (Exception ex)
            {
                Ui.Warn("读取启动项或服务失败：" + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private void RefreshStartup_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private void RefreshServices_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private void DeleteStartup_Click(object sender, RoutedEventArgs e)
        {
            if (!(StartupList.SelectedItem is StartupItem item))
            {
                Ui.Warn("请先选择一个启动项。");
                return;
            }

            if (!Ui.Confirm("确定删除启动项「" + item.Name + "」吗？\r\n\r\n" + item.Command))
            {
                return;
            }

            string error;
            if (ProcessTools.DeleteStartupItem(item, out error))
            {
                RefreshAll();
            }
            else
            {
                Ui.Error("删除失败（可能需要管理员权限）：" + error);
            }
        }

        private void TaskManager_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("taskmgr.exe");
        }

        private void Msconfig_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("msconfig.exe");
        }

        private void Services_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("services.msc");
        }

        private void StartService_Click(object sender, RoutedEventArgs e)
        {
            Control("start");
        }

        private void StopService_Click(object sender, RoutedEventArgs e)
        {
            Control("stop");
        }

        private void RestartService_Click(object sender, RoutedEventArgs e)
        {
            Control("restart");
        }

        private async void Control(string action)
        {
            if (!(ServiceList.SelectedItem is ServiceInfo service))
            {
                Ui.Warn("请先选择一个服务。");
                return;
            }

            ServiceHint.Text = "正在" + action + "服务 " + service.DisplayName + "…";
            string error = null;
            var ok = await Task.Run(() => ProcessTools.ControlService(service.Name, action, out error));

            if (ok)
            {
                ServiceHint.Text = "服务操作完成。";
                RefreshAll();
            }
            else
            {
                ServiceHint.Text = "操作失败（通常需要管理员权限）：" + error;
            }
        }
    }
}