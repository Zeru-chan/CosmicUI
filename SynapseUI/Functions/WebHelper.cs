using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SynapseUI.Functions.Web
{
    internal class SecurityProtocolPatch
    {
        internal static bool initialised = false;

        public static void Init()
        {
            if (!initialised)
            {
                var winVer = Environment.OSVersion.Version;
                if (winVer.Major == 6 && winVer.Minor == 1)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                initialised = true;
            }
        }
    }

    public class FileDownloader
    {
        public string BaseUrl { get; set; }
        public string BasePath { get; set; }

        public List<FileEntry> FileEntries = new List<FileEntry>();

        private List<(string Url, string Path)> _entries;

        public void Add(FileEntry entry)
        {
            FileEntries.Add(entry);
        }

        public FileDownloader()
        {
            SecurityProtocolPatch.Init();
        }

        public void Begin()
        {
            using (WebClient client = new WebClient())
            {
                foreach ((string url, string path) in BuildEntries())
                {
                    if (!File.Exists(path))
                        client.DownloadFile(url, path);
                }
            }
        }

        public List<(string url, string path)> BuildEntries()
        {
            if (_entries != null)
                return _entries;

            var entries = new List<(string url, string path)>();
            foreach (var entry in FileEntries)
            {
                string url = BaseUrl + entry.Url + "/" + entry.Filename;
                string path = entry.RelativePath ? Path.Combine(BasePath, entry.Path, entry.Filename) :
                    Path.Combine(entry.Path, entry.Filename);

                entries.Add((url, path));
            }

            _entries = entries;
            return _entries;
        }

        public string Build()
        {
            var s = new StringBuilder();

            foreach ((string url, string path) in BuildEntries())
                s.Append($"{url}|{path}|");

            s.Length--;
            return s.ToString();
        }
    }

    public class FileEntry
    {
        public string Filename { get; }
        public string Path { get; }
        public bool RelativePath { get; }
        public string Url { get; }

        public FileEntry(string filename, string path = "", string url = "", bool relativePath = true)
        {
            Filename = filename;
            Path = path;
            Url = url;
            RelativePath = relativePath;
        }
    }

    public static class VersionChecker
    {
        private const string CosmicInfoUrl = "https://auth.cosmic.best/info";

        public static async Task<string> GetLatestVersionAsync()
        {
            SecurityProtocolPatch.Init();

            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("CosmicUI/1.0");
                    var version = await client.GetStringAsync(CosmicInfoUrl).ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
                }
                catch
                {
                    return null;
                }
            }
        }

        public static string GetCurrentVersion()
        {
            var ver = typeof(App).Assembly.GetName().Version;
            return $"{ver.Major}.{ver.Minor}.{ver.Build}";
        }
    }
}
