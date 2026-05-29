using System;
using System.Security;
using System.Runtime.InteropServices;
using pylorak.Utilities;

namespace pylorak.Windows
{
    class TrafficRateMonitor : Disposable
    {
        private readonly IntPtr hQuery;
        private readonly IntPtr hTxCounter;
        private readonly IntPtr hRxCounter;
        private byte[] buffer = Array.Empty<byte>();

        public TrafficRateMonitor()
        {
            _ = NativeMethods.PdhOpenQuery(null, IntPtr.Zero, out hQuery);
            _ = NativeMethods.PdhAddEnglishCounter(hQuery, "\\Network Interface(*)\\Bytes Sent/Sec", IntPtr.Zero, out hTxCounter);
            _ = NativeMethods.PdhAddEnglishCounter(hQuery, "\\Network Interface(*)\\Bytes Received/Sec", IntPtr.Zero, out hRxCounter);
            _ = NativeMethods.PdhCollectQueryData(hQuery);
        }

        public void Update()
        {
            _ = NativeMethods.PdhCollectQueryData(hQuery);
            BytesSentPerSec = ReadLongCounter(hTxCounter, false);
            BytesReceivedPerSec = ReadLongCounter(hRxCounter, false);
            // [FoxWall Enhancement] - Measure Physical Adapters Only
            PhysicalBytesSentPerSec = ReadLongCounter(hTxCounter, true);
            PhysicalBytesReceivedPerSec = ReadLongCounter(hRxCounter, true);
            // [FoxWall Enhancement] - End of Physical Adapters Only
        }

        private long ReadLongCounter(IntPtr hCounter, bool physicalOnly = false)
        {
            const int PDH_CSTATUS_VALID_DATA = 0;
            const int PDH_CSTATUS_NEW_DATA = 1;

            long ret = 0;

            int size = 0;
            int count = 0;
            _ = NativeMethods.PdhGetFormattedCounterArray(hCounter, PDH_FMT.LARGE | PDH_FMT.NOSCALE | PDH_FMT.NOCAP100, ref size, ref count, IntPtr.Zero);

            if (size > buffer.Length)
                buffer = new byte[size];

            unsafe
            {
                fixed (byte* bufferPtr = buffer)
                {
                    _ = NativeMethods.PdhGetFormattedCounterArray(hCounter, PDH_FMT.LARGE | PDH_FMT.NOSCALE | PDH_FMT.NOCAP100, ref size, ref count, (IntPtr)bufferPtr);

                    int stride = (IntPtr.Size == 8) ? 24 : 16;
                    int statusOffset = IntPtr.Size;
                    int largeValueOffset = IntPtr.Size * 2;
                    for (int i = 0; i < count; ++i)
                    {
                        byte* itemPtr = bufferPtr + i * stride;
                        int CStatus = *(int*)(itemPtr + statusOffset);
                        if ((CStatus == PDH_CSTATUS_NEW_DATA) || (CStatus == PDH_CSTATUS_VALID_DATA))
                        {
                            // [FoxWall Enhancement] - Filter Out Non-Physical/Virtual/VPN Interfaces
                            if (physicalOnly)
                            {
                                IntPtr namePtr = *(IntPtr*)itemPtr;
                                if (namePtr != IntPtr.Zero)
                                {
                                    string name = System.Runtime.InteropServices.Marshal.PtrToStringUni(namePtr) ?? "";
                                    string lowerName = name.ToLowerInvariant();
                                    if (lowerName.Contains("loopback") ||
                                        lowerName.Contains("pseudo") ||
                                        lowerName.Contains("virtual") ||
                                        lowerName.Contains("vpn") ||
                                        lowerName.Contains("tap") ||
                                        lowerName.Contains("tun") ||
                                        lowerName.Contains("vmware") ||
                                        lowerName.Contains("vbox") ||
                                        lowerName.Contains("virtualbox") ||
                                        lowerName.Contains("hyper-v") ||
                                        lowerName.Contains("vethernet") ||
                                        lowerName.Contains("wan miniport") ||
                                        lowerName.Contains("teredo") ||
                                        lowerName.Contains("isatap") ||
                                        lowerName.Contains("wi-fi direct") ||
                                        lowerName.Contains("microsoft adapter"))
                                    {
                                        continue;
                                    }
                                }
                            }
                            // [FoxWall Enhancement] - End of Filter Out Non-Physical/Virtual/VPN Interfaces

                            ret += *(long*)(itemPtr + largeValueOffset);
                        }
                    }

                }
            }

            return ret;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                // Release managed resources
            }

            _ = NativeMethods.PdhCloseQuery(hQuery);
            base.Dispose(disposing);
        }

        public long BytesSentPerSec { get; private set; }
        public long BytesReceivedPerSec { get; private set; }
        // [FoxWall Enhancement] - Physical Speed Properties
        public long PhysicalBytesSentPerSec { get; private set; }
        public long PhysicalBytesReceivedPerSec { get; private set; }
        // [FoxWall Enhancement] - End of Physical Speed Properties

#if false   // Not used due to inability to compile without platform-dependence
        [StructLayout(LayoutKind.Explicit)]
        private struct PDH_FMT_COUNTERVALUE
        {
            const int PTRSIZE = IntPtr.Size;    // the problematic line

            [FieldOffset(0)]
            public int CStatus;
            [FieldOffset(PTRSIZE)]
            public int longValue;
            [FieldOffset(PTRSIZE)]
            public double doubleValue;
            [FieldOffset(PTRSIZE)]
            public long largeValue;
            [FieldOffset(PTRSIZE)]
            public IntPtr AnsiStringValue;
            [FieldOffset(PTRSIZE)]
            public IntPtr WideStringValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_ITEM
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string szName;
            public PDH_FMT_COUNTERVALUE FmtValue;
        }
#endif

        [Flags]
        private enum PDH_FMT
        {
            DOUBLE = 0x00000200,
            LARGE = 0x00000400,
            LONG = 0x00000100,
            NOSCALE = 0x00001000,
            NOCAP100 = 0x00008000,
            Scale1000 = 0x00002000
        }

        [SuppressUnmanagedCodeSecurity]
        private static class NativeMethods
        {
            [DllImport("pdh", CharSet = CharSet.Unicode)]
            public static extern int PdhOpenQuery(string? szDataSource, IntPtr dwUserData, [Out] out IntPtr phQuery);

            [DllImport("pdh", CharSet = CharSet.Unicode)]
            public static extern int PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, [Out] out IntPtr phCounter);

            [DllImport("pdh", CharSet = CharSet.Unicode)]
            public static extern int PdhCollectQueryData(IntPtr hQuery);

            [DllImport("pdh", CharSet = CharSet.Unicode)]
            public static extern int PdhGetFormattedCounterArray(IntPtr hCounter, PDH_FMT dwFormat, ref int lpdwBufferSize, ref int lpdwItemCount, IntPtr ItemBuffer);

            [DllImport("pdh", CharSet = CharSet.Unicode)]
            public static extern int PdhCloseQuery(IntPtr hQuery);
        }
    }
}
