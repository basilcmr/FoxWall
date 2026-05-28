using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using pylorak.Windows;

namespace pylorak.TinyWall
{
    internal class TrafficHistoryLogger : IDisposable
    {
        private readonly string HistoryDir;
        private readonly TrafficRateMonitor Monitor;
        private readonly Timer LogTimer;
        private readonly object LockObj = new();

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

                DateTime now = DateTime.Now;
                string fileName = $"traffic_{now:yyyy-MM-dd}.csv";
                string filePath = Path.Combine(HistoryDir, fileName);

                lock (LockObj)
                {
                    bool exists = File.Exists(filePath);
                    using var writer = new StreamWriter(filePath, true, Encoding.UTF8);
                    if (!exists)
                    {
                        writer.WriteLine("time,rx,tx");
                    }
                    writer.WriteLine($"{now:yyyy-MM-ddTHH:mm:ss},{rx},{tx}");
                }
            }
            catch { }
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
                            if (parts.Length == 3 && DateTime.TryParse(parts[0], out DateTime time))
                            {
                                if (time >= start && time <= end)
                                {
                                    points.Add(new HistoryPoint
                                    {
                                        Time = parts[0],
                                        Rx = long.TryParse(parts[1], out long rx) ? rx : 0,
                                        Tx = long.TryParse(parts[2], out long tx) ? tx : 0
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
    }
}
