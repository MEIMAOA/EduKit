using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class SettingsPage : UserControl
    {
        private UpdateInfo _updateInfo;
        private CancellationTokenSource _updateCts;
        private bool _updating;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadValues();
        }

        private void LoadValues()
        {
            DarkSwitch.IsChecked = ThemeManager.IsDark;
            AutoStartSwitch.IsChecked = AppData.IsAutoStartEnabled();
            AutoCheckSwitch.IsChecked = AppData.Settings.AutoCheckUpdates;
            AutoInstallSwitch.IsChecked = AppData.Settings.AutoInstallUpdates;
            VersionText.Text = "v" + App.Version + " · " + (Environment.Is64BitProcess ? "64 位" : "32 位");
            DataPathText.Text = "数据目录：" + AppData.Dir;

            if (App.PendingUpdate != null)
            {
                _updateInfo = App.PendingUpdate;
                InstallButton.IsEnabled = true;
                UpdateText.Text = "发现新版本 v" + _updateInfo.LatestVersion + "，可点击「立即更新」。";
            }
        }

        private void Dark_Click(object sender, RoutedEventArgs e)
        {
            var dark = DarkSwitch.IsChecked == true;
            AppData.Settings.DarkMode = dark;
            ThemeManager.Apply(dark);
            AppData.SaveSettings();
            App.Shell?.SyncThemeIcon();
        }

        private void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            var enabled = AutoStartSwitch.IsChecked == true;
            AppData.Settings.StartWithWindows = enabled;
            AppData.TryApplyAutoStart(enabled);
            AppData.SaveSettings();
        }

        private void AutoCheck_Click(object sender, RoutedEventArgs e)
        {
            AppData.Settings.AutoCheckUpdates = AutoCheckSwitch.IsChecked == true;
            AppData.SaveSettings();
        }

        private void AutoInstall_Click(object sender, RoutedEventArgs e)
        {
            AppData.Settings.AutoInstallUpdates = AutoInstallSwitch.IsChecked == true;
            AppData.SaveSettings();
        }

        private void OpenData_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenDataFolder();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("确定恢复默认设置吗？（不会删除图吧工具箱路径）"))
            {
                return;
            }

            AppData.ResetSettings();
            App.Shell?.SyncThemeIcon();
            LoadValues();
            Ui.Info("已恢复默认设置。");
        }

        private void Github_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenUrl(App.ProjectUrl);
        }

        private void GithubReleases_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenUrl(App.ProjectUrl + "/releases");
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_updating)
            {
                return;
            }

            CheckButton.IsEnabled = false;
            UpdateText.Text = "正在检查更新…";
            try
            {
                _updateInfo = await UpdateChecker.CheckAsync();
                if (!string.IsNullOrEmpty(_updateInfo.Error))
                {
                    UpdateText.Text = "检查失败：" + _updateInfo.Error;
                }
                else if (_updateInfo.HasUpdate)
                {
                    UpdateText.Text = "发现新版本 v" + _updateInfo.LatestVersion + "（当前 v" + App.Version +
                                      "），更新包 " + Native.FormatBytes(_updateInfo.AssetSize) + "。";
                    InstallButton.IsEnabled = true;
                }
                else
                {
                    UpdateText.Text = "当前已是最新版本（v" + App.Version + "）。";
                    InstallButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                UpdateText.Text = "检查失败：" + ex.Message;
            }
            finally
            {
                CheckButton.IsEnabled = true;
            }
        }

        private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_updating)
            {
                return;
            }

            if (_updateInfo == null || !_updateInfo.HasUpdate)
            {
                CheckUpdate_Click(sender, e);
                return;
            }

            if (!Ui.Confirm("将下载并安装新版本 v" + _updateInfo.LatestVersion +
                            "，安装完成后程序会自动重启。\r\n\r\n是否继续？"))
            {
                return;
            }

            _updating = true;
            CheckButton.IsEnabled = false;
            InstallButton.IsEnabled = false;
            UpdateBar.Visibility = Visibility.Visible;
            UpdateBar.Value = 0;
            _updateCts = new CancellationTokenSource();

            try
            {
                var error = await UpdateChecker.DownloadAndApplyAsync(_updateInfo, progress =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateBar.Value = progress.Percent;
                        UpdateText.Text = "正在下载更新：" + progress.SizeText + " · " + progress.SpeedText +
                                          (string.IsNullOrEmpty(progress.EtaText) ? "" : " · 剩余 " + progress.EtaText);
                    }));
                }, _updateCts.Token);

                if (error == null)
                {
                    UpdateText.Text = "更新已就绪，程序即将退出并自动重启完成更新…";
                    await Task.Delay(800);
                    Application.Current.Shutdown();
                    return;
                }

                UpdateText.Text = "更新失败：" + error;
            }
            catch (OperationCanceledException)
            {
                UpdateText.Text = "已取消更新。";
            }
            catch (Exception ex)
            {
                UpdateText.Text = "更新失败：" + ex.Message;
            }
            finally
            {
                _updating = false;
                _updateCts?.Dispose();
                _updateCts = null;
                UpdateBar.Visibility = Visibility.Collapsed;
                CheckButton.IsEnabled = true;
                InstallButton.IsEnabled = _updateInfo != null && _updateInfo.HasUpdate;
            }
        }
    }
}