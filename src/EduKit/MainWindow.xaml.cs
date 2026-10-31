using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EduKit.Core;
using EduKit.Views;

namespace EduKit
{
    public class NavItem
    {
        public NavItem(string key, string title, string iconKey)
        {
            Key = key;
            Title = title;
            Icon = (Geometry)Application.Current.FindResource(iconKey);
        }

        public string Key { get; }

        public string Title { get; }

        public Geometry Icon { get; }
    }

    public partial class MainWindow : Window
    {
        private readonly List<NavItem> _navItems = new List<NavItem>();
        private readonly Dictionary<string, UserControl> _pages = new Dictionary<string, UserControl>();
        private bool _suppressSelection;
        private string _currentKey = "dashboard";

        public MainWindow()
        {
            InitializeComponent();

            FitToScreen();
            BuildNavigation();
            UpdateThemeIcon();
            VersionText.Text = "v" + App.Version;

            Go(AppData.Settings.LastPage);

            StateChanged += (s, e) =>
            {
                MaxBtn.Icon = (Geometry)FindResource(WindowState == WindowState.Maximized ? "IconRestore" : "IconMax");
            };
        }

        private void FitToScreen()
        {
            var area = SystemParameters.WorkArea;
            Width = Math.Min(1240, Math.Max(940, area.Width * 0.92));
            Height = Math.Min(820, Math.Max(600, area.Height * 0.9));
        }

        private void BuildNavigation()
        {
            _navItems.Add(new NavItem("dashboard", "系统概览", "IconHome"));
            _navItems.Add(new NavItem("hardware", "硬件信息", "IconSystem"));
            _navItems.Add(new NavItem("processes", "进程管理", "IconList"));
            _navItems.Add(new NavItem("performance", "性能监控", "IconChart"));
            _navItems.Add(new NavItem("startup", "启动与服务", "IconRefresh"));
            _navItems.Add(new NavItem("system", "系统维护", "IconWrench"));
            _navItems.Add(new NavItem("diskanalyzer", "磁盘分析", "IconFolder"));
            _navItems.Add(new NavItem("network", "网络诊断", "IconWifi"));
            _navItems.Add(new NavItem("display", "显示与投影", "IconDisplay"));
            _navItems.Add(new NavItem("audio", "音量控制", "IconVolume"));
            _navItems.Add(new NavItem("power", "电源管理", "IconPower"));
            _navItems.Add(new NavItem("tuba", "图吧工具箱", "IconRocket"));
            _navItems.Add(new NavItem("settings", "设置与关于", "IconSettings"));

            NavList.ItemsSource = _navItems;
        }

        public void Go(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                key = "dashboard";
            }

            var item = _navItems.Find(n => n.Key == key) ?? _navItems[0];
            if (!_pages.TryGetValue(item.Key, out var page))
            {
                page = CreatePage(item.Key);
                _pages[item.Key] = page;
            }

            ContentHost.Content = page;
            _currentKey = item.Key;
            AppData.Settings.LastPage = item.Key;

            _suppressSelection = true;
            NavList.SelectedItem = item;
            _suppressSelection = false;

            var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ContentHost.BeginAnimation(OpacityProperty, animation);
        }

        private UserControl CreatePage(string key)
        {
            switch (key)
            {
                case "hardware": return new HardwarePage();
                case "processes": return new ProcessesPage();
                case "performance": return new PerformancePage();
                case "startup": return new StartupPage();
                case "system": return new SystemPage();
                case "diskanalyzer": return new DiskAnalyzerPage();
                case "network": return new NetworkPage();
                case "display": return new DisplayPage();
                case "audio": return new AudioPage();
                case "power": return new PowerPage();
                case "tuba": return new TubaPage();
                case "settings": return new SettingsPage();
                default: return new DashboardPage();
            }
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection)
            {
                return;
            }

            if (NavList.SelectedItem is NavItem item)
            {
                Go(item.Key);
            }
        }

        public void SyncThemeIcon()
        {
            UpdateThemeIcon();
        }

        private void UpdateThemeIcon()
        {
            ThemeBtn.Icon = (Geometry)FindResource(ThemeManager.IsDark ? "IconMoon" : "IconSun");
            ThemeBtn.ToolTip = ThemeManager.IsDark ? "切换到浅色主题" : "切换到深色主题";
        }

        public void ToggleTheme()
        {
            AppData.Settings.DarkMode = !AppData.Settings.DarkMode;
            ThemeManager.Apply(AppData.Settings.DarkMode);
            AppData.SaveSettings();
            UpdateThemeIcon();
        }

        private void Theme_Click(object sender, RoutedEventArgs e)
        {
            ToggleTheme();
        }

        private void Github_Click(object sender, RoutedEventArgs e)
        {
            Native.OpenUrl(App.ProjectUrl);
        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // 拖动过程中窗口状态变化时忽略
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            AppData.Settings.LastPage = _currentKey;
            AppData.SaveSettings();
            base.OnClosed(e);
        }
    }
}
