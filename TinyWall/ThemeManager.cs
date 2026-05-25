using System;
using System.Drawing;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal static class ThemeManager
    {
        // Premium Dark Theme Palette (Jellyfin purple/magenta glow inspired)
        public static readonly Color BackgroundColor = Color.FromArgb(18, 18, 18);      // #121212
        public static readonly Color SurfaceColor = Color.FromArgb(30, 30, 30);         // #1E1E1E
        public static readonly Color AccentColor = Color.FromArgb(111, 53, 165);         // #6F35A5 (Purple)
        public static readonly Color AccentHighlight = Color.FromArgb(162, 0, 255);      // #A200FF (Glow)
        public static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);       // #FFFFFF
        public static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);     // #A0A0A0

        private static readonly ToolStripRenderer MenuRenderer = new DarkToolStripRenderer();

        public static ToolStripRenderer GetToolStripRenderer()
        {
            return MenuRenderer;
        }

        public static void Apply(Form form)
        {
            if (ActiveConfig.Controller == null || !ActiveConfig.Controller.EnableDarkMode)
                return;

            form.BackColor = BackgroundColor;
            form.ForeColor = TextPrimary;

            ApplyToControls(form.Controls);

            // Attempt to automatically find and theme any ContextMenuStrip fields using reflection
            try
            {
                var fields = form.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (typeof(ContextMenuStrip).IsAssignableFrom(field.FieldType))
                    {
                        if (field.GetValue(form) is ContextMenuStrip contextMenu)
                        {
                            contextMenu.Renderer = MenuRenderer;
                            // Recurse into context menu items
                            foreach (ToolStripItem item in contextMenu.Items)
                            {
                                ApplyToToolStripItem(item);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void ApplyToToolStripItem(ToolStripItem item)
        {
            item.ForeColor = TextPrimary;
            item.BackColor = BackgroundColor;
            if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
            {
                foreach (ToolStripItem subItem in dropDownItem.DropDownItems)
                {
                    ApplyToToolStripItem(subItem);
                }
            }
        }

        private static void ApplyToControls(Control.ControlCollection controls)
        {
            foreach (Control ctrl in controls)
            {
                ApplyToControl(ctrl);
                if (ctrl.HasChildren)
                {
                    ApplyToControls(ctrl.Controls);
                }
            }
        }

        private static void ApplyToControl(Control ctrl)
        {
            // Apply standard text & background behavior depending on control type
            if (ctrl is Label label)
            {
                label.ForeColor = TextPrimary;
                label.BackColor = Color.Transparent;
            }
            else if (ctrl is LinkLabel linkLabel)
            {
                linkLabel.ForeColor = TextPrimary;
                linkLabel.LinkColor = Color.FromArgb(180, 100, 240);
                linkLabel.ActiveLinkColor = Color.Magenta;
                linkLabel.VisitedLinkColor = Color.FromArgb(180, 100, 240);
                linkLabel.BackColor = Color.Transparent;
            }
            else if (ctrl is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = SurfaceColor;
                button.ForeColor = TextPrimary;
                button.FlatAppearance.BorderColor = AccentColor;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 20, 20);
            }
            else if (ctrl is TextBox textBox)
            {
                textBox.BackColor = SurfaceColor;
                textBox.ForeColor = TextPrimary;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (ctrl is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = SurfaceColor;
                comboBox.ForeColor = TextPrimary;
            }
            else if (ctrl is CheckBox checkBox)
            {
                checkBox.ForeColor = TextPrimary;
                checkBox.BackColor = Color.Transparent;
            }
            else if (ctrl is RadioButton radioButton)
            {
                radioButton.ForeColor = TextPrimary;
                radioButton.BackColor = Color.Transparent;
            }
            else if (ctrl is GroupBox groupBox)
            {
                groupBox.ForeColor = TextPrimary;
                groupBox.BackColor = Color.Transparent;
            }
            else if (ctrl is Panel panel)
            {
                panel.BackColor = Color.Transparent;
                panel.ForeColor = TextPrimary;
            }
            else if (ctrl is TableLayoutPanel tableLayoutPanel)
            {
                tableLayoutPanel.BackColor = Color.Transparent;
                tableLayoutPanel.ForeColor = TextPrimary;
            }
            else if (ctrl is TabControl tabControl)
            {
                tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl.DrawItem -= TabControl_DrawItem;
                tabControl.DrawItem += TabControl_DrawItem;
            }
            else if (ctrl is TabPage tabPage)
            {
                tabPage.BackColor = BackgroundColor;
                tabPage.ForeColor = TextPrimary;
            }
            else if (ctrl is ListView listView)
            {
                listView.BackColor = SurfaceColor;
                listView.ForeColor = TextPrimary;
                listView.GridLines = false; // standard WinForms gridlines can be overly harsh in dark mode
            }
            else if (ctrl is CheckedListBox checkedListBox)
            {
                checkedListBox.BackColor = SurfaceColor;
                checkedListBox.ForeColor = TextPrimary;
                checkedListBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (ctrl is ContextMenuStrip contextMenu)
            {
                contextMenu.Renderer = MenuRenderer;
            }
        }

        private static void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
                return;

            TabPage tabPage = tabControl.TabPages[e.Index];
            Rectangle tabRect = tabControl.GetTabRect(e.Index);
            bool isSelected = e.Index == tabControl.SelectedIndex;

            // Paint tab background
            using (var backBrush = new SolidBrush(isSelected ? AccentColor : SurfaceColor))
            {
                e.Graphics.FillRectangle(backBrush, tabRect);
            }

            // Draw tab text
            using (var textBrush = new SolidBrush(TextPrimary))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tabPage.Text, tabControl.Font ?? e.Font ?? SystemFonts.DefaultFont, textBrush, tabRect, sf);
            }

            // Draw glowing accent border on the selected tab
            if (isSelected)
            {
                using (var borderPen = new Pen(AccentHighlight, 2))
                {
                    e.Graphics.DrawRectangle(borderPen, tabRect.X + 1, tabRect.Y + 1, tabRect.Width - 2, tabRect.Height - 2);
                }
            }
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => BackgroundColor;
            public override Color MenuStripGradientBegin => BackgroundColor;
            public override Color MenuStripGradientEnd => BackgroundColor;
            public override Color MenuItemSelected => AccentColor;
            public override Color MenuItemSelectedGradientBegin => AccentColor;
            public override Color MenuItemSelectedGradientEnd => AccentColor;
            public override Color MenuItemPressedGradientBegin => SurfaceColor;
            public override Color MenuItemPressedGradientEnd => SurfaceColor;
            public override Color MenuItemBorder => AccentHighlight;
            public override Color MenuBorder => SurfaceColor;
            public override Color ImageMarginGradientBegin => BackgroundColor;
            public override Color ImageMarginGradientMiddle => BackgroundColor;
            public override Color ImageMarginGradientEnd => BackgroundColor;
            public override Color SeparatorDark => Color.FromArgb(50, 50, 50);
            public override Color SeparatorLight => BackgroundColor;
        }

        private class DarkToolStripRenderer : ToolStripProfessionalRenderer
        {
            public DarkToolStripRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = TextPrimary;
                base.OnRenderItemText(e);
            }
        }
    }
}
