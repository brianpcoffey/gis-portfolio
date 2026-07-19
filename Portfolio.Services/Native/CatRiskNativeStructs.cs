using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct CatLocationNative
    {
        public double Latitude;
        public double Longitude;
        public double InsuredValue;
        public double SiteHazard;
        public double DeductibleRate;
        public double LimitRate;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct CatEventNative
    {
        public double Latitude;
        public double Longitude;
        public double Intensity;
        public double RadiusKm;
        public double AnnualRate;
    }
}
