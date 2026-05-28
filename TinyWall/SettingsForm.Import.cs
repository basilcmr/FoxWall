using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class SettingsForm
    {
        private Button btnImportExceptions = null!;
        private Button btnExportExceptions = null!;
        private Button btnCopyExceptions = null!;

        private void InitializeFoxWallEnhancements()
        {
            // 1. Dynamically add Dark Mode checkbox
            this.chkEnableDarkMode = new CheckBox();
            this.chkEnableDarkMode.Text = "Enable Dark Mode";
            this.chkEnableDarkMode.AutoSize = true;
            this.chkEnableDarkMode.Name = "chkEnableDarkMode";
            this.tableLayoutPanel1.Controls.Add(this.chkEnableDarkMode, 3, 4);
            this.tableLayoutPanel1.SetColumnSpan(this.chkEnableDarkMode, 2);

            // 2. Programmatically create and add Import / Export / Copy buttons on Application Exceptions tab (tabPage3)
            this.btnImportExceptions = new Button();
            this.btnImportExceptions.Image = GlobalInstances.ImportBtnIcon;
            this.btnImportExceptions.Text = "Import Config";
            this.btnImportExceptions.Location = new Point(553, 265);
            this.btnImportExceptions.Size = new Size(140, 36);
            this.btnImportExceptions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnImportExceptions.TextImageRelation = TextImageRelation.ImageBeforeText;
            this.btnImportExceptions.Click += (s, e) => this.HandleImportCustom();
            this.tabPage3.Controls.Add(this.btnImportExceptions);

            this.btnExportExceptions = new Button();
            this.btnExportExceptions.Image = GlobalInstances.ExportBtnIcon;
            this.btnExportExceptions.Text = "Export Config";
            this.btnExportExceptions.Location = new Point(553, 307);
            this.btnExportExceptions.Size = new Size(140, 36);
            this.btnExportExceptions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnExportExceptions.TextImageRelation = TextImageRelation.ImageBeforeText;
            this.btnExportExceptions.Click += (s, e) => this.btnExport_Click(s, e);
            this.tabPage3.Controls.Add(this.btnExportExceptions);

            this.btnCopyExceptions = new Button();
            this.btnCopyExceptions.Image = GlobalInstances.ExportBtnIcon; // Scale and reuse export icon for copy/clipboard
            this.btnCopyExceptions.Text = "Copy Clipboard";
            this.btnCopyExceptions.Location = new Point(553, 349);
            this.btnCopyExceptions.Size = new Size(140, 36);
            this.btnCopyExceptions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnCopyExceptions.TextImageRelation = TextImageRelation.ImageBeforeText;
            this.btnCopyExceptions.Click += (s, e) => this.HandleCopyToClipboardClick();
            this.tabPage3.Controls.Add(this.btnCopyExceptions);

            // 3. Programmatically create and add ContextMenuStrip for listApplications
            var listContextMenu = new ContextMenuStrip();
            
            var mnuOpenFileLocation = new ToolStripMenuItem("Open File Location", null);
            mnuOpenFileLocation.Click += (s, e) => this.HandleOpenFileLocationClick();
            
            var mnuVerifySignature = new ToolStripMenuItem("Verify Digital Signature...", null);
            mnuVerifySignature.Click += (s, e) => this.HandleVerifySignatureClick();
            
            var mnuVirusTotal = new ToolStripMenuItem("Check Hash on VirusTotal...", null);
            mnuVirusTotal.Click += (s, e) => this.HandleVirusTotalClick();
            
            var mnuSearchGoogle = new ToolStripMenuItem("Search on Google for safety...", GlobalInstances.WebBtnIcon);
            mnuSearchGoogle.Click += (s, e) => this.HandleGoogleSearchClick();
            
            var mnuQuickPolicy = new ToolStripMenuItem("Quick Toggle Policy", null);
            var mnuQuickAllow = new ToolStripMenuItem("Quick Allow (Unrestricted)", GlobalInstances.ApplyBtnIcon);
            mnuQuickAllow.Click += (s, e) => this.HandleQuickPolicyClick(PolicyType.Unrestricted);
            var mnuQuickBlock = new ToolStripMenuItem("Quick Block (Hard Block)", GlobalInstances.CancelBtnIcon);
            mnuQuickBlock.Click += (s, e) => this.HandleQuickPolicyClick(PolicyType.HardBlock);
            var mnuQuickBlockInherit = new ToolStripMenuItem("Quick Block (Hard Block + Inherit)", GlobalInstances.CancelBtnIcon);
            mnuQuickBlockInherit.Click += (s, e) => this.HandleQuickPolicyClick(PolicyType.HardBlock, true);
            mnuQuickPolicy.DropDownItems.Add(mnuQuickAllow);
            mnuQuickPolicy.DropDownItems.Add(mnuQuickBlock);
            mnuQuickPolicy.DropDownItems.Add(mnuQuickBlockInherit);
            
            var mnuAuditSockets = new ToolStripMenuItem("Audit Active Sockets...", GlobalInstances.UpdateBtnIcon);
            mnuAuditSockets.Click += (s, e) => this.HandleAuditSocketsClick();

            var mnuCopyToClipboard = new ToolStripMenuItem("Copy to Clipboard...", GlobalInstances.ExportBtnIcon);
            mnuCopyToClipboard.Click += (s, e) => this.HandleCopyToClipboardClick();

            listContextMenu.Items.Add(mnuOpenFileLocation);
            listContextMenu.Items.Add(mnuVerifySignature);
            listContextMenu.Items.Add(mnuVirusTotal);
            listContextMenu.Items.Add(mnuSearchGoogle);
            listContextMenu.Items.Add(new ToolStripSeparator());
            listContextMenu.Items.Add(mnuQuickPolicy);
            listContextMenu.Items.Add(mnuAuditSockets);
            listContextMenu.Items.Add(new ToolStripSeparator());
            listContextMenu.Items.Add(mnuCopyToClipboard);

            listContextMenu.Opening += (s, e) =>
            {
                int count = this.listApplications.SelectedIndices.Count;
                bool isSingle = count == 1;
                
                bool isFileSubj = false;
                if (isSingle)
                {
                    ListViewItem li = FilteredExceptionItems[this.listApplications.SelectedIndices[0]];
                    FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;
                    isFileSubj = ex.Subject is ExecutableSubject || ex.Subject is ServiceSubject;
                }
                
                mnuOpenFileLocation.Enabled = isSingle && isFileSubj;
                mnuVerifySignature.Enabled = isSingle && isFileSubj;
                mnuVirusTotal.Enabled = isSingle && isFileSubj;
                mnuSearchGoogle.Enabled = isSingle;
                mnuQuickPolicy.Enabled = count > 0;
                mnuAuditSockets.Enabled = isSingle && isFileSubj;
                mnuCopyToClipboard.Enabled = count > 0;
            };

            this.listApplications.ContextMenuStrip = listContextMenu;

            // 4. Add FoxWall version line and shift other labels down programmatically to prevent overlap on tabPage4
            this.lblVersion.Text = string.Format(CultureInfo.CurrentCulture, "{0} {1}\nFoxWall 1.1.4", this.lblVersion.Text, Application.ProductVersion);
            this.label12.Top += 15;
            this.label6.Top += 15;
            this.lblAboutHomepageLink.Top += 15;
            this.lblLinkLicense.Top += 15;
            this.lblLinkAttributions.Top += 15;

            // 5. Apply themes
            ThemeManager.Apply(this);

            // Apply theme to our custom programmatic ContextMenu
            listContextMenu.Renderer = ThemeManager.GetToolStripRenderer();
            foreach (ToolStripItem item in listContextMenu.Items)
            {
                item.ForeColor = ThemeManager.TextPrimary;
                item.BackColor = ThemeManager.BackgroundColor;
                if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
                {
                    foreach (ToolStripItem subItem in dropDownItem.DropDownItems)
                    {
                        subItem.ForeColor = ThemeManager.TextPrimary;
                        subItem.BackColor = ThemeManager.BackgroundColor;
                    }
                }
            }

            // 6. Initialize customizable columns
            InitializeColumnCustomization();
        }

        private void HandleOpenFileLocationClick()
        {
            if (listApplications.SelectedIndices.Count != 1) return;

            ListViewItem li = FilteredExceptionItems[listApplications.SelectedIndices[0]];
            FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;

            string rawPath = "";
            if (ex.Subject is ExecutableSubject exeSubj)
                rawPath = exeSubj.ExecutablePath;
            else if (ex.Subject is ServiceSubject srvSubj)
                rawPath = srvSubj.ExecutablePath;

            if (string.IsNullOrEmpty(rawPath)) return;

            try
            {
                string resolvedPath = WildcardHelper.ResolveWildcardPath(rawPath);
                if (System.IO.File.Exists(resolvedPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{resolvedPath}\"");
                }
                else
                {
                    MessageBox.Show(this, "The file does not exist or the wildcard could not be resolved:\n" + resolvedPath, "FoxWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception exx)
            {
                MessageBox.Show(this, "Could not open file location: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleVerifySignatureClick()
        {
            if (listApplications.SelectedIndices.Count != 1) return;

            ListViewItem li = FilteredExceptionItems[listApplications.SelectedIndices[0]];
            FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;

            string rawPath = "";
            if (ex.Subject is ExecutableSubject exeSubj)
                rawPath = exeSubj.ExecutablePath;
            else if (ex.Subject is ServiceSubject srvSubj)
                rawPath = srvSubj.ExecutablePath;

            if (string.IsNullOrEmpty(rawPath)) return;

            try
            {
                string resolvedPath = WildcardHelper.ResolveWildcardPath(rawPath);
                if (!System.IO.File.Exists(resolvedPath))
                {
                    MessageBox.Show(this, "The file does not exist or the wildcard could not be resolved:\n" + resolvedPath, "FoxWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var tempSubj = new ExecutableSubject(resolvedPath);
                bool isSigned = tempSubj.IsSigned;
                bool certValid = tempSubj.CertValid;
                string certSubject = tempSubj.CertSubject ?? "Unknown / Unsigned";

                using (var sigForm = new SignatureDetailsForm(resolvedPath, isSigned, certValid, certSubject))
                {
                    sigForm.ShowDialog(this);
                }
            }
            catch (Exception exx)
            {
                MessageBox.Show(this, "Could not verify digital signature: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleVirusTotalClick()
        {
            if (listApplications.SelectedIndices.Count != 1) return;

            ListViewItem li = FilteredExceptionItems[listApplications.SelectedIndices[0]];
            FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;

            string rawPath = "";
            if (ex.Subject is ExecutableSubject exeSubj)
                rawPath = exeSubj.ExecutablePath;
            else if (ex.Subject is ServiceSubject srvSubj)
                rawPath = srvSubj.ExecutablePath;

            if (string.IsNullOrEmpty(rawPath)) return;

            try
            {
                string resolvedPath = WildcardHelper.ResolveWildcardPath(rawPath);
                if (!System.IO.File.Exists(resolvedPath))
                {
                    MessageBox.Show(this, "The file does not exist or the wildcard could not be resolved:\n" + resolvedPath, "FoxWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string hash = Hasher.HashFileSha1(resolvedPath);
                if (!string.IsNullOrEmpty(hash))
                {
                    string url = $"https://www.virustotal.com/gui/search/{hash}";
                    var psi = new ProcessStartInfo(url) { UseShellExecute = true };
                    Process.Start(psi)?.Dispose();
                }
                else
                {
                    MessageBox.Show(this, "Failed to compute SHA-1 hash for the selected file.", "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception exx)
            {
                MessageBox.Show(this, "Could not open VirusTotal: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleQuickPolicyClick(PolicyType policyType, bool? childProcessesInherit = null)
        {
            if (listApplications.SelectedIndices.Count == 0) return;

            try
            {
                foreach (int idx in listApplications.SelectedIndices)
                {
                    ListViewItem li = FilteredExceptionItems[idx];
                    FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;

                    if (policyType == PolicyType.HardBlock)
                    {
                        ex.Policy = HardBlockPolicy.Instance;
                    }
                    else if (policyType == PolicyType.Unrestricted)
                    {
                        ex.Policy = new UnrestrictedPolicy() { LocalNetworkOnly = false };
                    }

                    if (childProcessesInherit.HasValue)
                    {
                        ex.ChildProcessesInherit = childProcessesInherit.Value;
                    }
                }

                RebuildExceptionsList();
            }
            catch (Exception exx)
            {
                MessageBox.Show(this, "Could not update policy: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleAuditSocketsClick()
        {
            if (listApplications.SelectedIndices.Count != 1) return;

            ListViewItem li = FilteredExceptionItems[listApplications.SelectedIndices[0]];
            FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;

            string rawPath = "";
            if (ex.Subject is ExecutableSubject exeSubj)
                rawPath = exeSubj.ExecutablePath;
            else if (ex.Subject is ServiceSubject srvSubj)
                rawPath = srvSubj.ExecutablePath;

            if (string.IsNullOrEmpty(rawPath)) return;

            try
            {
                string resolvedPath = WildcardHelper.ResolveWildcardPath(rawPath);
                using (var connForm = new ConnectionsForm(GlobalInstances.TinyWallControllerInstance!))
                {
                    connForm.PathFilter = resolvedPath;
                    connForm.ShowDialog(this);
                }
            }
            catch (Exception exx)
            {
                MessageBox.Show(this, "Could not open socket auditor: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleGoogleSearchClick()
        {
            if (listApplications.SelectedIndices.Count != 1) return;

            ListViewItem li = FilteredExceptionItems[listApplications.SelectedIndices[0]];
            FirewallExceptionV3 ex = (FirewallExceptionV3)li.Tag;

            string exeName = "";
            if (ex.Subject is ExecutableSubject exeSubj)
            {
                exeName = exeSubj.ExecutableName;
            }
            else if (ex.Subject is AppContainerSubject uwpSubj)
            {
                exeName = uwpSubj.DisplayName;
            }
            else
            {
                exeName = ex.Subject.ToString();
            }

            if (string.IsNullOrEmpty(exeName)) return;

            try
            {
                string query = $"is {exeName} safe legitimate or malware virus";
                string url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
                var psi = new ProcessStartInfo(url) { UseShellExecute = true };
                Process.Start(psi)?.Dispose();
            }
            catch (Exception exx)
            {
                MessageBox.Show(this, "Could not open web browser: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleCopyToClipboardClick()
        {
            bool hasSelection = listApplications.SelectedIndices.Count > 0;
            using (var choiceForm = new CopySelectionForm(hasSelection))
            {
                if (choiceForm.ShowDialog(this) == DialogResult.OK)
                {
                    var sb = new System.Text.StringBuilder();

                    // 1. Build Header Row dynamically based on selected checkboxes
                    var headers = new List<string>();
                    if (choiceForm.ChkApplication.Checked) headers.Add("Application");
                    if (choiceForm.ChkType.Checked) headers.Add("Type");
                    if (choiceForm.ChkImportance.Checked) headers.Add("Importance");
                    if (choiceForm.ChkStatus.Checked) headers.Add("Status");
                    if (choiceForm.ChkLifetime.Checked) headers.Add("Lifetime");
                    if (choiceForm.ChkInherit.Checked) headers.Add("Child Inherit");
                    if (choiceForm.ChkDetails.Checked) headers.Add("Details / Path");
                    if (choiceForm.ChkLastModified.Checked) headers.Add("Last Modified");

                    sb.AppendLine(string.Join("\t", headers));

                    // 2. Determine source items
                    IEnumerable<ListViewItem> targetItems;
                    if (choiceForm.RadSelected.Checked)
                    {
                        var list = new List<ListViewItem>();
                        foreach (int idx in listApplications.SelectedIndices)
                        {
                            list.Add(FilteredExceptionItems[idx]);
                        }
                        targetItems = list;
                    }
                    else
                    {
                        targetItems = FilteredExceptionItems;
                    }

                    // 3. Populate rows
                    foreach (var li in targetItems)
                    {
                        var ex = (FirewallExceptionV3)li.Tag;
                        var row = new List<string>();

                        var exeSubj = ex.Subject as ExecutableSubject;
                        var srvSubj = ex.Subject as ServiceSubject;
                        var uwpSubj = ex.Subject as AppContainerSubject;

                        if (choiceForm.ChkApplication.Checked)
                        {
                            string appName = "";
                            switch (ex.Subject.SubjectType)
                            {
                                case SubjectType.Executable: appName = exeSubj!.ExecutableName; break;
                                case SubjectType.Service: appName = srvSubj!.ServiceName; break;
                                case SubjectType.Global: appName = "All Applications"; break;
                                case SubjectType.AppContainer: appName = uwpSubj!.DisplayName; break;
                            }
                            row.Add(appName);
                        }

                        if (choiceForm.ChkType.Checked)
                        {
                            string typeStr = "";
                            switch (ex.Subject.SubjectType)
                            {
                                case SubjectType.Executable: typeStr = "Executable"; break;
                                case SubjectType.Service: typeStr = "Service"; break;
                                case SubjectType.Global: typeStr = "Global"; break;
                                case SubjectType.AppContainer: typeStr = "UWP App"; break;
                            }
                            row.Add(typeStr);
                        }

                        if (choiceForm.ChkImportance.Checked)
                        {
                            row.Add(ex.Importance.ToString());
                        }

                        if (choiceForm.ChkStatus.Checked)
                        {
                            row.Add(GetStatusString(ex));
                        }

                        if (choiceForm.ChkLifetime.Checked)
                        {
                            row.Add(GetTimerString(ex));
                        }

                        if (choiceForm.ChkInherit.Checked)
                        {
                            row.Add(ex.ChildProcessesInherit ? "Yes" : "No");
                        }

                        if (choiceForm.ChkDetails.Checked)
                        {
                            string details = "";
                            switch (ex.Subject.SubjectType)
                            {
                                case SubjectType.Executable: details = exeSubj!.ExecutablePath; break;
                                case SubjectType.Service: details = srvSubj!.ExecutablePath; break;
                                case SubjectType.Global: details = ""; break;
                                case SubjectType.AppContainer: details = uwpSubj!.PublisherId + ", " + uwpSubj.Publisher; break;
                            }
                            row.Add(details);
                        }

                        if (choiceForm.ChkLastModified.Checked)
                        {
                            row.Add(ex.CreationDate.ToString("yyyy/MM/dd HH:mm"));
                        }

                        sb.AppendLine(string.Join("\t", row));
                    }

                    try
                    {
                        Clipboard.SetText(sb.ToString(), TextDataFormat.Text);
                        MessageBox.Show(this, "Application exception list copied to clipboard successfully.", "FoxWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception exx)
                    {
                        MessageBox.Show(this, "Failed to copy to clipboard: " + exx.Message, "FoxWall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void HandleImportCustom()
        {
            this.ofd.Filter = string.Format(CultureInfo.CurrentCulture, "{0} (*.tws)|*.tws|{1} (*)|*", Resources.Messages.TinyWallSettingsFileFilter, Resources.Messages.AllFilesFileFilter);
            if (this.ofd.ShowDialog(this) == DialogResult.OK)
            {
                ConfigContainer importedConfig;
                try
                {
                    importedConfig = SerializationHelper.DeserializeFromFile(this.ofd.FileName, new ConfigContainer(), true);
                }
                catch
                {
                    // Fail import.
                    MessageBox.Show(this, Resources.Messages.ConfigurationImportError, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Show the premium choice dialog
                using (var choiceForm = new ImportMethodForm())
                {
                    if (choiceForm.ShowDialog(this) == DialogResult.OK)
                    {
                        if (choiceForm.Choice == ImportChoice.Replace)
                        {
                            // Option A: Replace
                            this.TmpConfig = importedConfig;
                            
                            this.InitSettingsUI();
                            
                            MessageBox.Show(this, Resources.Messages.ConfigurationHasBeenImported, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else if (choiceForm.Choice == ImportChoice.Merge)
                        {
                            // Option B: Merge (Additive)
                            
                            // 1. Merge general blocklist flags
                            this.TmpConfig.Service.Blocklists.EnableBlocklists |= importedConfig.Service.Blocklists.EnableBlocklists;
                            this.TmpConfig.Service.Blocklists.EnablePortBlocklist |= importedConfig.Service.Blocklists.EnablePortBlocklist;
                            this.TmpConfig.Service.Blocklists.EnableHostsBlocklist |= importedConfig.Service.Blocklists.EnableHostsBlocklist;
                            
                            // 2. Merge profile settings and exceptions lists
                            foreach (var importedProfile in importedConfig.Service.Profiles)
                            {
                                var existingProfile = this.TmpConfig.Service.Profiles.Find(p => p.ProfileName.Equals(importedProfile.ProfileName, StringComparison.InvariantCultureIgnoreCase));
                                if (existingProfile == null)
                                {
                                    // Profile does not exist, add it in full
                                    this.TmpConfig.Service.Profiles.Add(importedProfile);
                                }
                                else
                                {
                                    // Profile exists, merge its exceptions (built-in logic deduplicates and aggregates rules)
                                    existingProfile.AddExceptions(importedProfile.AppExceptions);
                                }
                            }
                            
                            // Re-normalize in case of any duplicate keys/anomalies
                            this.TmpConfig.Service.ActiveProfile.Normalize();

                            this.InitSettingsUI();
                            
                            MessageBox.Show(this, "Configuration has been merged successfully.", Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        // Custom columns customization support
        private HeaderControlWrapper? headerWrapper;
        private ContextMenuStrip? headerContextMenuStrip;
        private Dictionary<string, int> lastKnownWidths = new();
        private HashSet<int> hiddenColumns = new();

        private void InitializeColumnCustomization()
        {
            // 1. Programmatically insert custom columns
            var columnImportance = new ColumnHeader { Tag = "colImportance", Text = "Importance", Width = 100 };
            var columnStatus = new ColumnHeader { Tag = "colStatus", Text = "Status", Width = 120 };
            var columnLifetime = new ColumnHeader { Tag = "colLifetime", Text = "Lifetime", Width = 90 };
            var columnInherit = new ColumnHeader { Tag = "colInherit", Text = "Child Inherit", Width = 80 };

            this.listApplications.Columns.Insert(2, columnImportance);
            this.listApplications.Columns.Insert(3, columnStatus);
            this.listApplications.Columns.Insert(4, columnLifetime);
            this.listApplications.Columns.Insert(5, columnInherit);

            // 2. Build default lastKnownWidths map
            var defaultWidths = new Dictionary<string, int>
            {
                { "colApplication", 180 },
                { "colType", 80 },
                { "colImportance", 100 },
                { "colStatus", 120 },
                { "colLifetime", 90 },
                { "colInherit", 80 },
                { "colDetails", 200 },
                { "colLastModified", 120 }
            };

            foreach (ColumnHeader col in this.listApplications.Columns)
            {
                string tag = (string)col.Tag;
                int currentWidth = col.Width;

                // Check settings first
                if (ActiveConfig.Controller.SettingsFormAppListColumnWidths.TryGetValue(tag, out int savedWidth))
                {
                    currentWidth = savedWidth;
                }

                if (currentWidth > 0)
                {
                    lastKnownWidths[tag] = currentWidth;
                    col.Width = currentWidth;
                }
                else
                {
                    // Saved as hidden (width 0), default to a non-zero value for when it gets restored
                    lastKnownWidths[tag] = defaultWidths.ContainsKey(tag) ? defaultWidths[tag] : 100;
                    col.Width = 0;
                    hiddenColumns.Add(col.Index);
                }
            }

            // 3. Create context menu for header
            headerContextMenuStrip = new ContextMenuStrip();
            if (ActiveConfig.Controller != null && ActiveConfig.Controller.EnableDarkMode)
            {
                headerContextMenuStrip.Renderer = ThemeManager.GetToolStripRenderer();
            }

            string[] columnTags = { "colApplication", "colType", "colImportance", "colStatus", "colLifetime", "colInherit", "colDetails", "colLastModified" };
            string[] columnNames = { "Application Name", "Exception Type", "Classification / Importance", "Rule Status (Allowed/Blocked)", "Lifetime / Timer", "Child Inherit", "File Path / Details", "Creation Date" };

            for (int i = 0; i < columnTags.Length; i++)
            {
                string tag = columnTags[i];
                string name = columnNames[i];

                var item = new ToolStripMenuItem(name)
                {
                    CheckOnClick = false,
                    Checked = !hiddenColumns.Contains(GetColumnIndexByTag(tag))
                };

                item.Click += (sender, e) => ToggleColumnVisibility(tag, item);

                if (ActiveConfig.Controller != null && ActiveConfig.Controller.EnableDarkMode)
                {
                    item.ForeColor = ThemeManager.TextPrimary;
                    item.BackColor = ThemeManager.BackgroundColor;
                }

                headerContextMenuStrip.Items.Add(item);
            }

            // Hook native subclassing wrapper
            headerWrapper = new HeaderControlWrapper(this.listApplications, headerContextMenuStrip);

            // Hook ListView events to handle width changes cleanly
            this.listApplications.ColumnWidthChanging += ListApplications_ColumnWidthChanging;
            this.listApplications.ColumnWidthChanged += ListApplications_ColumnWidthChanged;
        }

        private int GetColumnIndexByTag(string tag)
        {
            for (int i = 0; i < this.listApplications.Columns.Count; i++)
            {
                if ((string)this.listApplications.Columns[i].Tag == tag)
                    return i;
            }
            return -1;
        }

        private void ToggleColumnVisibility(string colTag, ToolStripMenuItem menuItem)
        {
            ColumnHeader? targetCol = null;
            foreach (ColumnHeader col in listApplications.Columns)
            {
                if ((string)col.Tag == colTag)
                {
                    targetCol = col;
                    break;
                }
            }

            if (targetCol == null) return;

            bool isChecked = !menuItem.Checked;
            menuItem.Checked = isChecked;

            if (isChecked)
            {
                // Show column: restore last known non-zero width
                if (lastKnownWidths.TryGetValue(colTag, out int savedWidth) && savedWidth > 0)
                {
                    targetCol.Width = savedWidth;
                }
                else
                {
                    targetCol.Width = 100; // default backup
                }
                hiddenColumns.Remove(targetCol.Index);
            }
            else
            {
                // Hide column: save current width if non-zero, then set to 0
                if (targetCol.Width > 0)
                {
                    lastKnownWidths[colTag] = targetCol.Width;
                }
                targetCol.Width = 0;
                hiddenColumns.Add(targetCol.Index);
            }

            // Immediately refresh list
            listApplications.Refresh();
        }

        private void ListApplications_ColumnWidthChanging(object? sender, ColumnWidthChangingEventArgs e)
        {
            if (hiddenColumns.Contains(e.ColumnIndex))
            {
                e.Cancel = true;
                e.NewWidth = 0;
            }
        }

        private void ListApplications_ColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.ColumnIndex < this.listApplications.Columns.Count)
            {
                ColumnHeader col = this.listApplications.Columns[e.ColumnIndex];
                string tag = (string)col.Tag;
                if (col.Width > 0)
                {
                    lastKnownWidths[tag] = col.Width;
                }
            }
        }
    }

    // Subclasses ListView header natively to intercept right-clicks safely
    internal class HeaderControlWrapper : NativeWindow
    {
        private readonly ListView listView;
        private readonly ContextMenuStrip contextMenu;

        private const int LVM_GETHEADER = 0x1000 + 31;
        private const int WM_CONTEXTMENU = 0x007B;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public HeaderControlWrapper(ListView listView, ContextMenuStrip contextMenu)
        {
            this.listView = listView;
            this.contextMenu = contextMenu;

            IntPtr headerHandle = SendMessage(listView.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (headerHandle != IntPtr.Zero)
            {
                this.AssignHandle(headerHandle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CONTEXTMENU)
            {
                Point pos = Cursor.Position;
                contextMenu.Show(pos);
                return; // Consume message
            }
            base.WndProc(ref m);
        }
    }
}
