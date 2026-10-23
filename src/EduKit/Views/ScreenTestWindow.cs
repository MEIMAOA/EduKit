using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EduKit.Views
{
    /// <summary>屏幕坏点 / 色彩测试：全屏纯色循环，适合检查投影仪和显示器。</summary>
    public partial class ScreenTestWindow : Window
    {
        private static readonly Color[] ColorList =
        {
            Color.FromRgb(255, 255, 255), Color.FromRgb(0, 0, 0), Color.FromRgb(255, 0, 0), Color.FromRgb(0, 255, 0),
            Color.FromRgb(0, 0, 255), Color.FromRgb(255, 255, 0), Color.FromRgb(0, 255, 255),
            Color.FromRgb(255, 0, 255), Color.FromRgb(128, 128, 128)
        };

        private static readonly string[] Names = { "白色", "黑色", "红色", "绿色", "蓝色", "黄色", "青色", "品红", "灰色" };

        private int _index;
        private readonly DispatcherTimer _auto;

        public ScreenTestWindow()
        {
            InitializeComponent();

            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            _auto = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _auto.Tick += (s, e) => Next();
            _auto.Start();

            Apply(0);
            KeyDown += OnKeyDown;
            MouseLeftButtonDown += (s, e) => Next();
        }

        private void Apply(int index)
        {
            _index = ((index % ColorList.Length) + ColorList.Length) % ColorList.Length;
            Background = new SolidColorBrush(ColorList[_index]);
            var dark = ColorList[_index] == Color.FromRgb(0, 0, 0) || ColorList[_index] == Color.FromRgb(0, 0, 255);
            HintText.Foreground = dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(60, 60, 60));
            HintText.Text = Names[_index] + "    ← / → 切换    Esc 退出    已自动播放，点击屏幕切换";
        }

        private void Next()
        {
            Apply(_index + 1);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    break;
                case Key.Left:
                    _auto.Stop();
                    Apply(_index - 1);
                    break;
                case Key.Right:
                    _auto.Stop();
                    Apply(_index + 1);
                    break;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _auto.Stop();
        }
    }
}
