using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace VencordFix
{
    public enum AppLanguage
    {
        ZhTw,
        En
    }

    public enum FeedbackState
    {
        Initial,
        ShortcutSuccess,
        ShortcutFail,
        StartupEnabled,
        StartupDisabled,
        StartupFail
    }

    public class MainForm : Form
    {
        private AppLanguage _currentLang;
        private FeedbackState _feedbackState = FeedbackState.Initial;

        private Label _lblTitle;
        private Label _lblSubtitle;
        private Label _lblStatusInfo;
        private Button _btnLang;
        private Button _btnShortcut;
        private Label _lblShortcutDesc;
        private Button _btnStartup;
        private Label _lblStartupDesc;
        private Label _lblFeedback;

        public MainForm()
        {
            _currentLang = DetectInitialLanguage();
            InitializeComponent();
            ApplyLanguage();
        }

        private static AppLanguage DetectInitialLanguage()
        {
            try
            {
                string name = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                return name.StartsWith("zh") ? AppLanguage.ZhTw : AppLanguage.En;
            }
            catch
            {
                return AppLanguage.ZhTw;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "VencordFix";
            this.ClientSize = new Size(400, 335);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 31, 34);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // 優先直接提取 VencordFix.exe 內嵌的 Win32 圖示
            try
            {
                string selfExe = Process.GetCurrentProcess().MainModule.FileName;
                Icon extracted = Icon.ExtractAssociatedIcon(selfExe);
                if (extracted != null)
                {
                    this.Icon = extracted;
                }
            }
            catch
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localIco = Path.Combine(baseDir, "assets", "app.ico");
                string parentIco = Path.Combine(baseDir, "..", "assets", "app.ico");
                if (File.Exists(localIco))
                {
                    this.Icon = new Icon(localIco);
                }
                else if (File.Exists(parentIco))
                {
                    this.Icon = new Icon(parentIco);
                }
            }

            // 標題
            _lblTitle = new Label();
            _lblTitle.Text = "VencordFix";
            _lblTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            _lblTitle.ForeColor = Color.FromArgb(242, 243, 245);
            _lblTitle.Location = new Point(24, 16);
            _lblTitle.AutoSize = true;
            this.Controls.Add(_lblTitle);

            // 語言切換按鈕 (右上角)
            _btnLang = new Button();
            _btnLang.Location = new Point(281, 16);
            _btnLang.Size = new Size(94, 26);
            _btnLang.FlatStyle = FlatStyle.Flat;
            _btnLang.FlatAppearance.BorderSize = 1;
            _btnLang.FlatAppearance.BorderColor = Color.FromArgb(78, 80, 88);
            _btnLang.BackColor = Color.FromArgb(43, 45, 49);
            _btnLang.ForeColor = Color.FromArgb(219, 222, 225);
            _btnLang.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _btnLang.Cursor = Cursors.Hand;
            _btnLang.Click += BtnLang_Click;
            _btnLang.MouseEnter += (s, e) => _btnLang.BackColor = Color.FromArgb(53, 55, 60);
            _btnLang.MouseLeave += (s, e) => _btnLang.BackColor = Color.FromArgb(43, 45, 49);
            this.Controls.Add(_btnLang);

            // 副標題
            _lblSubtitle = new Label();
            _lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _lblSubtitle.ForeColor = Color.FromArgb(148, 155, 164);
            _lblSubtitle.Location = new Point(25, 45);
            _lblSubtitle.AutoSize = true;
            this.Controls.Add(_lblSubtitle);

            // 偵測狀態標籤
            _lblStatusInfo = new Label();
            _lblStatusInfo.Location = new Point(25, 70);
            _lblStatusInfo.AutoSize = true;
            _lblStatusInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            this.Controls.Add(_lblStatusInfo);

            // 按鈕 1：建立桌面捷徑
            _btnShortcut = new Button();
            _btnShortcut.Location = new Point(25, 105);
            _btnShortcut.Size = new Size(350, 42);
            _btnShortcut.FlatStyle = FlatStyle.Flat;
            _btnShortcut.FlatAppearance.BorderSize = 0;
            _btnShortcut.BackColor = Color.FromArgb(88, 101, 242);
            _btnShortcut.ForeColor = Color.White;
            _btnShortcut.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnShortcut.Cursor = Cursors.Hand;
            _btnShortcut.Click += BtnShortcut_Click;
            this.Controls.Add(_btnShortcut);

            _lblShortcutDesc = new Label();
            _lblShortcutDesc.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            _lblShortcutDesc.ForeColor = Color.FromArgb(148, 155, 164);
            _lblShortcutDesc.Location = new Point(27, 150);
            _lblShortcutDesc.AutoSize = true;
            this.Controls.Add(_lblShortcutDesc);

            // 按鈕 2：開機背景監控
            _btnStartup = new Button();
            _btnStartup.Location = new Point(25, 182);
            _btnStartup.Size = new Size(350, 42);
            _btnStartup.FlatStyle = FlatStyle.Flat;
            _btnStartup.FlatAppearance.BorderSize = 0;
            _btnStartup.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnStartup.Cursor = Cursors.Hand;
            _btnStartup.Click += BtnStartup_Click;
            this.Controls.Add(_btnStartup);

            _lblStartupDesc = new Label();
            _lblStartupDesc.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            _lblStartupDesc.ForeColor = Color.FromArgb(148, 155, 164);
            _lblStartupDesc.Location = new Point(27, 227);
            _lblStartupDesc.AutoSize = true;
            this.Controls.Add(_lblStartupDesc);

            // 底部回饋提示區
            _lblFeedback = new Label();
            _lblFeedback.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _lblFeedback.ForeColor = Color.FromArgb(180, 185, 192);
            _lblFeedback.Location = new Point(25, 270);
            _lblFeedback.Size = new Size(350, 45);
            this.Controls.Add(_lblFeedback);
        }

        private void BtnLang_Click(object sender, EventArgs e)
        {
            _currentLang = (_currentLang == AppLanguage.ZhTw) ? AppLanguage.En : AppLanguage.ZhTw;
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            bool isZh = (_currentLang == AppLanguage.ZhTw);

            _btnLang.Text = isZh ? "🌐 English" : "🌐 繁體中文";

            _lblSubtitle.Text = isZh 
                ? "讓 Vencord 在 Discord 自動更新後維持有效" 
                : "Keep Vencord working across Discord updates";

            _btnShortcut.Text = isZh 
                ? "1. 在桌面建立啟動捷徑" 
                : "1. Create Desktop Shortcut";

            _lblShortcutDesc.Text = isZh 
                ? "平時由桌面捷徑啟動，自動檢查並修補 Vencord。" 
                : "Launch Discord from this shortcut to auto-check and patch Vencord.";

            _lblStartupDesc.Text = isZh 
                ? "開機在後台自動監控，Discord 更新時即時修補。" 
                : "Monitors Discord on Windows startup and patches updates silently.";

            RefreshState();
            UpdateFeedback();
        }

        private void RefreshState()
        {
            bool isZh = (_currentLang == AppLanguage.ZhTw);

            var discords = DiscordApp.DetectInstalledDiscords();
            if (discords.Count > 0)
            {
                var d = discords[0];
                _lblStatusInfo.Text = isZh 
                    ? "本機偵測：" + d.Title + " (v" + d.AppVersion + ") 已就緒" 
                    : "Detected: " + d.Title + " (v" + d.AppVersion + ") Ready";
                _lblStatusInfo.ForeColor = Color.FromArgb(35, 165, 89); // 綠色
            }
            else
            {
                _lblStatusInfo.Text = isZh 
                    ? "未偵測到本機 Discord 安裝路徑" 
                    : "Discord installation path not found";
                _lblStatusInfo.ForeColor = Color.FromArgb(237, 66, 69); // 紅色
            }

            bool isStartup = ShortcutHelper.IsStartupEnabled();
            if (isStartup)
            {
                _btnStartup.Text = isZh 
                    ? "2. 開機背景監控 [已開啟] (點擊可關閉)" 
                    : "2. Background Watcher [Active] (Click to turn off)";
                _btnStartup.BackColor = Color.FromArgb(35, 165, 89); // 綠色
                _btnStartup.ForeColor = Color.White;
            }
            else
            {
                _btnStartup.Text = isZh 
                    ? "2. 開機背景監控 [未開啟] (點擊開啟)" 
                    : "2. Background Watcher [Disabled] (Click to enable)";
                _btnStartup.BackColor = Color.FromArgb(78, 80, 88); // 灰色
                _btnStartup.ForeColor = Color.White;
            }
        }

        private void UpdateFeedback()
        {
            bool isZh = (_currentLang == AppLanguage.ZhTw);

            switch (_feedbackState)
            {
                case FeedbackState.ShortcutSuccess:
                    _lblFeedback.Text = isZh 
                        ? "成功：已在桌面建立「Discord (VencordFix)」捷徑！" 
                        : "Success: Created 'Discord (VencordFix)' shortcut on Desktop!";
                    _lblFeedback.ForeColor = Color.FromArgb(35, 165, 89);
                    break;
                case FeedbackState.ShortcutFail:
                    _lblFeedback.Text = isZh 
                        ? "建立捷徑失敗，請檢查權限。" 
                        : "Failed to create shortcut. Please check permissions.";
                    _lblFeedback.ForeColor = Color.FromArgb(237, 66, 69);
                    break;
                case FeedbackState.StartupEnabled:
                    _lblFeedback.Text = isZh 
                        ? "成功：已啟用開機背景自動監控！" 
                        : "Success: Enabled startup background watcher!";
                    _lblFeedback.ForeColor = Color.FromArgb(35, 165, 89);
                    break;
                case FeedbackState.StartupDisabled:
                    _lblFeedback.Text = isZh 
                        ? "已取消開機背景監控設定。" 
                        : "Disabled startup background watcher.";
                    _lblFeedback.ForeColor = Color.FromArgb(242, 243, 245);
                    break;
                case FeedbackState.StartupFail:
                    _lblFeedback.Text = isZh 
                        ? "更新開機啟動設定失敗。" 
                        : "Failed to update startup setting.";
                    _lblFeedback.ForeColor = Color.FromArgb(237, 66, 69);
                    break;
                case FeedbackState.Initial:
                default:
                    _lblFeedback.Text = isZh 
                        ? "提示：擇一設定即可，設定完成後可直接關閉此視窗。" 
                        : "Tip: Choose either option. You can close this window after setup.";
                    _lblFeedback.ForeColor = Color.FromArgb(180, 185, 192);
                    break;
            }
        }

        private void BtnShortcut_Click(object sender, EventArgs e)
        {
            bool ok = ShortcutHelper.CreateDesktopShortcut();
            _feedbackState = ok ? FeedbackState.ShortcutSuccess : FeedbackState.ShortcutFail;
            UpdateFeedback();
        }

        private void BtnStartup_Click(object sender, EventArgs e)
        {
            bool current = ShortcutHelper.IsStartupEnabled();
            bool target = !current;
            bool ok = ShortcutHelper.SetStartup(target, true);
            if (ok)
            {
                _feedbackState = target ? FeedbackState.StartupEnabled : FeedbackState.StartupDisabled;
                RefreshState();
            }
            else
            {
                _feedbackState = FeedbackState.StartupFail;
            }
            UpdateFeedback();
        }
    }
}
