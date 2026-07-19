namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Device-type codes shared by the wire format, the native kernel, and the frontend legend.
    /// </summary>
    public static class DeviceTypes
    {
        /// <summary>A span of conductor with no switching capability.</summary>
        public const int Conductor = 0;

        /// <summary>A manually or remotely operated sectionalizing switch.</summary>
        public const int Switch = 1;

        /// <summary>A one-shot protective device, typically on a lateral.</summary>
        public const int Fuse = 2;

        /// <summary>An automatic device that trips and re-tries before locking out.</summary>
        public const int Recloser = 3;

        /// <summary>The substation breaker energizing a feeder.</summary>
        public const int Breaker = 4;

        /// <summary>A normally-open point connecting two feeders, closed to backfeed.</summary>
        public const int TieSwitch = 5;

        /// <summary>A service transformer feeding customers directly.</summary>
        public const int Transformer = 6;

        /// <summary>Highest valid device-type code.</summary>
        public const int Max = Transformer;
    }

    /// <summary>
    /// One element of a distribution circuit: a conductor span, a device, or a transformer.
    /// </summary>
    public class NetworkElementDto
    {
        /// <summary>Stable identifier within the network.</summary>
        public int Id { get; set; }

        /// <summary>Human-readable element label, for example "A-LAT-3-SEG-2".</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Upstream-side node under normal radial flow.</summary>
        public int FromNodeId { get; set; }

        /// <summary>Downstream-side node under normal radial flow.</summary>
        public int ToNodeId { get; set; }

        /// <summary>Device-type code; see <see cref="DeviceTypes"/>.</summary>
        public int DeviceType { get; set; }

        /// <summary>True when the element is open and carries no current.</summary>
        public bool IsOpen { get; set; }

        /// <summary>Customers fed directly by this element.</summary>
        public int CustomerCount { get; set; }

        /// <summary>Latitude of the upstream-side node, in decimal degrees.</summary>
        public double FromLatitude { get; set; }

        /// <summary>Longitude of the upstream-side node, in decimal degrees.</summary>
        public double FromLongitude { get; set; }

        /// <summary>Latitude of the downstream-side node, in decimal degrees.</summary>
        public double ToLatitude { get; set; }

        /// <summary>Longitude of the downstream-side node, in decimal degrees.</summary>
        public double ToLongitude { get; set; }

        /// <summary>Feeder the element belongs to, or "Tie" for an inter-feeder tie.</summary>
        public string FeederName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A distribution network: one substation source, its feeders, and the ties between them.
    /// </summary>
    public class DistributionNetworkDto
    {
        /// <summary>Display name of the network.</summary>
        public string NetworkName { get; set; } = string.Empty;

        /// <summary>Node id of the substation bus that energizes every feeder.</summary>
        public int SourceNodeId { get; set; }

        /// <summary>Every element in the network.</summary>
        public List<NetworkElementDto> Elements { get; set; } = [];

        /// <summary>Sum of customers across every element.</summary>
        public int TotalCustomers { get; set; }

        /// <summary>Names of the feeders leaving the substation.</summary>
        public List<string> FeederNames { get; set; } = [];
    }

    /// <summary>
    /// Request to trace an outage from a faulted element.
    /// </summary>
    public class TraceRequestDto
    {
        /// <summary>Network elements to trace over.</summary>
        public List<NetworkElementDto> Elements { get; set; } = [];

        /// <summary>Node id of the energizing source.</summary>
        public int SourceNodeId { get; set; }

        /// <summary>Identifier of the faulted element.</summary>
        public int FaultElementId { get; set; }
    }

    /// <summary>
    /// Result of a fault trace: who is out, what feeds them, and what isolates the fault.
    /// </summary>
    public class TraceResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Identifier of the faulted element.</summary>
        public int FaultElementId { get; set; }

        /// <summary>Every element de-energized by the fault, faulted element first.</summary>
        public List<int> DownstreamElementIds { get; set; } = [];

        /// <summary>Ordered path from the faulted element back to the source.</summary>
        public List<int> UpstreamElementIds { get; set; } = [];

        /// <summary>Devices whose opening isolates the faulted section.</summary>
        public List<int> IsolationDeviceIds { get; set; } = [];

        /// <summary>Customers de-energized by the fault.</summary>
        public int CustomersAffected { get; set; }

        /// <summary>Customers served by the whole network.</summary>
        public int CustomersTotal { get; set; }

        /// <summary>Affected customers as a percentage of the system.</summary>
        public double PercentAffected { get; set; }
    }

    /// <summary>
    /// Request to propose a switching plan that restores customers around an isolated fault.
    /// </summary>
    public class RestoreRequestDto
    {
        /// <summary>Network elements to switch over.</summary>
        public List<NetworkElementDto> Elements { get; set; } = [];

        /// <summary>Node id of the energizing source.</summary>
        public int SourceNodeId { get; set; }

        /// <summary>Identifier of the faulted element.</summary>
        public int FaultElementId { get; set; }

        /// <summary>Devices to open in order to isolate the fault.</summary>
        public List<int> IsolationDeviceIds { get; set; } = [];

        /// <summary>Assumed repair duration in minutes, used for the SAIDI estimate.</summary>
        public double AssumedRepairMinutes { get; set; } = 180;
    }

    /// <summary>
    /// One operation in a switching plan.
    /// </summary>
    public class SwitchingStepDto
    {
        /// <summary>Identifier of the element to operate.</summary>
        public int ElementId { get; set; }

        /// <summary>Human-readable label of the element.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Operation to perform: "OPEN" or "CLOSE".</summary>
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of a restoration search: the switching plan and the customers it recovers.
    /// </summary>
    public class RestoreResultDto
    {
        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Ordered switching operations: isolate first, then backfeed.</summary>
        public List<SwitchingStepDto> Plan { get; set; } = [];

        /// <summary>Customers brought back by closing the winning tie.</summary>
        public int CustomersRestored { get; set; }

        /// <summary>Customers still without power once the plan is executed.</summary>
        public int CustomersStillOut { get; set; }

        /// <summary>Customers de-energized by isolation alone, before any backfeed.</summary>
        public int CustomersAffected { get; set; }

        /// <summary>Elements energized once the plan is executed.</summary>
        public List<int> EnergizedElementIds { get; set; } = [];

        /// <summary>Estimated SAIDI minutes avoided by the restoration.</summary>
        public double EstimatedSaidiMinutesAvoided { get; set; }

        /// <summary>True when a tie switch was found that restores customers.</summary>
        public bool RestorationFound { get; set; }

        /// <summary>Identifier of the tie switch closed by the plan, or zero when none was found.</summary>
        public int TieElementId { get; set; }
    }
}
