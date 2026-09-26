using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace VencordFix
{
    /// <summary>
    /// 具備逾時設定的 WebClient，避免網路異常時卡住啟動流程。
    /// </summary>
    internal class TimeoutWebClient : WebClient
    {
        private readonly int _timeoutMs;

        public TimeoutWebClient(int timeoutMs)
        {
            _timeoutMs = timeoutMs;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request != null)
            {
                request.Timeout = _timeoutMs;
                HttpWebRequest http = request as HttpWebRequest;
                if (http != null)
                {
                    http.ReadWriteTimeout = _timeoutMs;
                    http.AllowAutoRedirect = true;
                }
            }
            return request;
        }
    }

    [DataContract]
    public class VencordReleaseAsset
    {
        [DataMember(Name = "name")]
        public string Name;

        [DataMember(Name = "browser_download_url")]
        public string DownloadUrl;

        [DataMember(Name = "size")]
        public long Size;
    }

    [DataContract]
    public class VencordReleaseInfo
    {
        [DataMember(Name = "name")]
        public string Name;

        [DataMember(Name = "tag_name")]
        public string TagName;

        [DataMember(Name = "assets")]
        public List<VencordReleaseAsset> Assets;

        /// <summary>官方發行名稱格式為 "DevBuild &lt;git hash&gt;"，取最後一段作為版本雜湊。</summary>
        public string BuildHash
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                {
                    return null;
                }

                int index = Name.LastIndexOf(' ');
                string hash = index < 0 ? Name : Name.Substring(index + 1);
                hash = hash.Trim();
                return hash.Length == 0 ? null : hash;
            }
        }
    }

    public class ReleaseCheckResult
    {
        public bool Success;
        public bool FromCache;
        public string LatestHash;
        public VencordReleaseInfo Release;
        public string Error;
    }

    /// <summary>
    /// 負責比對並同步 %AppData%\Vencord\dist 內的 Vencord 執行檔。
    ///
    /// 背景：Vencord 內建自動更新是「執行中」才下載新檔案，舊程式碼仍在記憶體中，
    /// 因此會彈出「VENCORD HAS BEEN UPDATED! Click here to restart」。
    /// 這裡改成在啟動 Discord 之前先把檔案同步到最新版，啟動後雜湊一致，不會再出現重啟提示。
    /// </summary>
    public static class VencordBuildSync
    {
        public const string ReleaseApiUrl = "https://api.github.com/repos/Vendicated/Vencord/releases/latest";
        public const string ReleaseFallbackUrl = "https://vencord.dev/releases/vencord";
        public const string UserAgent = "VencordFix (Windows NT 10.0; Win64; x64)";
        public const string PatcherFileName = "patcher.js";

        static VencordBuildSync()
        {
            // .NET Framework 預設可能只啟用 TLS 1.0，GitHub API 需要 TLS 1.2 以上。
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls12;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
        }

        /// <summary>與官方 Installer / Vencord 自身更新器相同的檔案白名單。</summary>
        private static readonly string[] FilePrefixes = new string[]
        {
            "patcher.js",
            "preload.js",
            "renderer.js",
            "renderer.css"
        };

        /// <summary>必要檔案（缺少任一項就放棄同步，避免弄壞安裝）。</summary>
        private static readonly string[] RequiredFiles = new string[]
        {
            "patcher.js",
            "preload.js",
            "renderer.js",
            "renderer.css"
        };

        public static string CacheFilePath
        {
            get { return Path.Combine(FixConfig.CacheDir, "vencord-release.json"); }
        }

        /// <summary>Vencord 執行檔目錄（與官方 Installer 的 FilesDir 邏輯一致）。</summary>
        public static string DefaultDistDir()
        {
            try
            {
                string overrideDir = Environment.GetEnvironmentVariable("VENCORDFIX_DIST_DIR");
                if (!string.IsNullOrEmpty(overrideDir))
                {
                    return overrideDir;
                }

                string vencordDataDir = Environment.GetEnvironmentVariable("VENCORD_USER_DATA_DIR");
                if (!string.IsNullOrEmpty(vencordDataDir))
                {
                    return Path.Combine(vencordDataDir, "dist");
                }

                string discordDataDir = Environment.GetEnvironmentVariable("DISCORD_USER_DATA_DIR");
                if (!string.IsNullOrEmpty(discordDataDir))
                {
                    string parent = Path.GetFullPath(Path.Combine(discordDataDir, ".."));
                    return Path.Combine(parent, "VencordData", "dist");
                }
            }
            catch
            {
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Vencord",
                "dist");
        }

        /// <summary>由 app.asar 注入器內容取得實際的 Vencord 目錄，失敗時回退至預設路徑。</summary>
        public static string ResolveDistDir(DiscordApp app)
        {
            try
            {
                if (app != null && !string.IsNullOrEmpty(app.ResourcesDir))
                {
                    string injectedPath;
                    if (DiscordApp.TryGetInjectedPatcherPath(Path.Combine(app.ResourcesDir, "app.asar"), out injectedPath))
                    {
                        string dir = Path.GetDirectoryName(injectedPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            return dir;
                        }
                    }
                }
            }
            catch
            {
            }

            return DefaultDistDir();
        }

        /// <summary>讀取已安裝的 Vencord 版本雜湊（patcher.js 第一行的 "// Vencord &lt;hash&gt;"）。</summary>
        public static string ReadInstalledHash(string distDir)
        {
            if (string.IsNullOrEmpty(distDir))
            {
                return null;
            }

            try
            {
                string patcherPath = Path.Combine(distDir, PatcherFileName);
                if (!File.Exists(patcherPath))
                {
                    return null;
                }

                using (FileStream stream = new FileStream(patcherPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string firstLine = reader.ReadLine();
                    if (firstLine != null && firstLine.StartsWith("// Vencord ", StringComparison.Ordinal))
                    {
                        string hash = firstLine.Substring("// Vencord ".Length).Trim();
                        return hash.Length == 0 ? null : hash;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static bool IsOutdated(string installedHash, string latestHash)
        {
            if (string.IsNullOrEmpty(latestHash))
            {
                return false;
            }

            if (string.IsNullOrEmpty(installedHash))
            {
                return true;
            }

            return !string.Equals(installedHash.Trim(), latestHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>取得官方最新發行資訊（可選擇使用快取）。</summary>
        public static ReleaseCheckResult CheckLatestRelease(bool forceRefresh, int cacheMinutes, Action<string> log)
        {
            ReleaseCheckResult result = new ReleaseCheckResult();

            if (!forceRefresh && cacheMinutes > 0)
            {
                try
                {
                    if (File.Exists(CacheFilePath))
                    {
                        FileInfo cacheInfo = new FileInfo(CacheFilePath);
                        if ((DateTime.UtcNow - cacheInfo.LastWriteTimeUtc).TotalMinutes < cacheMinutes)
                        {
                            VencordReleaseInfo cached = ParseRelease(File.ReadAllText(CacheFilePath, Encoding.UTF8));
                            if (cached != null && !string.IsNullOrEmpty(cached.BuildHash))
                            {
                                result.Success = true;
                                result.FromCache = true;
                                result.Release = cached;
                                result.LatestHash = cached.BuildHash;
                                return result;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            string[] urls = new string[] { ReleaseApiUrl, ReleaseFallbackUrl };
            foreach (string url in urls)
            {
                string json = null;
                try
                {
                    using (TimeoutWebClient client = new TimeoutWebClient(10000))
                    {
                        client.Headers.Add("User-Agent", UserAgent);
                        client.Headers.Add("Accept", "application/vnd.github+json");
                        json = client.DownloadString(url);
                    }
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                    if (log != null)
                    {
                        log("[!] 讀取 " + url + " 失敗: " + ex.Message);
                    }
                    continue;
                }

                VencordReleaseInfo info = ParseRelease(json);
                if (info != null && !string.IsNullOrEmpty(info.BuildHash))
                {
                    WriteCache(json);
                    result.Success = true;
                    result.Release = info;
                    result.LatestHash = info.BuildHash;
                    return result;
                }

                result.Error = "無法解析官方發行資訊";
            }

            return result;
        }

        private static VencordReleaseInfo ParseRelease(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(VencordReleaseInfo));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as VencordReleaseInfo;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void WriteCache(string json)
        {
            try
            {
                if (!Directory.Exists(FixConfig.CacheDir))
                {
                    Directory.CreateDirectory(FixConfig.CacheDir);
                }
                File.WriteAllText(CacheFilePath, json, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        /// <summary>依官方白名單挑選需要下載的檔案。</summary>
        public static List<VencordReleaseAsset> SelectBuildAssets(VencordReleaseInfo release)
        {
            List<VencordReleaseAsset> selected = new List<VencordReleaseAsset>();
            if (release == null || release.Assets == null)
            {
                return selected;
            }

            foreach (VencordReleaseAsset asset in release.Assets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.Name) || string.IsNullOrEmpty(asset.DownloadUrl))
                {
                    continue;
                }

                foreach (string prefix in FilePrefixes)
                {
                    if (asset.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        selected.Add(asset);
                        break;
                    }
                }
            }

            return selected;
        }

        private static bool HasRequiredFiles(List<VencordReleaseAsset> assets)
        {
            foreach (string required in RequiredFiles)
            {
                bool found = false;
                foreach (VencordReleaseAsset asset in assets)
                {
                    if (string.Equals(asset.Name, required, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 下載官方最新檔案並覆蓋至 Vencord 目錄。
        /// 先全部下載至暫存檔並驗證大小，全數成功後才置換，避免中斷造成半殘的安裝。
        /// </summary>
        public static bool ApplyRelease(VencordReleaseInfo release, string distDir, Action<string> log)
        {
            Action<string> logger = log ?? (delegate(string msg) { });
            List<VencordReleaseAsset> assets = SelectBuildAssets(release);

            if (!HasRequiredFiles(assets))
            {
                logger("[-] 官方發行檔缺少必要檔案，已中止同步以避免損壞 Vencord。");
                return false;
            }

            try
            {
                if (!Directory.Exists(distDir))
                {
                    Directory.CreateDirectory(distDir);
                }
            }
            catch (Exception ex)
            {
                logger("[-] 無法建立 Vencord 目錄 (" + distDir + "): " + ex.Message);
                return false;
            }

            List<KeyValuePair<string, string>> staged = new List<KeyValuePair<string, string>>();

            try
            {
                using (TimeoutWebClient client = new TimeoutWebClient(60000))
                {
                    client.Headers.Add("User-Agent", UserAgent);

                    foreach (VencordReleaseAsset asset in assets)
                    {
                        string finalPath = Path.Combine(distDir, asset.Name);
                        string stagingPath = finalPath + ".vcnew";

                        logger("    下載 " + asset.Name + " ...");

                        try
                        {
                            if (File.Exists(stagingPath))
                            {
                                File.Delete(stagingPath);
                            }
                        }
                        catch
                        {
                        }

                        client.DownloadFile(asset.DownloadUrl, stagingPath);

                        if (asset.Size > 0)
                        {
                            long actualSize = new FileInfo(stagingPath).Length;
                            if (actualSize != asset.Size)
                            {
                                throw new IOException(asset.Name + " 下載不完整 (" + actualSize + "/" + asset.Size + " bytes)");
                            }
                        }

                        staged.Add(new KeyValuePair<string, string>(stagingPath, finalPath));
                    }
                }

                foreach (KeyValuePair<string, string> pair in staged)
                {
                    File.Copy(pair.Key, pair.Value, true);
                }
            }
            catch (Exception ex)
            {
                logger("[-] 同步 Vencord 檔案失敗: " + ex.Message);
                CleanupStaging(staged);
                return false;
            }

            CleanupStaging(staged);

            // 官方 Installer 會建立空的 package.json，避免 node 往上層目錄尋找 package.json。
            try
            {
                string pkgPath = Path.Combine(distDir, "package.json");
                if (!File.Exists(pkgPath))
                {
                    File.WriteAllText(pkgPath, "{}", new UTF8Encoding(false));
                }
            }
            catch
            {
            }

            logger("[+] 已更新 " + staged.Count + " 個 Vencord 檔案至版本 " + release.BuildHash + "。");
            return true;
        }

        private static void CleanupStaging(List<KeyValuePair<string, string>> staged)
        {
            foreach (KeyValuePair<string, string> pair in staged)
            {
                try
                {
                    if (File.Exists(pair.Key))
                    {
                        File.Delete(pair.Key);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
