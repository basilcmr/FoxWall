using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using pylorak.Windows;

namespace pylorak.TinyWall
{
    public enum PowerAction
    {
        Shutdown,
        Restart,
        Sleep,
        Lock
    }

    public enum TriggerType
    {
        Duration,
        ExactTime,
        Idle,
        Download,
        Jellyfin
    }

    public enum ExecutionMode
    {
        Smart,      // 5-minute graceful save prompt, then force
        Graceful,   // Wait for apps to exit / prompt to save
        Force       // Immediate close and action
    }

    public class PowerScheduler : IDisposable
    {
        private static readonly Lazy<PowerScheduler> _instance = new Lazy<PowerScheduler>(() => new PowerScheduler());
        public static PowerScheduler Instance => _instance.Value;

        // Native API Imports
        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        // State variables
        private readonly object _lock = new object();
        private System.Threading.Timer? _pollTimer;
        
        public bool IsActive { get; private set; }
        public PowerAction Action { get; private set; }
        public TriggerType Trigger { get; private set; }
        public ExecutionMode Mode { get; private set; }
        public bool CanCancel { get; private set; }
        public DateTime TargetTime { get; private set; }
        public int SecondsRemaining { get; private set; }
        
        // Settings / Thresholds
        public int IdleThresholdMinutes { get; private set; }
        public int BandwidthThresholdKbps { get; private set; }
        public int JellyfinCustomPort { get; private set; }
        
        // Smart Hybrid Grace Period
        public int GraceSeconds { get; private set; } = 300;

        // Chain Schedule properties
        public bool HasChainTrigger { get; private set; }
        public TriggerType ChainTrigger { get; private set; }
        public int ChainValue { get; private set; }
        public string? ChainExactTimeStr { get; private set; }

        // Dynamic counters for smart triggers
        private int _idleCounterSeconds;
        private int _bandwidthCounterSeconds;
        private int _jellyfinCounterSeconds;
        private bool _jellyfinHadStream;

        private PowerScheduler()
        {
            JellyfinCustomPort = 8096; // Default Jellyfin HTTP Port
        }

        public void StartSchedule(
            PowerAction action, 
            TriggerType trigger, 
            int value, // Value depends on trigger: seconds for countdown, minutes for idle, kbps for download, seconds for jellyfin
            string? exactTimeStr = null, // Used for ExactTime trigger
            ExecutionMode mode = ExecutionMode.Smart,
            bool canCancel = true,
            bool hasChainTrigger = false,
            TriggerType chainTrigger = TriggerType.Duration,
            int chainValue = 0,
            string? chainExactTimeStr = null,
            int graceSeconds = 300)
        {
            lock (_lock)
            {
                Cancel();

                Action = action;
                Trigger = trigger;
                Mode = mode;
                CanCancel = canCancel;
                GraceSeconds = graceSeconds;
                _idleCounterSeconds = 0;
                _bandwidthCounterSeconds = 0;
                _jellyfinCounterSeconds = 0;
                _jellyfinHadStream = false;

                HasChainTrigger = hasChainTrigger;
                ChainTrigger = chainTrigger;
                ChainValue = chainValue;
                ChainExactTimeStr = chainExactTimeStr;

                DateTime now = DateTime.Now;

                switch (trigger)
                {
                    case TriggerType.Duration:
                        SecondsRemaining = value;
                        TargetTime = now.AddSeconds(value);
                        break;

                    case TriggerType.ExactTime:
                        if (DateTime.TryParse(exactTimeStr, out DateTime parsed))
                        {
                            TargetTime = parsed;
                            if (TargetTime <= now)
                            {
                                TargetTime = TargetTime.AddDays(1); // Set for tomorrow if time is already past
                            }
                            SecondsRemaining = (int)(TargetTime - now).TotalSeconds;
                        }
                        else
                        {
                            throw new ArgumentException("Invalid exact time string format.");
                        }
                        break;

                    case TriggerType.Idle:
                        IdleThresholdMinutes = value;
                        SecondsRemaining = value * 60;
                        TargetTime = now.AddMinutes(value);
                        break;

                    case TriggerType.Download:
                        BandwidthThresholdKbps = value;
                        SecondsRemaining = 180; // Default: require 3 minutes below threshold
                        TargetTime = now.AddMinutes(3);
                        break;

                    case TriggerType.Jellyfin:
                        SecondsRemaining = 300; // Require 5 minutes of no streams
                        TargetTime = now.AddMinutes(5);
                        break;
                }

                IsActive = true;
                _pollTimer = new System.Threading.Timer(PollTick, null, 1000, 1000);
            }
        }

