using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct GraphNodeNative
    {
        public int Id;
        public double Latitude;
        public double Longitude;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct GraphEdgeNative
    {
        public int FromNodeId;
        public int ToNodeId;
        public double Cost;
        public int Bidirectional;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct GraphPathResultNative
    {
        public int Found;
        public double TotalCost;
        public int PathCount;
    }
}
