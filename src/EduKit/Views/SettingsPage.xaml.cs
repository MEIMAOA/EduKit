using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadValues();
        }

        private void LoadValues()
        {
            DarkSwitch.IsChecked = ThemeManager.IsDark;
            AutoStartSwitch.IsChecked = AppData.IsAutoStartEnabled();
            VersionText.Text = "v" + App.Version + " · " + (Environment.Is64BitProcess ? "64 位" : "32 位");
            DataPathText.Text = "数据目录：" + AppData.Dir;
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

        private void OpenData_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenDataFolder();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.Confirm("确定恢复默认设置吗？（不会删除名单、课程表和快捷方式）"))
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

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckButton.IsEnabled = false;
            UpdateText.Text = "正在检查更新…";

            try
            {
                var info = await UpdateChecker.CheckAsync();
                if (!string.IsNullOrEmpty(info.Error))
                {
                    UpdateText.Text = "检查失败：" + info.Error;
                }
                else if (info.HasUpdate)
                {
                    UpdateText.Text = "发现新版本 v" + info.LatestVersion + "（当前 v" + App.Version + "）。";
                    if (Ui.Confirm("发现新版本 v" + info.LatestVersion + "，要打开下载页面吗？"))
                    {
                        Native.OpenUrl(info.ReleaseUrl);
                    }
                }
                else
                {
                    UpdateText.Text = "当前已是最新版本（v" + App.Version + "）。";
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
    }
}
