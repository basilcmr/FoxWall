using System;
using System.Drawing;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal enum ImportChoice
    {
        Merge,
        Replace,
        Cancel
    }

    internal class ImportMethodForm : Form
    {
        public ImportChoice Choice { get; private set; } = ImportChoice.Cancel;

        private Label lblHeader = null!;
        private Label lblDescription = null!;
        private Button btnMerge = null!;
        private Button btnReplace = null!;
        private Button btnCancel = null!;

        public ImportMethodForm()
        {
            InitializeComponent();
            ThemeManager.Apply(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Import Configuration Options";
            this.Size = new Size(480, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Icon = Resources.Icons.firewall;

            lblHeader = new Label
            {
                Text = "Choose Import Method",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(25, 20),
                AutoSize = true
            };

            lblDescription = new Label
            {
                Text = "Specify how you want to apply the imported settings and application exceptions. You can either merge them with your existing rules or overwrite them completely.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(25, 55),
                Size = new Size(410, 50)
            };

            btnMerge = new Button
            {
                Text = "Merge (Additive) - Combine rules",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(25, 120),
                Size = new Size(410, 40),
                FlatStyle = FlatStyle.Flat
            };
            btnMerge.Click += (s, e) => { Choice = ImportChoice.Merge; DialogResult = DialogResult.OK; Close(); };

            btnReplace = new Button
            {
                Text = "Replace (Overwrite) - Overwrite all settings",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(25, 170),
                Size = new Size(410, 40),
                FlatStyle = FlatStyle.Flat
            };
            btnReplace.Click += (s, e) => { Choice = ImportChoice.Replace; DialogResult = DialogResult.OK; Close(); };

            btnCancel = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(25, 220),
                Size = new Size(410, 30),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => { Choice = ImportChoice.Cancel; DialogResult = DialogResult.Cancel; Close(); };

            this.Controls.Add(lblHeader);
            this.Controls.Add(lblDescription);
            this.Controls.Add(btnMerge);
            this.Controls.Add(btnReplace);
            this.Controls.Add(btnCancel);
        }
    }
}
