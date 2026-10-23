using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace EduKit.Core
{
    /// <summary>极简 JSON 持久化（使用 .NET Framework 内置 DataContractJsonSerializer，不引入任何第三方依赖）。</summary>
    internal static class Json
    {
        private static readonly DataContractJsonSerializerSettings Settings =
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };

        public static T Load<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using (var stream = File.OpenRead(path))
                {
                    return (T)new DataContractJsonSerializer(typeof(T), Settings).ReadObject(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        public static void Save<T>(string path, T value)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (var stream = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(T), Settings).WriteObject(stream, value);
                    File.WriteAllBytes(path, stream.ToArray());
                }
            }
            catch
            {
                // 写入失败不阻断使用（例如磁盘只读）
            }
        }
    }
}
