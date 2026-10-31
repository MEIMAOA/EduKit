using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using EduKit.Core;

namespace EduKit
{
    /// <summary>
    /// 自检模式（--selftest 参数）：验证数据持久化、硬件信息、进程/服务、网络、显示、清理等核心功能。
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
                lines.Add("管理员权限：" + (Elevation.IsAdministrator ? "是" : "否"));
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

                AppData.Settings.TubaPath = @"C:\SelfTest";
                AppData.SaveSettings();
                AppData.Load();
                Check("配置保存并重新加载", AppData.Settings.TubaPath == @"C:\SelfTest");
                AppData.Settings.TubaPath = string.Empty;
                AppData.SaveSettings();

                var info = HardwareDetails.GetHardwareInfo();
                Check("读取硬件信息", info.Count >= 5, info.Count + " 项");
                Check("读取磁盘信息", HardwareDetails.GetDisks().Count >= 1);
                Check("读取系统激活状态", !string.IsNullOrEmpty(HardwareDetails.GetActivationStatus()));

                var processes = ProcessTools.GetProcesses();
                Check("枚举进程", processes.Count > 0, processes.Count + " 个");

                var services = ProcessTools.GetServices();
                Check("枚举系统服务", services.Count > 0, services.Count + " 个");

                var startup = ProcessTools.GetStartupItems();
                if (startup.Count > 0)
                {
                    Check("读取启动项", true, startup.Count + " 项");
                }
                else
                {
                    lines.Add("SKIP  读取启动项  -> 当前环境没有启动项");
                }

                Check("读取网络适配器", NetUtil.GetAdapters().Count > 0,
                    NetUtil.GetAdapters().Count + " 个适配器");
                Check("获取本机 IP", NetUtil.GetLocalIPv4().Count > 0,
                    string.Join(",", NetUtil.GetLocalIPv4()));

                var mode = Native.GetCurrentMode();
                if (mode.dmPelsWidth > 0)
                {
                    Check("读取显示模式", true,
                        mode.dmPelsWidth + "x" + mode.dmPelsHeight + "@" + mode.dmDisplayFrequency + "Hz");
                }
                else
                {
                    lines.Add("SKIP  读取显示模式  -> 当前环境没有可用的图形会话");
                }

                var modeCount = Native.GetAvailableModes().Count;
                if (modeCount > 0)
                {
                    Check("枚举分辨率", true, modeCount + " 种");
                }
                else
                {
                    lines.Add("SKIP  枚举分辨率  -> 当前环境没有可用的显示模式");
                }

                try
                {
                    using (var cpu = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total"))
                    {
                        cpu.NextValue();
                        cpu.NextValue();
                        lines.Add("PASS  性能计数器  -> 可用");
                    }
                }
                catch
                {
                    lines.Add("SKIP  性能计数器  -> 不可用（可在系统里启用性能计数器）");
                }

                var volume = AudioManager.GetVolume();
                lines.Add((volume >= 0 ? "PASS" : "SKIP") + "  系统音量读取  -> " +
                          (volume >= 0 ? volume + "%" : "无音频设备或不可读"));

                var temp = SystemCleanup.GetTempSize();
                Check("统计临时文件", temp >= 0, Native.FormatBytes(temp));

                try
                {
                    var updateInfo = await UpdateChecker.CheckAsync();
                    if (string.IsNullOrEmpty(updateInfo.Error))
                    {
                        lines.Add("PASS  应用更新检查  -> 最新 v" + updateInfo.LatestVersion +
                                  "，当前 v" + App.Version);
                    }
                    else
                    {
                        lines.Add("SKIP  应用更新检查  -> " + updateInfo.Error);
                    }
                }
                catch (Exception ex)
                {
                    lines.Add("SKIP  应用更新检查  -> " + ex.Message);
                }

                try
                {
                    var package = await TubaInstaller.GetLatestAsync();
                    if (package != null)
                    {
                        lines.Add("PASS  图吧工具箱在线版本  -> v" + package.Version +
                                  "，安装版 " + Native.FormatBytes(package.SetupSize) +
                                  "，便携版 " + Native.FormatBytes(package.PortableSize));
                    }
                    else
                    {
                        lines.Add("SKIP  图吧工具箱在线版本  -> 无法获取（网络或 API 限流）");
                    }
                }
                catch (Exception ex)
                {
                    lines.Add("SKIP  图吧工具箱在线版本  -> " + ex.Message);
                }

                var tuba = TubaToolbox.Detect();
                if (string.IsNullOrEmpty(tuba))
                {
                    lines.Add("SKIP  图吧工具箱检测  -> 本机未安装，属正常情况");
                }
                else
                {
                    var tubaTools = TubaToolbox.Scan(tuba);
                    Check("图吧工具箱检测与扫描", tubaTools.Count > 0,
                        tuba + "，" + tubaTools.Count + " 个工具");
                }

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
                var dir = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(reportPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
            }
            catch
            {
                // ignored
            }

            Application.Current.Shutdown(failed == 0 ? 0 : 1);
        }
    }
}