using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace VencordFix
{
    /// <summary>
    /// 使用者設定資料模型（序列化為 %LocalAppData%\VencordFix\config.json）。
    /// </summary>
    [DataContract]
    public class FixConfigData
    {
        [DataMember(Name = "syncBuilds")]
        public bool SyncBuilds = true;

        [DataMember(Name = "checkIntervalMinutes")]
        public int CheckIntervalMinutes = 0;
    }

    /// <summary>
    /// VencordFix 的設定與路徑管理。
    /// </summary>
    public static class FixConfig
    {
        public const string AppFolderName = "VencordFix";

        /// <summary>是否在啟動 Discord 前先同步官方最新 Vencord 執行檔（預設開啟）。</summary>
        public static bool SyncBuilds = true;

        /// <summary>檢查官方最新版本的快取時間（分鐘）。0 表示每次啟動都重新檢查。</summary>
        public static int CheckIntervalMinutes = 0;

        public static string AppDataDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppFolderName);
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(AppDataDir, "config.json"); }
        }

        public static string CacheDir
        {
            get { return Path.Combine(AppDataDir, "cache"); }
        }

        public static string TempDir
        {
            get { return Path.Combine(AppDataDir, "temp"); }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return;
                }

                string json = File.ReadAllText(ConfigPath, System.Text.Encoding.UTF8);
                var serializer = new DataContractJsonSerializer(typeof(FixConfigData));
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    FixConfigData data = serializer.ReadObject(stream) as FixConfigData;
                    if (data != null)
                    {
                        SyncBuilds = data.SyncBuilds;
                        CheckIntervalMinutes = data.CheckIntervalMinutes < 0 ? 0 : data.CheckIntervalMinutes;
                    }
                }
            }
            catch
            {
                // 設定檔毀損時沿用預設值，不影響啟動流程。
            }
        }

        public static bool Save()
        {
            try
            {
                if (!Directory.Exists(AppDataDir))
                {
                    Directory.CreateDirectory(AppDataDir);
                }

                FixConfigData data = new FixConfigData();
                data.SyncBuilds = SyncBuilds;
                data.CheckIntervalMinutes = CheckIntervalMinutes;

                var serializer = new DataContractJsonSerializer(typeof(FixConfigData));
                using (var stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, data);
                    File.WriteAllBytes(ConfigPath, stream.ToArray());
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
