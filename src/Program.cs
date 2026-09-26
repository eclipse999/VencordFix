using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VencordFix
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        private const int STD_OUTPUT_HANDLE = -11;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const int ATTACH_PARENT_PROCESS = -1;
        private static bool _consoleAttached = false;

        private static void EnsureConsole(bool allowAlloc = false)
        {
            if (_consoleAttached) return;

            IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            bool isRedirected = (stdOutHandle != IntPtr.Zero && stdOutHandle != INVALID_HANDLE_VALUE);

            if (!isRedirected)
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    _consoleAttached = true;
                }
                else if (allowAlloc)
                {
                    _consoleAttached = AllocConsole();
                }
            }
            else
            {
                _consoleAttached = true;
            }

            if (_consoleAttached)
            {
                try
                {
                    var stdOut = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
                    var stdErr = new StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true };
                    Console.SetOut(stdOut);
                    Console.SetError(stdErr);
                    Console.OutputEncoding = Encoding.UTF8;
                }
                catch { }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            FixConfig.Load();

            // 若沒有傳入任何參數，預設開啟極簡 GUI 設定視窗 (完全不掛載或建立終端機)
            if (args == null || args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return;
            }

            bool force = false;
            bool noLaunch = false;
            bool openAsar = false;
            bool watch = false;
            bool tray = false;
            bool installShortcut = false;
            bool installStartup = false;
            bool uninstallStartup = false;
            bool silent = false;
            bool gui = false;
            bool showHelp = false;
            bool status = false;
            bool? syncBuildsFlag = null;
            int? checkIntervalFlag = null;
            string branch = "auto";

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "-h" || arg == "--help" || arg == "/?")
                {
                    showHelp = true;
                }
                else if (arg == "--gui")
                {
                    gui = true;
                }
                else if (arg == "-f" || arg == "--force")
                {
                    force = true;
                }
                else if (arg == "--no-launch")
                {
                    noLaunch = true;
                }
                else if (arg == "--launch")
                {
                    silent = true;
                }
                else if (arg == "--openasar")
                {
                    openAsar = true;
                }
                else if (arg == "-w" || arg == "--watch")
                {
                    watch = true;
                }
                else if (arg == "--tray")
                {
                    tray = true;
                }
                else if (arg == "--install-shortcut")
                {
                    installShortcut = true;
                }
                else if (arg == "--install-startup")
                {
                    installStartup = true;
                }
                else if (arg == "--uninstall-startup")
                {
                    uninstallStartup = true;
                }
                else if (arg == "--silent" || arg == "-s")
                {
                    silent = true;
                }
                else if (arg == "--status")
                {
                    status = true;
                }
                else if (arg == "--sync-builds")
                {
                    syncBuildsFlag = true;
                }
                else if (arg == "--no-sync-builds")
                {
                    syncBuildsFlag = false;
                }
                else if (arg == "--check-interval" && i + 1 < args.Length)
                {
                    int parsedInterval;
                    if (int.TryParse(args[++i], out parsedInterval) && parsedInterval >= 0)
                    {
                        checkIntervalFlag = parsedInterval;
                    }
                }
                else if ((arg == "-b" || arg == "--branch") && i + 1 < args.Length)
                {
                    branch = args[++i].ToLowerInvariant();
                }
            }

            if (syncBuildsFlag.HasValue)
            {
                FixConfig.SyncBuilds = syncBuildsFlag.Value;
            }

            if (checkIntervalFlag.HasValue)
            {
                FixConfig.CheckIntervalMinutes = checkIntervalFlag.Value;
            }

            if (gui)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return;
            }

            if (tray)
            {
                RunTrayApp();
                return;
            }

            if (status)
            {
                EnsureConsole(false);
                ShowStatus();
                return;
            }

            if (showHelp)
            {
                EnsureConsole(false);
                ShowHelp();
                return;
            }

            if (!silent)
            {
                EnsureConsole(watch);
            }

            if (installShortcut)
            {
                ShortcutHelper.CreateDesktopShortcut();
                return;
            }

            if (installStartup)
            {
                ShortcutHelper.SetStartup(true, true);
                return;
            }

            if (uninstallStartup)
            {
                ShortcutHelper.SetStartup(false);
                return;
            }

            if (watch)
            {
                RunWatcherConsole();
                return;
            }

            RunLauncher(branch, force, noLaunch, openAsar, silent, FixConfig.SyncBuilds, FixConfig.CheckIntervalMinutes);
        }

        static void ShowHelp()
        {
            bool isZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            if (isZh)
            {
                Console.WriteLine("==========================================================");
                Console.WriteLine("  VencordFix for Windows");
                Console.WriteLine("  Discord 自動修補與智慧啟動器");
                Console.WriteLine("==========================================================");
                Console.WriteLine("用法:");
                Console.WriteLine("  VencordFix.exe [選項]");
                Console.WriteLine("");
                Console.WriteLine("選項:");
                Console.WriteLine("      --gui                 開啟極簡圖形設定介面 (無參數雙擊時亦會開啟)");
                Console.WriteLine("      --launch              捷徑模式：自動檢查並秒速啟動 Discord");
                Console.WriteLine("  -b, --branch <branch>     指定 Discord 分支 (auto, stable, ptb, canary, dev)。預設: auto");
                Console.WriteLine("  -f, --force               強制重新下載並修補 Vencord (即使目前已被修補)");
                Console.WriteLine("      --no-launch           修補後不自動啟動 Discord");
                Console.WriteLine("      --sync-builds         啟動前自動同步官方最新 Vencord 執行檔 (預設即開啟)");
                Console.WriteLine("      --no-sync-builds      關閉啟動前自動同步 (改由 Vencord 自行更新)");
                Console.WriteLine("      --check-interval <分鐘> 檢查官方版本的快取時間，0 表示每次都檢查 (預設 0)");
                Console.WriteLine("      --status              顯示修補狀態與 Vencord 版本診斷後結束");
                Console.WriteLine("      --openasar            一併安裝 OpenAsar");
                Console.WriteLine("  -w, --watch               背景監控模式 (監聽 Discord 自動更新並即時修補)");
                Console.WriteLine("      --tray                以系統托盤常駐模式運行");
                Console.WriteLine("      --install-shortcut    在桌面建立 VencordFix 啟動捷徑");
                Console.WriteLine("      --install-startup     將背景更新監控加入開機自動啟動");
                Console.WriteLine("      --uninstall-startup   移除開機自動啟動");
                Console.WriteLine("  -s, --silent              靜默執行模式");
                Console.WriteLine("  -h, --help                顯示說明畫面");
                Console.WriteLine("==========================================================");
            }
            else
            {
                Console.WriteLine("==========================================================");
                Console.WriteLine("  VencordFix for Windows");
                Console.WriteLine("  Automated Discord Patcher & Smart Launcher");
                Console.WriteLine("==========================================================");
                Console.WriteLine("Usage:");
                Console.WriteLine("  VencordFix.exe [options]");
                Console.WriteLine("");
                Console.WriteLine("Options:");
                Console.WriteLine("      --gui                 Open graphical setup interface (default on double-click)");
                Console.WriteLine("      --launch              Shortcut mode: check & launch Discord silently");
                Console.WriteLine("  -b, --branch <branch>     Specify Discord branch (auto, stable, ptb, canary, dev). Default: auto");
                Console.WriteLine("  -f, --force               Force re-download and re-patch Vencord");
                Console.WriteLine("      --no-launch           Patch only; do not launch Discord");
                Console.WriteLine("      --sync-builds         Sync the latest official Vencord build before launching (default: on)");
                Console.WriteLine("      --no-sync-builds      Disable pre-launch build sync (let Vencord update itself instead)");
                Console.WriteLine("      --check-interval <min>  Cache TTL for the release check, 0 = always check (default 0)");
                Console.WriteLine("      --status              Print patch state and Vencord version diagnostics, then exit");
                Console.WriteLine("      --openasar            Install OpenAsar along with Vencord");
                Console.WriteLine("  -w, --watch               Background watcher mode (monitor Discord updates in real-time)");
                Console.WriteLine("      --tray                Run in system tray background mode");
                Console.WriteLine("      --install-shortcut    Create desktop shortcut");
                Console.WriteLine("      --install-startup     Register background watcher in Windows Startup (HKCU Run)");
                Console.WriteLine("      --uninstall-startup   Remove Windows Startup entry");
                Console.WriteLine("  -s, --silent              Silent mode (suppress console output)");
                Console.WriteLine("  -h, --help                Display help screen");
                Console.WriteLine("==========================================================");
            }
        }

        static void RunLauncher(string branch, bool force, bool noLaunch, bool openAsar, bool silent, bool syncBuilds, int checkIntervalMinutes)
        {
            if (!silent)
            {
                Console.WriteLine("=== VencordFix: Discord 自動修補與啟動器 ===");
            }

            var discords = DiscordApp.DetectInstalledDiscords();
            if (discords.Count == 0)
            {
                if (!silent)
                {
                    Console.WriteLine("[-] 未偵測到本機已安裝的 Discord！請確認 Discord 是否安裝於 %LocalAppData%。");
                }
                return;
            }

            List<DiscordApp> targets = new List<DiscordApp>();
            if (branch == "auto")
            {
                targets.AddRange(discords);
            }
            else
            {
                foreach (var d in discords)
                {
                    if (d.BranchName.Equals(branch, StringComparison.OrdinalIgnoreCase))
                    {
                        targets.Add(d);
                    }
                }
            }

            if (targets.Count == 0)
            {
                if (!silent)
                {
                    Console.WriteLine("[-] 找不到指定的 Discord 分支: " + branch);
                }
                return;
            }

            bool needPatch = false;
            bool patchedByInstaller = false;
            List<DiscordApp> toPatch = new List<DiscordApp>();

            foreach (var d in targets)
            {
                bool isPatched = d.IsPatched();
                if (!silent)
                {
                    Console.WriteLine("[*] 偵測到 " + d.Title + " (版本 " + d.AppVersion + ")");
                    Console.WriteLine("    修補狀態: " + (isPatched ? "[已修補 Vencord]" : "[尚未修補或剛更新]"));
                }

                if (!isPatched)
                {
                    needPatch = true;
                    toPatch.Add(d);
                }
            }

            if (needPatch || force)
            {
                if (force)
                {
                    if (!silent) Console.WriteLine("[!] 強制模式：將對目標 Discord 重新執行 Vencord 修補...");
                    toPatch = targets;
                }
                else
                {
                    if (!silent) Console.WriteLine("[!] 偵測到 Discord 尚未修補 (可能剛更新)，開始修補...");
                }

                foreach (var d in toPatch)
                {
                    d.KillProcesses();
                }

                string patchBranch = (branch == "auto") ? "auto" : targets[0].BranchName;
                bool ok = VencordInstaller.DownloadAndPatch(patchBranch, openAsar, (msg) => {
                    if (!silent) Console.WriteLine(msg);
                });

                if (ok)
                {
                    // 官方 Installer 的 -install 已經會依雜湊比對同步最新的 Vencord 執行檔，
                    // 因此本次執行不需要再重複檢查一次。
                    patchedByInstaller = true;
                }
                else if (!silent)
                {
                    Console.WriteLine("[-] 修補失敗，請檢查網路連線。");
                }
            }
            else
            {
                if (!silent)
                {
                    Console.WriteLine("[+] Discord 已是最新修補狀態，秒速直接啟動！");
                }
            }

            // 啟動前先同步官方最新 Vencord 執行檔，避免啟動後才由 Vencord 自身更新而要求「點此重啟」。
            if (syncBuilds && !patchedByInstaller)
            {
                SyncVencordBuilds(targets, checkIntervalMinutes, delegate(string msg)
                {
                    if (!silent) Console.WriteLine(msg);
                });
            }

            if (!noLaunch && targets.Count > 0)
            {
                if (!silent) Console.WriteLine("[*] 啟動 " + targets[0].Title + "...");
                targets[0].Launch();
            }

            if (!silent)
            {
                Console.WriteLine("[+] 完成！");
            }
        }

        static bool IsProcessRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 啟動前同步官方最新 Vencord 執行檔 (%AppData%\Vencord\dist)。
        ///
        /// 若已安裝版本與官方最新版不同，會先關閉執行中的 Discord、更新檔案，
        /// 再由後續流程啟動 Discord；如此一來「點桌面捷徑」就等於更新 + 重啟一次完成，
        /// 不會再出現 Vencord 內建的「VENCORD HAS BEEN UPDATED! Click here to restart」提示。
        /// </summary>
        static bool SyncVencordBuilds(List<DiscordApp> targets, int checkIntervalMinutes, Action<string> log)
        {
            List<string> distDirs = new List<string>();

            foreach (var d in targets)
            {
                string distDir = VencordBuildSync.ResolveDistDir(d);
                if (string.IsNullOrEmpty(distDir))
                {
                    continue;
                }

                bool known = false;
                foreach (var existing in distDirs)
                {
                    if (string.Equals(existing, distDir, StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    distDirs.Add(distDir);
                }
            }

            if (distDirs.Count == 0)
            {
                distDirs.Add(VencordBuildSync.DefaultDistDir());
            }

            bool applied = false;

            foreach (var distDir in distDirs)
            {
                string installedHash = VencordBuildSync.ReadInstalledHash(distDir);
                ReleaseCheckResult check = VencordBuildSync.CheckLatestRelease(false, checkIntervalMinutes, log);

                if (!check.Success)
                {
                    log("[!] 無法確認 Vencord 最新版本 (" + check.Error + ")，略過同步，直接啟動。");
                    continue;
                }

                if (!VencordBuildSync.IsOutdated(installedHash, check.LatestHash))
                {
                    log("[+] Vencord 執行檔已是最新版本 (" + check.LatestHash + ")。");
                    continue;
                }

                log("[!] 偵測到 Vencord 新版本: " + (string.IsNullOrEmpty(installedHash) ? "未知" : installedHash) + " -> " + check.LatestHash);

                // 執行中的 Discord 仍載入舊版程式碼，必須重啟才能套用。
                foreach (var d in targets)
                {
                    if (IsProcessRunning(d.ProcessName))
                    {
                        log("[*] 關閉執行中的 " + d.Title + "，準備套用最新 Vencord...");
                        d.KillProcesses();
                    }
                }

                if (VencordBuildSync.ApplyRelease(check.Release, distDir, log))
                {
                    applied = true;
                    log("[+] 啟動後即為最新版 Vencord，不會再出現「重新啟動」提示。");
                }
                else
                {
                    log("[-] 同步失敗，維持現有版本啟動。");
                }
            }

            return applied;
        }

        static void ShowStatus()
        {
            Console.WriteLine("=== VencordFix: 狀態診斷 (--status) ===");
            Console.WriteLine("");

            var discords = DiscordApp.DetectInstalledDiscords();
            if (discords.Count == 0)
            {
                Console.WriteLine("[-] 未偵測到本機已安裝的 Discord (%LocalAppData%\\Discord*)。");
            }

            foreach (var d in discords)
            {
                Console.WriteLine("[*] " + d.Title + " (app-" + d.AppVersion + ")");
                Console.WriteLine("    資源目錄: " + d.ResourcesDir);

                string appAsar = Path.Combine(d.ResourcesDir, "app.asar");
                string origAsar = Path.Combine(d.ResourcesDir, "_app.asar");

                long appSize = 0;
                try
                {
                    if (File.Exists(appAsar))
                    {
                        appSize = new FileInfo(appAsar).Length;
                    }
                }
                catch
                {
                }

                Console.WriteLine("    app.asar: " + appSize + " bytes / _app.asar: " + (File.Exists(origAsar) ? "存在" : "不存在"));
                Console.WriteLine("    修補狀態: " + (d.IsPatched() ? "[已修補 Vencord]" : "[尚未修補或需要重新修補]"));

                string injectedPath;
                if (DiscordApp.TryGetInjectedPatcherPath(appAsar, out injectedPath))
                {
                    Console.WriteLine("    注入目標: " + injectedPath + (File.Exists(injectedPath) ? " (存在)" : " (遺失!)"));
                }
            }

            Console.WriteLine("");

            string distDir = discords.Count > 0 ? VencordBuildSync.ResolveDistDir(discords[0]) : VencordBuildSync.DefaultDistDir();
            string installedHash = VencordBuildSync.ReadInstalledHash(distDir);

            Console.WriteLine("[*] Vencord 執行檔目錄: " + distDir);
            Console.WriteLine("    已安裝版本: " + (string.IsNullOrEmpty(installedHash) ? "(未知 / 尚未安裝)" : installedHash));

            ReleaseCheckResult check = VencordBuildSync.CheckLatestRelease(true, FixConfig.CheckIntervalMinutes, delegate(string msg)
            {
                Console.WriteLine(msg);
            });

            if (check.Success)
            {
                Console.WriteLine("    官方最新版本: " + check.LatestHash);
                Console.WriteLine("    是否需要同步: " + (VencordBuildSync.IsOutdated(installedHash, check.LatestHash) ? "是 (下次啟動會自動更新)" : "否，已是最新"));
            }
            else
            {
                Console.WriteLine("[-] 無法取得官方最新版本: " + check.Error);
            }

            Console.WriteLine("");
            Console.WriteLine("[*] 啟動前自動同步 Vencord: " + (FixConfig.SyncBuilds ? "已啟用" : "已停用"));
            Console.WriteLine("    版本檢查快取: " + (FixConfig.CheckIntervalMinutes <= 0 ? "每次都重新檢查" : FixConfig.CheckIntervalMinutes + " 分鐘"));
            Console.WriteLine("[*] 設定檔: " + FixConfig.ConfigPath);
            Console.WriteLine("==========================================================");
        }


        static void RunWatcherConsole()
        {
            Console.WriteLine("=== VencordFix: Discord 背景更新監控 (Watcher Mode) ===");
            using (var watcher = new WatcherService((msg) => Console.WriteLine(msg)))
            {
                watcher.Start();
                Console.WriteLine("[*] 按 Ctrl+C 結束監控...");
                ManualResetEvent quitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) => {
                    e.Cancel = true;
                    quitEvent.Set();
                };
                quitEvent.WaitOne();
            }
            Console.WriteLine("[*] 監控已停止。");
        }

        static void RunTrayApp()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            NotifyIcon trayIcon = new NotifyIcon();
            bool isZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            trayIcon.Text = isZh ? "VencordFix 守護中" : "VencordFix Watcher Active";
            
            string selfExe = Process.GetCurrentProcess().MainModule.FileName;
            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(selfExe);
                if (extracted != null)
                {
                    trayIcon.Icon = extracted;
                }
                else
                {
                    trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                trayIcon.Icon = SystemIcons.Application;
            }

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(isZh ? "開啟設定介面" : "Open Settings...", (s, e) => {
                new MainForm().Show();
            });
            contextMenu.MenuItems.Add(isZh ? "修補並啟動 Discord" : "Patch & Launch Discord", (s, e) => {
                RunLauncher("auto", false, false, false, false, FixConfig.SyncBuilds, FixConfig.CheckIntervalMinutes);
            });
            contextMenu.MenuItems.Add(isZh ? "強制重新修補 Vencord" : "Force Re-patch Vencord", (s, e) => {
                RunLauncher("auto", true, false, false, false, FixConfig.SyncBuilds, FixConfig.CheckIntervalMinutes);
            });
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add(isZh ? "建立桌面捷徑" : "Create Desktop Shortcut", (s, e) => {
                ShortcutHelper.CreateDesktopShortcut();
                trayIcon.ShowBalloonTip(3000, "VencordFix", isZh ? "已成功在桌面建立捷徑！" : "Successfully created Desktop shortcut!", ToolTipIcon.Info);
            });
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add(isZh ? "結束 (Exit)" : "Exit", (s, e) => {
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon.ContextMenu = contextMenu;
            trayIcon.Visible = true;

            var watcher = new WatcherService((msg) => {
                if (msg.Contains("已成功完成 Vencord 修補") || msg.IndexOf("Vencord", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    trayIcon.ShowBalloonTip(3000, "VencordFix", isZh ? "Discord 已自動重新修補 Vencord！" : "Discord re-patched with Vencord automatically!", ToolTipIcon.Info);
                }
            });
            watcher.Start();

            trayIcon.ShowBalloonTip(2000, "VencordFix", isZh ? "VencordFix 背景更新監控已在系統托盤中啟動！" : "VencordFix background watcher started in system tray!", ToolTipIcon.Info);

            Application.Run();

            watcher.Dispose();
            trayIcon.Dispose();
        }
    }
}
