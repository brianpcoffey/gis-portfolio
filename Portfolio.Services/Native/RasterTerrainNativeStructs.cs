using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct WeightedPointNative
    {
        public double X;
        public double Y;
        public double Weight;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct RasterExtentNative
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;
    }
}
