using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VencordFix
{
    public class MainForm : Form
    {
        private Label _lblStatusInfo;
        private Button _btnShortcut;
        private Button _btnStartup;
        private Label _lblFeedback;

        public MainForm()
        {
            InitializeComponent();
            RefreshState();
        }

        private void InitializeComponent()
        {
            this.Text = "VencordFix";
            this.ClientSize = new Size(390, 330);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 31, 34);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // 優先載入 VencordFix 專屬圖示
            string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "app.ico");
            if (File.Exists(localIco))
            {
                try
                {
                    this.Icon = new Icon(localIco);
                }
                catch { }
            }
            else
            {
                var discords = DiscordApp.DetectInstalledDiscords();
                if (discords.Count > 0 && File.Exists(Path.Combine(discords[0].RootPath, "app.ico")))
                {
                    try
                    {
                        this.Icon = new Icon(Path.Combine(discords[0].RootPath, "app.ico"));
                    }
                    catch { }
                }
            }

            // 標題
            Label lblTitle = new Label();
            lblTitle.Text = "VencordFix";
            lblTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(242, 243, 245);
            lblTitle.Location = new Point(24, 18);
            lblTitle.AutoSize = true;
            this.Controls.Add(lblTitle);

            // 副標題
            Label lblSubtitle = new Label();
            lblSubtitle.Text = "讓 Vencord 在 Discord 自動更新後維持有效";
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(148, 155, 164);
            lblSubtitle.Location = new Point(25, 45);
            lblSubtitle.AutoSize = true;
            this.Controls.Add(lblSubtitle);

            // 偵測狀態標籤
            _lblStatusInfo = new Label();
            _lblStatusInfo.Location = new Point(25, 70);
            _lblStatusInfo.AutoSize = true;
            _lblStatusInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            this.Controls.Add(_lblStatusInfo);

            // 按鈕 1：建立桌面捷徑
            _btnShortcut = new Button();
            _btnShortcut.Text = "1. 在桌面建立啟動捷徑";
            _btnShortcut.Location = new Point(25, 105);
            _btnShortcut.Size = new Size(340, 42);
            _btnShortcut.FlatStyle = FlatStyle.Flat;
            _btnShortcut.FlatAppearance.BorderSize = 0;
            _btnShortcut.BackColor = Color.FromArgb(88, 101, 242);
            _btnShortcut.ForeColor = Color.White;
            _btnShortcut.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnShortcut.Cursor = Cursors.Hand;
            _btnShortcut.Click += BtnShortcut_Click;
            this.Controls.Add(_btnShortcut);

            Label lblShortcutDesc = new Label();
            lblShortcutDesc.Text = "平時由桌面捷徑啟動，自動檢查並修補 Vencord。";
            lblShortcutDesc.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            lblShortcutDesc.ForeColor = Color.FromArgb(148, 155, 164);
            lblShortcutDesc.Location = new Point(27, 150);
            lblShortcutDesc.AutoSize = true;
            this.Controls.Add(lblShortcutDesc);

            // 按鈕 2：開機背景監控
            _btnStartup = new Button();
            _btnStartup.Location = new Point(25, 182);
            _btnStartup.Size = new Size(340, 42);
            _btnStartup.FlatStyle = FlatStyle.Flat;
            _btnStartup.FlatAppearance.BorderSize = 0;
            _btnStartup.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnStartup.Cursor = Cursors.Hand;
            _btnStartup.Click += BtnStartup_Click;
            this.Controls.Add(_btnStartup);

            Label lblStartupDesc = new Label();
            lblStartupDesc.Text = "開機在後台自動監控，Discord 更新時即時修補。";
            lblStartupDesc.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            lblStartupDesc.ForeColor = Color.FromArgb(148, 155, 164);
            lblStartupDesc.Location = new Point(27, 227);
            lblStartupDesc.AutoSize = true;
            this.Controls.Add(lblStartupDesc);

            // 底部回饋提示區
            _lblFeedback = new Label();
            _lblFeedback.Text = "提示：擇一設定即可，設定完成後可直接關閉此視窗。";
            _lblFeedback.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _lblFeedback.ForeColor = Color.FromArgb(180, 185, 192);
            _lblFeedback.Location = new Point(25, 270);
            _lblFeedback.Size = new Size(340, 36);
            this.Controls.Add(_lblFeedback);
        }

        private void RefreshState()
        {
            var discords = DiscordApp.DetectInstalledDiscords();
            if (discords.Count > 0)
            {
                var d = discords[0];
                _lblStatusInfo.Text = "本機偵測：" + d.Title + " (v" + d.AppVersion + ") 已就緒";
                _lblStatusInfo.ForeColor = Color.FromArgb(35, 165, 89); // 綠色
            }
            else
            {
                _lblStatusInfo.Text = "未偵測到本機 Discord 安裝路徑";
                _lblStatusInfo.ForeColor = Color.FromArgb(237, 66, 69); // 紅色
            }

            bool isStartup = ShortcutHelper.IsStartupEnabled();
            if (isStartup)
            {
                _btnStartup.Text = "2. 開機背景監控 [已開啟] (點擊可關閉)";
                _btnStartup.BackColor = Color.FromArgb(35, 165, 89); // 綠色
                _btnStartup.ForeColor = Color.White;
            }
            else
            {
                _btnStartup.Text = "2. 開機背景監控 [未開啟] (點擊開啟)";
                _btnStartup.BackColor = Color.FromArgb(78, 80, 88); // 灰色
                _btnStartup.ForeColor = Color.White;
            }
        }

        private void BtnShortcut_Click(object sender, EventArgs e)
        {
            bool ok = ShortcutHelper.CreateDesktopShortcut();
            if (ok)
            {
                _lblFeedback.Text = "成功：已在桌面建立「Discord (VencordFix)」捷徑！";
                _lblFeedback.ForeColor = Color.FromArgb(35, 165, 89);
            }
            else
            {
                _lblFeedback.Text = "建立捷徑失敗，請檢查權限。";
                _lblFeedback.ForeColor = Color.FromArgb(237, 66, 69);
            }
        }

        private void BtnStartup_Click(object sender, EventArgs e)
        {
            bool current = ShortcutHelper.IsStartupEnabled();
            bool target = !current;
            bool ok = ShortcutHelper.SetStartup(target, true);
            if (ok)
            {
                RefreshState();
                if (target)
                {
                    _lblFeedback.Text = "成功：已啟用開機背景自動監控！";
                    _lblFeedback.ForeColor = Color.FromArgb(35, 165, 89);
                }
                else
                {
                    _lblFeedback.Text = "已取消開機背景監控設定。";
                    _lblFeedback.ForeColor = Color.FromArgb(242, 243, 245);
                }
            }
            else
            {
                _lblFeedback.Text = "更新開機啟動設定失敗。";
                _lblFeedback.ForeColor = Color.FromArgb(237, 66, 69);
            }
        }
    }
}
