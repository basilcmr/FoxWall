using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            BlockAlways,
            Customized
        }

        public PromptResult SelectedResult { get; private set; } = PromptResult.BlockOnce;
        public bool ChildProcessesInherit => ActiveConfig.Controller.AutoAskChildInherit;

        private readonly string _appPath;
        private readonly string _remoteIp;
        private readonly int _remotePort;
        private readonly Protocol _protocol;
        private readonly RuleDirection _direction;

        private PictureBox? picIcon;
        private Label? lblAppName;
        private Label? lblAppPath;
        private Label? lblDetails;
        private FlowLayoutPanel? pnlOptions;
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
            this.Size = new Size(500, 395);
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
                Size = new Size(370, 25),
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
                Size = new Size(460, 45),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.LightGray
            };

            pnlOptions = new FlowLayoutPanel
            {
                Location = new Point(20, 145),
                Size = new Size(460, 65),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            btnAllowUnrestricted = new Button
            {
                Text = "Allow (Always)",
                Location = new Point(20, 225),
                Size = new Size(220, 35),
                DialogResult = DialogResult.OK
            };
            btnAllowUnrestricted.Click += (s, e) => { SelectedResult = PromptResult.AllowUnrestricted; };

            btnAllowWebOnly = new Button
            {
                Text = "Allow (Web Only - 80/443)",
                Location = new Point(260, 225),
                Size = new Size(220, 35),
                DialogResult = DialogResult.OK
            };
            btnAllowWebOnly.Click += (s, e) => { SelectedResult = PromptResult.AllowWebOnly; };

            btnBlockOnce = new Button
            {
                Text = "Block (Once)",
                Location = new Point(20, 275),
                Size = new Size(220, 35),
                DialogResult = DialogResult.OK
            };
            btnBlockOnce.Click += (s, e) => { SelectedResult = PromptResult.BlockOnce; };

            btnBlockAlways = new Button
            {
                Text = "Block (Always)",
                Location = new Point(260, 275),
                Size = new Size(220, 35),
                DialogResult = DialogResult.OK
            };
            btnBlockAlways.Click += (s, e) => { SelectedResult = PromptResult.BlockAlways; };

            // Create copy to clipboard context menu
            var copyMenu = new ContextMenuStrip();
            ThemeManager.ApplyToControl(copyMenu);
            copyMenu.Renderer = ThemeManager.GetToolStripRenderer();

            var copyNameItem = new ToolStripMenuItem("Copy application name");
            copyNameItem.Click += (s, e) => {
                try 
                { 
                    string filename = Path.GetFileName(_appPath);
                    Clipboard.SetText(string.IsNullOrEmpty(filename) ? "Unknown Application" : filename); 
                } 
                catch { }
            };
            var copyPathItem = new ToolStripMenuItem("Copy full file path");
            copyPathItem.Click += (s, e) => {
                try { Clipboard.SetText(_appPath); } catch { }
            };

            copyMenu.Items.Add(copyNameItem);
            copyMenu.Items.Add(copyPathItem);

            foreach (ToolStripItem item in copyMenu.Items)
            {
                item.ForeColor = ThemeManager.TextPrimary;
                item.BackColor = ThemeManager.BackgroundColor;
            }

            var btnCopy = new Button
            {
                Text = "📋",
                Location = new Point(455, 20),
                Size = new Size(24, 24),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            btnCopy.Click += (s, e) => {
                copyMenu.Show(btnCopy, new Point(0, btnCopy.Height));
            };
            ThemeManager.ApplyToControl(btnCopy);
            btnCopy.BackColor = Color.Transparent;
            btnCopy.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnCopy.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 45);
            btnCopy.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 30, 30);

            this.Controls.Add(picIcon);
            this.Controls.Add(lblAppName);
            this.Controls.Add(lblAppPath);
            this.Controls.Add(btnCopy);
            this.Controls.Add(lblDetails);
            this.Controls.Add(pnlOptions);
            this.Controls.Add(btnAllowUnrestricted);
            this.Controls.Add(btnAllowWebOnly);
            this.Controls.Add(btnBlockOnce);
            this.Controls.Add(btnBlockAlways);

            lblAppName.ContextMenuStrip = copyMenu;
            lblAppPath.ContextMenuStrip = copyMenu;
            this.ContextMenuStrip = copyMenu;

            // Add ToolTips to guide the user
            var toolTip = new ToolTip();
            toolTip.SetToolTip(lblAppName, "Right-click to copy name or path");
            toolTip.SetToolTip(lblAppPath, "Right-click to copy name or path");
            toolTip.SetToolTip(btnCopy, "Copy details to clipboard");
        }

        private void LoadAppDetails()
        {
            if (lblAppName == null || lblAppPath == null || lblDetails == null || picIcon == null || pnlOptions == null)
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
                              $"Destination: {destStr}";

            var links = new List<(string text, string tag)>();
            links.Add(("Open Folder", "folder"));
            links.Add(("Verify Signature", "signature"));
            links.Add(("Check VirusTotal", "virustotal"));
            links.Add(("Check Path", "google_path"));
            links.Add(("Google Process", "google_process"));
            links.Add(("Can I Block?", "google_block"));
            if (!string.IsNullOrEmpty(_remoteIp) && _remoteIp != "::" && _remoteIp != "0.0.0.0" && _remoteIp != "127.0.0.1" && _remoteIp != "::1")
            {
                links.Add(("Search IP", "google_ip"));
            }
            links.Add(("Customize Rule", "customize"));

            if (pnlOptions == null) return;

            pnlOptions.Controls.Clear();
            foreach (var link in links)
            {
                var btn = new Button
                {
                    Text = link.text,
                    Tag = link.tag,
                    FlatStyle = FlatStyle.Flat,
                    Height = 25,
                    Margin = new Padding(0, 0, 5, 5),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8.25F, FontStyle.Regular)
                };
                btn.Click += OptionButton_Click;
                
                // Dynamically apply themes if dark mode is active
                ThemeManager.ApplyToControl(btn);
                
                // Style option buttons differently (transparent background, subtle gray border)
                btn.BackColor = Color.Transparent;
                btn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 45);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 30, 30);
                
                pnlOptions.Controls.Add(btn);

                // Add small setup cogwheel next to Google Process and Can I Block
                if (link.tag == "google_process" || link.tag == "google_block")
                {
                    var btnSetup = new Button
                    {
                        Text = "⚙",
                        Tag = link.tag + "_setup",
                        FlatStyle = FlatStyle.Flat,
                        Height = 25,
                        Width = 25,
                        Margin = new Padding(0, 0, 8, 5),
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                    };
                    btnSetup.Click += SetupButton_Click;
                    ThemeManager.ApplyToControl(btnSetup);
                    
                    // Style differently (transparent background, subtle gray border)
                    btnSetup.BackColor = Color.Transparent;
                    btnSetup.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
                    btnSetup.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 45);
                    btnSetup.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 30, 30);

                    pnlOptions.Controls.Add(btnSetup);
                }
            }
        }

        private void SetupButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var contextMenu = new ContextMenuStrip();
                ThemeManager.ApplyToControl(contextMenu);
                contextMenu.Renderer = ThemeManager.GetToolStripRenderer();

                if (tag == "google_process_setup")
                {
                    var item = new ToolStripMenuItem("Include block safety check in search")
                    {
                        CheckOnClick = true,
                        Checked = ActiveConfig.Controller.AutoAskIncludeBlockCheck
                    };
                    item.CheckedChanged += (s, ev) =>
                    {
                        ActiveConfig.Controller.AutoAskIncludeBlockCheck = item.Checked;
                        ActiveConfig.Controller.Save();
                    };
                    contextMenu.Items.Add(item);
                }
                else if (tag == "google_block_setup")
                {
                    var item = new ToolStripMenuItem("Apply rules to child processes (subtasks)")
                    {
                        CheckOnClick = true,
                        Checked = ActiveConfig.Controller.AutoAskChildInherit
                    };
                    item.CheckedChanged += (s, ev) =>
                    {
                        ActiveConfig.Controller.AutoAskChildInherit = item.Checked;
                        ActiveConfig.Controller.Save();
                    };
                    contextMenu.Items.Add(item);
                }

                foreach (ToolStripItem item in contextMenu.Items)
                {
                    item.ForeColor = ThemeManager.TextPrimary;
                    item.BackColor = ThemeManager.BackgroundColor;
                }

                contextMenu.Show(btn, new Point(0, btn.Height));
            }
        }

        private void OptionButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                TriggerOptionAction(tag);
            }
        }

        private void TriggerOptionAction(string tag)
        {

            try
            {
                if (tag == "folder")
                {
                    if (File.Exists(_appPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{_appPath}\"");
                    }
                    else
                    {
                        string? dir = Path.GetDirectoryName(_appPath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            Process.Start("explorer.exe", $"\"{dir}\"");
                        }
                    }
                }
                else if (tag == "signature")
                {
                    if (File.Exists(_appPath))
                    {
                        var tempSubj = new ExecutableSubject(_appPath);
                        bool isSigned = tempSubj.IsSigned;
                        bool certValid = tempSubj.CertValid;
                        string certSubject = tempSubj.CertSubject ?? "Unknown / Unsigned";

                        using var sigForm = new SignatureDetailsForm(_appPath, isSigned, certValid, certSubject);
                        sigForm.ShowDialog(this);
                    }
                    else
                    {
                        MessageBox.Show(this, "The file does not exist to verify signature.", "FoxWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (tag == "virustotal")
                {
                    string hash = Hasher.HashFile(_appPath);
                    string url = $"https://www.virustotal.com/gui/search/{hash}";
                    Utils.StartProcess(url, string.Empty, false);
                }
                else if (tag == "google_path")
                {
                    string filename = Path.GetFileName(_appPath);
                    string directory = Path.GetDirectoryName(_appPath) ?? "";
                    string query = !string.IsNullOrEmpty(directory)
                        ? $"is {filename} in {directory} safe legitimate or malware virus"
                        : $"is {filename} safe legitimate or malware virus";

                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                    Utils.StartProcess(url, string.Empty, false);
                }
                else if (tag == "google_process")
                {
                    string filename = Path.GetFileName(_appPath);
                    string query = $"is {filename} safe legitimate or malware virus";

                    if (ActiveConfig.Controller.AutoAskIncludeBlockCheck)
                    {
                        bool withSubtasks = ActiveConfig.Controller.AutoAskChildInherit;
                        query += withSubtasks
                            ? $" and is it ok to block it and its child processes using TinyWall"
                            : $" and is it ok to block it using TinyWall";
                    }
                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                    Utils.StartProcess(url, string.Empty, false);
                }
                else if (tag == "google_block")
                {
                    string filename = Path.GetFileName(_appPath);
                    bool withSubtasks = ActiveConfig.Controller.AutoAskChildInherit;

                    string query = withSubtasks
                        ? $"is it ok to block {filename} and its child processes using TinyWall"
                        : $"is it ok to block {filename} using TinyWall";

                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                    Utils.StartProcess(url, string.Empty, false);
                }
                else if (tag == "google_ip")
                {
                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(_remoteIp)}";
                    Utils.StartProcess(url, string.Empty, false);
                }
                else if (tag == "customize")
                {
                    var subject = new ExecutableSubject(_appPath);
                    var exception = new FirewallExceptionV3(subject, new TcpUdpPolicy(unrestricted: true));
                    
                    using var f = new ApplicationExceptionForm(exception);
                    if (f.ShowDialog(this) == DialogResult.OK)
                    {
                        var controller = GlobalInstances.TinyWallControllerInstance;
                        if (controller != null && f.ExceptionSettings != null && f.ExceptionSettings.Count > 0)
                        {
                            controller.AddExceptions(f.ExceptionSettings, false);
                        }
                        this.SelectedResult = PromptResult.Customized;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not perform action: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    // [FoxWall Enhancement] - End
}
