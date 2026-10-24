using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EduKit.Core
{
    public class DownloadProgress
    {
        public long Received { get; set; }

        public long Total { get; set; }

        public double BytesPerSecond { get; set; }

        public double Percent => Total > 0 ? Received * 100.0 / Total : 0;

        public string SpeedText => Native.FormatBytes(BytesPerSecond) + "/s";

        public string SizeText => Total > 0
            ? Native.FormatBytes(Received) + " / " + Native.FormatBytes(Total)
            : Native.FormatBytes(Received);

        public string EtaText
        {
            get
            {
                if (Total <= 0 || BytesPerSecond <= 0 || Received >= Total)
                {
                    return string.Empty;
                }

                var seconds = (long)((Total - Received) / BytesPerSecond);
                return seconds >= 60 ? (seconds / 60) + " 分 " + (seconds % 60) + " 秒" : seconds + " 秒";
            }
        }
    }

    [DataContract]
    public class GitHubRelease
    {
        [DataMember(Name = "tag_name")] public string TagName { get; set; }

        [DataMember(Name = "name")] public string Name { get; set; }

        [DataMember(Name = "body")] public string Body { get; set; }

        [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }

        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }

        [DataMember(Name = "assets")] public List<GitHubAsset> Assets { get; set; }
    }

    [DataContract]
    public class GitHubAsset
    {
        [DataMember(Name = "name")] public string Name { get; set; }

        [DataMember(Name = "size")] public long Size { get; set; }

        [DataMember(Name = "browser_download_url")] public string DownloadUrl { get; set; }

        [DataMember(Name = "digest")] public string Digest { get; set; }
    }

    /// <summary>GitHub Releases API 的极简客户端（下载、查询最新版本）。</summary>
    public static class GitHubClient
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EduKit/" + App.Version);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static async Task<GitHubRelease> GetLatestReleaseAsync(string repo)
        {
            try
            {
                var json = await Client.GetStringAsync("https://api.github.com/repos/" + repo + "/releases/latest")
                    .ConfigureAwait(false);
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    return (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>下载文件，按块回调进度（约每 200ms 一次），支持取消。</summary>
        public static async Task DownloadFileAsync(string url, string destination,
            Action<DownloadProgress> onProgress, CancellationToken token)
        {
            using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");

                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None,
                           81920, true))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    var stopwatch = Stopwatch.StartNew();
                    var lastReport = TimeSpan.Zero;

                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        var read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        if (read <= 0)
                        {
                            break;
                        }

                        await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                        received += read;

                        if (stopwatch.Elapsed - lastReport > TimeSpan.FromMilliseconds(200))
                        {
                            lastReport = stopwatch.Elapsed;
                            onProgress?.Invoke(new DownloadProgress
                            {
                                Received = received,
                                Total = total,
                                BytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0
                                    ? received / stopwatch.Elapsed.TotalSeconds
                                    : 0
                            });
                        }
                    }

                    onProgress?.Invoke(new DownloadProgress
                    {
                        Received = received,
                        Total = total > 0 ? total : received,
                        BytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0
                            ? received / stopwatch.Elapsed.TotalSeconds
                            : 0
                    });
                }
            }
        }

        public static string GetSha256(string file)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(file))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}