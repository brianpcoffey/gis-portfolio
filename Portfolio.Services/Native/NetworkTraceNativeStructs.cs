using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct TraceElementNative
    {
        public int Id;
        public int FromNodeId;
        public int ToNodeId;
        public int DeviceType;
        public int IsOpen;
        public int CustomerCount;
    }
}
