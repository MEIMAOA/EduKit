using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class ProcessesPage : UserControl
    {
        private List<ProcessRow> _all = new List<ProcessRow>();
        private bool _loading;

        public ProcessesPage()
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
                _all = await Task.Run(() => ProcessTools.GetProcesses());
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Ui.Warn("读取进程失败：" + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private void ApplyFilter()
        {
            var keyword = SearchBox.Text.Trim();
            var filtered = string.IsNullOrEmpty(keyword)
                ? _all
                : _all.Where(p => p.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  p.Path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            ProcessList.ItemsSource = null;
            ProcessList.ItemsSource = filtered;
            CountText.Text = "共 " + _all.Count + " 个进程" +
                             (filtered.Count != _all.Count ? "，筛选出 " + filtered.Count + " 个" : "");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Kill_Click(object sender, RoutedEventArgs e)
        {
            if (!(ProcessList.SelectedItem is ProcessRow row))
            {
                Ui.Warn("请先在列表中选择一个进程。");
                return;
            }

            if (!Ui.Confirm("确定要结束进程「" + row.Name + "」（PID " + row.Id + "）吗？\r\n" +
                            "结束系统关键进程可能导致蓝屏或重启。"))
            {
                return;
            }

            string error;
            if (ProcessTools.KillProcess(row.Id, out error))
            {
                Refresh();
            }
            else
            {
                Ui.Error("结束进程失败（可能需要管理员权限）：" + error);
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (!(ProcessList.SelectedItem is ProcessRow row))
            {
                Ui.Warn("请先在列表中选择一个进程。");
                return;
            }

            if (!ProcessTools.OpenFileLocation(row.Path))
            {
                Ui.Info("无法打开文件位置：可能没有权限或文件已被删除。");
            }
        }
    }
}