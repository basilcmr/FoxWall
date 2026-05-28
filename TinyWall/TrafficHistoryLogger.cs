using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using pylorak.Windows;
using pylorak.Windows.NetStat;

namespace pylorak.TinyWall
{
    internal class TrafficHistoryLogger : IDisposable
    {
        private readonly string HistoryDir;
        private readonly TrafficRateMonitor Monitor;
        private readonly Timer LogTimer;
        private readonly object LockObj = new();

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherOperationCount;
            public ulong OtherTransferCount;
        }

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private struct IoState
        {
            public ulong Bytes;
            public DateTime Timestamp;
        }

        private readonly Dictionary<uint, IoState> ProcessIoCache = new();

        public long CurrentRx { get; private set; }
        public long CurrentTx { get; private set; }

        public TrafficHistoryLogger()
        {
            this.HistoryDir = Path.Combine(Utils.AppDataPath, "TrafficHistory");
            if (!Directory.Exists(HistoryDir))
            {
                Directory.CreateDirectory(HistoryDir);
            }

            this.Monitor = new TrafficRateMonitor();
            // Poll and log every 5 seconds
            this.LogTimer = new Timer(LogTick, null, 1000, 5000);
        }

        private void LogTick(object? state)
        {
            try
            {
                Monitor.Update();
                long rx = Monitor.BytesReceivedPerSec;
                long tx = Monitor.BytesSentPerSec;

                CurrentRx = rx;
                CurrentTx = tx;

                DateTime now = DateTime.Now;
                string fileName = $"traffic_{now:yyyy-MM-dd}.csv";
                string filePath = Path.Combine(HistoryDir, fileName);

                string peakTask = GetPeakTask(rx + tx);

                lock (LockObj)
                {
                    bool exists = File.Exists(filePath);
                    using var writer = new StreamWriter(filePath, true, Encoding.UTF8);
                    if (!exists)
                    {
                        writer.WriteLine("time,rx,tx,peak_task");
                    }
                    writer.WriteLine($"{now:yyyy-MM-ddTHH:mm:ss},{rx},{tx},{peakTask}");
                }
            }
            catch { }
        }

        private ulong GetProcessIoBytes(uint pid)
        {
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                hProcess = OpenProcess(PROCESS_QUERY_INFORMATION, false, pid);
            }

            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    if (GetProcessIoCounters(hProcess, out var counters))
                    {
                        return counters.ReadTransferCount + counters.WriteTransferCount;
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            return 0;
        }

