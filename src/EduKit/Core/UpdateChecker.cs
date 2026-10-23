using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EduKit.Core
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }

        public string LatestVersion { get; set; }

        public string ReleaseUrl { get; set; }

        public string Notes { get; set; }

        public string Error { get; set; }
    }

    /// <summary>通过 GitHub Releases API 检查更新。</summary>
    public static class UpdateChecker
    {
        public static async Task<UpdateInfo> CheckAsync()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("EduKit/" + App.Version);
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                    var json = await client.GetStringAsync(
                        "https://api.github.com/repos/" + App.ProjectUrl.Replace("https://github.com/", "") + "/releases/latest");

                    var tag = Match(json, "tag_name");
                    var url = Match(json, "html_url");
                    var notes = Match(json, "body");

                    var latest = tag.TrimStart('v', 'V');
                    Version latestVersion;
                    if (!Version.TryParse(latest, out latestVersion))
                    {
                        return new UpdateInfo { Error = "无法解析版本号：" + tag };
                    }

                    Version current;
                    if (!Version.TryParse(App.Version, out current))
                    {
                        current = new Version(1, 0);
                    }

                    return new UpdateInfo
                    {
                        HasUpdate = latestVersion > current,
                        LatestVersion = latest,
                        ReleaseUrl = string.IsNullOrEmpty(url) ? App.ProjectUrl : url,
                        Notes = notes
                    };
                }
            }
            catch (Exception ex)
            {
                return new UpdateInfo { Error = ex.Message };
            }
        }

        private static string Match(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!match.Success)
            {
                return string.Empty;
            }

            return Regex.Unescape(match.Groups[1].Value);
        }
    }
}
