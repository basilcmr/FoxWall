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
        public static readonly Color TextSecondary = Color.FromArgb(180, 180, 180);     // #B4B4B4

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int LVM_GETHEADER = 0x101F;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            int useDarkMode = enabled ? 1 : 0;
            int result = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, 4);
            if (result != 0)
            {
                result = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, 4);
            }
            return result == 0;
        }

        private static void Form_HandleCreated(object? sender, EventArgs e)
        {
            if (sender is Form form)
            {
                try
                {
                    UseImmersiveDarkMode(form.Handle, true);
                }
                catch { }
            }
        }

        private static void TabControl_HandleCreated(object? sender, EventArgs e)
        {
            if (sender is TabControl tabControl)
            {
                try
                {
                    SetWindowTheme(tabControl.Handle, "", "");
                }
                catch { }
            }
        }

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

            // Apply modern dark title bar immediately
            try
            {
                UseImmersiveDarkMode(form.Handle, true);
            }
            catch { }

            // Ensure dark title bar remains applied if form handle is recreated
            form.HandleCreated -= Form_HandleCreated;
            form.HandleCreated += Form_HandleCreated;

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
            // Dynamically upgrade control fonts to Segoe UI for a modern visual feel
            if (ctrl.Font != null && ctrl.Font.Name != "Segoe UI")
            {
                ctrl.Font = new Font("Segoe UI", ctrl.Font.Size, ctrl.Font.Style);
            }

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
                // Enlarge buttons slightly for comfortable spacing and modern look
                if (button.Height < 30)
                {
                    button.Height = 30;
                }

                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = SurfaceColor;
                button.ForeColor = TextPrimary;
                button.FlatAppearance.BorderColor = AccentColor;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 25, 60); // subtle purple tint hover
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 10, 30); // deep purple tint click

                button.MouseEnter -= Button_MouseEnter;
                button.MouseEnter += Button_MouseEnter;
                button.MouseLeave -= Button_MouseLeave;
                button.MouseLeave += Button_MouseLeave;
                button.Paint -= Button_Paint;
                button.Paint += Button_Paint;
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
                groupBox.Paint -= GroupBox_Paint;
                groupBox.Paint += GroupBox_Paint;
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
                try
                {
                    SetWindowTheme(tabControl.Handle, "", "");
                }
                catch { }

                // Ensure the themed border is stripped again if the handle is recreated by WinForms
                tabControl.HandleCreated -= TabControl_HandleCreated;
                tabControl.HandleCreated += TabControl_HandleCreated;

                tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl.DrawItem -= TabControl_DrawItem;
                tabControl.DrawItem += TabControl_DrawItem;

                // Spacious modern horizontal and vertical padding for tab headers
                tabControl.Padding = new Point(20, 8);

                // Make the font of the tab control slightly larger for beautiful readability
                tabControl.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

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
                    IntPtr hHeader = SendMessage(listView.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                    if (hHeader != IntPtr.Zero)
                    {
                        SetWindowTheme(hHeader, "DarkMode_Explorer", null);
                    }
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

            // Paint entire tab rect background as flat BackgroundColor (same as dialog backdrop)
            // This ensures NO grey or bordered squares are visible!
            using (var backBrush = new SolidBrush(BackgroundColor))
            {
                e.Graphics.FillRectangle(backBrush, tabRect.X - 2, tabRect.Y - 2, tabRect.Width + 4, tabRect.Height + 4);
            }

            // Draw tab text centered with correct modern font weight and size
            using (var textBrush = new SolidBrush(isSelected ? TextPrimary : TextSecondary))
            {
                var font = isSelected 
                    ? new Font(tabControl.Font ?? e.Font ?? SystemFonts.DefaultFont, FontStyle.Bold)
                    : tabControl.Font ?? e.Font ?? SystemFonts.DefaultFont;

                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tabPage.Text, font, textBrush, tabRect, sf);
                if (isSelected && font != tabControl.Font)
                {
                    font.Dispose(); // Clean up dynamic bold font!
                }
            }

            // Draw gorgeous bottom accent underline on the selected tab
            if (isSelected)
            {
                using (var accentBrush = new SolidBrush(AccentHighlight))
                {
                    // Draw a 3px thick underline at the bottom of the active tab header
                    var underlineRect = new Rectangle(tabRect.X + 6, tabRect.Bottom - 3, tabRect.Width - 12, 3);
                    e.Graphics.FillRectangle(accentBrush, underlineRect);
                }
            }
        }

        private static void Button_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.FlatAppearance.BorderColor = AccentHighlight;
            }
        }

        private static void Button_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.FlatAppearance.BorderColor = AccentColor;
            }
        }

        private static void GroupBox_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not GroupBox groupBox)
                return;

            // Clear legacy border
            using (var brush = new SolidBrush(BackgroundColor))
            {
                e.Graphics.FillRectangle(brush, groupBox.ClientRectangle);
            }

            // Draw clean flat border
            using (var pen = new Pen(Color.FromArgb(45, 45, 55), 1))
            {
                var rect = new Rectangle(0, 7, groupBox.Width - 1, groupBox.Height - 8);
                e.Graphics.DrawRectangle(pen, rect);
            }

            // Draw header text
            if (!string.IsNullOrEmpty(groupBox.Text))
            {
                var textRect = new Rectangle(10, 0, groupBox.Width - 20, 16);
                var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
                using (var brush = new SolidBrush(BackgroundColor))
                {
                    var textWidth = TextRenderer.MeasureText(groupBox.Text, groupBox.Font).Width;
                    e.Graphics.FillRectangle(brush, 8, 0, textWidth + 4, 15);
                }
                TextRenderer.DrawText(e.Graphics, groupBox.Text, groupBox.Font, textRect, TextPrimary, flags);
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
            if (e.Item?.ListView != null && e.Item.ListView.View == View.Details)
            {
                e.DrawDefault = false;
            }
            else
            {
                e.DrawDefault = true;
            }
        }

        private static void ListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (sender is not ListView listView)
            {
                e.DrawDefault = true;
                return;
            }

            // Draw item background
            bool isSelected = e.Item.Selected;
            Color backColor;
            Color foreColor;

            if (isSelected)
            {
                // Premium selection color (deep purple or classic selection color)
                backColor = Color.FromArgb(70, 40, 110); // Nice dark purple selection background
                foreColor = TextPrimary; // Keep text white and readable!
            }
            else
            {
                // Use the custom background color or item background color
                backColor = e.Item.BackColor == SystemColors.Window ? SurfaceColor : e.Item.BackColor;
                foreColor = e.Item.ForeColor == SystemColors.WindowText ? TextPrimary : e.Item.ForeColor;
            }

            // Paint the background
            using (var brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // Draw text
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            
            // Adjust bounds slightly for padding
            Rectangle textBounds = e.Bounds;
            textBounds.Offset(4, 0);
            textBounds.Width -= 8;

            // Draw icon for the first column if available
            if (e.ColumnIndex == 0 && listView.SmallImageList != null && e.Item.ImageIndex >= 0)
            {
                Image img = listView.SmallImageList.Images[e.Item.ImageIndex];
                int imgY = e.Bounds.Top + (e.Bounds.Height - img.Height) / 2;
                e.Graphics.DrawImage(img, e.Bounds.Left + 4, imgY);
                
                // Adjust text bounds to not overlap with the icon
                textBounds.X += img.Width + 4;
                textBounds.Width -= img.Width + 4;
            }
            else if (e.ColumnIndex == 0 && listView.SmallImageList != null && !string.IsNullOrEmpty(e.Item.ImageKey))
            {
                Image img = listView.SmallImageList.Images[e.Item.ImageKey];
                if (img != null)
                {
                    int imgY = e.Bounds.Top + (e.Bounds.Height - img.Height) / 2;
                    e.Graphics.DrawImage(img, e.Bounds.Left + 4, imgY);
                    
                    textBounds.X += img.Width + 4;
                    textBounds.Width -= img.Width + 4;
                }
            }

            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, textBounds, foreColor, flags);
        }

        private static void Button_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button) return;

            if (!button.Enabled)
            {
                // If disabled, we will custom paint the button background and draw the text in a readable gray color.
                
                // 1. Draw background
                using (var backBrush = new SolidBrush(SurfaceColor))
                {
                    e.Graphics.FillRectangle(backBrush, button.ClientRectangle);
                }

                // 2. Draw border
                using (var borderPen = new Pen(Color.FromArgb(50, 50, 50), 1))
                {
                    var rect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
                    e.Graphics.DrawRectangle(borderPen, rect);
                }

                // 3. Draw text in a readable secondary color
                Color disabledTextColor = Color.FromArgb(120, 120, 120); // nice readable gray text
                
                // Draw text center aligned
                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
                
                // Adjust text rectangle for image relation if there is an image
                Rectangle textRect = button.ClientRectangle;
                if (button.Image != null)
                {
                    ControlPaint.DrawImageDisabled(e.Graphics, button.Image, 6, (button.Height - button.Image.Height) / 2, SurfaceColor);
                    
                    // Adjust textRect
                    textRect = new Rectangle(button.Image.Width + 10, 0, button.Width - button.Image.Width - 15, button.Height);
                    flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
                }

                TextRenderer.DrawText(e.Graphics, button.Text, button.Font, textRect, disabledTextColor, flags);
            }
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

                            // 3. Draw a modern divider line below the entire tab strip
                            using (var linePen = new Pen(Color.FromArgb(45, 45, 55), 1))
                            {
                                g.DrawLine(linePen, 0, tabPage.Top - 1, _tabControl.Width, tabPage.Top - 1);
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
