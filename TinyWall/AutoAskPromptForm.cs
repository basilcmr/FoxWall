using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    // [FoxWall Enhancement] - Start
    public class AutoAskPromptForm : Form
    {
        public enum PromptResult
        {
            AllowUnrestricted,
            AllowWebOnly,
            BlockOnce,
            BlockAlways
        }

        public PromptResult SelectedResult { get; private set; } = PromptResult.BlockOnce;

        private readonly string _appPath;
        private readonly string _remoteIp;
        private readonly int _remotePort;
        private readonly Protocol _protocol;
        private readonly RuleDirection _direction;

        private PictureBox? picIcon;
        private Label? lblAppName;
        private Label? lblAppPath;
        private Label? lblDetails;
        private Button? btnAllowUnrestricted;
        private Button? btnAllowWebOnly;
        private Button? btnBlockOnce;
        private Button? btnBlockAlways;

        public AutoAskPromptForm(AutoAskPendingEntry entry)
        {
            _appPath = entry.AppPath;
            _remoteIp = entry.RemoteIp;
            _remotePort = entry.RemotePort;
            _protocol = entry.Protocol;
            _direction = entry.Direction;

            InitializeComponent();
            ThemeManager.Apply(this);
            LoadAppDetails();
        }

        private void InitializeComponent()
        {
            this.Text = "FoxWall Connection Alert";
            this.Size = new Size(500, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;

            picIcon = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            lblAppName = new Label
            {
                Location = new Point(80, 20),
                Size = new Size(380, 25),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White
            };

            lblAppPath = new Label
            {
                Location = new Point(80, 48),
                Size = new Size(380, 35),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.DarkGray
            };

            lblDetails = new Label
            {
                Location = new Point(20, 95),
                Size = new Size(440, 65),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.LightGray
            };

            btnAllowUnrestricted = new Button
            {
                Text = "Allow (Always)",
                Location = new Point(20, 180),
                Size = new Size(210, 35),
                DialogResult = DialogResult.OK
            };
            btnAllowUnrestricted.Click += (s, e) => { SelectedResult = PromptResult.AllowUnrestricted; };

            btnAllowWebOnly = new Button
            {
                Text = "Allow (Web Only - 80/443)",
                Location = new Point(250, 180),
                Size = new Size(210, 35),
                DialogResult = DialogResult.OK
            };
            btnAllowWebOnly.Click += (s, e) => { SelectedResult = PromptResult.AllowWebOnly; };

            btnBlockOnce = new Button
            {
                Text = "Block (Once)",
                Location = new Point(20, 230),
                Size = new Size(210, 35),
                DialogResult = DialogResult.OK
            };
            btnBlockOnce.Click += (s, e) => { SelectedResult = PromptResult.BlockOnce; };

            btnBlockAlways = new Button
            {
                Text = "Block (Always)",
                Location = new Point(250, 230),
                Size = new Size(210, 35),
                DialogResult = DialogResult.OK
            };
            btnBlockAlways.Click += (s, e) => { SelectedResult = PromptResult.BlockAlways; };

            this.Controls.Add(picIcon);
            this.Controls.Add(lblAppName);
            this.Controls.Add(lblAppPath);
            this.Controls.Add(lblDetails);
            this.Controls.Add(btnAllowUnrestricted);
            this.Controls.Add(btnAllowWebOnly);
            this.Controls.Add(btnBlockOnce);
            this.Controls.Add(btnBlockAlways);
        }

        private void LoadAppDetails()
        {
            if (lblAppName == null || lblAppPath == null || lblDetails == null || picIcon == null)
                return;

            try
            {
                string filename = Path.GetFileName(_appPath);
                lblAppName.Text = string.IsNullOrEmpty(filename) ? "Unknown Application" : filename;
                lblAppPath.Text = _appPath;

                if (File.Exists(_appPath))
                {
                    using var icon = Icon.ExtractAssociatedIcon(_appPath);
                    if (icon != null)
                    {
                        picIcon.Image = icon.ToBitmap();
                    }
                }
            }
            catch
            {
                // Fallback icon or empty
            }

            string dirStr = _direction == RuleDirection.Out ? "outbound" : "inbound";
            string protoStr = _protocol.ToString().ToUpper();
            string portStr = _remotePort > 0 ? $":{_remotePort}" : "";
            string destStr = string.IsNullOrEmpty(_remoteIp) ? "unknown address" : $"{_remoteIp}{portStr}";

            lblDetails.Text = $"An unknown application wants to initiate an {dirStr} {protoStr} connection.\n" +
                              $"Destination: {destStr}\n\n" +
                              "How do you want to handle this request?";
        }
    }
    // [FoxWall Enhancement] - End
}