        public void Cancel()
        {
            lock (_lock)
            {
                if (!CanCancel && IsActive)
                {
                    throw new InvalidOperationException("This active schedule is marked as non-cancelable.");
                }

                IsActive = false;
                if (_pollTimer != null)
                {
                    _pollTimer.Dispose();
                    _pollTimer = null;
                }
            }
        }

        private void PollTick(object? state)
        {
            lock (_lock)
            {
                if (!IsActive) return;

                bool shouldExecute = false;

                switch (Trigger)
                {
                    case TriggerType.Duration:
                    case TriggerType.ExactTime:
                        // Jellyfin stream check to pause timer:
                        if (IsJellyfinStreamingActive())
                        {
                            // Postpone target time by 1 second to hold countdown
                            TargetTime = TargetTime.AddSeconds(1);
                        }
                        else
                        {
                            SecondsRemaining--;
                            if (SecondsRemaining <= 0)
                            {
                                shouldExecute = true;
                            }
                        }
                        break;

                    case TriggerType.Idle:
                        int currentIdleSeconds = GetSystemIdleSeconds();
                        if (currentIdleSeconds >= IdleThresholdMinutes * 60)
                        {
                            // If Jellyfin is streaming, wait/postpone the idle shutdown
                            if (!IsJellyfinStreamingActive())
                            {
                                shouldExecute = true;
                            }
                        }
                        else
                        {
                            // Reset countdown
                            SecondsRemaining = IdleThresholdMinutes * 60;
                        }
                        break;

                    case TriggerType.Download:
                        double currentDownloadSpeedBps = GetCurrentDownloadSpeedBps();
                        double thresholdBps = BandwidthThresholdKbps * 1024;

                        if (currentDownloadSpeedBps < thresholdBps)
                        {
                            // If Jellyfin is streaming, don't tick down the countdown
                            if (!IsJellyfinStreamingActive())
                            {
                                _bandwidthCounterSeconds++;
                                SecondsRemaining = Math.Max(0, 180 - _bandwidthCounterSeconds);
                                if (SecondsRemaining <= 0)
                                {
                                    shouldExecute = true;
                                }
                            }
                        }
                        else
                        {
                            _bandwidthCounterSeconds = 0;
                            SecondsRemaining = 180;
                        }
                        break;

                    case TriggerType.Jellyfin:
                        bool activeStream = IsJellyfinStreamingActive();
                        if (activeStream)
                        {
                            _jellyfinHadStream = true;
                            _jellyfinCounterSeconds = 0;
                            SecondsRemaining = 300; // Reset countdown
                        }
                        else
                        {
                            // To prevent immediate shutdown if the user just started the watch trigger
                            // and hasn't loaded Jellyfin on TV yet, we allow 5 minutes to start, 
                            // but if a stream was active and now stopped, we definitely shut down after 5 minutes.
                            _jellyfinCounterSeconds++;
                            SecondsRemaining = Math.Max(0, 300 - _jellyfinCounterSeconds);
                            if (SecondsRemaining <= 0)
                            {
                                shouldExecute = true;
                            }
                        }
                        break;
                }

                if (shouldExecute)
                {
                    if (HasChainTrigger)
                    {
                        // Transition to the chained trigger
                        Trigger = ChainTrigger;
                        HasChainTrigger = false; // Prevent infinite loops

                        // Reset dynamic counters
                        _idleCounterSeconds = 0;
                        _bandwidthCounterSeconds = 0;
                        _jellyfinCounterSeconds = 0;
                        _jellyfinHadStream = false;

                        DateTime now = DateTime.Now;

                        switch (Trigger)
                        {
                            case TriggerType.Duration:
                                SecondsRemaining = ChainValue;
                                TargetTime = now.AddSeconds(ChainValue);
                                break;

                            case TriggerType.ExactTime:
                                if (DateTime.TryParse(ChainExactTimeStr, out DateTime parsed))
                                {
                                    TargetTime = parsed;
                                    if (TargetTime <= now)
                                    {
                                        TargetTime = TargetTime.AddDays(1);
                                    }
                                    SecondsRemaining = (int)(TargetTime - now).TotalSeconds;
                                }
                                else
                                {
                                    SecondsRemaining = 300;
                                    TargetTime = now.AddMinutes(5);
                                }
                                break;

                            case TriggerType.Idle:
                                IdleThresholdMinutes = ChainValue;
                                SecondsRemaining = ChainValue * 60;
                                TargetTime = now.AddMinutes(ChainValue);
                                break;

                            case TriggerType.Download:
                                BandwidthThresholdKbps = ChainValue;
                                SecondsRemaining = 180;
                                TargetTime = now.AddMinutes(3);
                                break;

                            case TriggerType.Jellyfin:
                                SecondsRemaining = 300;
                                TargetTime = now.AddMinutes(5);
                                break;
                        }
                    }
                    else
                    {
                        IsActive = false;
                        _pollTimer?.Dispose();
                        _pollTimer = null;
                        ExecutePowerAction();
                    }
                }
            }
        }

