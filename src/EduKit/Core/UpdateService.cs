using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EduKit.Core
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }

        public string LatestVersion { get; set; }

        public string ReleaseUrl { get; set; }

        public string Notes { get; set; }

        public string AssetName { get; set; }

        public string AssetUrl { get; set; }

        public long AssetSize { get; set; }

        public string Sha256 { get; set; }

        public string Error { get; set; }
    }

    /// <summary>应用自更新：检查 GitHub Releases → 下载新版本 zip → 校验 → 退出并替换文件后重启。</summary>
    public static class UpdateChecker
    {
        private const string Repo = "MEIMAOA/EduKit";

        public static async Task<UpdateInfo> CheckAsync()
        {
            try
            {
                var release = await GitHubClient.GetLatestReleaseAsync(Repo).ConfigureAwait(false);
                if (release == null)
                {
                    return new UpdateInfo { Error = "无法连接 GitHub（可能网络受限）" };
                }

                var tag = (release.TagName ?? string.Empty).TrimStart('v', 'V');
                Version latest;
                if (!Version.TryParse(tag, out latest))
                {
                    return new UpdateInfo { Error = "无法解析版本号：" + release.TagName };
                }

                Version current;
                if (!Version.TryParse(App.Version, out current))
                {
                    current = new Version(1, 0);
                }

                var info = new UpdateInfo
                {
                    HasUpdate = latest > current,
                    LatestVersion = tag,
                    ReleaseUrl = release.HtmlUrl,
                    Notes = release.Body
                };

                if (release.Assets != null)
                {
                    foreach (var asset in release.Assets)
                    {
                        var name = asset.Name ?? string.Empty;
                        if (name.StartsWith("EduKit-v", StringComparison.OrdinalIgnoreCase) &&
                            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            info.AssetName = name;
                            info.AssetUrl = asset.DownloadUrl;
                            info.AssetSize = asset.Size;
                            break;
                        }
                    }
                }

                // 从更新说明中解析出 SHA256（发布说明中包含 "SHA256：<hash>"）
                if (!string.IsNullOrEmpty(release.Body))
                {
                    var match = Regex.Match(release.Body, @"SHA256[：:]\s*([0-9a-fA-F]{64})");
                    if (match.Success)
                    {
                        info.Sha256 = match.Groups[1].Value.ToLowerInvariant();
                    }
                }

                if (string.IsNullOrEmpty(info.AssetUrl))
                {
                    info.Error = "该版本没有找到可下载的 EduKit zip 资产";
                }

                return info;
            }
            catch (Exception ex)
            {
                return new UpdateInfo { Error = ex.Message };
            }
        }

        /// <summary>下载并应用更新。成功后调用方应退出程序，更新脚本会在退出后替换文件并重启。</summary>
        public static async Task<string> DownloadAndApplyAsync(UpdateInfo info,
            Action<DownloadProgress> onProgress, CancellationToken token)
        {
            if (info == null || string.IsNullOrEmpty(info.AssetUrl))
            {
                return "没有可用的更新下载地址";
            }

            var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty);
            if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
            {
                return "无法定位程序目录";
            }

            // 检查程序目录是否可写
            try
            {
                var probe = Path.Combine(appDir, ".update-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
            }
            catch
            {
                return "程序目录不可写，请把 EduKit 放到有写入权限的目录后再更新";
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "EduKit-Update-" + DateTime.Now.Ticks);
            var zipPath = Path.Combine(tempRoot, string.IsNullOrEmpty(info.AssetName) ? "EduKit-update.zip" : info.AssetName);
            var extractDir = Path.Combine(tempRoot, "files");
            Directory.CreateDirectory(tempRoot);

            try
            {
                await GitHubClient.DownloadFileAsync(info.AssetUrl, zipPath, onProgress, token).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(info.Sha256))
                {
                    var hash = GitHubClient.GetSha256(zipPath);
                    if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return "更新包校验失败（SHA256 不匹配），已取消更新";
                    }
                }

                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                if (!File.Exists(Path.Combine(extractDir, "EduKit.exe")))
                {
                    return "更新包内容不正确（缺少 EduKit.exe）";
                }

                // 生成替换脚本：等待当前进程退出 → 复制文件 → 重启 → 清理
                var batPath = Path.Combine(tempRoot, "apply-update.bat");
                var bat = "@echo off\r\n" +
                          "chcp 65001 >nul\r\n" +
                          "set \"SRC=" + extractDir + "\"\r\n" +
                          "set \"DST=" + appDir + "\"\r\n" +
                          "set \"PID=" + System.Diagnostics.Process.GetCurrentProcess().Id + "\"\r\n" +
                          ":wait\r\n" +
                          "tasklist /fi \"PID eq %PID%\" 2>nul | find /i \"EduKit.exe\" >nul\r\n" +
                          "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
                          "xcopy /y /e /i \"%SRC%\\*\" \"%DST%\\\" >nul\r\n" +
                          "start \"\" \"%DST%\\EduKit.exe\"\r\n" +
                          "rmdir /s /q \"" + tempRoot + "\" >nul 2>nul\r\n";

                File.WriteAllText(batPath, bat, System.Text.Encoding.Default);

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + batPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    WorkingDirectory = tempRoot
                };

                System.Diagnostics.Process.Start(startInfo);
                return null;
            }
            catch (OperationCanceledException)
            {
                return "更新已取消";
            }
            catch (Exception ex)
            {
                return "更新失败：" + ex.Message;
            }
        }
    }
}