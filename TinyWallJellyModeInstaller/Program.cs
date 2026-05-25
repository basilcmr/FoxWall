using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;

namespace TinyWallJellyModeInstaller
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // 1. Confirm with user before proceeding
                var result = MessageBox.Show(
                    "This will install the custom-modified TinyWall with Jelly Mode.\n\n" +
                    "It will stop the firewall service, copy files to 'C:\\Program Files (x86)\\TinyWall', and restart it.\n\n" +
                    "Do you want to proceed?",
                    "TinyWall Jelly Mode Installer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result != DialogResult.Yes)
                    return;

                string destDir = @"C:\Program Files (x86)\TinyWall";
                
                // 2. Stop TinyWall Service and terminate tray processes using taskkill to fully unlock files
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

                // Wait another second to ensure files are fully unlocked
                Thread.Sleep(1500);

                // 4. Extract embedded ZIP resource
                string tempZipPath = Path.Combine(Path.GetTempPath(), "TinyWallFiles.zip");
                
                // Read from embedded resources
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("TinyWallJellyModeInstaller.TinyWallFiles.zip"))
                {
                    if (stream == null)
                    {
                        MessageBox.Show("Error: Embedded resource TinyWallFiles.zip not found inside the installer!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    using (FileStream fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                }

                // 5. Unzip to C:\Program Files (x86)\TinyWall (overwriting)
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string fullPath = Path.Combine(destDir, entry.FullName);
                        string dirPath = Path.GetDirectoryName(fullPath);

                        if (!Directory.Exists(dirPath))
                        {
                            Directory.CreateDirectory(dirPath);
                        }

                        // Extract file, overwriting existing
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(fullPath, true);
                        }
                    }
                }

                // Clean up temp ZIP file
                try
                {
                    File.Delete(tempZipPath);
                }
                catch { }

                // 6. Start TinyWall Service
                try
                {
                    using (var sc = new ServiceController("TinyWall"))
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Warning: TinyWall Service could not be started automatically: {ex.Message}\nYou can start it manually inside Services.msc.", "Service Start Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // 7. Start TinyWall Controller (Tray App)
                try
                {
                    string controllerPath = Path.Combine(destDir, "TinyWall.exe");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = controllerPath,
                        UseShellExecute = true
                    });
                }
                catch { }

                MessageBox.Show(
                    "TinyWall Custom Jelly Mode has been successfully installed and started!\n\nEnjoy watching movies on your TV with JellyMode whitelisted!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during installation:\n\n{ex.Message}", "Installation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
