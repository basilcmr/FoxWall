using System;
using System.Text.Json.Serialization;

namespace pylorak.TinyWall
{
    // [FoxWall Enhancement] - Start
    public record AutoAskPendingEntry
    {
        public string AppPath { get; set; } = string.Empty;
        public string RemoteIp { get; set; } = string.Empty;
        public int RemotePort { get; set; } = 0;
        public Protocol Protocol { get; set; } = Protocol.Any;
        public RuleDirection Direction { get; set; } = RuleDirection.Out;
    }

    public record TwMessageAutoAskEntries : TwMessage
    {
        public AutoAskPendingEntry[] Entries { get; set; } = Array.Empty<AutoAskPendingEntry>();

        [JsonConstructor]
        public TwMessageAutoAskEntries(AutoAskPendingEntry[] entries) :
            base(MessageType.AUTOASK_PENDING_ENTRIES)
        {
            Entries = entries;
        }

        public static TwMessageAutoAskEntries CreateRequest()
        {
            return new TwMessageAutoAskEntries(Array.Empty<AutoAskPendingEntry>());
        }

        public TwMessageAutoAskEntries CreateResponse(AutoAskPendingEntry[] entries)
        {
            return new TwMessageAutoAskEntries(entries);
        }
    }
    // [FoxWall Enhancement] - End
}
