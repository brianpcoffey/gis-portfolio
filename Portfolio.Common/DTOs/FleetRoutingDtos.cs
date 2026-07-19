namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// One customer stop on a delivery run: where it is, how much it consumes, and when
    /// it can be served.
    /// </summary>
    public class FleetStopDto
    {
        /// <summary>Stable identifier within the scenario.</summary>
        public int Id { get; set; }

        /// <summary>Human-readable stop name, usually the nearest street.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Load consumed at the stop, in kilograms.</summary>
        public double Demand { get; set; }

        /// <summary>Earliest service time, in minutes from midnight.</summary>
        public double ReadyMinutes { get; set; }

        /// <summary>Latest service time, in minutes from midnight; arriving later is infeasible.</summary>
        public double DueMinutes { get; set; }

        /// <summary>Minutes spent at the stop, separate from travel.</summary>
        public double ServiceMinutes { get; set; }
    }

    /// <summary>
    /// A ready-to-run delivery scenario: depot, stops, and the fleet that has to cover them.
    /// </summary>
    public class FleetScenarioDto
    {
        /// <summary>Preset key that produced the scenario.</summary>
        public string Preset { get; set; } = string.Empty;

        /// <summary>Display name of the scenario.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>One-line description of what makes the scenario interesting.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Depot latitude in decimal degrees.</summary>
        public double DepotLatitude { get; set; }

        /// <summary>Depot longitude in decimal degrees.</summary>
        public double DepotLongitude { get; set; }

        /// <summary>Depot display name.</summary>
        public string DepotLabel { get; set; } = string.Empty;

        /// <summary>Customer stops to be served.</summary>
        public List<FleetStopDto> Stops { get; set; } = [];

        /// <summary>Number of vehicles available.</summary>
        public int VehicleCount { get; set; }

        /// <summary>Per-vehicle load ceiling, in kilograms.</summary>
        public double VehicleCapacity { get; set; }

        /// <summary>Shift start, in minutes from midnight.</summary>
        public double ShiftStartMinutes { get; set; }

        /// <summary>Shift end, in minutes from midnight; every vehicle must be back by then.</summary>
        public double ShiftEndMinutes { get; set; }

        /// <summary>Suggested fixed cost per vehicle used, in kilometre equivalents.</summary>
        public double VehicleFixedCost { get; set; }
    }

    /// <summary>
    /// Request to solve a capacitated vehicle routing problem with time windows.
    /// </summary>
    public class OptimizeRequestDto
    {
        /// <summary>Depot latitude in decimal degrees.</summary>
        public double DepotLatitude { get; set; }

        /// <summary>Depot longitude in decimal degrees.</summary>
        public double DepotLongitude { get; set; }

        /// <summary>Customer stops to be served.</summary>
        public List<FleetStopDto> Stops { get; set; } = [];

        /// <summary>Number of vehicles available.</summary>
        public int VehicleCount { get; set; } = 5;

        /// <summary>Per-vehicle load ceiling, in kilograms.</summary>
        public double VehicleCapacity { get; set; } = 1200;

        /// <summary>Shift start, in minutes from midnight.</summary>
        public double ShiftStartMinutes { get; set; } = 480;

        /// <summary>Shift end, in minutes from midnight.</summary>
        public double ShiftEndMinutes { get; set; } = 960;

        /// <summary>Fixed penalty per vehicle used, in kilometre equivalents.</summary>
        public double VehicleFixedCost { get; set; } = 25;

        /// <summary>Maximum local-search passes before the solver stops.</summary>
        public int MaxIterations { get; set; } = 1000;
    }

    /// <summary>
    /// One vehicle's assignment: the ordered stops, the road-following path, and the schedule.
    /// </summary>
    public class VehicleRouteDto
    {
        /// <summary>Zero-based index of the vehicle within the fleet.</summary>
        public int VehicleIndex { get; set; }

        /// <summary>Identifiers of the served stops, in visit order.</summary>
        public List<int> StopIds { get; set; } = [];

        /// <summary>Road-following polyline from the depot through every stop and back.</summary>
        public List<CoordinateDto> Path { get; set; } = [];

        /// <summary>Total along-road distance of the route, in kilometres.</summary>
        public double DistanceKm { get; set; }

        /// <summary>Elapsed minutes from leaving the depot to returning, waiting included.</summary>
        public double DurationMinutes { get; set; }

        /// <summary>Sum of the served stops' demands, in kilograms.</summary>
        public double Load { get; set; }

        /// <summary>Arrival time at each stop, in minutes from midnight, parallel to <see cref="StopIds"/>.</summary>
        public List<double> ArrivalMinutes { get; set; } = [];

        /// <summary>Minute the vehicle returns to the depot, from midnight.</summary>
        public double ReturnMinutes { get; set; }

        /// <summary>True when the route respects capacity, every time window, and the shift end.</summary>
        public bool Feasible { get; set; }
    }

    /// <summary>
    /// Result of a CVRPTW solve: the routes, the convergence trace, and honest timings.
    /// </summary>
    public class OptimizeResultDto
    {
        /// <summary>True when the solve was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>True when every stop is served and every route respects its constraints.</summary>
        public bool Feasible { get; set; }

        /// <summary>Routes produced, one per vehicle used.</summary>
        public List<VehicleRouteDto> Routes { get; set; } = [];

        /// <summary>Number of vehicles carrying at least one stop.</summary>
        public int VehiclesUsed { get; set; }

        /// <summary>Summed along-road distance across every route, in kilometres.</summary>
        public double TotalDistanceKm { get; set; }

        /// <summary>Summed elapsed minutes across every route.</summary>
        public double TotalDurationMinutes { get; set; }

        /// <summary>Identifiers of stops no vehicle could serve.</summary>
        public List<int> UnservedStopIds { get; set; } = [];

        /// <summary>Objective after Clarke-Wright construction, before local search.</summary>
        public double InitialObjective { get; set; }

        /// <summary>Objective after local search converged.</summary>
        public double FinalObjective { get; set; }

        /// <summary>Percentage the objective fell between construction and the final solution.</summary>
        public double ImprovementPercent { get; set; }

        /// <summary>Objective after construction and after each local-search pass.</summary>
        public List<double> IterationCosts { get; set; } = [];

        /// <summary>Milliseconds spent building the road-distance matrix.</summary>
        public double MatrixBuildMs { get; set; }

        /// <summary>Milliseconds spent inside the solver itself.</summary>
        public double SolveMs { get; set; }

        /// <summary>Milliseconds spent expanding route legs into road-following polylines.</summary>
        public double PathExpandMs { get; set; }
    }
}
