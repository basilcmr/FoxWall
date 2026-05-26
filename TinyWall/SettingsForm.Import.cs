using System;
using System.Globalization;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class SettingsForm
    {
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
