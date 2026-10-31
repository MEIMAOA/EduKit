using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class PerformancePage : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly List<PerfChart> _charts = new List<PerfChart>();

        public PerformancePage()
        {
            InitializeComponent();

            var cpu = CreateCounter(new[] { "Processor", "处理器" }, new[] { "% Processor Time", "% 处理器时间" }, "_Total");
            var mem = CreateCounter(new[] { "Memory", "内存" }, new[] { "Available MBytes", "可用 MB" }, null);
            var disk = CreateCounter(new[] { "PhysicalDisk", "物理磁盘" }, new[] { "% Disk Time", "% 磁盘时间" }, "_Total");
            var net = CreateNetworkCounter();

            double memTotalKb = 0;
            if (mem != null)
            {
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher(
                               "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    {
                        foreach (var item in searcher.Get())
                        {
                            double.TryParse(item["TotalVisibleMemorySize"]?.ToString(), out memTotalKb);
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            AddChart("CPU 使用率", cpu, d => Math.Max(0, Math.Min(100, d)), true, d => d.ToString("0") + "%");
            AddChart("内存使用率", mem, d =>
            {
                if (memTotalKb <= 0)
                {
                    return null;
                }

                var used = (memTotalKb - d * 1024) / memTotalKb * 100;
                return Math.Max(0, Math.Min(100, used));
            }, true, d => d.ToString("0") + "%");
            AddChart("磁盘活动", disk, d => Math.Max(0, Math.Min(100, d)), true, d => d.ToString("0") + "%");
            AddChart("网络吞吐", net, d => d, false, d => Native.FormatBytes(d) + "/s");

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
            Tick();

            var unavailable = _charts.Count(c => c.Reader == null);
            CounterHint.Text = unavailable > 0 ? unavailable + " 项指标不可用（性能计数器未启用）" : "采样周期 1 秒";
            Unloaded += (s, e) => Stop();
        }

        private void AddChart(string title, PerformanceCounter counter, Func<double, double?> transform,
            bool fixedScale, Func<double, string> format)
        {
            var chart = new PerfChart(title, counter, transform, fixedScale, format);
            var index = _charts.Count;
            var border = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(16, 12, 16, 12) };
            border.Child = chart;
            Grid.SetRow(border, index / 2 * 2);
            Grid.SetColumn(border, index % 2 * 2);
            ChartGrid.Children.Add(border);
            _charts.Add(chart);
        }

        private void Tick()
        {
            foreach (var chart in _charts)
            {
                chart.Sample();
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var chart in _charts)
            {
                chart.Clear();
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_charts.Count == 0)
            {
                return;
            }

            var max = _charts.Max(c => c.History.Count);
            if (max == 0)
            {
                Ui.Info("还没有采样数据，请稍候再导出。");
                return;
            }

            try
            {
                var builder = new System.Text.StringBuilder();
                builder.Append("采样");
                foreach (var chart in _charts)
                {
                    builder.Append(",").Append(chart.Title);
                }

                builder.AppendLine();
                for (var i = 0; i < max; i++)
                {
                    builder.Append(i + 1);
                    foreach (var chart in _charts)
                    {
                        builder.Append(",");
                        if (i < chart.History.Count)
                        {
                            builder.Append(chart.History[i].ToString("0.##"));
                        }
                    }

                    builder.AppendLine();
                }

                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "SysKit性能记录-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                File.WriteAllText(path, builder.ToString(), System.Text.Encoding.UTF8);
                if (Ui.Confirm("性能历史已导出到：\r\n" + path + "\r\n\r\n要立即打开吗？"))
                {
                    Native.Start(path);
                }
            }
            catch (Exception ex)
            {
                Ui.Error("导出失败：" + ex.Message);
            }
        }

        private void Stop()
        {
            _timer.Stop();
            foreach (var chart in _charts)
            {
                chart.Dispose();
            }
        }

        private static PerformanceCounter CreateCounter(string[] categories, string[] counters, string instance)
        {
            foreach (var category in categories)
            {
                foreach (var counter in counters)
                {
                    try
                    {
                        var p = new PerformanceCounter(category, counter, instance ?? string.Empty);
                        p.NextValue();
                        return p;
                    }
                    catch
                    {
                        // try next
                    }
                }
            }

            // 自动发现：遍历所有类别，找到包含目标计数器的类别
            try
            {
                foreach (var cat in PerformanceCounterCategory.GetCategories())
                {
                    foreach (var counter in counters)
                    {
                        if (!cat.CounterExists(counter))
                        {
                            continue;
                        }

                        foreach (var inst in new[] { instance, "_Total", null }.Where(x => x != null).Distinct())
                        {
                            try
                            {
                                var p = new PerformanceCounter(cat.CategoryName, counter, inst);
                                p.NextValue();
                                return p;
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static PerformanceCounter CreateNetworkCounter()
        {
            try
            {
                foreach (var category in new[] { "Network Interface", "网络接口" })
                {
                    if (!PerformanceCounterCategory.Exists(category))
                    {
                        continue;
                    }

                    var counterName = "Bytes Total/sec";
                    var cat = new PerformanceCounterCategory(category);
                    if (!cat.CounterExists(counterName))
                    {
                        counterName = "字节/秒";
                    }

                    var instances = cat.GetInstanceNames();
                    foreach (var instance in instances)
                    {
                        try
                        {
                            var p = new PerformanceCounter(category, counterName, instance);
                            p.NextValue();
                            return p;
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }

    internal sealed class PerfChart : StackPanel
    {
        private readonly PerformanceCounter _counter;
        private readonly Func<double, double?> _transform;
        private readonly bool _fixedScale;
        private readonly Func<double, string> _format;
        private readonly List<double> _history = new List<double>();
        private readonly string _title;
        private readonly TextBlock _valueText;
        private readonly Polyline _line;
        private readonly Canvas _canvas;

        public PerformanceCounter Reader => _counter;

        public string Title => _title;

        public IReadOnlyList<double> History => _history;

        public PerfChart(string title, PerformanceCounter counter, Func<double, double?> transform,
            bool fixedScale, Func<double, string> format)
        {
            _counter = counter;
            _title = title;
            _transform = transform;
            _fixedScale = fixedScale;
            _format = format;

            Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.FindResource("Sub") });

            _valueText = new TextBlock
            {
                Text = "—",
                Style = (Style)Application.Current.FindResource("H2"),
                Margin = new Thickness(0, 6, 0, 6)
            };
            Children.Add(_valueText);

            var border = new Border
            {
                Background = (Brush)Application.Current.FindResource("BgCardAlt"),
                CornerRadius = new CornerRadius(6),
                Height = 130,
                ClipToBounds = true
            };
            _canvas = new Canvas { Background = Brushes.Transparent };
            _line = new Polyline
            {
                Stroke = (Brush)Application.Current.FindResource("Accent"),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            _canvas.Children.Add(_line);
            border.Child = _canvas;
            Children.Add(border);
        }

        public void Sample()
        {
            if (_counter == null)
            {
                _valueText.Text = "不可用";
                return;
            }

            double raw;
            try
            {
                raw = _counter.NextValue();
            }
            catch
            {
                _valueText.Text = "不可用";
                return;
            }

            var value = _transform(raw);
            if (!value.HasValue)
            {
                return;
            }

            _history.Add(value.Value);
            if (_history.Count > 160)
            {
                _history.RemoveAt(0);
            }

            _valueText.Text = _format(value.Value);
            Redraw();
        }

        public void Clear()
        {
            _history.Clear();
            _valueText.Text = "—";
            _line.Points.Clear();
        }

        public void Dispose()
        {
            try
            {
                _counter?.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        private void Redraw()
        {
            var width = _canvas.ActualWidth;
            var height = _canvas.ActualHeight;
            if (width < 4 || height < 4 || _history.Count < 2)
            {
                return;
            }

            double min;
            double max;
            if (_fixedScale)
            {
                min = 0;
                max = 100;
            }
            else
            {
                min = _history.Min();
                max = _history.Max();
                if (max - min < 1)
                {
                    max = min + 1;
                }

                var padding = (max - min) * 0.1;
                min = Math.Max(0, min - padding);
                max += padding;
            }

            var range = max - min;
            if (range <= 0)
            {
                range = 1;
            }

            _line.Points.Clear();
            var step = width / 160.0;
            for (var i = 0; i < _history.Count; i++)
            {
                var x = width - (_history.Count - 1 - i) * step;
                var y = height - (Math.Max(min, Math.Min(max, _history[i])) - min) / range * height;
                _line.Points.Add(new Point(x, Math.Max(0, Math.Min(height, y))));
            }
        }
    }
}
