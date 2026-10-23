using System;
using System.Windows;

namespace EduKit.Core
{
    /// <summary>亮色 / 暗色主题切换（动态替换资源字典，全部控件使用 DynamicResource）。</summary>
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var uri = new Uri(
                dark ? "pack://application:,,,/Themes/Dark.xaml" : "pack://application:,,,/Themes/Light.xaml",
                UriKind.Absolute);

            var dictionary = new ResourceDictionary { Source = uri };
            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count > 0)
            {
                merged[0] = dictionary;
            }
            else
            {
                merged.Add(dictionary);
            }
        }
    }
}
