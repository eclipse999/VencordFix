using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace VencordFix
{
    public class VencordInstaller
    {
        public const string DefaultInstallerUrl = "https://github.com/Vencord/Installer/releases/latest/download/VencordInstallerCli.exe";

        static VencordInstaller()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls12;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
        }

        public static bool DownloadAndPatch(string branch = "auto", bool includeOpenAsar = false, Action<string> logCallback = null)
        {
            Action<string> log = logCallback ?? ((msg) => Console.WriteLine(msg));

            string workingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VencordFix", "temp");
            try
            {
                if (!Directory.Exists(workingDir))
                {
                    Directory.CreateDirectory(workingDir);
                }
            }
            catch { }

            string tempPath = Path.Combine(workingDir, "VencordInstallerCli_" + Guid.NewGuid().ToString("N") + ".exe");
            log("[*] 正在從官方發行版下載最新 Vencord 安裝檔...");
            log("    下載來源: " + DefaultInstallerUrl);

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "VencordFix/1.0 (Windows NT 10.0; Win64; x64)");
                    client.DownloadFile(DefaultInstallerUrl, tempPath);
                }

                log("[+] Vencord 安裝檔下載完成！");

                string args = "-install -branch " + branch;
                if (includeOpenAsar)
                {
                    args += " -install-openasar";
                }

                log("[*] 執行修補命令: VencordInstallerCli " + args);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = tempPath;
                psi.Arguments = args;
                psi.WorkingDirectory = workingDir;
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.CreateNoWindow = true;

                StringBuilder outputBuilder = new StringBuilder();
                StringBuilder errorBuilder = new StringBuilder();

                using (Process proc = new Process())
                {
                    proc.StartInfo = psi;
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            log("    " + e.Data);
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                            log("    " + e.Data);
                        }
                    };

                    proc.Start();
                    proc.StandardInput.Close();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    bool exited = proc.WaitForExit(60000);
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { }
                        log("[-] 修補程序執行逾時。");
                        return false;
                    }

                    if (proc.ExitCode == 0)
                    {
                        log("[+] Discord 已成功完成 Vencord 修補！");
                        return true;
                    }
                    else
                    {
                        log("[-] 修補失敗，結束代碼: " + proc.ExitCode);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                log("[-] 下載或修補過程中發生錯誤: " + ex.Message);
                return false;
            }
            finally
            {
                DeleteFileWithRetry(tempPath, log);
                PurgeStaleDownloads(workingDir, log);
            }
        }

        /// <summary>
        /// 刪除暫存安裝檔。安裝程式剛結束時檔案可能仍被系統鎖定，因此加入重試機制。
        /// </summary>
        private static void DeleteFileWithRetry(string path, Action<string> log)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Delete(path);
                    log("[*] 已清理暫存安裝檔。");
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == 4)
                    {
                        log("[-] 清理暫存檔失敗 (可稍後手動刪除): " + ex.Message);
                        return;
                    }

                    System.Threading.Thread.Sleep(400);
                }
            }
        }

        /// <summary>清除先前執行遺留在 temp 目錄的安裝檔。</summary>
        private static void PurgeStaleDownloads(string workingDir, Action<string> log)
        {
            try
            {
                if (!Directory.Exists(workingDir))
                {
                    return;
                }

                foreach (string stale in Directory.GetFiles(workingDir, "VencordInstallerCli_*.exe"))
                {
                    try
                    {
                        FileInfo info = new FileInfo(stale);

                        // 保留剛建立的檔案，避免誤刪其他正在執行的實例。
                        if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalMinutes < 10)
                        {
                            continue;
                        }

                        File.Delete(stale);
                        log("[*] 已清除殘留的暫存安裝檔: " + info.Name);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
