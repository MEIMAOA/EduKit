using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EduKit
{
    /// <summary>
    /// 离屏截图模式（--render 参数）：把每个模块导出为 PNG，用于 README 截图与 UI 自检。
    /// 窗口显示在屏幕外，不会打扰正在使用的电脑。
    /// </summary>
    public static class ScreenshotRenderer
    {
        private static readonly string[] Pages =
        {
            "dashboard", "hardware", "processes", "performance", "startup", "system", "diskanalyzer", "network",
            "display", "audio", "power", "tuba", "settings"
        };

        public static async void Run(string outputDir)
        {
            try
            {
                Directory.CreateDirectory(outputDir);

                var shell = new MainWindow
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -6000,
                    Top = 0,
                    Width = 1280,
                    Height = 820,
                    ShowInTaskbar = false
                };

                shell.Show();
                await Task.Delay(500);

                foreach (var dark in new[] { false, true })
                {
                    Core.AppData.Settings.DarkMode = dark;
                    Core.ThemeManager.Apply(dark);
                    await Task.Delay(200);

                    foreach (var page in Pages)
                    {
                        shell.Go(page);

                        var settle = page == "dashboard" || page == "system" || page == "hardware" ? 2000
                            : page == "processes" || page == "startup" ? 1300
                            : page == "tuba" ? 2200
                            : page == "network" ? 900
                            : 450;
                        await Task.Delay(settle);
                        shell.UpdateLayout();

                        var path = Path.Combine(outputDir, (dark ? "dark-" : "light-") + page + ".png");
                        Capture(shell, path);
                    }
                }

                shell.Close();
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Core.AppData.Dir, "render-error.log"),
                        DateTime.Now + " " + ex + Environment.NewLine);
                }
                catch
                {
                    // ignored
                }
            }

            Application.Current.Shutdown();
        }

        private static void Capture(Window window, string path)
        {
            var width = (int)Math.Round(window.ActualWidth);
            var height = (int)Math.Round(window.ActualHeight);
            if (width < 2 || height < 2)
            {
                width = 1280;
                height = 820;
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }
    }
}
