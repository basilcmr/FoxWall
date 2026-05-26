using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class SettingsForm
    {
        private Button btnImportExceptions = null!;
        private Button btnExportExceptions = null!;

        private void InitializeFoxWallEnhancements()
        {
            // 1. Dynamically add Dark Mode checkbox
            this.chkEnableDarkMode = new CheckBox();
            this.chkEnableDarkMode.Text = "Enable Dark Mode";
            this.chkEnableDarkMode.AutoSize = true;
            this.chkEnableDarkMode.Name = "chkEnableDarkMode";
            this.tableLayoutPanel1.Controls.Add(this.chkEnableDarkMode, 3, 4);
            this.tableLayoutPanel1.SetColumnSpan(this.chkEnableDarkMode, 2);

            // 2. Programmatically create and add Import / Export buttons on Application Exceptions tab (tabPage3)
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

            // 3. Add FoxWall version line and shift other labels down programmatically to prevent overlap on tabPage4
            this.lblVersion.Text = string.Format(CultureInfo.CurrentCulture, "{0} {1}\nFoxWall 1.0.2", this.lblVersion.Text, Application.ProductVersion);
            this.label12.Top += 15;
            this.label6.Top += 15;
            this.lblAboutHomepageLink.Top += 15;
            this.lblLinkLicense.Top += 15;
            this.lblLinkAttributions.Top += 15;

            // 4. Apply themes
            ThemeManager.Apply(this);
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
