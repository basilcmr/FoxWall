using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TinyWallJellyModeInstaller
{
    public class InstallerForm : Form
    {
        private Panel headerPanel = null!;
        private Label titleLabel = null!;
        private Label subtitleLabel = null!;
        private TextBox logTextBox = null!;
        private Panel bottomPanel = null!;
        private Button btnInstall = null!;
        private Button btnUninstall = null!;
        private Button btnCancel = null!;

        private const string DestDir = @"C:\Program Files (x86)\TinyWall";

        public InstallerForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(525, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "FoxWall Jelly Mode Setup (v1.0.4)";
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.White;

            // 1. Header Panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 75,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            headerPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(111, 53, 165), 2)) // Purple bottom line
                {
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            titleLabel = new Label
            {
                Text = "FoxWall Custom Setup (v1.0.4)",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(15, 12),
                AutoSize = true
            };

            subtitleLabel = new Label
            {
                Text = "Install or completely remove the custom-themed FoxWall Jelly Mode config (v1.0.4).",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(16, 38),
                AutoSize = true
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);

            // 2. Bottom Panel
            bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            bottomPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(111, 53, 165), 2)) // Purple top line
                {
                    e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
                }
            };

            btnInstall = new Button
            {
                Text = "Install / Update",
                Size = new Size(130, 32),
                Location = new Point(15, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(111, 53, 165),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnInstall.FlatAppearance.BorderColor = Color.FromArgb(162, 0, 255);
            btnInstall.Click += async (s, e) => await StartInstall();

            btnUninstall = new Button
            {
                Text = "Uninstall",
                Size = new Size(110, 32),
                Location = new Point(155, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(162, 0, 255),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnUninstall.FlatAppearance.BorderColor = Color.FromArgb(162, 0, 255);
            btnUninstall.Click += async (s, e) => await StartUninstall();

            btnCancel = new Button
            {
                Text = "Close",
                Size = new Size(90, 32),
                Location = new Point(405, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnCancel.Click += (s, e) => this.Close();

            bottomPanel.Controls.Add(btnInstall);
            bottomPanel.Controls.Add(btnUninstall);
            bottomPanel.Controls.Add(btnCancel);

            // 3. Central Log Textbox
            logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9),
                Location = new Point(15, 90),
                Size = new Size(480, 175),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };

            this.Controls.Add(headerPanel);
            this.Controls.Add(logTextBox);
            this.Controls.Add(bottomPanel);

            AppendLog("Ready. Click 'Install / Update' to begin installation, or 'Uninstall' to fully remove.");
        }

        private void AppendLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendLog), message);
                return;
            }
            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool>(SetControlsEnabled), enabled);
                return;
            }
            btnInstall.Enabled = enabled;
            btnUninstall.Enabled = enabled;
            btnCancel.Enabled = enabled;
        }

        private void PerformUninstall(bool silent)
        {
            try
            {
                // 1. Close active tray processes
                AppendLog("Terminating running controller instances...");
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/F /IM TinyWall.exe",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = Process.Start(psi))
                    {
                        p?.WaitForExit(5000);
                    }
                }
                catch { }

                Thread.Sleep(1500);

                // 2. Invoke native uninstaller
                string nativeExe = Path.Combine(DestDir, "TinyWall.exe");
                if (File.Exists(nativeExe))
                {
                    AppendLog("Launching native TinyWall uninstaller engine to remove WFP filters, tasks, and services...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = nativeExe,
                        Arguments = "/uninstall",
                        UseShellExecute = true
                    };
                    try
                    {
                        using (var p = Process.Start(psi))
                        {
                            p?.WaitForExit();
                        }
                        AppendLog("Native uninstallation finished.");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Notice: Native uninstall returned: {ex.Message}");
                    }
                }
                else
                {
                    AppendLog("Native engine not found. Running folder cleanup directly...");
                }

                Thread.Sleep(2000);

                // 3. Clean up folder
                if (Directory.Exists(DestDir))
                {
                    AppendLog("Removing program files folder...");
                    try
                    {
                        Directory.Delete(DestDir, true);
                        AppendLog("Folder cleanup completed.");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Notice: Some files couldn't be deleted immediately: {ex.Message}");
                    }
                }

                AppendLog("Uninstall finished successfully!");
                if (!silent)
                {
                    MessageBox.Show("FoxWall Jelly Mode has been completely uninstalled and all configuration cleaned.", "Uninstall Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Uninstall Error: {ex.Message}");
                if (!silent)
                {
                    MessageBox.Show($"An error occurred during uninstallation:\n\n{ex.Message}", "Uninstall Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task StartInstall()
        {
            SetControlsEnabled(false);
            logTextBox.Clear();
            AppendLog("Starting custom FoxWall 1.0.4 installation...");

            await Task.Run(() =>
            {
                // 1. Perform silent uninstall of previous version if exists to ensure clean state
                string nativeExe = Path.Combine(DestDir, "TinyWall.exe");
                if (File.Exists(nativeExe))
                {
                    AppendLog("Previous installation detected. Running clean uninstallation first...");
                    PerformUninstall(true);
                    AppendLog("Clean uninstallation completed. Proceeding with fresh installation...");
                    Thread.Sleep(2000);
                }

                try
                {
                    // 2. Extract embedded ZIP resource
                    AppendLog("Staging assets...");
                    string tempZipPath = Path.Combine(Path.GetTempPath(), "TinyWallFiles.zip");
                    var assembly = Assembly.GetExecutingAssembly();
                    using (Stream? stream = assembly.GetManifestResourceStream("TinyWallJellyModeInstaller.TinyWallFiles.zip"))
                    {
                        if (stream == null)
                        {
                            AppendLog("Error: Embedded resource TinyWallFiles.zip not found!");
                            return;
                        }

                        using (FileStream fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fs);
                        }
                    }

                    // 3. Extract to Destination
                    AppendLog($"Extracting files to '{DestDir}'...");
                    if (!Directory.Exists(DestDir))
                    {
                        Directory.CreateDirectory(DestDir);
                    }

                    using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string fullPath = Path.Combine(DestDir, entry.FullName);
                            string dirPath = Path.GetDirectoryName(fullPath) ?? DestDir;

                            if (!Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }

                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(fullPath, true);
                            }
                        }
                    }

                    try { File.Delete(tempZipPath); } catch { }

                    // 4. Start Service
                    AppendLog("Starting TinyWall background service...");
                    try
                    {
                        using (var sc = new ServiceController("TinyWall"))
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }
                        AppendLog("Service started successfully.");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Warning: Service start delayed: {ex.Message}");
                    }

                    // 5. Start Tray App
                    AppendLog("Launching controller...");
                    try
                    {
                        string controllerPath = Path.Combine(DestDir, "TinyWall.exe");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = controllerPath,
                            UseShellExecute = true
                        });
                        AppendLog("Controller launched.");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Notice: Launch failed: {ex.Message}");
                    }

                    AppendLog("Installation completed successfully!");
                    MessageBox.Show("FoxWall version 1.0.4 (Custom Jelly Mode) has been successfully installed and started!\n\nEnjoy watching movies on your TV with JellyMode whitelisted!", "Installation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppendLog($"CRITICAL ERROR: {ex.Message}");
                    MessageBox.Show($"An error occurred during installation:\n\n{ex.Message}", "Installation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            SetControlsEnabled(true);
        }

        private async Task StartUninstall()
        {
            var confirm = MessageBox.Show(
                "Are you sure you want to completely uninstall FoxWall Jelly Mode?\n\nThis will remove all firewall rules, start tasks, and delete the binaries.",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes)
                return;

            SetControlsEnabled(false);
            logTextBox.Clear();
            AppendLog("Starting custom FoxWall uninstallation...");

            await Task.Run(() => PerformUninstall(false));

            SetControlsEnabled(true);
        }
    }
}
