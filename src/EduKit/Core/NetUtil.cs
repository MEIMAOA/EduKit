using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EduKit.Core
{
    public class AdapterInfo
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Type { get; set; }

        public string Status { get; set; }

        public string Speed { get; set; }

        public string IPv4 { get; set; }

        public string Mask { get; set; }

        public string Gateway { get; set; }

        public string Mac { get; set; }

        public string Dns { get; set; }

        public bool IsUp { get; set; }
    }

    /// <summary>网络相关的读取与操作。</summary>
    public static class NetUtil
    {
        public static List<AdapterInfo> GetAdapters()
        {
            var list = new List<AdapterInfo>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    var properties = nic.GetIPProperties();
                    var ipv4 = properties.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (ipv4 == null && nic.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    var gateway = properties.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                    var dns = string.Join(", ",
                        properties.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                            .Select(a => a.ToString()));

                    list.Add(new AdapterInfo
                    {
                        Name = nic.Name,
                        Description = nic.Description,
                        Type = TranslateType(nic.NetworkInterfaceType),
                        Status = TranslateStatus(nic.OperationalStatus),
                        Speed = nic.Speed > 0 ? Native.FormatBytes(nic.Speed) + "/s" : "-",
                        IPv4 = ipv4?.Address.ToString() ?? "-",
                        Mask = ipv4?.IPv4Mask != null && !ipv4.IPv4Mask.Equals(IPAddress.Any)
                            ? ipv4.IPv4Mask.ToString()
                            : "-",
                        Gateway = gateway?.Address.ToString() ?? "-",
                        Mac = FormatMac(nic.GetPhysicalAddress()?.ToString()),
                        Dns = string.IsNullOrEmpty(dns) ? "-" : dns,
                        IsUp = nic.OperationalStatus == OperationalStatus.Up
                    });
                }
            }
            catch
            {
                // ignored
            }

            return list.OrderByDescending(a => a.IsUp).ThenBy(a => a.Name).ToList();
        }

        public static List<string> GetLocalIPv4()
        {
            var result = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var address in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(address.Address))
                        {
                            var text = address.Address.ToString();
                            if (!result.Contains(text))
                            {
                                result.Add(text);
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return result;
        }

        public static bool IsPortInUse(int port)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                return properties.GetActiveTcpListeners().Any(p => p.Port == port);
            }
            catch
            {
                return false;
            }
        }

        private static string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length != 12)
            {
                return string.IsNullOrEmpty(mac) ? "-" : mac;
            }

            var parts = new List<string>();
            for (var i = 0; i < 12; i += 2)
            {
                parts.Add(mac.Substring(i, 2));
            }

            return string.Join(":", parts);
        }

        private static string TranslateType(NetworkInterfaceType type)
        {
            switch (type)
            {
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.GigabitEthernet:
                case NetworkInterfaceType.FastEthernetT:
                    return "有线";
                case NetworkInterfaceType.Wireless80211:
                    return "无线";
                case NetworkInterfaceType.Ppp:
                    return "拨号";
                default:
                    return type.ToString();
            }
        }

        private static string TranslateStatus(OperationalStatus status)
        {
            switch (status)
            {
                case OperationalStatus.Up:
                    return "已连接";
                case OperationalStatus.Down:
                    return "已断开";
                case OperationalStatus.Dormant:
                    return "休眠";
                case OperationalStatus.NotPresent:
                    return "未安装";
                default:
                    return status.ToString();
            }
        }
    }
}
