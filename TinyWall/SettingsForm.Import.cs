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
            var mnuSearchGoogle = new ToolStripMenuItem("Search on Google for safety...", GlobalInstances.WebBtnIcon);
            mnuSearchGoogle.Click += (s, e) => this.HandleGoogleSearchClick();
            
            var mnuCopyToClipboard = new ToolStripMenuItem("Copy to Clipboard...", GlobalInstances.ExportBtnIcon);
            mnuCopyToClipboard.Click += (s, e) => this.HandleCopyToClipboardClick();

            listContextMenu.Items.Add(mnuSearchGoogle);
            listContextMenu.Items.Add(new ToolStripSeparator());
            listContextMenu.Items.Add(mnuCopyToClipboard);

            listContextMenu.Opening += (s, e) =>
            {
                mnuSearchGoogle.Enabled = this.listApplications.SelectedIndices.Count == 1;
                mnuCopyToClipboard.Enabled = this.listApplications.SelectedIndices.Count > 0;
            };

            this.listApplications.ContextMenuStrip = listContextMenu;

            // 4. Add FoxWall version line and shift other labels down programmatically to prevent overlap on tabPage4
            this.lblVersion.Text = string.Format(CultureInfo.CurrentCulture, "{0} {1}\nFoxWall 1.0.3", this.lblVersion.Text, Application.ProductVersion);
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
    }
}
