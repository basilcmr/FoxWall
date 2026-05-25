using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Samples; // For TaskDialogCommonButtons and TaskDialogIcon

namespace pylorak.TinyWall
{
    internal sealed class DarkMessageBoxForm : Form
    {
        private readonly string mainInstruction;
        private readonly string contentText;
        private readonly TaskDialogCommonButtons buttons;
        private readonly TaskDialogIcon icon;

        private Label lblMainInstruction = null!;
        private Label lblContent = null!;
        private PictureBox pbIcon = null!;
        private FlowLayoutPanel buttonPanel = null!;

        public DarkMessageBoxForm(string title, string mainInstruction, string contentText, TaskDialogCommonButtons buttons, TaskDialogIcon icon)
        {
            this.mainInstruction = mainInstruction;
            this.contentText = contentText;
            this.buttons = buttons;
            this.icon = icon;

            InitializeForm(title);
        }

        private void InitializeForm(string title)
        {
            // Set Form properties
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ThemeManager.BackgroundColor;
            this.ForeColor = ThemeManager.TextPrimary;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            this.Width = 480;
            this.MinimumSize = new Size(480, 180);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // Main layout container (dynamic padding and scaling)
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                Padding = new Padding(20),
                BackColor = Color.Transparent
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50)); // Icon column
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Text column
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // Content row
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Buttons row

            // Icon
            pbIcon = new PictureBox
            {
                Width = 32,
                Height = 32,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 15, 0)
            };
            SetIcon();
            mainLayout.Controls.Add(pbIcon, 0, 0);

            // Text Panel
            var textPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            lblMainInstruction = new Label
            {
                Text = mainInstruction,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                AutoSize = true,
                MaximumSize = new Size(380, 0),
                Margin = new Padding(0, 0, 0, 10)
            };
            textPanel.Controls.Add(lblMainInstruction);

            if (!string.IsNullOrEmpty(contentText))
            {
                lblContent = new Label
                {
                    Text = contentText,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
                    ForeColor = ThemeManager.TextSecondary,
                    AutoSize = true,
                    MaximumSize = new Size(380, 0),
                    Margin = Padding.Empty
                };
                textPanel.Controls.Add(lblContent);
            }

            mainLayout.Controls.Add(textPanel, 1, 0);

            // Buttons panel at the bottom
            buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 20, 0, 0)
            };

            // TableLayout colspan = 2 for the buttons
            mainLayout.Controls.Add(buttonPanel, 0, 1);
            mainLayout.SetColumnSpan(buttonPanel, 2);

            CreateButtons();

            this.Controls.Add(mainLayout);
        }

        private void SetIcon()
        {
            Icon? sysIcon = null;
            switch (icon)
            {
                case TaskDialogIcon.Information:
                    sysIcon = SystemIcons.Information;
                    break;
                case TaskDialogIcon.Warning:
                    sysIcon = SystemIcons.Warning;
                    break;
                case TaskDialogIcon.Error:
                    sysIcon = SystemIcons.Error;
                    break;
                case TaskDialogIcon.Shield:
                    // Fallback to warning/shield if needed, or SystemIcons.Shield on Windows 8+
                    sysIcon = SystemIcons.Shield ?? SystemIcons.Information;
                    break;
                default:
                    sysIcon = SystemIcons.Information;
                    break;
            }

            if (sysIcon != null)
            {
                pbIcon.Image = sysIcon.ToBitmap();
            }
        }

        private void CreateButtons()
        {
            // The TaskDialogCommonButtons enum is a flags enum. We extract the flags and add buttons.
            int flags = (int)buttons;

            // Mask values for TaskDialogCommonButtons:
            // OK = 1, Yes = 2, No = 4, Cancel = 8, Retry = 16, Close = 32
            if ((flags & 8) != 0) // Cancel
            {
                AddButton("Cancel", DialogResult.Cancel, false);
            }
            if ((flags & 4) != 0) // No
            {
                AddButton("No", DialogResult.No, false);
            }
            if ((flags & 2) != 0) // Yes
            {
                AddButton("Yes", DialogResult.Yes, true);
            }
            if ((flags & 1) != 0) // OK
            {
                AddButton("OK", DialogResult.OK, true);
            }
            if ((flags & 32) != 0) // Close
            {
                AddButton("Close", DialogResult.Cancel, false);
            }
            if ((flags & 16) != 0) // Retry
            {
                AddButton("Retry", DialogResult.Retry, true);
            }

            // Fallback button if none are specified
            if (buttonPanel.Controls.Count == 0)
            {
                AddButton("OK", DialogResult.OK, true);
            }
        }

        private void AddButton(string text, DialogResult result, bool isPrimary)
        {
            var btn = new Button
            {
                Text = text,
                DialogResult = result,
                Size = new Size(95, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, isPrimary ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
                BackColor = isPrimary ? ThemeManager.AccentColor : ThemeManager.SurfaceColor,
                ForeColor = Color.White,
                Margin = new Padding(8, 0, 0, 0)
            };

            btn.FlatAppearance.BorderColor = isPrimary ? ThemeManager.AccentHighlight : ThemeManager.AccentColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = isPrimary ? Color.FromArgb(130, 70, 190) : Color.FromArgb(50, 50, 50);
            btn.FlatAppearance.MouseDownBackColor = isPrimary ? Color.FromArgb(90, 40, 140) : Color.FromArgb(20, 20, 20);

            btn.Click += (s, e) => {
                this.DialogResult = result;
                this.Close();
            };

            buttonPanel.Controls.Add(btn);

            if (isPrimary)
            {
                this.AcceptButton = btn;
            }
            else if (result == DialogResult.Cancel)
            {
                this.CancelButton = btn;
            }
        }
    }
}