        private void ExecutePowerAction()
        {
            try
            {
                switch (Action)
                {
                    case PowerAction.Lock:
                        LockWorkStation();
                        break;

                    case PowerAction.Sleep:
                        if (Mode == ExecutionMode.Smart)
                        {
                            // Smart Sleep: Wait customized grace period, then sleep
                            int delayMs = GraceSeconds * 1000;
                            ThreadPool.QueueUserWorkItem((s) =>
                            {
                                Thread.Sleep(delayMs);
                                Application.SetSuspendState(PowerState.Suspend, true, true);
                            });
                        }
                        else
                        {
                            Application.SetSuspendState(PowerState.Suspend, true, true);
                        }
                        break;

                    case PowerAction.Shutdown:
                        string shutdownArgs = Mode switch
                        {
                            ExecutionMode.Force => "/s /t 0 /f",
                            ExecutionMode.Graceful => "/s /t 0",
                            ExecutionMode.Smart => $"/s /t {GraceSeconds} /f", // Waits grace period, then forces shutdown
                            _ => $"/s /t {GraceSeconds} /f"
                        };
                        Process.Start("shutdown.exe", shutdownArgs);
                        break;

                    case PowerAction.Restart:
                        string restartArgs = Mode switch
                        {
                            ExecutionMode.Force => "/r /t 0 /f",
                            ExecutionMode.Graceful => "/r /t 0",
                            ExecutionMode.Smart => $"/r /t {GraceSeconds} /f", // Waits grace period, then forces restart
                            _ => $"/r /t {GraceSeconds} /f"
                        };
                        Process.Start("shutdown.exe", restartArgs);
                        break;
                }
            }
            catch (Exception ex)
            {
                Utils.LogException(ex, Utils.LOG_ID_GUI);
            }
        }

        public bool IsJellyfinStreamingActive()
        {
            try
            {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnections = ipProperties.GetActiveTcpConnections();

                foreach (var conn in tcpConnections)
                {
                    // Check if local port is Jellyfin Port and connection is active/streaming
                    if (conn.LocalEndPoint.Port == 8096 || conn.LocalEndPoint.Port == 8920 || conn.LocalEndPoint.Port == JellyfinCustomPort)
                    {
                        if (conn.State == TcpState.Established)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private int GetSystemIdleSeconds()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (GetLastInputInfo(ref lii))
            {
                int tickCount = Environment.TickCount;
                int idleMs = tickCount - (int)lii.dwTime;
                return Math.Max(0, idleMs / 1000);
            }
            return 0;
        }

        private double GetCurrentDownloadSpeedBps()
        {
            try
            {
                var monitor = GlobalInstances.TinyWallControllerInstance?.GetType()
                    .GetField("TrafficMonitor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(GlobalInstances.TinyWallControllerInstance) as TrafficRateMonitor;
                
                if (monitor != null)
                {
                    return monitor.BytesReceivedPerSec;
                }
            }
            catch { }
            return 0;
        }

        public object GetStatus()
        {
            lock (_lock)
            {
                bool requiresPassword = GlobalInstances.Controller.IsServerLocked;

                return new
                {
                    isActive = IsActive,
                    action = Action.ToString().ToLowerInvariant(),
                    triggerType = Trigger.ToString().ToLowerInvariant(),
                    mode = Mode.ToString().ToLowerInvariant(),
                    canCancel = CanCancel,
                    targetTime = TargetTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    secondsRemaining = SecondsRemaining,
                    requiresPassword = requiresPassword,
                    jellyfinStreaming = IsJellyfinStreamingActive(),
                    hasChainTrigger = HasChainTrigger,
                    chainTrigger = ChainTrigger.ToString().ToLowerInvariant(),
                    chainValue = ChainValue,
                    chainExactTime = ChainExactTimeStr
                };
            }
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
        }
    }
}
