using System;
using System.Media;
using System.Windows.Threading;

namespace EduKit.Core
{
    public enum TimerMode
    {
        Countdown,
        Stopwatch
    }

    /// <summary>课堂计时器引擎：倒计时 / 秒表、暂停、结束提醒、进度。</summary>
    public class TimerEngine : ObservableObject
    {
        private readonly DispatcherTimer _timer;
        private TimeSpan _total = TimeSpan.FromMinutes(10);
        private TimeSpan _remaining = TimeSpan.FromMinutes(10);
        private bool _isRunning;
        private TimerMode _mode = TimerMode.Countdown;
        private string _display = "10:00";
        private double _progress;
        private string _status = "未开始";

        public TimerEngine()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += OnTick;
        }

        public event EventHandler Finished;

        public TimerMode Mode
        {
            get => _mode;
            set
            {
                if (Set(ref _mode, value))
                {
                    Stop();
                    Reset();
                    OnPropertyChanged(nameof(ModeText));
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (Set(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(IsPaused));
                    OnPropertyChanged(nameof(StartText));
                }
            }
        }

        public bool IsPaused => !IsRunning && _remaining != _total && _remaining.TotalSeconds > 0 && _started;

        public string StartText => IsRunning ? "暂停" : IsPaused ? "继续" : "开始";

        public TimeSpan Total
        {
            get => _total;
            private set
            {
                if (Set(ref _total, value))
                {
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public TimeSpan Remaining
        {
            get => _remaining;
            private set
            {
                if (Set(ref _remaining, value))
                {
                    UpdateDisplay();
                }
            }
        }

        public string Display
        {
            get => _display;
            private set => Set(ref _display, value);
        }

        public double Progress
        {
            get => _progress;
            private set => Set(ref _progress, value);
        }

        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        public string ModeText => Mode == TimerMode.Countdown ? "倒计时" : "秒表";

        private bool _started;

        public void SetCountdown(TimeSpan duration)
        {
            Mode = TimerMode.Countdown;
            Stop();
            Total = duration;
            Remaining = duration;
            _started = false;
            Progress = 0;
            Status = "未开始";
            UpdateDisplay();
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(StartText));
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            if (Mode == TimerMode.Countdown && Remaining <= TimeSpan.Zero)
            {
                Remaining = Total;
            }

            _started = true;
            IsRunning = true;
            Status = "计时中";
            _timer.Start();
        }

        public void Pause()
        {
            _timer.Stop();
            IsRunning = false;
            Status = "已暂停";
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(StartText));
        }

        public void Toggle()
        {
            if (IsRunning)
            {
                Pause();
            }
            else
            {
                Start();
            }
        }

        public void Stop()
        {
            _timer.Stop();
            IsRunning = false;
        }

        public void Reset()
        {
            Stop();
            Remaining = Mode == TimerMode.Countdown ? Total : TimeSpan.Zero;
            _started = false;
            Progress = 0;
            Status = "未开始";
            UpdateDisplay();
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(StartText));
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Mode == TimerMode.Countdown)
            {
                var next = Remaining - _timer.Interval;
                if (next <= TimeSpan.Zero)
                {
                    Remaining = TimeSpan.Zero;
                    Progress = 1;
                    _timer.Stop();
                    IsRunning = false;
                    Status = "时间到";
                    UpdateDisplay();
                    PlayAlarm();
                    Finished?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(IsPaused));
                    OnPropertyChanged(nameof(StartText));
                    return;
                }

                Remaining = next;
                Progress = Total.TotalSeconds <= 0 ? 0 : 1 - next.TotalSeconds / Total.TotalSeconds;
            }
            else
            {
                Remaining = Remaining + _timer.Interval;
                Progress = 0;
                Status = "计时中";
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            var value = Mode == TimerMode.Countdown ? Remaining : Remaining;
            if (value < TimeSpan.Zero)
            {
                value = TimeSpan.Zero;
            }

            Display = value.TotalHours >= 1
                ? ((int)value.TotalHours).ToString("0") + ":" + value.ToString("mm\\:ss")
                : ((int)value.TotalMinutes).ToString("00") + ":" + value.Seconds.ToString("00");
        }

        private static void PlayAlarm()
        {
            try
            {
                if (AppData.Settings != null && !AppData.Settings.TimerSound)
                {
                    return;
                }

                for (var i = 0; i < 3; i++)
                {
                    Console.Beep(880, 220);
                    Console.Beep(1320, 220);
                }
            }
            catch
            {
                try
                {
                    SystemSounds.Exclamation.Play();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
