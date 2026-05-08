using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct CoordinateNative
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct TriangleNative
    {
        public CoordinateNative A;
        public CoordinateNative B;
        public CoordinateNative C;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct BoundingBoxNative
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;
    }
}
