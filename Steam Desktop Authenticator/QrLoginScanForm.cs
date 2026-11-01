using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;
using ZXing;
using ZXing.Common;

namespace Steam_Desktop_Authenticator
{
    /// <summary>Decodes and confirms a Steam login QR code captured from the desktop.</summary>
    public sealed class QrLoginScanForm : Form
    {
        private readonly SteamGuardAccount account;
        private readonly Label status = new Label();
        private readonly Button scan = new Button();
        private readonly Button approve = new Button();
        private readonly Button reject = new Button();
        private string challengeUrl;

        public QrLoginScanForm(SteamGuardAccount account)
        {
            this.account = account ?? throw new ArgumentNullException("account");
            Text = "扫描 Steam 登录二维码";
            ClientSize = new Size(470, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            status.AutoSize = false;
            status.Location = new Point(18, 18);
            status.Size = new Size(434, 60);
            status.Text = "点击开始扫描后，SDA 会隐藏并截取桌面上的 Steam QR 码。";
            scan.Text = "开始扫描";
            scan.SetBounds(18, 92, 120, 32);
            approve.Text = "批准登录";
            approve.SetBounds(220, 92, 105, 32);
            reject.Text = "拒绝登录";
            reject.SetBounds(340, 92, 105, 32);
            approve.Enabled = reject.Enabled = false;
            scan.Click += async (s, e) => await ScanScreenAsync();
            approve.Click += async (s, e) => await DecideAsync(true);
            reject.Click += async (s, e) => await DecideAsync(false);
            Controls.AddRange(new Control[] { status, scan, approve, reject });
        }

        private async Task ScanScreenAsync()
        {
            scan.Enabled = false;
            approve.Enabled = reject.Enabled = false;
            challengeUrl = null;
            status.Text = "正在隐藏窗口并截取桌面，请保持 Steam QR 码可见……";
            // Do not call Hide() on a modal form: ShowDialog() would return and the
            // MainForm using block could dispose this form while the scan is running.
            Opacity = 0;
            try
            {
                await Task.Delay(350);
                using (Bitmap screenshot = CaptureDesktop())
                    Decode(screenshot);
            }
            catch (Exception ex)
            {
                challengeUrl = null;
                status.Text = "截取桌面失败: " + ex.Message;
                approve.Enabled = reject.Enabled = false;
            }
            finally
            {
                if (!IsDisposed)
                {
                    Opacity = 1;
                    Activate();
                }
                if (!IsDisposed) scan.Enabled = true;
            }
        }

        private static Bitmap CaptureDesktop()
        {
            Rectangle bounds = Rectangle.Empty;
            foreach (Screen screen in Screen.AllScreens)
                bounds = bounds == Rectangle.Empty ? screen.Bounds : Rectangle.Union(bounds, screen.Bounds);
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }

        private void Decode(Bitmap bitmap)
        {
            if (bitmap == null) return;
            try
            {
                var reader = new BarcodeReaderGeneric
                {
                    Options = new DecodingOptions
                    {
                        TryHarder = true,
                        PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                    }
                };
                Result result = reader.Decode(ToLuminance(bitmap));
                challengeUrl = result == null ? null : result.Text;
                var challenge = LoginSessionConfirmationApi.ParseQrChallenge(challengeUrl);
                status.Text = "已解析 Steam 登录请求。Client ID: " + challenge.ClientID + "\r\n请选择批准或拒绝。";
                approve.Enabled = reject.Enabled = true;
            }
            catch (Exception ex)
            {
                challengeUrl = null;
                approve.Enabled = reject.Enabled = false;
                status.Text = "无法识别 Steam QR 码: " + ex.Message;
            }
        }

        private static RGBLuminanceSource ToLuminance(Bitmap bitmap)
        {
            using (var copy = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(copy))
            {
                graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                Rectangle rect = new Rectangle(0, 0, copy.Width, copy.Height);
                BitmapData data = copy.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int length = Math.Abs(data.Stride) * data.Height;
                    byte[] bytes = new byte[length];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, length);
                    return new RGBLuminanceSource(bytes, copy.Width, copy.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
                }
                finally { copy.UnlockBits(data); }
            }
        }

        private async Task DecideAsync(bool allow)
        {
            scan.Enabled = approve.Enabled = reject.Enabled = false;
            status.Text = allow ? "正在向 Steam 提交登录批准……" : "正在向 Steam 提交登录拒绝……";
            try
            {
                await account.SetQrLoginApprovalAsync(challengeUrl, allow);
                if (IsDisposed) return;
                status.Text = allow ? "登录已批准，浏览器将继续登录。" : "登录已拒绝。";
                DialogResult = allow ? DialogResult.Yes : DialogResult.No;
                Close();
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                status.Text = "登录请求处理失败。可重试，或刷新浏览器二维码后重新扫描。";
                MessageBox.Show(this, ex.Message, "Steam 扫码登录失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                scan.Enabled = approve.Enabled = reject.Enabled = true;
            }
        }
    }
}
