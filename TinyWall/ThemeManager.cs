using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

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
                groupBox.BackColor = BackgroundColor;
            }
            else if (ctrl is Panel panel)
            {
                panel.BackColor = BackgroundColor;
                panel.ForeColor = TextPrimary;
            }
            else if (ctrl is TableLayoutPanel tableLayoutPanel)
            {
                tableLayoutPanel.BackColor = BackgroundColor;
                tableLayoutPanel.ForeColor = TextPrimary;
            }
            else if (ctrl is FlowLayoutPanel flowLayoutPanel)
            {
                flowLayoutPanel.BackColor = BackgroundColor;
                flowLayoutPanel.ForeColor = TextPrimary;
            }
            else if (ctrl is UserControl userControl)
            {
                userControl.BackColor = BackgroundColor;
                userControl.ForeColor = TextPrimary;
            }
            else if (ctrl is TabControl tabControl)
            {
                tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl.DrawItem -= TabControl_DrawItem;
                tabControl.DrawItem += TabControl_DrawItem;

                if (tabControl.Tag is not TabControlBrush)
                {
                    var brush = new TabControlBrush(tabControl);
                    tabControl.Tag = brush;
                }
            }
            else if (ctrl is TabPage tabPage)
            {
                tabPage.UseVisualStyleBackColor = false;
                tabPage.BackColor = BackgroundColor;
                tabPage.ForeColor = TextPrimary;
            }
            else if (ctrl is ListView listView)
            {
                listView.BackColor = SurfaceColor;
                listView.ForeColor = TextPrimary;
                listView.GridLines = false; // standard WinForms gridlines can be overly harsh in dark mode
                listView.OwnerDraw = true;
                listView.DrawColumnHeader -= ListView_DrawColumnHeader;
                listView.DrawColumnHeader += ListView_DrawColumnHeader;
                listView.DrawItem -= ListView_DrawItem;
                listView.DrawItem += ListView_DrawItem;
                listView.DrawSubItem -= ListView_DrawSubItem;
                listView.DrawSubItem += ListView_DrawSubItem;

                try
                {
                    SetWindowTheme(listView.Handle, "DarkMode_Explorer", null);
                }
                catch { }
            }
            else if (ctrl is CheckedListBox checkedListBox)
            {
                checkedListBox.BackColor = SurfaceColor;
                checkedListBox.ForeColor = TextPrimary;
                checkedListBox.BorderStyle = BorderStyle.FixedSingle;
                try
                {
                    SetWindowTheme(checkedListBox.Handle, "DarkMode_Explorer", null);
                }
                catch { }
            }
            else if (ctrl is ContextMenuStrip contextMenu)
            {
                contextMenu.Renderer = MenuRenderer;
            }
        }

        private static void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
                return;

            TabPage tabPage = tabControl.TabPages[e.Index];
            Rectangle tabRect = tabControl.GetTabRect(e.Index);
            bool isSelected = e.Index == tabControl.SelectedIndex;

            // Paint tab background
            using (var backBrush = new SolidBrush(isSelected ? SurfaceColor : BackgroundColor))
            {
                e.Graphics.FillRectangle(backBrush, tabRect);
            }

            // Draw tab text
            using (var textBrush = new SolidBrush(isSelected ? TextPrimary : TextSecondary))
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
                    // Draw top highlight line to give standard premium modern tab look
                    e.Graphics.DrawLine(borderPen, tabRect.X, tabRect.Y + 1, tabRect.Right, tabRect.Y + 1);
                }
            }
        }

        private static void ListView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            // Paint header background with SurfaceColor
            using (var backBrush = new SolidBrush(SurfaceColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            // Draw a thin separator on the right side of the header
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
            }

            // Draw a thin accent line at the bottom of the headers
            using (var pen = new Pen(AccentColor, 1))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            // Draw the column text
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font ?? SystemFonts.DefaultFont, e.Bounds, TextPrimary, flags);
        }

        private static void ListView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private static void ListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private class TabControlBrush : NativeWindow
        {
            private readonly TabControl _tabControl;

            public TabControlBrush(TabControl tabControl)
            {
                _tabControl = tabControl;
                _tabControl.HandleCreated += (s, e) => {
                    AssignHandle(_tabControl.Handle);
                };
                _tabControl.HandleDestroyed += (s, e) => {
                    ReleaseHandle();
                };
                if (_tabControl.IsHandleCreated)
                {
                    AssignHandle(_tabControl.Handle);
                }
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                // WM_PAINT = 0x000F
                if (m.Msg == 0x000F && _tabControl.TabCount > 0 && _tabControl.SelectedIndex >= 0)
                {
                    TabPage tabPage = _tabControl.TabPages[_tabControl.SelectedIndex];
                    Rectangle lastTabRect = _tabControl.GetTabRect(_tabControl.TabCount - 1);

                    try
                    {
                        using (var g = Graphics.FromHwnd(_tabControl.Handle))
                        using (var brush = new SolidBrush(ThemeManager.BackgroundColor))
                        {
                            // 1. Draw empty tab strip area to the right of the last tab
                            int emptyWidth = _tabControl.Width - lastTabRect.Right;
                            if (emptyWidth > 0)
                            {
                                var emptyStrip = new Rectangle(lastTabRect.Right, 0, emptyWidth, tabPage.Top);
                                g.FillRectangle(brush, emptyStrip);
                            }

                            // 2. Draw thin borders around the tab page to cover the system's light grey border
                            // Left border
                            if (tabPage.Left > 0)
                            {
                                g.FillRectangle(brush, 0, tabPage.Top, tabPage.Left, _tabControl.Height - tabPage.Top);
                            }
                            // Right border
                            int rightWidth = _tabControl.Width - tabPage.Right;
                            if (rightWidth > 0)
                            {
                                g.FillRectangle(brush, tabPage.Right, tabPage.Top, rightWidth, _tabControl.Height - tabPage.Top);
                            }
                            // Bottom border
                            int bottomHeight = _tabControl.Height - tabPage.Bottom;
                            if (bottomHeight > 0)
                            {
                                g.FillRectangle(brush, 0, tabPage.Bottom, _tabControl.Width, bottomHeight);
                            }
                            // Small gap between the tab headers and tabpage top (if any)
                            if (tabPage.Top > lastTabRect.Bottom)
                            {
                                g.FillRectangle(brush, 0, lastTabRect.Bottom, _tabControl.Width, tabPage.Top - lastTabRect.Bottom);
                            }
                        }
                    }
                    catch
                    {
                        // Safe catch to prevent crash if graphics context is lost
                    }
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
