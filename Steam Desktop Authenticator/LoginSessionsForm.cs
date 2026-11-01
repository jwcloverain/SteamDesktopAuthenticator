using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;

namespace Steam_Desktop_Authenticator
{
    /// <summary>Combined entry for scanning QR codes and managing browser login sessions.</summary>
    public sealed class LoginSessionsForm : Form
    {
        private readonly SteamGuardAccount account;
        private readonly FlowLayoutPanel list = new FlowLayoutPanel();
        private readonly Button refresh = new Button();
        private readonly Button scan = new Button();

        public LoginSessionsForm(SteamGuardAccount account)
        {
            this.account = account;
            Text = "Steam 登录 — 扫码与登录请求";
            ClientSize = new Size(620, 360);
            MinimumSize = Size;
            StartPosition = FormStartPosition.CenterParent;
            scan.Text = "扫描 QR 登录";
            scan.SetBounds(12, 12, 130, 30);
            scan.Click += async (s, e) => await ScanQrAsync();
            refresh.Text = "刷新登录请求";
            refresh.SetBounds(150, 12, 130, 30);
            refresh.Click += async (s, e) => await LoadSessionsAsync();
            list.Location = new Point(12, 52);
            list.Size = new Size(596, 296);
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(scan);
            Controls.Add(refresh);
            Controls.Add(list);
            Shown += async (s, e) => await LoadSessionsAsync();
        }

        private async Task ScanQrAsync()
        {
            scan.Enabled = refresh.Enabled = false;
            Form mainForm = Owner;
            double mainOpacity = mainForm == null ? 1 : mainForm.Opacity;
            // Keep the modal page open while exposing the browser for screen capture.
            Opacity = 0;
            if (mainForm != null) mainForm.Opacity = 0;
            try
            {
                using (var form = new QrLoginScanForm(account))
                    form.ShowDialog(this);
            }
            finally
            {
                if (mainForm != null && !mainForm.IsDisposed) mainForm.Opacity = mainOpacity;
                if (!IsDisposed)
                {
                    Opacity = 1;
                    Activate();
                    scan.Enabled = refresh.Enabled = true;
                }
            }
            if (!IsDisposed) await LoadSessionsAsync();
        }

        private async Task LoadSessionsAsync()
        {
            scan.Enabled = refresh.Enabled = false;
            list.Controls.Clear();
            try
            {
                // The API validates and refreshes the access token only when needed.
                LoginSessionConfirmation[] sessions = await account.FetchLoginSessionsAsync();
                if (IsDisposed) return;
                if (sessions.Length == 0)
                {
                    list.Controls.Add(new Label { Text = "没有待处理的 Steam 登录请求。", AutoSize = true, Margin = new Padding(8) });
                    return;
                }
                foreach (LoginSessionConfirmation session in sessions)
                    AddSession(session);
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                // The API already includes sanitized transport details in its message.
                list.Controls.Add(new Label { Text = "读取登录请求失败: " + ex.Message, AutoSize = true, ForeColor = Color.DarkRed, Margin = new Padding(8) });
            }
            finally { if (!IsDisposed) scan.Enabled = refresh.Enabled = true; }
        }

        private void AddSession(LoginSessionConfirmation session)
        {
            var panel = new Panel { Width = 565, Height = 82, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(4) };
            var text = new Label { AutoSize = false, Location = new Point(8, 8), Size = new Size(370, 64), Text = "设备: " + (session.DeviceFriendlyName ?? "未知") + "\r\n位置: " + (session.Location ?? "未知") + "\r\nIP: " + (session.IP ?? "未知") };
            var approve = new Button { Text = "批准", Location = new Point(390, 12), Size = new Size(75, 28) };
            var reject = new Button { Text = "取消", Location = new Point(475, 12), Size = new Size(75, 28) };
            approve.Click += async (s, e) => await DecideAsync(panel, session, true);
            reject.Click += async (s, e) => await DecideAsync(panel, session, false);
            panel.Controls.AddRange(new Control[] { text, approve, reject });
            list.Controls.Add(panel);
        }

        private async Task DecideAsync(Control panel, LoginSessionConfirmation session, bool approve)
        {
            try
            {
                await account.SetLoginSessionApprovalAsync(session, approve);
                panel.Controls.Clear();
                panel.Controls.Add(new Label { Text = approve ? "已批准登录" : "已取消登录", AutoSize = true, Location = new Point(8, 30) });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "登录请求处理失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
