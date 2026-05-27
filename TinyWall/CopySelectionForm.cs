using System;
using System.Drawing;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal class CopySelectionForm : Form
    {
        internal CheckBox ChkApplication { get; private set; }
        internal CheckBox ChkType { get; private set; }
        internal CheckBox ChkImportance { get; private set; }
        internal CheckBox ChkStatus { get; private set; }
        internal CheckBox ChkLifetime { get; private set; }
        internal CheckBox ChkInherit { get; private set; }
        internal CheckBox ChkDetails { get; private set; }
        internal CheckBox ChkLastModified { get; private set; }

        internal RadioButton RadAll { get; private set; }
        internal RadioButton RadSelected { get; private set; }

        internal Button BtnCopy { get; private set; }
        internal Button BtnCancel { get; private set; }

        internal CopySelectionForm(bool hasSelection)
        {
            this.Text = "Copy to Clipboard Options";
            this.Size = new Size(340, 330);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            // 1. GroupBox for Columns Selection
            var grpColumns = new GroupBox
            {
                Text = "Include Columns",
                Location = new Point(12, 12),
                Size = new Size(300, 135)
            };

            ChkApplication = new CheckBox { Text = "Application Name", Checked = true, Location = new Point(15, 22), Size = new Size(130, 24) };
            ChkType = new CheckBox { Text = "Exception Type", Checked = true, Location = new Point(155, 22), Size = new Size(130, 24) };
            ChkImportance = new CheckBox { Text = "Importance", Checked = true, Location = new Point(15, 48), Size = new Size(130, 24) };
            ChkStatus = new CheckBox { Text = "Status", Checked = true, Location = new Point(155, 48), Size = new Size(130, 24) };
            ChkLifetime = new CheckBox { Text = "Lifetime / Timer", Checked = true, Location = new Point(15, 74), Size = new Size(130, 24) };
            ChkInherit = new CheckBox { Text = "Child Inherit", Checked = true, Location = new Point(155, 74), Size = new Size(130, 24) };
            ChkDetails = new CheckBox { Text = "Details / Path", Checked = true, Location = new Point(15, 100), Size = new Size(130, 24) };
            ChkLastModified = new CheckBox { Text = "Last Modified", Checked = true, Location = new Point(155, 100), Size = new Size(130, 24) };

            grpColumns.Controls.Add(ChkApplication);
            grpColumns.Controls.Add(ChkType);
            grpColumns.Controls.Add(ChkImportance);
            grpColumns.Controls.Add(ChkStatus);
            grpColumns.Controls.Add(ChkLifetime);
            grpColumns.Controls.Add(ChkInherit);
            grpColumns.Controls.Add(ChkDetails);
            grpColumns.Controls.Add(ChkLastModified);
            this.Controls.Add(grpColumns);

            // 2. GroupBox for Scope Selection
            var grpScope = new GroupBox
            {
                Text = "Copy Scope",
                Location = new Point(12, 155),
                Size = new Size(300, 85)
            };

            RadAll = new RadioButton { Text = "Copy all listed exceptions", Checked = !hasSelection, Location = new Point(15, 22), Size = new Size(250, 24) };
            RadSelected = new RadioButton { Text = "Copy selected exceptions only", Checked = hasSelection, Enabled = hasSelection, Location = new Point(15, 50), Size = new Size(250, 24) };

            grpScope.Controls.Add(RadAll);
            grpScope.Controls.Add(RadSelected);
            this.Controls.Add(grpScope);

            // 3. Action Buttons
            BtnCopy = new Button
            {
                Text = "Copy",
                DialogResult = DialogResult.OK,
                Location = new Point(140, 252),
                Size = new Size(80, 30)
            };
            this.AcceptButton = BtnCopy;

            BtnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(232, 252),
                Size = new Size(80, 30)
            };
            this.CancelButton = BtnCancel;

            this.Controls.Add(BtnCopy);
            this.Controls.Add(BtnCancel);

            // 4. Apply FoxWall theme dynamically (supports dark mode automatically)
            ThemeManager.Apply(this);
        }
    }
}
