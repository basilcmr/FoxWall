using System;
using System.Drawing;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class ApplicationExceptionForm
    {
        private Label lblImportance = null!;
        private ComboBox cmbImportance = null!;

        protected override void OnLoad(EventArgs e)
        {
            InitializeImportanceControl();
            base.OnLoad(e);
        }

        private void InitializeImportanceControl()
        {
            if (lblImportance != null) return; // Prevent double initialization

            // 1. Create label
            lblImportance = new Label
            {
                Name = "lblImportance",
                Text = "Importance:",
                Location = new Point(19, 114),
                Size = new Size(92, 13),
                BackColor = Color.Transparent
            };

            // 2. Create ComboBox
            cmbImportance = new ComboBox
            {
                Name = "cmbImportance",
                Location = new Point(126, 111),
                Size = new Size(144, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // 3. Add priority values to list
            cmbImportance.Items.Add(RuleImportance.Unclassified);
            cmbImportance.Items.Add(RuleImportance.Critical);
            cmbImportance.Items.Add(RuleImportance.Important);
            cmbImportance.Items.Add(RuleImportance.Optional);
            cmbImportance.Items.Add(RuleImportance.Unnecessary);

            // Select initial value safely
            if (TmpExceptionSettings.Count > 0)
            {
                cmbImportance.SelectedItem = TmpExceptionSettings[0].Importance;
            }
            else
            {
                cmbImportance.SelectedIndex = 0;
            }

            // Handle user changes
            cmbImportance.SelectedIndexChanged += (sender, e) =>
            {
                if (TmpExceptionSettings.Count > 0 && cmbImportance.SelectedItem is RuleImportance imp)
                {
                    TmpExceptionSettings[0].Importance = imp;
                }
            };

            // Add controls to the container panel
            this.panel2.Controls.Add(lblImportance);
            this.panel2.Controls.Add(cmbImportance);

            // Apply style dynamically
            if (ActiveConfig.Controller != null && ActiveConfig.Controller.EnableDarkMode)
            {
                lblImportance.ForeColor = ThemeManager.TextPrimary;
                lblImportance.Font = new Font("Segoe UI", lblImportance.Font.Size, lblImportance.Font.Style);

                cmbImportance.FlatStyle = FlatStyle.Flat;
                cmbImportance.BackColor = ThemeManager.SurfaceColor;
                cmbImportance.ForeColor = ThemeManager.TextPrimary;
                cmbImportance.Font = new Font("Segoe UI", cmbImportance.Font.Size, cmbImportance.Font.Style);
            }
        }

        partial void UpdateImportanceUI()
        {
            if (cmbImportance != null && TmpExceptionSettings.Count > 0)
            {
                cmbImportance.SelectedItem = TmpExceptionSettings[0].Importance;
            }
        }
    }
}
