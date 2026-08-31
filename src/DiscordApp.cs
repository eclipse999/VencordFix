using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VencordAutoPatcher
{
    public class DiscordApp
    {
        public string BranchName { get; set; }
        public string Title { get; set; }
        public string FolderName { get; set; }
        public string ExeName { get; set; }
        public string ProcessName { get; set; }
        public string RootPath { get; set; }
        public string LatestAppDir { get; set; }
        public Version AppVersion { get; set; }

        public string UpdateExePath
        {
            get { return Path.Combine(RootPath, "Update.exe"); }
        }

        public string ResourcesDir
        {
            get { return string.IsNullOrEmpty(LatestAppDir) ? null : Path.Combine(LatestAppDir, "resources"); }
        }

        public bool IsPatched()
        {
            if (string.IsNullOrEmpty(ResourcesDir) || !Directory.Exists(ResourcesDir))
                return false;

            string appAsar = Path.Combine(ResourcesDir, "app.asar");
            string origAppAsar = Path.Combine(ResourcesDir, "_app.asar");

            // 當 Discord 被 Vencord 修補時：
            // 1. 原始 Discord asar 被重新命名為 _app.asar
            // 2. 新的 app.asar 是小型的 shim 載入器 (大小通常 < 200KB)
            if (File.Exists(origAppAsar) && File.Exists(appAsar))
            {
                try
                {
                    FileInfo fi = new FileInfo(appAsar);
                    if (fi.Length < 200000)
                    {
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public void KillProcesses()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(ProcessName);
                if (processes.Length > 0)
                {
                    Console.WriteLine("[*] 正在關閉運作中的 " + ProcessName + " 程序...");
                    foreach (var p in processes)
                    {
                        try
                        {
                            p.CloseMainWindow();
                            if (!p.WaitForExit(1000))
                            {
                                p.Kill();
                            }
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(500);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] 關閉程序時警告: " + ex.Message);
            }
        }

        public bool Launch()
        {
            try
            {
                if (File.Exists(UpdateExePath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = UpdateExePath;
                    psi.Arguments = "--processStart " + ExeName;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    return true;
                }
                else if (!string.IsNullOrEmpty(LatestAppDir))
                {
                    string directExe = Path.Combine(LatestAppDir, ExeName);
                    if (File.Exists(directExe))
                    {
                        Process.Start(directExe);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] 啟動 Discord 失敗: " + ex.Message);
            }
            return false;
        }

        public static List<DiscordApp> DetectInstalledDiscords()
        {
            var results = new List<DiscordApp>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var branchDefs = new[]
            {
                new { Branch = "stable", Folder = "Discord", Exe = "Discord.exe", Process = "Discord", Title = "Discord (Stable)" },
                new { Branch = "ptb", Folder = "DiscordPTB", Exe = "DiscordPTB.exe", Process = "DiscordPTB", Title = "Discord PTB" },
                new { Branch = "canary", Folder = "DiscordCanary", Exe = "DiscordCanary.exe", Process = "DiscordCanary", Title = "Discord Canary" },
                new { Branch = "dev", Folder = "DiscordDevelopment", Exe = "DiscordDevelopment.exe", Process = "DiscordDevelopment", Title = "Discord Development" }
            };

            foreach (var def in branchDefs)
            {
                string branchPath = Path.Combine(localAppData, def.Folder);
                if (Directory.Exists(branchPath))
                {
                    string[] appDirs = Directory.GetDirectories(branchPath, "app-*");
                    if (appDirs.Length > 0)
                    {
                        string latestDir = null;
                        Version highestVer = new Version(0, 0, 0, 0);

                        foreach (string d in appDirs)
                        {
                            string dirName = Path.GetFileName(d);
                            string verStr = dirName.Replace("app-", "");
                            Version v;
                            if (Version.TryParse(verStr, out v))
                            {
                                if (v > highestVer)
                                {
                                    highestVer = v;
                                    latestDir = d;
                                }
                            }
                            else if (latestDir == null)
                            {
                                latestDir = d;
                            }
                        }

                        if (latestDir != null)
                        {
                            results.Add(new DiscordApp
                            {
                                BranchName = def.Branch,
                                Title = def.Title,
                                FolderName = def.Folder,
                                ExeName = def.Exe,
                                ProcessName = def.Process,
                                RootPath = branchPath,
                                LatestAppDir = latestDir,
                                AppVersion = highestVer
                            });
                        }
                    }
                }
            }

            return results;
        }
    }
}
