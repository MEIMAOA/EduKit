using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class NetworkPage : UserControl
    {
        public NetworkPage()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshAdapters();
        }

        private void RefreshAdapters()
        {
            try
            {
                AdapterList.ItemsSource = NetUtil.GetAdapters();
            }
            catch (Exception ex)
            {
                Append("读取网卡信息失败：" + ex.Message);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAdapters();
        }

        private void Append(string text)
        {
            OutputBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        }

        private void ClearOutput_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.Clear();
        }

        private async void Ping_Click(object sender, RoutedEventArgs e)
        {
            var host = PingHost.Text.Trim();
            int count;
            if (string.IsNullOrWhiteSpace(host))
            {
                Ui.Warn("请输入要 Ping 的地址。");
                return;
            }

            if (!int.TryParse(PingCount.Text.Trim(), out count) || count < 1 || count > 50)
            {
                Ui.Warn("Ping 次数请填 1 - 50。");
                return;
            }

            Append("正在 Ping " + host + " …");
            var sent = 0;
            var received = 0;
            long total = 0;
            long min = long.MaxValue;
            long max = 0;

            try
            {
                for (var i = 0; i < count; i++)
                {
                    sent++;
                    var reply = await new Ping().SendPingAsync(host, 1500);
                    if (reply.Status == IPStatus.Success)
                    {
                        received++;
                        total += reply.RoundtripTime;
                        min = Math.Min(min, reply.RoundtripTime);
                        max = Math.Max(max, reply.RoundtripTime);
                        Append("来自 " + reply.Address + " 的回复：时间=" + reply.RoundtripTime + "ms TTL=" +
                               (reply.Options?.Ttl.ToString() ?? "-"));
                    }
                    else
                    {
                        Append("请求失败：" + reply.Status);
                    }

                    await Task.Delay(200);
                }

                if (received > 0)
                {
                    Append("统计：已发送 " + sent + "，已接收 " + received + "，平均 " + (total / received) +
                           "ms，最小 " + min + "ms，最大 " + max + "ms");
                }
                else
                {
                    Append("全部超时，请检查网线 / Wi-Fi 或 IP 地址。");
                }
            }
            catch (Exception ex)
            {
                Append("Ping 出错：" + ex.Message);
            }
        }

        private async void Dns_Click(object sender, RoutedEventArgs e)
        {
            var host = DnsHost.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                Ui.Warn("请输入要解析的域名。");
                return;
            }

            Append("正在解析 " + host + " …");
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                if (addresses.Length == 0)
                {
                    Append("没有解析到地址。");
                    return;
                }

                var ipv4 = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
                foreach (var address in ipv4.Count > 0 ? ipv4 : addresses.ToList())
                {
                    Append("  → " + address);
                }
            }
            catch (Exception ex)
            {
                Append("解析失败：" + ex.Message);
            }
        }

        private async void Port_Click(object sender, RoutedEventArgs e)
        {
            var host = PortHost.Text.Trim();
            int port;
            if (string.IsNullOrWhiteSpace(host) || !int.TryParse(PortNumber.Text.Trim(), out port) || port <= 0 ||
                port > 65535)
            {
                Ui.Warn("请输入正确的主机和端口（1 - 65535）。");
                return;
            }

            Append("测试 " + host + ":" + port + " 是否可连接 …");
            using (var client = new TcpClient())
            {
                try
                {
                    var connect = client.ConnectAsync(host, port);
                    var completed = await Task.WhenAny(connect, Task.Delay(3000));
                    if (completed != connect)
                    {
                        Append("连接超时（3 秒），端口可能被防火墙拦截。");
                        return;
                    }

                    await connect;
                    Append("端口开放：可以连接。");
                }
                catch (Exception ex)
                {
                    Append("连接失败：" + ex.Message);
                }
            }
        }

        private void CopyIp_Click(object sender, RoutedEventArgs e)
        {
            var ips = NetUtil.GetLocalIPv4();
            if (ips.Count == 0)
            {
                Ui.Warn("没有找到本机 IPv4 地址。");
                return;
            }

            Ui.Copy(string.Join("  ", ips));
            Ui.Info("已复制本机 IP：" + string.Join("  ", ips));
        }

        private void OpenAdapter_Click(object sender, RoutedEventArgs e)
        {
            Native.Start("ncpa.cpl");
        }

        private void FlushDns_Click(object sender, RoutedEventArgs e)
        {
            Native.StartQuiet("ipconfig.exe", "/flushdns");
            Append("已执行 DNS 缓存刷新。");
        }

        private async void LanScan_Click(object sender, RoutedEventArgs e)
        {
            var ip = NetUtil.GetLocalIPv4().FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
            {
                Ui.Warn("没有找到本机 IPv4 地址，无法扫描局域网。");
                return;
            }

            var prefix = ip.Substring(0, ip.LastIndexOf('.') + 1);
            Append("开始扫描 " + prefix + "1 - 254 …");
            var found = new ConcurrentBag<string>();
            await Task.Run(() =>
            {
                Parallel.ForEach(Enumerable.Range(1, 254),
                    new ParallelOptions { MaxDegreeOfParallelism = 64 },
                    i =>
                    {
                        try
                        {
                            using (var ping = new Ping())
                            {
                                var reply = ping.Send(prefix + i, 350);
                                if (reply.Status == IPStatus.Success)
                                {
                                    found.Add(prefix + i);
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    });
            });

            var list = found.OrderBy(s => int.Parse(s.Substring(s.LastIndexOf('.') + 1))).ToList();
            Append("扫描完成，发现 " + list.Count + " 台在线设备：");
            foreach (var address in list)
            {
                Append("  → " + address);
            }
        }

        private async void Tracert_Click(object sender, RoutedEventArgs e)
        {
            var host = PingHost.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                Ui.Warn("请输入要跟踪路由的地址。");
                return;
            }

            Append("路由跟踪 " + host + " …");
            await Task.Run(() =>
            {
                try
                {
                    using (var process = new System.Diagnostics.Process())
                    {
                        process.StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "tracert.exe",
                            Arguments = "-d " + host,
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        process.Start();
                        string line;
                        while ((line = process.StandardOutput.ReadLine()) != null)
                        {
                            var text = line;
                            Dispatcher.BeginInvoke(new Action(() => Append(text)));
                        }

                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() => Append("跟踪失败：" + ex.Message)));
                }
            });
        }

        private void Hosts_Click(object sender, RoutedEventArgs e)
        {
            var hosts = System.IO.Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
            string error;
            if (!Elevation.RunAsAdmin("notepad.exe", "\"" + hosts + "\"", out error))
            {
                Ui.Warn("编辑 hosts 需要管理员权限，提权失败：" + error);
            }
        }
    }
}
