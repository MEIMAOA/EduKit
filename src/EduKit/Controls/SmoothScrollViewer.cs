using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace EduKit.Controls
{
    /// <summary>
    /// 触控友好的 ScrollViewer：
    /// 1) 默认开启惯性平移（PanningMode.VerticalFirst），手指上下滑动可滚动；
    /// 2) 鼠标滚轮 / 触控板双指滑动使用平滑动画，不再一格一格跳；
    /// 3) 使用像素滚动，长列表滑动更顺滑。
    /// </summary>
    public class SmoothScrollViewer : ScrollViewer
    {
        private DispatcherTimer _timer;
        private double _targetOffset;
        private DateTime _lastTick;

        public SmoothScrollViewer()
        {
            SetPanningMode(this, PanningMode.VerticalFirst);
            SetPanningDeceleration(this, 0.001);
            SetPanningRatio(this, 1.0);
            CanContentScroll = false;
            IsDeferredScrollingEnabled = false;
            Focusable = false;
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (ScrollableHeight <= 0)
            {
                base.OnPreviewMouseWheel(e);
                return;
            }

            e.Handled = true;

            var current = _timer != null && _timer.IsEnabled ? _targetOffset : VerticalOffset;
            var step = -e.Delta * 0.7;
            _targetOffset = Math.Max(0, Math.Min(ScrollableHeight, current + step));
            StartAnimation();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Home)
            {
                AnimateToTop();
            }
            else if (e.Key == Key.End)
            {
                AnimateToBottom();
            }
        }

        private void AnimateToTop()
        {
            _targetOffset = 0;
            StartAnimation();
        }

        private void AnimateToBottom()
        {
            _targetOffset = ScrollableHeight;
            StartAnimation();
        }

        private void StartAnimation()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
                _timer.Tick += OnTick;
            }

            _lastTick = DateTime.Now;
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            var diff = _targetOffset - VerticalOffset;
            if (Math.Abs(diff) < 0.6)
            {
                ScrollToVerticalOffset(_targetOffset);
                _timer.Stop();
                return;
            }

            var elapsed = Math.Max(1, (DateTime.Now - _lastTick).TotalMilliseconds);
            _lastTick = DateTime.Now;
            var factor = Math.Min(1.0, elapsed / 90.0);

            if (diff > 0)
            {
                ScrollToVerticalOffset(VerticalOffset + Math.Max(0.8, diff * factor));
            }
            else
            {
                ScrollToVerticalOffset(VerticalOffset + Math.Min(-0.8, diff * factor));
            }
        }
    }
}
