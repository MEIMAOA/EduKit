using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class TubaPage : UserControl
    {
        private List<TubaTool> _tools = new List<TubaTool>();

        public TubaPage()
        {
            InitializeComponent();
            Loaded += (s, e) => DetectAndScan();
        }

        private async void DetectAndScan()
        {
            PathText.Text = "正在检测图吧工具箱…";
            CountText.Text = "检测中";
            try
            {
                var path = await System.Threading.Tasks.Task.Run(() => TubaToolbox.Detect());
                if (string.IsNullOrEmpty(path))
                {
                    PathText.Text = "未检测到图吧工具箱。请点击「选择目录」手动指定，或到官网下载安装。";
                    CountChip.Background = (System.Windows.Media.Brush)FindResource("BgHover");
                    CountText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary");
                    _tools = new List<TubaTool>();
                    ToolList.ItemsSource = null;
                    EmptyText.Visibility = Visibility.Visible;
                    CountText.Text = "未检测";
                    return;
                }

                PathText.Text = path;
                _tools = await System.Threading.Tasks.Task.Run(() => TubaToolbox.Scan(path));
                ApplyFilter();
                EmptyText.Visibility = _tools.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                CountChip.Background = (System.Windows.Media.Brush)FindResource("SuccessSoft");
                CountText.Foreground = (System.Windows.Media.Brush)FindResource("Success");
                CountText.Text = _tools.Count + " 个工具";
            }
            catch (Exception ex)
            {
                PathText.Text = "检测失败：" + ex.Message;
                CountText.Text = "检测失败";
            }
        }

        private void ApplyFilter()
        {
            var keyword = SearchBox.Text.Trim();
            var filtered = string.IsNullOrEmpty(keyword)
                ? _tools
                : _tools.Where(t => t.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    t.Category.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            ToolList.ItemsSource = null;
            ToolList.ItemsSource = filtered;
            EmptyText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Detect_Click(object sender, RoutedEventArgs e)
        {
            DetectAndScan();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择图吧工具箱所在文件夹";
                var current = TubaToolbox.Detect();
                if (!string.IsNullOrEmpty(current))
                {
                    dialog.SelectedPath = current;
                }

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                AppData.Settings.TubaPath = dialog.SelectedPath;
                AppData.SaveSettings();
                DetectAndScan();
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = TubaToolbox.Detect();
            if (string.IsNullOrEmpty(path))
            {
                Ui.Info("还没有检测到图吧工具箱目录。");
                return;
            }

            Native.Start("explorer.exe", "\"" + path + "\"");
        }

        private void OpenSite_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenUrl(TubaToolbox.OfficialSite);
        }

        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string path)
            {
                var tool = _tools.FirstOrDefault(t => t.Path == path);
                string error;
                if (!TubaToolbox.Launch(tool, out error))
                {
                    Ui.Error("启动失败：" + error);
                }
            }
        }
    }
}