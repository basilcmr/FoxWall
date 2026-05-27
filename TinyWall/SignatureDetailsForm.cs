using System;
using System.Drawing;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal class SignatureDetailsForm : Form
    {
        private Label lblHeader = null!;
        private Panel pnlStatusBadge = null!;
        private Label lblStatusText = null!;
        
        private Label lblFileTitle = null!;
        private TextBox txtFilePath = null!;
        
        private Label lblPublisherTitle = null!;
        private TextBox txtPublisher = null!;
        
        private Label lblValidityTitle = null!;
        private Label lblValidityValue = null!;
        
        private Button btnClose = null!;

        public SignatureDetailsForm(string filePath, bool isSigned, bool certValid, string publisherSubject)
        {
            InitializeComponent(filePath, isSigned, certValid, publisherSubject);
            ThemeManager.Apply(this);
            
            // Customize badge backgrounds/colors post-theme application
            if (certValid)
            {
                pnlStatusBadge.BackColor = Color.FromArgb(40, 167, 69); // Green
                lblStatusText.Text = "VALID DIGITAL SIGNATURE";
                lblValidityValue.Text = "Trusted & Validated";
                lblValidityValue.ForeColor = Color.FromArgb(40, 167, 69);
            }
            else if (isSigned)
            {
                pnlStatusBadge.BackColor = Color.FromArgb(255, 193, 7); // Amber
                lblStatusText.Text = "INVALID OR UNTRUSTED SIGNATURE";
                lblStatusText.ForeColor = Color.Black;
                lblValidityValue.Text = "Signed but Untrusted / Self-signed";
                lblValidityValue.ForeColor = Color.FromArgb(255, 193, 7);
            }
            else
            {
                pnlStatusBadge.BackColor = Color.FromArgb(220, 53, 69); // Red
                lblStatusText.Text = "UNSIGNED EXECUTABLE";
                lblValidityValue.Text = "No digital signature found";
                lblValidityValue.ForeColor = Color.FromArgb(220, 53, 69);
            }
        }

        private void InitializeComponent(string filePath, bool isSigned, bool certValid, string publisherSubject)
        {
            this.Text = "FoxWall Digital Signature Viewer";
            this.Size = new Size(500, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Icon = Resources.Icons.firewall;

            lblHeader = new Label
            {
                Text = "Security Verification",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            pnlStatusBadge = new Panel
            {
                Location = new Point(20, 50),
                Size = new Size(440, 30),
            };

            lblStatusText = new Label
            {
                Text = "VERIFYING...",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlStatusBadge.Controls.Add(lblStatusText);

            lblFileTitle = new Label
            {
                Text = "File Path:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 95),
                AutoSize = true
            };

            txtFilePath = new TextBox
            {
                Text = filePath,
                Location = new Point(20, 115),
                Size = new Size(440, 23),
                ReadOnly = true,
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblPublisherTitle = new Label
            {
                Text = "Publisher / Certificate Subject:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 155),
                AutoSize = true
            };

            txtPublisher = new TextBox
            {
                Text = publisherSubject,
                Location = new Point(20, 175),
                Size = new Size(440, 50),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblValidityTitle = new Label
            {
                Text = "Authenticode Status:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 240),
                AutoSize = true
            };

            lblValidityValue = new Label
            {
                Text = "Unknown",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Location = new Point(20, 260),
                AutoSize = true
            };

            btnClose = new Button
            {
                Text = "Close",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 300),
                Size = new Size(440, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblHeader);
            this.Controls.Add(pnlStatusBadge);
            this.Controls.Add(lblFileTitle);
            this.Controls.Add(txtFilePath);
            this.Controls.Add(lblPublisherTitle);
            this.Controls.Add(txtPublisher);
            this.Controls.Add(lblValidityTitle);
            this.Controls.Add(lblValidityValue);
            this.Controls.Add(btnClose);
        }
    }
}
