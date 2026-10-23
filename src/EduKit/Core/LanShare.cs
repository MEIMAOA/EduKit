using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EduKit.Core
{
    /// <summary>
    /// 局域网快传：内置一个极简 HTTP 服务，手机/其他电脑用浏览器打开即可上传、下载文件。
    /// 使用 TcpListener 自行解析 HTTP，不依赖 http.sys 的 URL 预留，普通权限即可运行。
    /// </summary>
    public class LanShareServer : ObservableObject
    {
        private TcpListener _listener;
        private CancellationTokenSource _cancellation;
        private string _root;
        private int _port;

        public event Action<string> Logged;

        public bool IsRunning => _listener != null;

        public int Port => _port;

        public string Root => _root;

        public List<string> GetUrls()
        {
            var urls = new List<string>();
            foreach (var ip in NetUtil.GetLocalIPv4())
            {
                urls.Add("http://" + ip + ":" + _port + "/");
            }

            if (urls.Count == 0)
            {
                urls.Add("http://127.0.0.1:" + _port + "/");
            }

            return urls;
        }

        public bool Start(int port, string root)
        {
            if (IsRunning)
            {
                return true;
            }

            try
            {
                if (NetUtil.IsPortInUse(port))
                {
                    Logged?.Invoke("端口 " + port + " 已被占用，请换一个端口。");
                    return false;
                }

                _root = root;
                _port = port;
                Directory.CreateDirectory(root);

                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _cancellation = new CancellationTokenSource();
                var token = _cancellation.Token;
                _ = Task.Run(() => AcceptLoopAsync(token));

                Logged?.Invoke("服务已启动，手机连同一 Wi-Fi 后打开上方地址即可传文件。");
                OnPropertyChanged(nameof(IsRunning));
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke("启动失败：" + ex.Message + "（可尝试换端口，或检查防火墙）");
                _listener = null;
                OnPropertyChanged(nameof(IsRunning));
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                _cancellation?.Cancel();
                _listener?.Stop();
            }
            catch
            {
                // ignored
            }
            finally
            {
                _listener = null;
                _cancellation = null;
                OnPropertyChanged(nameof(IsRunning));
                Logged?.Invoke("服务已停止。");
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch
                {
                    break;
                }

                var current = client;
                _ = Task.Run(() => HandleClientAsync(current));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;
                    var stream = client.GetStream();
                    var request = await ReadRequestAsync(stream);
                    if (request == null)
                    {
                        return;
                    }

                    var path = request.Path;
                    if (request.Method == "GET" && (path == "/" || path == "/index.html"))
                    {
                        await WriteAsync(stream, "text/html; charset=utf-8", BuildPage());
                    }
                    else if (request.Method == "GET" && path.StartsWith("/api/list"))
                    {
                        await WriteAsync(stream, "application/json; charset=utf-8", BuildFileListJson());
                    }
                    else if (request.Method == "GET" && path.StartsWith("/download"))
                    {
                        await ServeFileAsync(stream, QueryValue(path, "name"));
                    }
                    else if (request.Method == "POST" && path.StartsWith("/upload"))
                    {
                        await ReceiveUploadAsync(stream, request, QueryValue(path, "name"));
                    }
                    else if (request.Method == "POST" && path.StartsWith("/delete"))
                    {
                        DeleteFile(QueryValue(path, "name"));
                        await WriteAsync(stream, "application/json; charset=utf-8", "{\"ok\":true}");
                    }
                    else
                    {
                        await WriteAsync(stream, "text/plain; charset=utf-8", "404 Not Found");
                    }
                }
                catch (Exception ex)
                {
                    Logged?.Invoke("请求处理失败：" + ex.Message);
                }
            }
        }

        private sealed class HttpRequest
        {
            public string Method { get; set; }

            public string Path { get; set; }

            public int ContentLength { get; set; }

            public byte[] BodyStart { get; set; }
        }

        private static async Task<HttpRequest> ReadRequestAsync(NetworkStream stream)
        {
            var received = new List<byte>(4096);
            var buffer = new byte[8192];
            var headerEnd = -1;

            while (headerEnd < 0)
            {
                var count = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (count <= 0)
                {
                    return null;
                }

                received.AddRange(buffer.Take(count));
                headerEnd = IndexOfHeaderEnd(received);
                if (received.Count > 1024 * 64)
                {
                    return null;
                }
            }

            var headerText = Encoding.UTF8.GetString(received.Take(headerEnd).ToArray());
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return null;
            }

            var first = lines[0].Split(' ');
            if (first.Length < 2)
            {
                return null;
            }

            var request = new HttpRequest
            {
                Method = first[0].ToUpperInvariant(),
                Path = first[1],
                BodyStart = received.Skip(headerEnd + 4).ToArray()
            };

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var index = line.IndexOf(':');
                if (index <= 0)
                {
                    continue;
                }

                if (line.Substring(0, index).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int length;
                    if (int.TryParse(line.Substring(index + 1).Trim(), out length))
                    {
                        request.ContentLength = length;
                    }
                }
            }

            return request;
        }

        private static int IndexOfHeaderEnd(IList<byte> bytes)
        {
            for (var i = 0; i + 3 < bytes.Count; i++)
            {
                if (bytes[i] == 13 && bytes[i + 1] == 10 && bytes[i + 2] == 13 && bytes[i + 3] == 10)
                {
                    return i;
                }
            }

            return -1;
        }

        private static async Task WriteAsync(NetworkStream stream, string contentType, string content)
        {
            var body = Encoding.UTF8.GetBytes(content);
            var header = "HTTP/1.1 200 OK\r\nContent-Type: " + contentType +
                         "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(body, 0, body.Length);
        }

        private async Task ServeFileAsync(NetworkStream stream, string rawName)
        {
            var name = SafeName(rawName);
            var path = name == null ? null : Path.Combine(_root, name);
            if (path == null || !File.Exists(path))
            {
                await WriteAsync(stream, "text/plain; charset=utf-8", "文件不存在或已被删除。");
                return;
            }

            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var header = "HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: " +
                             file.Length + "\r\nContent-Disposition: attachment; filename*=UTF-8''" +
                             Uri.EscapeDataString(name) + "\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(header);
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

                var buffer = new byte[81920];
                int read;
                while ((read = await file.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, read);
                }
            }
        }

        private async Task ReceiveUploadAsync(NetworkStream stream, HttpRequest request, string rawName)
        {
            var name = SafeName(rawName);
            if (name == null)
            {
                await WriteAsync(stream, "application/json; charset=utf-8",
                    "{\"ok\":false,\"error\":\"文件名无效\"}");
                return;
            }

            var size = request.ContentLength;
            if (size <= 0)
            {
                await WriteAsync(stream, "application/json; charset=utf-8",
                    "{\"ok\":false,\"error\":\"文件为空\"}");
                return;
            }

            if (size > 1024 * 1024 * 1024)
            {
                await WriteAsync(stream, "application/json; charset=utf-8",
                    "{\"ok\":false,\"error\":\"文件超过 1GB 限制\"}");
                return;
            }

            Directory.CreateDirectory(_root);
            var target = UniquePath(name);
            long written = 0;

            using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (request.BodyStart != null && request.BodyStart.Length > 0)
                {
                    var startCount = Math.Min(request.BodyStart.Length, size);
                    await file.WriteAsync(request.BodyStart, 0, startCount);
                    written += startCount;
                }

                var buffer = new byte[81920];
                while (written < size)
                {
                    var toRead = (int)Math.Min(buffer.Length, size - written);
                    var read = await stream.ReadAsync(buffer, 0, toRead);
                    if (read <= 0)
                    {
                        break;
                    }

                    await file.WriteAsync(buffer, 0, read);
                    written += read;
                }
            }

            if (written <= 0)
            {
                TryDelete(target);
                await WriteAsync(stream, "application/json; charset=utf-8",
                    "{\"ok\":false,\"error\":\"接收失败\"}");
                return;
            }

            Logged?.Invoke("收到文件：" + Path.GetFileName(target) + "（" + Native.FormatBytes(written) + "）");
            await WriteAsync(stream, "application/json; charset=utf-8",
                "{\"ok\":true,\"name\":" + JsonString(Path.GetFileName(target)) + ",\"size\":" + written + "}");
        }

        private void DeleteFile(string rawName)
        {
            var name = SafeName(rawName);
            if (name == null)
            {
                return;
            }

            TryDelete(Path.Combine(_root, name));
            Logged?.Invoke("已删除文件：" + name);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignored
            }
        }

        private string UniquePath(string name)
        {
            var path = Path.Combine(_root, name);
            if (!File.Exists(path))
            {
                return path;
            }

            var baseName = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);
            for (var i = 1; i < 1000; i++)
            {
                var candidate = Path.Combine(_root, baseName + " (" + i + ")" + extension);
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(_root, Guid.NewGuid().ToString("N") + extension);
        }

        private static string SafeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var name = Path.GetFileName(WebUtility.UrlDecode(raw));
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }

            return name;
        }

        private static string QueryValue(string path, string key)
        {
            var index = path.IndexOf('?');
            if (index < 0)
            {
                return null;
            }

            var query = path.Substring(index + 1);
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }
            }

            return null;
        }

        private static string JsonString(string text)
        {
            if (text == null)
            {
                return "null";
            }

            var builder = new StringBuilder("\"");
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (ch < 32)
                        {
                            builder.Append("\\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }

            return builder.Append('"').ToString();
        }

        private string BuildFileListJson()
        {
            var items = new List<string>();
            try
            {
                foreach (var file in new DirectoryInfo(_root).GetFiles().OrderByDescending(f => f.LastWriteTime))
                {
                    items.Add("{\"name\":" + JsonString(file.Name) + ",\"size\":" + file.Length +
                              ",\"sizeText\":" + JsonString(Native.FormatBytes(file.Length)) +
                              ",\"time\":" + JsonString(file.LastWriteTime.ToString("MM-dd HH:mm")) + "}");
                }
            }
            catch
            {
                // ignored
            }

            return "[" + string.Join(",", items) + "]";
        }

        private static string BuildPage()
        {
            try
            {
                using (var stream = typeof(LanShareServer).Assembly.GetManifestResourceStream("EduKit.Core.SharePage.html"))
                {
                    if (stream == null)
                    {
                        return "<h1>EduKit</h1><p>页面资源缺失。</p>";
                    }

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                return "<h1>EduKit</h1><p>页面加载失败。</p>";
            }
        }
    }
}
