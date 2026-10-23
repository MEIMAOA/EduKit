using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using EduKit.Core;

namespace EduKit.Views
{
    /// <summary>全屏投影窗口：课堂计时全屏显示，或全屏公告文字。</summary>
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();

            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }

        public static OverlayWindow ForTimer(TimerEngine engine)
        {
            var window = new OverlayWindow { DataContext = engine };
            window.MainText.SetBinding(TextBlock.TextProperty,
                new Binding(nameof(TimerEngine.Display)));
            window.MainText.SetBinding(TextBlock.ForegroundProperty,
                new Binding(nameof(TimerEngine.Progress)) { Converter = new ProgressToBrushConverter() });
            window.SubText.SetBinding(TextBlock.TextProperty,
                new Binding(nameof(TimerEngine.Status)));
            return window;
        }

        public static OverlayWindow ForNotice(string title, string subtitle)
        {
            var window = new OverlayWindow();
            window.MainText.Text = title ?? string.Empty;
            window.SubText.Text = subtitle ?? string.Empty;
            window.MainText.FontSize = (title?.Length ?? 0) > 18 ? 84 : 120;
            return window;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }
    }

    internal class ProgressToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double progress))
            {
                return Brushes.White;
            }

            if (progress > 0.9)
            {
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            }

            if (progress > 0.75)
            {
                return new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));
            }

            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
