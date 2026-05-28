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

        private string GetPeakTask(long totalBytes)
        {
            if (totalBytes <= 0) return "Idle";

            try
            {
                var procCache = new Dictionary<uint, string>();
                TcpTable tcpTable = NetStat.GetExtendedTcp4Table(false);
                var activeProcesses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
                    }
                }

                if (activeProcesses.Count > 0)
                {
                    var sortedList = new List<KeyValuePair<string, int>>(activeProcesses);
                    sortedList.Sort((x, y) => y.Value.CompareTo(x.Value));

                    var taskList = new List<string>();
                    int count = Math.Min(sortedList.Count, 4);

                    for (int i = 0; i < count; i++)
                    {
                        var pair = sortedList[i];
                        string connText = pair.Value == 1 ? "1 connection" : $"{pair.Value} connections";
                        taskList.Add($"{pair.Key} ({connText})");
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
