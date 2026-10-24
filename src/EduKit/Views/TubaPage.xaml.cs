using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class TubaPage : UserControl
    {
        private List<TubaTool> _tools = new List<TubaTool>();
        private TubaPackage _package;
        private CancellationTokenSource _downloadCts;
        private bool _downloading;

        public TubaPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                DetectAndScan();
                LoadPackageAsync();
            };
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

        private async void LoadPackageAsync()
        {
            PackageText.Text = "正在获取最新版本…";
            SetupButton.IsEnabled = false;
            PortableButton.IsEnabled = false;
            try
            {
                _package = await TubaInstaller.GetLatestAsync();
            }
            catch
            {
                _package = null;
            }

            if (_package == null)
            {
                PackageText.Text = "获取最新版本失败（网络受限或 GitHub API 限流）。可以点击「发布页」手动下载。";
                return;
            }

            PackageText.Text = "最新版本 v" + _package.Version +
                               " · 安装版 " + Native.FormatBytes(_package.SetupSize) +
                               " · 便携版 " + Native.FormatBytes(_package.PortableSize);
            SetupButton.IsEnabled = _package.SetupUrl != null;
            PortableButton.IsEnabled = _package.PortableUrl != null;
        }

        private void RefreshPackage_Click(object sender, RoutedEventArgs e)
        {
            LoadPackageAsync();
        }

        private void OpenRelease_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenUrl(_package?.DownloadPageUrl ?? "https://github.com/luolangaga/tubatools/releases");
        }

        private async void InstallSetup_Click(object sender, RoutedEventArgs e)
        {
            if (_package == null || _package.SetupUrl == null)
            {
                Ui.Warn("没有可用的安装包信息，请先刷新版本。");
                return;
            }

            if (!Ui.Confirm("将下载图吧工具箱CE 安装包（" + Native.FormatBytes(_package.SetupSize) +
                            "），下载完成后自动启动安装程序。\r\n\r\n继续吗？"))
            {
                return;
            }

            await DownloadAsync(async (progress, token) =>
            {
                DownloadText.Text = "正在下载安装包…";
                return await TubaInstaller.DownloadAndRunSetupAsync(_package,
                    p => ReportProgress(p), token);
            }, "已启动安装程序，请按提示完成安装。");
        }

        private async void InstallPortable_Click(object sender, RoutedEventArgs e)
        {
            if (_package == null || _package.PortableUrl == null)
            {
                Ui.Warn("没有可用的便携版信息，请先刷新版本。");
                return;
            }

            if (!Ui.Confirm("将下载图吧工具箱CE 便携版（" + Native.FormatBytes(_package.PortableSize) +
                            "）并解压到本机用户目录。\r\n\r\n继续吗？"))
            {
                return;
            }

            await DownloadAsync(async (progress, token) =>
            {
                DownloadText.Text = "正在下载便携版…";
                var target = await TubaInstaller.DownloadAndExtractPortableAsync(_package,
                    p => ReportProgress(p), token);
                return target != null;
            }, null, true);
        }

        private async Task DownloadAsync(Func<object, CancellationToken, Task<bool>> action,
            string successMessage, bool rescan = false)
        {
            if (_downloading)
            {
                return;
            }

            _downloading = true;
            _downloadCts = new CancellationTokenSource();
            SetupButton.IsEnabled = false;
            PortableButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;
            DownloadBar.Visibility = Visibility.Visible;
            DownloadBar.Value = 0;
            DownloadText.Text = "准备下载…";

            try
            {
                var ok = await action(null, _downloadCts.Token);
                if (ok)
                {
                    DownloadBar.Value = 100;
                    DownloadText.Text = "完成";
                    if (!string.IsNullOrEmpty(successMessage))
                    {
                        Ui.Info(successMessage);
                    }

                    if (rescan)
                    {
                        DetectAndScan();
                    }
                }
                else
                {
                    DownloadText.Text = "操作失败或被取消。";
                }
            }
            catch (OperationCanceledException)
            {
                DownloadText.Text = "已取消下载。";
            }
            catch (Exception ex)
            {
                DownloadText.Text = "操作失败：" + ex.Message;
            }
            finally
            {
                _downloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
                CancelButton.Visibility = Visibility.Collapsed;
                DownloadBar.Visibility = Visibility.Collapsed;
                SetupButton.IsEnabled = _package?.SetupUrl != null;
                PortableButton.IsEnabled = _package?.PortableUrl != null;
            }
        }

        private void ReportProgress(DownloadProgress progress)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DownloadBar.Value = progress.Percent;
                DownloadText.Text = progress.SizeText + " · " + progress.SpeedText +
                                    (string.IsNullOrEmpty(progress.EtaText) ? "" : " · 剩余 " + progress.EtaText);
            }));
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
        }
    }
}