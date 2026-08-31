<div align="center">

# VencordFix (Windows)

**專為 Windows 使用者設計的 Discord Vencord 自動修補與智慧啟動器**  
*Discord 更新後自動無痕修補並啟動，無需手動重新安裝。*

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue?logo=windows)](https://github.com/eclipse999/VencordFix)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../LICENSE)
[![Discord Branch](https://img.shields.io/badge/Discord-Stable%20%7C%20PTB%20%7C%20Canary%20%7C%20Dev-5865F2?logo=discord&logoColor=white)](https://discord.com)
[![Vencord](https://img.shields.io/badge/Mod-Vencord-informational)](https://vencord.dev)

[English](../README.md) | **繁體中文**

</div>

---

## 目錄 (Table of Contents)

- [背景與解決方案](#背景與解決方案)
- [核心功能特色](#核心功能特色)
- [運作流程圖](#運作流程圖)
- [專案結構](#專案結構)
- [快速開始指南](#快速開始指南)
- [完整命令列參數](#完整命令列參數)
- [防毒軟體安全與誤報說明](#防毒軟體安全與誤報說明)
- [自行編譯 (Build from Source)](#自行編譯-build-from-source)
- [免責聲明](#免責聲明)
- [開源授權](#開源授權)

---

## 背景與解決方案

每當 Discord 自動在後台發布更新時，原本被 Vencord 修補的 `app.asar` 會被 Discord 官方乾淨版本覆蓋，導致 Vencord 插件失效，使用者往往必須手動下載安裝程式並重新點擊安裝。

**VencordFix** 自動化了整個維護流程：
- **平日啟動**：毫秒級直接啟動 Discord（耗時 < 10ms），無網路延遲。
- **更新後啟動**：自動偵測到未修補狀態，自 GitHub 官方下載最新 `VencordInstallerCli.exe` 完成修補，刪除暫存檔並開啟 Discord。
- **背景守護**：亦可作為開機背景守護程式，在 Discord 更新檔案寫入硬碟時即時自動修補。

---

## 核心功能特色

- **快速驗證**：直接檢查 Discord 版本目錄與 `resources\_app.asar` 結構。
- **官方來源**：自 [Vencord 官方發行庫](https://github.com/Vencord/Installer) 自動下載最新修補檔。
- **無痕清理**：透過 `finally` 機制確保修補完成後立即刪除下載的安裝檔。
- **多版本支援**：支援 Discord (Stable)、Discord PTB、Discord Canary 與 Discord Development。
- **防毒友善設計**：使用專屬隔離目錄 `%LocalAppData%\VencordFix\temp\`，避免觸發 Dropper 誤判。
- **零外部依賴**：內建提供已編譯完成的獨立執行檔 `VencordFix.exe`（約 300KB）與開源 PowerShell 腳本。

---

## 運作流程圖

```mermaid
flowchart TD
    A["啟動 Discord 捷徑<br/>(執行 VencordFix)"] --> B{"檢查 Discord<br/>修補狀態"}
    B -->|已修補| C["秒速啟動 Discord<br/>(耗時 &lt; 10ms)"]
    B -->|未修補 / 剛更新| D["關閉運作中的<br/>Discord 程序"]
    D --> E["下載官方最新<br/>Vencord 安裝檔"]
    E --> F["執行自動修補<br/>(-install)"]
    F --> G["刪除暫存安裝檔<br/>(100% 無痕清理)"]
    G --> H["啟動 Discord<br/>(成功載入 Vencord)"]
```

---

## 專案結構

```text
VencordFix/
│
├── bin/                                # 獨立編譯執行檔 (VencordFix.exe)
├── docs/                               # 多語系文檔庫
│   └── README.zh-TW.md                 # 繁體中文說明手冊
├── scripts/                            # 輔助工具
│   ├── Install-Shortcut.bat            # 建立桌面捷徑
│   ├── Install-Startup-Watcher.bat     # 設定開機背景監控
│   ├── Uninstall-Startup-Watcher.bat   # 移除開機背景監控
│   └── Test-Cleanup-Verification.ps1   # 暫存檔清理驗證腳本
├── src/                                # C# 原生原始碼
│   ├── Program.cs                      # 程式進入點、參數解析與托盤介面
│   ├── DiscordApp.cs                   # Discord 安裝偵測與啟動
│   ├── VencordInstaller.cs             # 下載、修補與清理邏輯
│   ├── WatcherService.cs               # FileSystemWatcher 即時監控
│   ├── ShortcutHelper.cs               # 桌面捷徑與開機啟動管理
│   └── AssemblyInfo.cs                 # 組件中繼資料
│
├── .gitignore
├── build.bat / build.ps1               # 一鍵編譯工具
├── LICENSE                             # MIT 授權條款
├── README.md                           # 英文主說明手冊 (預設首頁)
├── Run.bat                             # 雙擊一鍵修補並啟動
└── VencordFix.ps1                      # 核心 PowerShell 腳本
```

---

## 快速開始指南

### 方式一：取代桌面 Discord 捷徑（推薦）

1. 雙擊執行 [`scripts\Install-Shortcut.bat`](../scripts/Install-Shortcut.bat)。
2. 桌面上會產生一個 **`Discord (VencordFix)`** 捷徑。
3. 日常直接使用此捷徑開啟 Discord：
   - 平日：直接秒速開啟 Discord。
   - Discord 更新後：點擊會自動在背景修補並開啟 Discord，無需手動重新安裝。

---

### 方式二：設定開機後台自動監控

如果希望 Discord 在背景默默更新時就被自動修補：
1. 雙擊執行 [`scripts\Install-Startup-Watcher.bat`](../scripts/Install-Startup-Watcher.bat)。
2. 程式會在 Windows 開機時於背景靜默監控 Discord 目錄，一旦 Discord 下載新版本，會即時自動完成 Vencord 修補。

> 若日後想取消開機啟動，只需執行 [`scripts\Uninstall-Startup-Watcher.bat`](../scripts/Uninstall-Startup-Watcher.bat)。

---

### 方式三：手動與命令列執行

- **直接執行**：雙擊 `Run.bat` 或 `bin\VencordFix.exe`。
- **命令列範例**：
  ```cmd
  # 一般啟動 (自動檢查修補並啟動 Discord)
  bin\VencordFix.exe

  # 強制重新修補 Vencord (即使目前已修補)
  bin\VencordFix.exe --force

  # 啟動系統托盤守護模式
  bin\VencordFix.exe --tray

  # 修補並一併安裝 OpenAsar
  bin\VencordFix.exe --openasar
  ```

---

## 完整命令列參數

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

## 防毒軟體安全與誤報說明

由於本工具涉及「從網路下載安裝檔」與「修補第三方軟體檔案 (`app.asar`)」之行為，部分防毒軟體（如 Kaspersky、Windows Defender）可能會觸發啟發式分析（Heuristic）警報。

本專案完全開源透明，並已採取以下安全防護：
1. **專屬目錄隔離**：下載檔案存放於 `%LocalAppData%\VencordFix\temp\`，絕不污染系統 `%TEMP%`。
2. **完整中繼資料**：包含完整的組件名稱、版本號與簽名中繼資訊。
3. **無加殼純淨編譯**：使用 Windows 原生 `csc.exe` 編譯，無任何混淆加殼。

> 若防毒軟體跳出提示，建議將專案目錄加入防毒軟體排除名單，或直接使用純文字開源的 [`VencordFix.ps1`](../VencordFix.ps1) 運行。

---

## 自行編譯 (Build from Source)

本專案使用 Windows 系統內建的 Microsoft .NET Framework C# 編譯器（`csc.exe`），無需安裝任何 Visual Studio 或 .NET SDK 即可編譯：

雙擊執行 `build.bat` 或在 PowerShell 執行：
```powershell
.\build.ps1
```
編譯後的可執行檔將產生於 `bin\VencordFix.exe`。

---

## 免責聲明

- 本工具非 Discord 或 Vencord 官方出品，僅為社群開發之自動化輔助工具。
- 修改 Discord 客戶端可能違反 Discord 服務條款 (ToS)，使用者須自行評估並承擔相關風險。

---

## 開源授權

本專案採用 [MIT License](../LICENSE) 開源授權。
