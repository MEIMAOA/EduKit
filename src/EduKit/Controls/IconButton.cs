using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EduKit.Controls
{
    /// <summary>矢量图标按钮：用 Geometry 作为图标，避免依赖 Windows 10 才有的 Segoe MDL2 字体。</summary>
    public class IconButton : Button
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(Geometry), typeof(IconButton), new PropertyMetadata(null));

        public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
            nameof(IconSize), typeof(double), typeof(IconButton), new PropertyMetadata(18.0));

        public Geometry Icon
        {
            get => (Geometry)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }
    }
}
