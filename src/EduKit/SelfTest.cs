using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using EduKit.Core;

namespace EduKit
{
    /// <summary>
    /// 自检模式（--selftest 参数）：验证数据持久化、网络、音频、局域网快传等核心功能是否正常。
    /// 结果写入指定文件，便于在没有界面的环境下做冒烟测试。
    /// </summary>
    public static class SelfTest
    {
        public static async void Run(string reportPath)
        {
            var lines = new List<string>();
            var failed = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (!ok)
                {
                    failed++;
                }

                lines.Add((ok ? "PASS" : "FAIL") + "  " + name + (string.IsNullOrEmpty(detail) ? "" : "  -> " + detail));
            }

            try
            {
                lines.Add("EduKit 自检 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                lines.Add("版本 " + App.Version + "，.NET " + Environment.Version + "，" +
                          (Environment.Is64BitProcess ? "x64" : "x86"));
                lines.Add("数据目录：" + AppData.Dir + "（可写：" + AppData.IsDataDirWritable + "）");
                lines.Add("");

                try
                {
                    var probe = Path.Combine(AppData.Dir, "write-test.txt");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    Check("数据目录可写", true);
                }
                catch (Exception ex)
                {
                    Check("数据目录可写", false, ex.Message);
                }

                // 1. 配置读写
                Check("配置已加载", AppData.Settings != null);
                var originalPort = AppData.Settings.SharePort;
                AppData.Settings.SharePort = 18999;
                AppData.SaveSettings();
                AppData.Load();
                Check("配置可保存并重新加载", AppData.Settings.SharePort == 18999,
                    "port=" + AppData.Settings.SharePort);
                AppData.Settings.SharePort = originalPort;
                AppData.SaveSettings();

                // 2. 名单与课程表数据
                Check("学生名单非空", AppData.Roster.Names.Count > 0, AppData.Roster.Names.Count + " 人");
                ScheduleHelper.SetCourse(0, 0, "语文");
                Check("课程表可写入", ScheduleHelper.GetCourse(0, 0) == "语文");
                ScheduleHelper.SetCourse(0, 0, string.Empty);

                // 3. 值日分组
                var groups = ScheduleHelper.BuildDutyGroups();
                Check("值日分组生成", groups.Count > 0 && groups.All(g => g.Members.Count > 0),
                    groups.Count + " 组");

                // 4. 网络信息
                var adapters = NetUtil.GetAdapters();
                Check("读取网卡信息", adapters.Count > 0, adapters.Count + " 个适配器");
                Check("获取本机 IP", NetUtil.GetLocalIPv4().Count > 0,
                    string.Join(",", NetUtil.GetLocalIPv4()));

                // 5. 显示信息
                var mode = Native.GetCurrentMode();
                Check("读取显示模式", mode.dmPelsWidth > 0,
                    mode.dmPelsWidth + "x" + mode.dmPelsHeight + "@" + mode.dmDisplayFrequency + "Hz");
                Check("枚举分辨率", Native.GetAvailableModes().Count > 0,
                    Native.GetAvailableModes().Count + " 种");

                // 6. 音频（虚拟机可能没有音频设备，不判为失败）
                var volume = AudioManager.GetVolume();
                lines.Add((volume >= 0 ? "PASS" : "SKIP") + "  系统音量读取  -> " +
                          (volume >= 0 ? volume + "%" : "无音频设备或不可读"));

                // 7. 硬件信息
                var hardware = SystemInfoHelper.GetHardwareInfo();
                Check("读取硬件信息", hardware.Count >= 3, hardware.Count + " 项");

                // 8. 局域网快传端到端
                await TestLanShare(lines).ContinueWith(t =>
                {
                    if (t.Result)
                    {
                        lines.Add("PASS  局域网快传（HTTP 上传 / 下载 / 列表 / 删除）");
                    }
                    else
                    {
                        failed++;
                        lines.Add("FAIL  局域网快传端到端测试");
                    }
                });

                lines.Add("");
                lines.Add(failed == 0 ? "自检结果：全部通过" : "自检结果：" + failed + " 项失败");
            }
            catch (Exception ex)
            {
                lines.Add("FAIL  自检异常：" + ex);
                failed++;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
            }
            catch
            {
                // ignored
            }

            Application.Current.Shutdown(failed == 0 ? 0 : 1);
        }

        private static async Task<bool> TestLanShare(List<string> lines)
        {
            var port = new Random().Next(19000, 24000);
            var root = Path.Combine(AppData.Dir, "selftest-share-" + port);
            var server = new LanShareServer();
            var messages = new List<string>();
            server.Logged += m => messages.Add(m);
            try
            {
                Directory.CreateDirectory(root);
                if (!server.Start(port, root))
                {
                    lines.Add("       快传服务启动失败（端口 " + port + "）：" + string.Join(" | ", messages));
                    return false;
                }

                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    var baseUrl = "http://127.0.0.1:" + port + "/";
                    var page = await http.GetStringAsync(baseUrl);
                    if (!page.Contains("电教委工具箱"))
                    {
                        lines.Add("       首页内容不正确");
                        return false;
                    }

                    var payload = Encoding.UTF8.GetBytes("EduKit 局域网快传测试文件 " + DateTime.Now.Ticks);
                    var upload = await http.PostAsync(baseUrl + "upload?name=" + Uri.EscapeDataString("测试文件.txt"),
                        new ByteArrayContent(payload));
                    var uploadText = await upload.Content.ReadAsStringAsync();
                    if (!uploadText.Contains("\"ok\":true"))
                    {
                        lines.Add("       上传接口返回：" + uploadText);
                        return false;
                    }

                    var list = await http.GetStringAsync(baseUrl + "api/list");
                    if (!list.Contains("测试文件.txt"))
                    {
                        lines.Add("       文件列表未包含上传文件");
                        return false;
                    }

                    var downloaded = await http.GetByteArrayAsync(baseUrl + "download?name=" +
                                                                  Uri.EscapeDataString("测试文件.txt"));
                    if (!downloaded.SequenceEqual(payload))
                    {
                        lines.Add("       下载内容与上传内容不一致");
                        return false;
                    }

                    var delete = await http.PostAsync(baseUrl + "delete?name=" +
                                                      Uri.EscapeDataString("测试文件.txt"), new StringContent(""));
                    if (!Directory.GetFiles(root).All(f => !f.EndsWith("测试文件.txt")))
                    {
                        lines.Add("       删除接口未删除文件");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                lines.Add("       快传测试异常：" + ex.Message);
                return false;
            }
            finally
            {
                server.Stop();
                try
                {
                    Directory.Delete(root, true);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
