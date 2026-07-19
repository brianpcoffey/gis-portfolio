namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// One insured location in a policy book (an "exposure").
    /// </summary>
    public class CatLocationDto
    {
        /// <summary>Stable identifier within the book.</summary>
        public int Id { get; set; }

        /// <summary>Human-readable location name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Community the location belongs to, used for exposure rollups.</summary>
        public string Community { get; set; } = string.Empty;

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Total Insured Value (TIV) in dollars.</summary>
        public double InsuredValue { get; set; }

        /// <summary>Baseline site susceptibility from 0 (none) to 1 (extreme).</summary>
        public double SiteHazard { get; set; }

        /// <summary>Deductible as a fraction of TIV (wildfire deductibles are percentage-based).</summary>
        public double DeductibleRate { get; set; }

        /// <summary>Payout cap as a fraction of TIV.</summary>
        public double LimitRate { get; set; }
    }

    /// <summary>
    /// One stochastic catastrophe event in the simulation catalog.
    /// </summary>
    public class CatEventDto
    {
        /// <summary>Stable identifier within the catalog.</summary>
        public int Id { get; set; }

        /// <summary>Epicenter latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Epicenter longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Event severity at the epicenter, from 0 to 1.</summary>
        public double Intensity { get; set; }

        /// <summary>Footprint radius in kilometres; intensity decays linearly to zero at the edge.</summary>
        public double RadiusKm { get; set; }

        /// <summary>Poisson frequency in events per year.</summary>
        public double AnnualRate { get; set; }
    }

    /// <summary>
    /// A book of insured locations with its aggregate exposure.
    /// </summary>
    public class PolicyBookDto
    {
        /// <summary>Display name of the book.</summary>
        public string BookName { get; set; } = string.Empty;

        /// <summary>Insured locations making up the book.</summary>
        public List<CatLocationDto> Locations { get; set; } = [];

        /// <summary>Stochastic event catalog paired with the book.</summary>
        public List<CatEventDto> Events { get; set; } = [];

        /// <summary>Sum of every location's TIV, in dollars.</summary>
        public double TotalInsuredValue { get; set; }

        /// <summary>Number of insured locations.</summary>
        public int LocationCount { get; set; }

        /// <summary>Per-community exposure rollups.</summary>
        public List<CommunityExposureDto> Communities { get; set; } = [];
    }

    /// <summary>
    /// Aggregate exposure for one community within a book.
    /// </summary>
    public class CommunityExposureDto
    {
        /// <summary>Community name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Number of insured locations in the community.</summary>
        public int LocationCount { get; set; }

        /// <summary>Sum of TIV across the community, in dollars.</summary>
        public double TotalInsuredValue { get; set; }

        /// <summary>Mean site hazard across the community, from 0 to 1.</summary>
        public double MeanSiteHazard { get; set; }
    }

    /// <summary>
    /// Request to compute ring accumulation (TIV concentration) across a book.
    /// </summary>
    public class AccumulationRequestDto
    {
        /// <summary>Insured locations to analyse.</summary>
        public List<CatLocationDto> Locations { get; set; } = [];

        /// <summary>Ring radius in kilometres.</summary>
        public double RadiusKm { get; set; } = 3;

        /// <summary>Concentration limit in dollars; rings above this are flagged as breaches.</summary>
        public double ConcentrationLimit { get; set; } = 200_000_000;
    }

    /// <summary>
    /// Accumulated TIV within the ring centred on one location.
    /// </summary>
    public class RingDto
    {
        /// <summary>Identifier of the location at the centre of the ring.</summary>
        public int LocationId { get; set; }

        /// <summary>Sum of TIV for every location inside the ring, including the centre.</summary>
        public double RingTiv { get; set; }

        /// <summary>Number of locations inside the ring, including the centre.</summary>
        public int NeighborCount { get; set; }

        /// <summary>True when the ring TIV exceeds the concentration limit.</summary>
        public bool Breached { get; set; }
    }

    /// <summary>
    /// Result of a ring-accumulation analysis across a policy book.
    /// </summary>
    public class AccumulationResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Ring radius used, in kilometres.</summary>
        public double RadiusKm { get; set; }

        /// <summary>Concentration limit applied, in dollars.</summary>
        public double ConcentrationLimit { get; set; }

        /// <summary>Per-location ring totals, in input order.</summary>
        public List<RingDto> Rings { get; set; } = [];

        /// <summary>Number of rings exceeding the concentration limit.</summary>
        public int BreachCount { get; set; }

        /// <summary>Largest ring TIV observed, in dollars.</summary>
        public double WorstRingTiv { get; set; }

        /// <summary>Identifier of the location with the largest ring TIV.</summary>
        public int WorstLocationId { get; set; }
    }

    /// <summary>
    /// Request to run a Monte Carlo catastrophe loss simulation over a book.
    /// </summary>
    public class SimulationRequestDto
    {
        /// <summary>Insured locations exposed to the event catalog.</summary>
        public List<CatLocationDto> Locations { get; set; } = [];

        /// <summary>Stochastic event catalog to simulate.</summary>
        public List<CatEventDto> Events { get; set; } = [];

        /// <summary>Shape parameter of the vulnerability curve; higher means more damage per unit intensity.</summary>
        public double VulnerabilityAlpha { get; set; } = 3.0;
    }

    /// <summary>
    /// One point on the occurrence exceedance probability (OEP) curve.
    /// </summary>
    public class ExceedancePointDto
    {
        /// <summary>Return period in years (the reciprocal of the annual exceedance rate).</summary>
        public double ReturnPeriod { get; set; }

        /// <summary>Loss exceeded at this return period, in dollars.</summary>
        public double Loss { get; set; }
    }

    /// <summary>
    /// Loss at one of the reported benchmark return periods.
    /// </summary>
    public class ReturnPeriodLossDto
    {
        /// <summary>Return period in years.</summary>
        public double ReturnPeriod { get; set; }

        /// <summary>Interpolated loss at that return period, in dollars.</summary>
        public double Loss { get; set; }
    }

    /// <summary>
    /// The single worst event in the simulated catalog.
    /// </summary>
    public class EventLossDto
    {
        /// <summary>Identifier of the event in the catalog.</summary>
        public int EventId { get; set; }

        /// <summary>Epicenter latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Epicenter longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Footprint radius in kilometres.</summary>
        public double RadiusKm { get; set; }

        /// <summary>Gross loss produced by the event, in dollars.</summary>
        public double Loss { get; set; }

        /// <summary>Number of locations taking a non-zero loss.</summary>
        public int AffectedLocations { get; set; }
    }

    /// <summary>
    /// Result of a catastrophe loss simulation: AAL, PML, and the exceedance probability curve.
    /// </summary>
    public class SimulationResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Number of events simulated.</summary>
        public int EventCount { get; set; }

        /// <summary>Number of locations exposed.</summary>
        public int LocationCount { get; set; }

        /// <summary>Average Annual Loss: the rate-weighted mean event loss, in dollars per year.</summary>
        public double AverageAnnualLoss { get; set; }

        /// <summary>Probable Maximum Loss, conventionally the 1-in-250-year OEP loss, in dollars.</summary>
        public double ProbableMaximumLoss { get; set; }

        /// <summary>The full OEP curve, ordered by ascending return period.</summary>
        public List<ExceedancePointDto> ExceedanceCurve { get; set; } = [];

        /// <summary>Losses at the benchmark return periods (10, 25, 50, 100, 250, 500 years).</summary>
        public List<ReturnPeriodLossDto> ReturnPeriodLosses { get; set; } = [];

        /// <summary>The single largest-loss event in the catalog.</summary>
        public EventLossDto? WorstEvent { get; set; }

        /// <summary>Total annual frequency across the whole catalog, in events per year.</summary>
        public double TotalAnnualRate { get; set; }
    }
}
