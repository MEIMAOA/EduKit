using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class DiskAnalyzerPage : UserControl
    {
        private sealed class DirView
        {
            public string Name { get; set; }

            public string SizeText { get; set; }

            public double Percent { get; set; }
        }

        private sealed class FileView
        {
            public string Name { get; set; }

            public string SizeText { get; set; }

            public string Path { get; set; }
        }

        private sealed class FileEntry
        {
            public string Path;

            public long Size;
        }

        private CancellationTokenSource _cts;

        public DiskAnalyzerPage()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择要分析的文件夹";
                dialog.SelectedPath = Directory.Exists(PathBox.Text) ? PathBox.Text : "C:\\";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Ui.Warn("请选择一个存在的文件夹。");
                return;
            }

            StartButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;
            DirList.ItemsSource = null;
            FileList.ItemsSource = null;
            StatusText.Text = "正在扫描…";
            _cts = new CancellationTokenSource();

            try
            {
                var result = await Task.Run(() => Scan(path, _cts.Token));
                ShowResult(result);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "已取消。";
            }
            catch (Exception ex)
            {
                Ui.Error("扫描失败：" + ex.Message);
            }
            finally
            {
                StartButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private sealed class ScanResult
        {
            public List<DirView> Dirs = new List<DirView>();

            public List<FileView> Files = new List<FileView>();
        }

        private ScanResult Scan(string root, CancellationToken token)
        {
            var result = new ScanResult();
            var fileEntries = new List<FileEntry>();
            var fileCount = 0;
            var bytesTotal = 0L;
            var lastUpdate = DateTime.MinValue;

            var subDirs = new List<string>();
            try
            {
                subDirs = Directory.GetDirectories(root).ToList();
            }
            catch
            {
                // ignored
            }

            var dirSizes = new List<KeyValuePair<string, long>>();
            foreach (var dir in subDirs)
            {
                token.ThrowIfCancellationRequested();
                dirSizes.Add(new KeyValuePair<string, long>(Path.GetFileName(dir), MeasureDir(dir, token, fileEntries, ref fileCount, ref bytesTotal, ref lastUpdate)));
            }

            // 根目录本身散落的文件也计入
            try
            {
                foreach (var file in Directory.GetFiles(root))
                {
                    token.ThrowIfCancellationRequested();
                    var size = TryFileSize(file);
                    bytesTotal += size;
                    fileCount++;
                    AddFile(fileEntries, file, size);
                }
            }
            catch
            {
                // ignored
            }

            var dirs = dirSizes.Where(d => d.Value > 0).OrderByDescending(d => d.Value).Take(30).ToList();
            var maxDir = dirs.Count > 0 ? dirs.Max(d => d.Value) : 1;
            foreach (var d in dirs)
            {
                result.Dirs.Add(new DirView
                {
                    Name = d.Key,
                    SizeText = Native.FormatBytes(d.Value),
                    Percent = d.Value * 100.0 / maxDir
                });
            }

            result.Files = fileEntries.OrderByDescending(f => f.Size).Take(100)
                .Select(f => new FileView
                {
                    Name = Path.GetFileName(f.Path),
                    SizeText = Native.FormatBytes(f.Size),
                    Path = f.Path
                }).ToList();

            Dispatcher.BeginInvoke(new Action(() =>
                StatusText.Text = "已扫描 " + fileCount + " 个文件，共 " + Native.FormatBytes(bytesTotal)));

            return result;
        }

        private long MeasureDir(string dir, CancellationToken token, List<FileEntry> fileEntries,
            ref int fileCount, ref long bytesTotal, ref DateTime lastUpdate)
        {
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(dir);

            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var current = stack.Pop();
                string[] files;
                string[] subDirs;
                try
                {
                    files = Directory.GetFiles(current);
                    subDirs = Directory.GetDirectories(current);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    var size = TryFileSize(file);
                    total += size;
                    bytesTotal += size;
                    fileCount++;
                    AddFile(fileEntries, file, size);

                    if ((DateTime.Now - lastUpdate).TotalMilliseconds > 200)
                    {
                        lastUpdate = DateTime.Now;
                        var text = "正在扫描 " + fileCount + " 个文件，共 " + Native.FormatBytes(bytesTotal);
                        Dispatcher.BeginInvoke(new Action(() => StatusText.Text = text));
                    }
                }

                foreach (var sub in subDirs)
                {
                    stack.Push(sub);
                }
            }

            return total;
        }

        private static void AddFile(List<FileEntry> list, string path, long size)
        {
            if (size <= 0)
            {
                return;
            }

            list.Add(new FileEntry { Path = path, Size = size });
            if (list.Count > 3000)
            {
                list.RemoveAll(f => f.Size < list.Min(x => x.Size));
            }
        }

        private static long TryFileSize(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private void ShowResult(ScanResult result)
        {
            DirList.ItemsSource = result.Dirs;
            FileList.ItemsSource = result.Files;
        }

        private void FileList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FileList.SelectedItem is FileView file && File.Exists(file.Path))
            {
                Native.Start("explorer.exe", "/select,\"" + file.Path + "\"");
            }
        }
    }
}
