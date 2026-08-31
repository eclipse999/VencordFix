using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace VencordFix
{
    public class WatcherService : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly object _lockObj = new object();
        private Timer _debounceTimer;
        private readonly Action<string> _logger;

        public WatcherService(Action<string> logger = null)
        {
            _logger = logger ?? ((msg) => Console.WriteLine(msg));
        }

        public void Start()
        {
            var discords = DiscordApp.DetectInstalledDiscords();
            if (discords.Count == 0)
            {
                _logger("[-] 未偵測到本機已安裝的 Discord，無法啟動背景監控。");
                return;
            }

            foreach (var d in discords)
            {
                _logger("[*] 監控 Discord 目錄: " + d.Title + " -> " + d.RootPath);
                try
                {
                    var fsw = new FileSystemWatcher(d.RootPath);
                    fsw.IncludeSubdirectories = true;
                    fsw.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite;
                    fsw.Created += OnFileSystemEvent;
                    fsw.Changed += OnFileSystemEvent;
                    fsw.EnableRaisingEvents = true;

                    _watchers.Add(fsw);
                }
                catch (Exception ex)
                {
                    _logger("[-] 建立監控失敗: " + ex.Message);
                }
            }

            _logger("[+] 背景監控已就緒。正在等待 Discord 更新事件...");
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            string name = e.Name ?? "";
            string fullPath = e.FullPath ?? "";

            bool isRelevant = name.StartsWith("app-", StringComparison.OrdinalIgnoreCase) ||
                              fullPath.IndexOf("resources", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isRelevant)
            {
                lock (_lockObj)
                {
                    if (_debounceTimer != null)
                    {
                        _debounceTimer.Dispose();
                    }

                    _debounceTimer = new Timer(CheckAndPatchTriggered, null, 3000, Timeout.Infinite);
                }
            }
        }

        private void CheckAndPatchTriggered(object state)
        {
            try
            {
                _logger("\n[!] 偵測到 Discord 目錄異動，正在檢查修補狀態...");
                var discords = DiscordApp.DetectInstalledDiscords();

                foreach (var d in discords)
                {
                    if (!d.IsPatched())
                    {
                        _logger("[*] 發現 " + d.Title + " (版本 " + d.AppVersion + ") 尚未修補，開始自動修補...");
                        VencordInstaller.DownloadAndPatch(d.BranchName, false, _logger);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger("[-] 背景修補處理錯誤: " + ex.Message);
            }
        }

        public void Dispose()
        {
            lock (_lockObj)
            {
                if (_debounceTimer != null)
                {
                    _debounceTimer.Dispose();
                    _debounceTimer = null;
                }
            }

            foreach (var w in _watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            _watchers.Clear();
        }
    }
}
