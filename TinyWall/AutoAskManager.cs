using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace pylorak.TinyWall
{
    // [FoxWall Enhancement] - Start
    public class AutoAskManager
    {
        private static readonly Lazy<AutoAskManager> _instance = new(() => new AutoAskManager());
        public static AutoAskManager Instance => _instance.Value;

        private readonly ConcurrentQueue<AutoAskPendingEntry> _pendingQueue = new();
        private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(30);

        private AutoAskManager() { }

        public void HandleBlockedEntry(FirewallLogEntry entry)
        {
            if (string.IsNullOrEmpty(entry.AppPath))
                return;

            string appPath = entry.AppPath!;
            DateTime now = DateTime.UtcNow;

            // Check if we are in cooldown for this process path to prevent spam
            if (_cooldowns.TryGetValue(appPath, out DateTime lastTime))
            {
                if (now - lastTime < CooldownDuration)
                {
                    return;
                }
            }

            // Update cooldown timestamp
            _cooldowns[appPath] = now;

            // Queue the event
            _pendingQueue.Enqueue(new AutoAskPendingEntry
            {
                AppPath = appPath,
                RemoteIp = entry.RemoteIp ?? string.Empty,
                RemotePort = entry.RemotePort,
                Protocol = entry.Protocol,
                Direction = entry.Direction
            });
        }

        public AutoAskPendingEntry[] PopPendingEntries()
        {
            var list = new List<AutoAskPendingEntry>();
            while (_pendingQueue.TryDequeue(out var item))
            {
                list.Add(item);
            }
            return list.ToArray();
        }

        public void Clear()
        {
            while (_pendingQueue.TryDequeue(out _)) { }
            _cooldowns.Clear();
        }
    }
    // [FoxWall Enhancement] - End
}
