using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;

namespace Steam_Desktop_Authenticator
{
    /// <summary>Approval dialog for Steam browser login sessions. Trade confirmations use a different form.</summary>
    public sealed class LoginSessionPopupForm : Form
    {
        private readonly SteamGuardAccount account;
        private readonly LoginSessionConfirmation session;
        private readonly Label details;
        private readonly Button approve;
        private readonly Button reject;

        public LoginSessionPopupForm(SteamGuardAccount account, LoginSessionConfirmation session)
        {
            this.account = account;
            this.session = session;
            Text = "Steam 登录会话确认";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 190);
            details = new Label { AutoSize = false, Location = new Point(18, 18), Size = new Size(394, 95), Text = BuildText(), Font = new Font("Segoe UI", 10F) };
            approve = new Button { Text = "批准登录", Location = new Point(210, 135), Size = new Size(100, 32) };
            reject = new Button { Text = "拒绝登录", Location = new Point(320, 135), Size = new Size(100, 32) };
            approve.Click += async (s, e) => await DecideAsync(true);
            reject.Click += async (s, e) => await DecideAsync(false);
            Controls.AddRange(new Control[] { details, approve, reject });
        }

        private string BuildText()
        {
            string device = String.IsNullOrEmpty(session.DeviceFriendlyName) ? "未知设备" : session.DeviceFriendlyName;
            string location = String.IsNullOrEmpty(session.Location) ? "未知位置" : session.Location;
            return "检测到 Steam 浏览器登录请求。\r\n设备: " + device + "\r\n位置: " + location + "\r\nIP: " + (session.IP ?? "未知") + "\r\n是否允许此登录？";
        }

        private async Task DecideAsync(bool allow)
        {
            approve.Enabled = reject.Enabled = false;
            try
            {
                await account.SetLoginSessionApprovalAsync(session, allow);
                DialogResult = allow ? DialogResult.Yes : DialogResult.No;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Steam 登录确认失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                approve.Enabled = reject.Enabled = true;
            }
        }
    }
}
