using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    // Blittable interop structs that mirror the C struct layout in portfolio_scoring.h.
    // Field order and Pack=8 MUST stay in sync with the #pragma pack(push, 8) block
    // in the header.  All fields are primitive types (double / int) so no marshalling
    // copies are needed; the CLR pins the managed arrays directly.

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct PropertyInputNative
    {
        // Financial
        public double Price;
        public double PropertyTax;       // annual, USD
        public double HoaFee;            // monthly, USD
        public double Utilities;         // monthly, USD

        // Size
        public double LotSqft;
        public int    Bedrooms;
        public double Bathrooms;

        // Commute
        public double CommuteMin;

        // Neighbourhood (0-100 integer scores promoted to double)
        public double SchoolRating;
        public double Walkability;
        public double TransitAccess;
        public double CrimeScore;

        // Investment
        public double FutureAppreciation;
        public double ResalePotential;

        // Condition (0-100)
        public double RoofCondition;
        public double AcCondition;
        public double PlumbingCondition;
        public double ElectricalCondition;
        public int    LastRenovationYear;  // 0 = unknown

        // Amenities / misc
        public double AmenitiesScore;

        // Environmental (0-100)
        public double FloodRisk;
        public double NoiseLevel;

        // GIS
        public double Latitude;
        public double Longitude;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct PreferencesInputNative
    {
        // Hard-filter thresholds
        public double MaxPrice;
        public double MaxMonthlyBudget;
        public int    MinBedrooms;
        public double MinSqft;
        public int    MaxCommuteMin;

        // Importance weights (normalised internally by the kernel)
        public double WAffordability;
        public double WNeighborhood;
        public double WSize;
        public double WAppreciation;
        public double WCondition;
        public double WCommute;
        public double WAmenities;
        public double WTaxUtilities;
        public double WResale;
        public double WEnvironment;

        // Current UTC year – passed in so the kernel is deterministic in tests
        public int CurrentYear;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct ScoreOutputNative
    {
        public double Affordability;
        public double Neighborhood;
        public double Size;
        public double Appreciation;
        public double Condition;
        public double Commute;
        public double Amenities;
        public double TaxUtilities;
        public double Resale;
        public double Environment;
        public double Composite;           // weighted sum, clamped to [0, 100]
        public double EstimatedMonthlyCost;
    }
}
