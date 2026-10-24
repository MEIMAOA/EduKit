using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EduKit.Core
{
    public class TubaPackage
    {
        public string Version { get; set; }

        public string SetupName { get; set; }

        public string SetupUrl { get; set; }

        public long SetupSize { get; set; }

        public string PortableName { get; set; }

        public string PortableUrl { get; set; }

        public long PortableSize { get; set; }

        public string DownloadPageUrl { get; set; } = "https://github.com/luolangaga/tubatools/releases/latest";
    }

    /// <summary>
    /// 图吧工具箱自动下载安装：
    /// 原版官网只提供网盘下载，无法程序化直链；这里对接社区版「图吧工具箱CE」（GPL-3.0）的 GitHub Releases，
    /// 支持下载安装包（Setup）或便携版（Portable）。下载地址与版本均实时从 GitHub API 获取。
    /// </summary>
    public static class TubaInstaller
    {
        private const string Repo = "luolangaga/tubatools";

        public static async Task<TubaPackage> GetLatestAsync()
        {
            var release = await GitHubClient.GetLatestReleaseAsync(Repo).ConfigureAwait(false);
            if (release?.Assets == null)
            {
                return null;
            }

            var package = new TubaPackage
            {
                Version = (release.TagName ?? string.Empty).TrimStart('v', 'V'),
                DownloadPageUrl = string.IsNullOrEmpty(release.HtmlUrl) ? "https://github.com/luolangaga/tubatools/releases" : release.HtmlUrl
            };

            foreach (var asset in release.Assets)
            {
                var name = asset.Name ?? string.Empty;
                if (name.StartsWith("TubaWinUi3_Setup_", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith("_x64.exe", StringComparison.OrdinalIgnoreCase))
                {
                    package.SetupName = name;
                    package.SetupUrl = asset.DownloadUrl;
                    package.SetupSize = asset.Size;
                }
                else if (name.StartsWith("TubaWinUi3_Portable_", StringComparison.OrdinalIgnoreCase) &&
                         name.EndsWith("_x64.zip", StringComparison.OrdinalIgnoreCase))
                {
                    package.PortableName = name;
                    package.PortableUrl = asset.DownloadUrl;
                    package.PortableSize = asset.Size;
                }
            }

            return package.SetupUrl == null && package.PortableUrl == null ? null : package;
        }

        /// <summary>下载安装包并启动安装程序。</summary>
        public static async Task<bool> DownloadAndRunSetupAsync(TubaPackage package,
            Action<DownloadProgress> onProgress, CancellationToken token)
        {
            if (package == null || string.IsNullOrEmpty(package.SetupUrl))
            {
                return false;
            }

            var target = Path.Combine(Path.GetTempPath(), "EduKit-Tuba", package.SetupName);
            if (File.Exists(target) && new FileInfo(target).Length == package.SetupSize)
            {
                return Native.Start(target);
            }

            await GitHubClient.DownloadFileAsync(package.SetupUrl, target, onProgress, token).ConfigureAwait(false);
            var ok = Native.Start(target);
            AppData.Settings.TubaPath = string.Empty;
            AppData.SaveSettings();
            return ok;
        }

        /// <summary>下载便携版并解压到用户目录，自动设置为图吧工具箱路径。</summary>
        public static async Task<string> DownloadAndExtractPortableAsync(TubaPackage package,
            Action<DownloadProgress> onProgress, CancellationToken token)
        {
            if (package == null || string.IsNullOrEmpty(package.PortableUrl))
            {
                return null;
            }

            var zip = Path.Combine(Path.GetTempPath(), "EduKit-Tuba", package.PortableName);
            await GitHubClient.DownloadFileAsync(package.PortableUrl, zip, onProgress, token).ConfigureAwait(false);

            var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TubaToolboxCE");
            try
            {
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }

                Directory.CreateDirectory(target);
                ZipFile.ExtractToDirectory(zip, target);

                AppData.Settings.TubaPath = target;
                AppData.SaveSettings();
                return target;
            }
            catch
            {
                return null;
            }
        }
    }
}