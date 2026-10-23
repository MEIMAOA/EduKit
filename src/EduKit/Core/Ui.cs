using System.Windows;

namespace EduKit.Core
{
    /// <summary>简单的对话框封装。</summary>
    public static class Ui
    {
        private const string Title = "电教委工具箱";

        public static void Info(string message)
        {
            MessageBox.Show(Owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Warn(string message)
        {
            MessageBox.Show(Owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void Error(string message)
        {
            MessageBox.Show(Owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static bool Confirm(string message)
        {
            return MessageBox.Show(Owner, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                   == MessageBoxResult.Yes;
        }

        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // 剪贴板被占用时忽略
            }
        }

        private static Window Owner => Application.Current?.MainWindow;
    }
}
