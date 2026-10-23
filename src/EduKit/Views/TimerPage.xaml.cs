using System;
using System.Windows;
using System.Windows.Controls;

namespace EduKit.Views
{
    public partial class TimerPage : UserControl
    {
        private static readonly int[] Presets = { 1, 3, 5, 10, 15, 20, 30, 45 };

        public TimerPage()
        {
            InitializeComponent();
            DataContext = App.Timer;

            foreach (var minutes in Presets)
            {
                var button = new Button
                {
                    Content = minutes + " 分钟",
                    Style = (Style)FindResource("BtnSmall"),
                    Margin = new Thickness(0, 0, 6, 6),
                    Tag = minutes
                };
                button.Click += Preset_Click;
                PresetPanel.Children.Add(button);
            }

            SoundSwitch.IsChecked = Core.AppData.Settings.TimerSound;
            Loaded += (s, e) => UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            StartButton.Content = App.Timer.StartText;
            StopwatchSwitch.IsChecked = App.Timer.Mode == Core.TimerMode.Stopwatch;
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int minutes)
            {
                App.Timer.SetCountdown(TimeSpan.FromMinutes(minutes));
                UpdateStartButton();
            }
        }

        private void Custom_Click(object sender, RoutedEventArgs e)
        {
            int minutes;
            if (!int.TryParse(CustomMinutes.Text.Trim(), out minutes) || minutes <= 0 || minutes > 600)
            {
                Core.Ui.Warn("请输入 1 - 600 之间的分钟数。");
                return;
            }

            App.Timer.SetCountdown(TimeSpan.FromMinutes(minutes));
            UpdateStartButton();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            App.Timer.Toggle();
            UpdateStartButton();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            App.Timer.Reset();
            UpdateStartButton();
        }

        private void Stopwatch_Click(object sender, RoutedEventArgs e)
        {
            App.Timer.Mode = StopwatchSwitch.IsChecked == true
                ? Core.TimerMode.Stopwatch
                : Core.TimerMode.Countdown;
            UpdateStartButton();
        }

        private void Sound_Click(object sender, RoutedEventArgs e)
        {
            Core.AppData.Settings.TimerSound = SoundSwitch.IsChecked == true;
            Core.AppData.SaveSettings();
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            var window = OverlayWindow.ForTimer(App.Timer);
            window.ShowDialog();
        }
    }
}
