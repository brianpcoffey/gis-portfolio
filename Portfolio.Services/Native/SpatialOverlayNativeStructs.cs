using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct OverlayPointNative
    {
        public double X;
        public double Y;
    }
}
