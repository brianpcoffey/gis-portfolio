namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// One demand point: a location generating emergency calls, weighted by historical
    /// call volume and snapped to a road-network node.
    /// </summary>
    public class DemandPointDto
    {
        /// <summary>Stable identifier within the scenario.</summary>
        public int Id { get; set; }

        /// <summary>Road-network node the demand point sits on.</summary>
        public int NodeId { get; set; }

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Historical annual call volume, used as the demand weight.</summary>
        public double CallVolume { get; set; }
    }

    /// <summary>
    /// One candidate site: a location where a station could be sited. Existing stations
    /// are included as candidates so the optimizer may keep them.
    /// </summary>
    public class CandidateSiteDto
    {
        /// <summary>Stable identifier within the scenario.</summary>
        public int Id { get; set; }

        /// <summary>Road-network node the site sits on.</summary>
        public int NodeId { get; set; }

        /// <summary>Human-readable site name.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>True when the site is an existing station rather than a proposed one.</summary>
        public bool IsExisting { get; set; }
    }

    /// <summary>
    /// The demo response scenario: clustered demand, candidate sites, and the stations
    /// operating today.
    /// </summary>
    public class ResponseScenarioDto
    {
        /// <summary>Human-readable scenario name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Call-weighted demand points across the service area.</summary>
        public List<DemandPointDto> DemandPoints { get; set; } = [];

        /// <summary>Candidate station sites, including the existing stations.</summary>
        public List<CandidateSiteDto> Candidates { get; set; } = [];

        /// <summary>Candidate ids of the stations operating today, used as the baseline.</summary>
        public List<int> ExistingStationIds { get; set; } = [];

        /// <summary>Summed call volume across every demand point.</summary>
        public double TotalCallVolume { get; set; }
    }

    /// <summary>
    /// Request for a drive-time isochrone from one road-network node.
    /// </summary>
    public class IsochroneRequestDto
    {
        /// <summary>Road-network node the apparatus responds from.</summary>
        public int OriginNodeId { get; set; }

        /// <summary>Average travel speed in km/h used to convert distance to minutes.</summary>
        public double AvgSpeedKmh { get; set; } = 40;

        /// <summary>Ascending band upper bounds in minutes; NFPA thresholds are 4, 8 and 12.</summary>
        public List<double> BandMinutes { get; set; } = [];
    }

    /// <summary>
    /// One reachable road-network node with its travel time and isochrone band.
    /// </summary>
    public class IsochroneNodeDto
    {
        /// <summary>Road-network node id.</summary>
        public int NodeId { get; set; }

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Travel time from the origin in minutes.</summary>
        public double Minutes { get; set; }

        /// <summary>Zero-based band index; the band count itself means "beyond the last band".</summary>
        public int BandIndex { get; set; }
    }

    /// <summary>
    /// Drive-time bands over the road network from a single origin.
    /// </summary>
    public class IsochroneResultDto
    {
        /// <summary>True when the native kernel produced the result.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Road-network node the isochrone was computed from.</summary>
        public int OriginNodeId { get; set; }

        /// <summary>Every reachable node with its travel time and band.</summary>
        public List<IsochroneNodeDto> Nodes { get; set; } = [];

        /// <summary>Node count per band; one entry per requested band plus a trailing overflow band.</summary>
        public List<int> BandCounts { get; set; } = [];

        /// <summary>Number of road-network nodes reachable from the origin.</summary>
        public int ReachableNodes { get; set; }

        /// <summary>Number of road-network nodes with no path from the origin.</summary>
        public int UnreachableNodes { get; set; }
    }

    /// <summary>
    /// Request to site a fixed number of stations against weighted demand.
    /// </summary>
    public class OptimizeCoverageRequestDto
    {
        /// <summary>Call-weighted demand points to cover.</summary>
        public List<DemandPointDto> DemandPoints { get; set; } = [];

        /// <summary>Candidate station sites the optimizer may choose from.</summary>
        public List<CandidateSiteDto> Candidates { get; set; } = [];

        /// <summary>Number of stations to site.</summary>
        public int StationCount { get; set; } = 3;

        /// <summary>0 = weighted mean, 1 = weighted 90th percentile, 2 = maximise covered demand.</summary>
        public int ObjectiveMode { get; set; }

        /// <summary>Average apparatus speed in km/h used to convert distance to travel minutes.</summary>
        public double AvgSpeedKmh { get; set; } = 40;

        /// <summary>First NFPA 1710 travel-time threshold in minutes (first-due engine).</summary>
        public double FirstThresholdMinutes { get; set; } = 4;

        /// <summary>Second NFPA 1710 travel-time threshold in minutes (ALS unit).</summary>
        public double SecondThresholdMinutes { get; set; } = 8;

        /// <summary>Maximum Teitz-Bart substitution passes before the search stops.</summary>
        public int MaxIterations { get; set; } = 50;

        /// <summary>Candidate ids of the stations operating today; supplies the baseline comparison.</summary>
        public List<int> ExistingStationIds { get; set; } = [];
    }

    /// <summary>
    /// Demand-weighted response-time statistics for one station configuration.
    /// </summary>
    public class CoverageStatsDto
    {
        /// <summary>Call-weighted mean travel time in minutes.</summary>
        public double MeanMinutes { get; set; }

        /// <summary>Call-weighted median travel time in minutes.</summary>
        public double P50Minutes { get; set; }

        /// <summary>Call-weighted 90th-percentile travel time in minutes — the NFPA 1710 measure.</summary>
        public double P90Minutes { get; set; }

        /// <summary>Percentage of call volume reached within the first threshold.</summary>
        public double PercentWithinFirstThreshold { get; set; }

        /// <summary>Percentage of call volume reached within the second threshold.</summary>
        public double PercentWithinSecondThreshold { get; set; }
    }

    /// <summary>
    /// One demand point and the station that would respond to it.
    /// </summary>
    public class DemandAssignmentDto
    {
        /// <summary>Demand point identifier.</summary>
        public int DemandPointId { get; set; }

        /// <summary>Candidate id of the first-due station, or zero when unreachable.</summary>
        public int AssignedCandidateId { get; set; }

        /// <summary>Travel time from that station in minutes.</summary>
        public double ResponseMinutes { get; set; }
    }

    /// <summary>
    /// Optimized station siting with the baseline comparison a chief presents to council.
    /// </summary>
    public class OptimizeCoverageResultDto
    {
        /// <summary>True when the native kernel produced the result.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Candidate ids of the chosen stations, ascending.</summary>
        public List<int> ChosenCandidateIds { get; set; } = [];

        /// <summary>Response statistics under the optimized siting.</summary>
        public CoverageStatsDto Optimized { get; set; } = new();

        /// <summary>Response statistics under the stations operating today.</summary>
        public CoverageStatsDto Baseline { get; set; } = new();

        /// <summary>Nearest-station assignment for every demand point under the optimized siting.</summary>
        public List<DemandAssignmentDto> Assignments { get; set; } = [];

        /// <summary>The same assignment under the stations operating today; empty when none are declared.</summary>
        public List<DemandAssignmentDto> BaselineAssignments { get; set; } = [];

        /// <summary>Objective after the greedy seed and after each applied substitution.</summary>
        public List<double> IterationObjectives { get; set; } = [];

        /// <summary>Milliseconds spent building the candidate-to-demand travel-time matrix.</summary>
        public double MatrixBuildMs { get; set; }

        /// <summary>Milliseconds spent in the facility-location search.</summary>
        public double SolveMs { get; set; }

        /// <summary>True when at least 90% of call volume is within the first threshold.</summary>
        public bool MeetsNfpa1710 { get; set; }
    }
}
