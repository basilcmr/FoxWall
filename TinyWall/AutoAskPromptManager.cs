using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    // [FoxWall Enhancement] - Start
    public class AutoAskPromptManager
    {
        private static readonly Lazy<AutoAskPromptManager> _instance = new(() => new AutoAskPromptManager());
        public static AutoAskPromptManager Instance => _instance.Value;

        private System.Windows.Forms.Timer? _pollTimer;
        private bool _isShowingPrompt = false;

        private AutoAskPromptManager() { }

        public void Start()
        {
            if (_pollTimer != null) return;

            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 2000; // Poll every 2 seconds
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        public void Stop()
        {
            if (_pollTimer == null) return;

            _pollTimer.Stop();
            _pollTimer.Dispose();
            _pollTimer = null;
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (_isShowingPrompt) return;

            var controller = GlobalInstances.TinyWallControllerInstance;
            if (controller == null) return;

            // Check if server is currently locked
            if (GlobalInstances.Controller.IsServerLocked) return;

            // Fetch pending entries
            AutoAskPendingEntry[] entries;
            try
            {
                entries = GlobalInstances.Controller.GetPendingAutoAskEntries();
            }
            catch
            {
                return;
            }

            if (entries == null || entries.Length == 0) return;

            _isShowingPrompt = true;
            try
            {
                foreach (var entry in entries)
                {
                    using var form = new AutoAskPromptForm(entry);
                    DialogResult res = form.ShowDialog();
                    if (res == DialogResult.OK)
                    {
                        ProcessUserDecision(entry, form.SelectedResult);
                    }
                }
            }
            finally
            {
                _isShowingPrompt = false;
            }
        }

        private void ProcessUserDecision(AutoAskPendingEntry entry, AutoAskPromptForm.PromptResult decision)
        {
            var controller = GlobalInstances.TinyWallControllerInstance;
            if (controller == null) return;

            var newSubject = new ExecutableSubject(entry.AppPath);
            List<FirewallExceptionV3> exceptions = new();

            switch (decision)
            {
                case AutoAskPromptForm.PromptResult.AllowUnrestricted:
                    // Full access
                    exceptions.Add(new FirewallExceptionV3(newSubject, new UnrestrictedPolicy()));
                    break;

                case AutoAskPromptForm.PromptResult.AllowWebOnly:
                    // Web only: Outbound TCP 80 and 443
                    exceptions.Add(new FirewallExceptionV3(newSubject, new TcpUdpPolicy
                    {
                        AllowedRemoteTcpConnectPorts = "80,443"
                    }));
                    break;

                case AutoAskPromptForm.PromptResult.BlockAlways:
                    // Hard block
                    exceptions.Add(new FirewallExceptionV3(newSubject, new HardBlockPolicy()));
                    break;

                case AutoAskPromptForm.PromptResult.BlockOnce:
                default:
                    // Block once: do nothing. WFP naturally blocks it.
                    return;
            }

            // Save exceptions to the service
            if (exceptions.Count > 0)
            {
                controller.AddExceptions(exceptions, false);
            }
        }
    }
    // [FoxWall Enhancement] - End
}
