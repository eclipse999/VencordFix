using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace VencordAutoPatcher
{
    public static class ShortcutHelper
    {
        public const string StartupKeyName = "VencordAutoPatcherWatcher";

        public static bool CreateDesktopShortcut(string iconPath = null)
        {
            try
            {
                string exePath = ProcessPath();
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutLocation = Path.Combine(desktopPath, "Discord (Vencord Auto-Patch).lnk");

                // 使用 late-binding 呼叫 WScript.Shell 建立捷徑，無需依賴 Interop DLL
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    Console.WriteLine("[-] 無法建立 WScript.Shell 物件。");
                    return false;
                }

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutLocation);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                shortcut.Description = "啟動 Discord 並自動檢測與修補 Vencord";

                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath + ",0";
                }
                else
                {
                    // 尋找已安裝 Discord 的 app.ico
                    var discords = DiscordApp.DetectInstalledDiscords();
                    if (discords.Count > 0)
                    {
                        string discIco = Path.Combine(discords[0].RootPath, "app.ico");
                        if (File.Exists(discIco))
                        {
                            shortcut.IconLocation = discIco + ",0";
                        }
                    }
                }

                shortcut.Save();
                Console.WriteLine("[+] 成功在桌面建立捷徑: " + shortcutLocation);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] 建立捷徑失敗: " + ex.Message);
                return false;
            }
        }

        public static bool SetStartup(bool enable, bool watchMode = true)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null)
                    {
                        Console.WriteLine("[-] 無法開啟註冊表開機啟動項。");
                        return false;
                    }

                    if (enable)
                    {
                        string exePath = ProcessPath();
                        string cmd = "\"" + exePath + "\" " + (watchMode ? "--watch --silent" : "--silent");
                        key.SetValue(StartupKeyName, cmd);
                        Console.WriteLine("[+] 已成功將 Vencord 背景監控加入開機自動啟動 (HKCU Run)");
                    }
                    else
                    {
                        key.DeleteValue(StartupKeyName, false);
                        Console.WriteLine("[+] 已移除 Vencord 開機自動啟動設定");
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] 設定開機啟動失敗: " + ex.Message);
                return false;
            }
        }

        private static string ProcessPath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }
    }
}
