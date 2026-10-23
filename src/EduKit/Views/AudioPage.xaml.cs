using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class AudioPage : UserControl
    {
        private bool _updating;
        private readonly DispatcherTimer _syncTimer;

        public AudioPage()
        {
            InitializeComponent();

            _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _syncTimer.Tick += (s, e) => SyncFromSystem();
            _syncTimer.Start();

            Loaded += (s, e) => SyncFromSystem();
            Unloaded += (s, e) => _syncTimer.Stop();
        }

        private void SyncFromSystem()
        {
            if (_updating)
            {
                return;
            }

            var volume = AudioManager.GetVolume();
            if (volume < 0)
            {
                VolumeText.Text = "—";
                AudioHint.Text = "无法读取系统音量，可能没有可用的播放设备。";
                VolumeSlider.IsEnabled = false;
                return;
            }

            VolumeSlider.IsEnabled = true;
            _updating = true;
            VolumeSlider.Value = volume;
            VolumeText.Text = volume + "%";
            var muted = AudioManager.GetMuted();
            MuteSwitch.IsChecked = muted == true;
            AudioHint.Text = muted == true ? "当前已静音" : string.Empty;
            _updating = false;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating)
            {
                return;
            }

            var value = (int)Math.Round(e.NewValue);
            AudioManager.SetVolume(value);
            VolumeText.Text = value + "%";
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            AudioManager.SetMuted(MuteSwitch.IsChecked == true);
            SyncFromSystem();
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string text)
            {
                int value;
                if (int.TryParse(text, out value))
                {
                    AudioManager.SetVolume(value);
                    SyncFromSystem();
                }
            }
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            Native.MediaKey(Native.VkMediaPrevTrack);
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            Native.MediaKey(Native.VkMediaPlayPause);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Native.MediaKey(Native.VkMediaNextTrack);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Native.MediaKey(Native.VkMediaStop);
        }

        private void OpenMixer_Click(object sender, RoutedEventArgs e)
        {
            if (!Native.Start("SndVol.exe"))
            {
                Native.Start("mmsys.cpl");
            }
        }

        private void TestSound_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(880, 200);
                    Console.Beep(1100, 200);
                    Console.Beep(1320, 320);
                }
                catch
                {
                    // ignored
                }
            });
        }
    }
}
