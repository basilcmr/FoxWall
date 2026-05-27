using System;
using System.Drawing;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class ConnectionsForm
    {
        public string PathFilter { get; set; }

        private Panel pnlFilterBar;
        private Label lblFilterText;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!string.IsNullOrEmpty(PathFilter))
            {
                // Let's create an elegant visual panel showing that a filter is currently active
                pnlFilterBar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 35,
                    BackColor = ThemeManager.SurfaceColor
                };

                lblFilterText = new Label
                {
                    Text = $"Filtered Connections for: {System.IO.Path.GetFileName(PathFilter)}",
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = ThemeManager.TextPrimary,
                    Location = new Point(12, 8),
                    AutoSize = true
                };

                var btnClear = new Button
                {
                    Text = "Clear Filter",
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Size = new Size(110, 25),
                    Location = new Point(this.Width - 140, 5),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat
                };
                btnClear.Click += (s, ev) =>
                {
                    this.PathFilter = null;
                    pnlFilterBar.Visible = false;
                    this.UpdateList();
                };

                pnlFilterBar.Controls.Add(lblFilterText);
                pnlFilterBar.Controls.Add(btnClear);
                
                // Add the panel to controls and bring to front
                this.Controls.Add(pnlFilterBar);
                pnlFilterBar.BringToFront();

                // Theme our programmatically added buttons and panels
                ThemeManager.Apply(this);
            }
        }
    }
}