        private string GetPeakTask(long totalBytes)
        {
            if (totalBytes <= 0) return "Idle";

            try
            {
                var procCache = new Dictionary<uint, string>();
                TcpTable tcpTable = NetStat.GetExtendedTcp4Table(false);
                var activeProcesses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var processPids = new Dictionary<string, HashSet<uint>>(StringComparer.OrdinalIgnoreCase);

                foreach (TcpRow row in tcpTable)
                {
                    if (row.ProcessId == 0) continue;
                    if (!procCache.TryGetValue(row.ProcessId, out string? name))
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)row.ProcessId);
                            name = proc.MainModule?.ModuleName ?? proc.ProcessName;
                        }
                        catch
                        {
                            name = "System / Services";
                        }
                        procCache[row.ProcessId] = name;
                    }

                    if (name != "System / Services")
                    {
                        string app = name;
                        if (app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            app = app.Substring(0, app.Length - 4);
                        }
                        if (app.Length > 0)
                        {
                            app = char.ToUpper(app[0]) + app.Substring(1);
                        }

                        if (activeProcesses.ContainsKey(app))
                        {
                            activeProcesses[app]++;
                        }
                        else
                        {
                            activeProcesses[app] = 1;
                        }

                        if (!processPids.TryGetValue(app, out var pids))
                        {
                            pids = new HashSet<uint>();
                            processPids[app] = pids;
                        }
                        pids.Add(row.ProcessId);
                    }
                }

                // Prune dead PIDs from ProcessIoCache to avoid memory leak
                var currentPids = new HashSet<uint>();
                foreach (TcpRow row in tcpTable)
                {
                    if (row.ProcessId != 0) currentPids.Add(row.ProcessId);
                }
                var pidsToRemove = new List<uint>();
                foreach (uint cachedPid in ProcessIoCache.Keys)
                {
                    if (!currentPids.Contains(cachedPid)) pidsToRemove.Add(cachedPid);
                }
                foreach (uint pidToRemove in pidsToRemove)
                {
                    ProcessIoCache.Remove(pidToRemove);
                }

                // Calculate real speeds per process
                var processSpeeds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double totalProcessSpeed = 0;

                foreach (var pair in processPids)
                {
                    string app = pair.Key;
                    double appSpeed = 0;
                    foreach (uint pid in pair.Value)
                    {
                        ulong currentBytes = GetProcessIoBytes(pid);
                        ulong deltaBytes = 0;
                        double elapsedSeconds = 5.0;

                        if (ProcessIoCache.TryGetValue(pid, out var lastState))
                        {
                            if (currentBytes >= lastState.Bytes)
                            {
                                deltaBytes = currentBytes - lastState.Bytes;
                                elapsedSeconds = (DateTime.Now - lastState.Timestamp).TotalSeconds;
                                if (elapsedSeconds <= 0) elapsedSeconds = 5.0;
                            }
                        }

                        ProcessIoCache[pid] = new IoState { Bytes = currentBytes, Timestamp = DateTime.Now };
                        appSpeed += deltaBytes / elapsedSeconds;
                    }

                    processSpeeds[app] = appSpeed;
                    totalProcessSpeed += appSpeed;
                }

                if (activeProcesses.Count > 0)
                {
                    var sortedList = new List<KeyValuePair<string, int>>(activeProcesses);
                    sortedList.Sort((x, y) => y.Value.CompareTo(x.Value));

                    double totalConnections = 0;
                    foreach (var val in activeProcesses.Values)
                        totalConnections += val;

                    var taskList = new List<string>();
                    int count = Math.Min(sortedList.Count, 4);

                    for (int i = 0; i < count; i++)
                    {
                        var pair = sortedList[i];
                        string app = pair.Key;
                        int connCount = pair.Value;

                        double appSpeed = 0;
                        processSpeeds.TryGetValue(app, out appSpeed);

                        double share = 0;
                        if (totalProcessSpeed > 1024)
                        {
                            share = appSpeed / totalProcessSpeed;
                        }
                        else if (totalConnections > 0)
                        {
                            share = (double)connCount / totalConnections;
                        }

                        long allocatedBytes = (long)(totalBytes * share);
                        string connText = connCount == 1 ? "1 connection" : $"{connCount} connections";
                        taskList.Add($"{app} ({connText} - {FormatSpeed(allocatedBytes)})");
                    }
                    return string.Join(";", taskList);
                }
            }
            catch { }

            return "System Services (Active)";
        }

        private static string FormatSpeed(long bytesPerSec)
        {
            double kb = bytesPerSec / 1024.0;
            if (kb > 1024.0)
            {
                return $"{(kb / 1024.0):F1} MiB/s";
            }
            return $"{kb:F1} KiB/s";
        }

        public List<HistoryPoint> GetHistory(DateTime start, DateTime end)
        {
            var points = new List<HistoryPoint>();
            
            try
            {
                lock (LockObj)
                {
                    // Scan all files in the history directory matching traffic_*.csv
                    for (DateTime date = start.Date; date <= end.Date; date = date.AddDays(1))
                    {
                        string fileName = $"traffic_{date:yyyy-MM-dd}.csv";
                        string filePath = Path.Combine(HistoryDir, fileName);
                        if (!File.Exists(filePath)) continue;

                        using var reader = new StreamReader(filePath, Encoding.UTF8);
                        string? header = reader.ReadLine(); // Skip header
                        while (!reader.EndOfStream)
                        {
                            string? line = reader.ReadLine();
                            if (string.IsNullOrEmpty(line)) continue;

                            string[] parts = line.Split(',');
                            if (parts.Length >= 3 && DateTime.TryParse(parts[0], out DateTime time))
                            {
                                if (time >= start && time <= end)
                                {
                                    string peakTask = parts.Length >= 4 ? parts[3] : "System / Idle";
                                    points.Add(new HistoryPoint
                                    {
                                        Time = parts[0],
                                        Rx = long.TryParse(parts[1], out long rx) ? rx : 0,
                                        Tx = long.TryParse(parts[2], out long tx) ? tx : 0,
                                        PeakTask = peakTask
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return points;
        }

        public void Dispose()
        {
            LogTimer.Dispose();
            Monitor.Dispose();
        }
    }

    internal struct HistoryPoint
    {
        public string Time { get; set; }
        public long Rx { get; set; }
        public long Tx { get; set; }
        public string PeakTask { get; set; }
    }
}
