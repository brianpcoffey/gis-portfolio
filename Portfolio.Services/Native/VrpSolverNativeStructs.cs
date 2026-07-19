using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct VrpStopNative
    {
        public double Demand;
        public double ReadyTime;
        public double DueTime;
        public double ServiceTime;
    }
}
