using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace VencordAutoPatcher
{
    public class VencordInstaller
    {
        public const string DefaultInstallerUrl = "https://github.com/Vencord/Installer/releases/latest/download/VencordInstallerCli.exe";

        static VencordInstaller()
        {
            // 啟用 TLS 1.2 與 TLS 1.3 支援
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

            // 使用專屬應用目錄而非根目錄 %TEMP%，大幅降低防毒軟體的 Dropper/Downloader 誤判
            string workingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VencordAutoPatcher", "temp");
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
                    client.Headers.Add("User-Agent", "VencordAutoPatcher/1.0 (Windows NT 10.0; Win64; x64)");
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
                    // 立即關閉標準輸入以避免等待鍵盤輸入
                    proc.StandardInput.Close();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    bool exited = proc.WaitForExit(60000); // 逾時上限 60 秒
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
                // 確保清理下載的安裝檔
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        log("[*] 已清理暫存安裝檔。");
                    }
                    catch (Exception ex)
                    {
                        log("[-] 清理暫存檔失敗: " + ex.Message);
                    }
                }
            }
        }
    }
}
