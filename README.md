# Vencord Auto Patcher for Windows (Discord 自動修補與智慧啟動器)

專為 Windows 使用者設計的 Discord Vencord 自動修補與更新啟動方案。解決每次 Discord 自動更新後 Vencord 失效、需手動重新下載與安裝修補程式的困擾。

---

## 🌟 核心功能特色

1. **⚡ 自動檢測 Discord 更新狀態**
   - 快速檢查 Discord 最新版本目錄（`app-*`）的 Vencord 修補狀態。
   - 若已修補，**秒速直接啟動 Discord（耗時 < 10ms）**，完全不產生額外的網路請求與等待。
2. **📦 官方來源自動下載與修補**
   - 若 Discord 剛更新或尚未修補，自動從 [Vencord 官方發行版](https://github.com/Vencord/Installer) 下載最新的 `VencordInstallerCli.exe` 執行自動修補。
3. **🧹 暫存檔自動無痕清理**
   - 修補完成後立即刪除下載的暫存安裝檔，不佔用磁碟空間。
4. **🚀 支援多種 Discord 分支**
   - 自動識別並支援：`Discord (Stable)`、`Discord PTB`、`Discord Canary`、`Discord Development`。
5. **🎯 多重使用模式**
   - **智慧啟動器模式 (Smart Launcher)**：直接取代 Discord 捷徑，每次啟動 Discord 時自動檢查修補。
   - **後台即時監控模式 (Watcher / Daemon)**：在背景監聽 Discord 目錄，Discord 自動更新下載新版時即時修補。
   - **系統托盤常駐模式 (Tray Icon)**：常駐在右下角工作列，提供快速修補、啟動與狀態提示。
6. **💎 雙軌格式與零外部依賴**
   - 提供編譯完成的獨立執行檔 **`bin\VencordAutoPatcher.exe`**（僅 ~300KB，內建 Discord 圖示，無需安裝任何 Runtime / SDK）。
   - 提供開源 **`VencordAutoPatcher.ps1`** PowerShell 腳本。

---

## 📁 專案目錄結構

```text
vencord-autopatcher/
│
├── bin/
│   └── VencordAutoPatcher.exe      # 原生獨立執行檔 (免安裝、極速啟動)
│
├── src/                            # C# 原生程式碼 (使用 Windows 內建 csc 編譯)
│   ├── Program.cs                  # 程式進入點、參數解析與托盤介面
│   ├── DiscordApp.cs               # Discord 安裝偵測、版本排序與啟動
│   ├── VencordInstaller.cs         # 下載、修補、清理暫存邏輯
│   ├── WatcherService.cs           # FileSystemWatcher 即時背景更新監聽
│   └── ShortcutHelper.cs           # 桌面捷徑與開機啟動管理
│
├── scripts/                        # 輔助一鍵設定腳本
│   ├── Install-Shortcut.bat        # 一鍵在桌面建立 Discord (Vencord) 捷徑
│   ├── Install-Startup-Watcher.bat # 一鍵將背景監控加入開機自動啟動
│   └── Uninstall.bat               # 移除捷徑與開機設定
│
├── VencordAutoPatcher.ps1          # 核心 PowerShell 自動化腳本
├── Run-Patcher.bat                 # 一鍵雙擊執行修補與啟動
├── build.bat / build.ps1           # 一鍵編譯 C# 為 exe
└── README.md                       # 使用說明手冊
```

---

## 🚀 快速開始指南 (推薦使用方式)

### 方式一：取代桌面 Discord 捷徑（最推薦、最簡單）

1. 雙擊執行 `scripts\Install-Shortcut.bat`。
2. 桌面將會生成一個 **`Discord (Vencord Auto-Patch)`** 捷徑。
3. 未來**直接點擊此捷徑開啟 Discord**：
   - 平時：直接秒速開啟 Discord。
   - Discord 更新後：自動在背景下載最新修補程式修補並開啟 Discord，您完全不需要做任何手動操作！

---

### 方式二：設定開機後台自動監控（完全無感）

如果您希望 Discord 在背景默默更新時就被自動修補：
1. 雙擊執行 `scripts\Install-Startup-Watcher.bat`。
2. 程式會在 Windows 開機時於背景靜默監控 Discord 目錄，一旦 Discord 下載新版本，程式會即時在背景完成 Vencord 修補。

> 若日後想取消開機啟動，只需執行 `scripts\Uninstall.bat`。

---

### 方式三：直接手動執行

- **雙擊執行**：直接點擊 `Run-Patcher.bat` 或 `bin\VencordAutoPatcher.exe`。
- **命令列執行**：
  ```cmd
  # 一般啟動 (自動檢查修補並啟動 Discord)
  bin\VencordAutoPatcher.exe

  # 強制重新修補 Vencord (即使目前已修補)
  bin\VencordAutoPatcher.exe --force

  # 啟動系統托盤守護模式
  bin\VencordAutoPatcher.exe --tray

  # 修補並一併安裝 OpenAsar
  bin\VencordAutoPatcher.exe --openasar
  ```

---

## ⚙️ 完整命令列參數

| 參數 | 簡寫 | 說明 |
| :--- | :--- | :--- |
| `-b, --branch <branch>` | `-b` | 指定 Discord 分支（`auto`、`stable`、`ptb`、`canary`、`dev`），預設為 `auto` |
| `-f, --force` | `-f` | 強制重新自 GitHub 下載並修補 Vencord |
| `--no-launch` | | 僅執行檢查與修補，完成後不自動啟動 Discord |
| `--openasar` | | 修補時一併安裝 OpenAsar |
| `-w, --watch` | `-w` | 啟動 Console 背景監控模式 (按 Ctrl+C 結束) |
| `--tray` | | 啟動 Windows 系統托盤背景守護模式 |
| `--install-shortcut` | | 在桌面建立快捷方式 |
| `--install-startup` | | 將背景監控寫入 Windows 開機自動啟動 (HKCU Run) |
| `--uninstall-startup` | | 移除開機自動啟動項目 |
| `-s, --silent` | `-s` | 靜默模式（隱藏終端機輸出） |
| `-h, --help` | `-h` | 顯示參數說明畫面 |

---

## 🛠️ 自行編譯 (Build from Source)

本專案使用 Windows 系統內建的 Microsoft .NET Framework C# 編譯器（`csc.exe`），您**無需安裝任何 Visual Studio 或 .NET SDK** 即可編譯：

雙擊執行 `build.bat` 或在 PowerShell 執行：
```powershell
.\build.ps1
```
編譯後的可執行檔將產生於 `bin\VencordAutoPatcher.exe`。
