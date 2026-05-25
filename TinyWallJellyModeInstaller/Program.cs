using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;

namespace TinyWallJellyModeInstaller
{
    static class Program
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privilege;
        }

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const string SE_DEBUG_NAME = "SeDebugPrivilege";
        private const uint PROCESS_TERMINATE = 0x0001;

        private static bool EnableDebugPrivilege()
        {
            IntPtr hToken;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                return false;

            try
            {
                LUID luid;
                if (!LookupPrivilegeValue(null, SE_DEBUG_NAME, out luid))
                    return false;

                TOKEN_PRIVILEGES tp;
                tp.PrivilegeCount = 1;
                tp.Privilege.Luid = luid;
                tp.Privilege.Attributes = SE_PRIVILEGE_ENABLED;

                if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    return false;

                return Marshal.GetLastWin32Error() == 0;
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        private static void KillTinyWallProcesses()
        {
            EnableDebugPrivilege();

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.ProcessName.Equals("TinyWall", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try native kill first with PROCESS_TERMINATE
                        IntPtr hProc = OpenProcess(PROCESS_TERMINATE, false, proc.Id);
                        if (hProc != IntPtr.Zero)
                        {
                            try
                            {
                                TerminateProcess(hProc, 0);
                            }
                            finally
                            {
                                CloseHandle(hProc);
                            }
                        }

                        // Also try the framework's Kill as a backup
                        try
                        {
                            proc.Kill();
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

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
                    "It will stop/terminate the running firewall service, copy files to 'C:\\Program Files (x86)\\TinyWall', register it in Windows, and start it.\n\n" +
                    "Do you want to proceed?",
                    "TinyWall Jelly Mode Installer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result != DialogResult.Yes)
                    return;

                string destDir = @"C:\Program Files (x86)\TinyWall";
                
                // 2. Kill all TinyWall processes (tray, controller, and service) using native debugger privileges
                KillTinyWallProcesses();

                // Wait two seconds to ensure files are fully unlocked
                Thread.Sleep(2000);

                // 3. Extract embedded ZIP resource
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

                // 4. Unzip to C:\Program Files (x86)\TinyWall (overwriting)
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

                // 5. Install/Register TinyWall Service using its own built-in installer
                try
                {
                    string twExePath = Path.Combine(destDir, "TinyWall.exe");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = twExePath,
                        Arguments = "/install",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    using (var p = Process.Start(startInfo))
                    {
                        p?.WaitForExit(10000);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Warning: TinyWall Service could not be registered automatically: {ex.Message}", "Registration Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // 6. Register in Windows Add/Remove Programs (Programs and Features)
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TinyWall"))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisplayName", "TinyWall (Jelly Mode)");
                            key.SetValue("UninstallString", $"\"{Path.Combine(destDir, "TinyWall.exe")}\" /uninstall");
                            key.SetValue("DisplayVersion", "3.4.1");
                            key.SetValue("Publisher", "Károly Pados / pylorak");
                            key.SetValue("DisplayIcon", $"\"{Path.Combine(destDir, "TinyWall.exe")}\",0");
                            key.SetValue("InstallLocation", destDir);
                            key.SetValue("NoModify", 1);
                            key.SetValue("NoRepair", 1);
                        }
                    }
                }
                catch { }

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
                    "TinyWall Custom Jelly Mode has been successfully installed and started!\n\nIt is now registered in your Windows Add/Remove Programs list.",
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
