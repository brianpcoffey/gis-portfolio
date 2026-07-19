using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class OutageTraceService : IOutageTraceService
    {
        private const int MaxElements = 5_000;
        private const int MaxOverrides = 100;
        private const double MaxRepairMinutes = 10_080; // one week

        private readonly ILogger<OutageTraceService> _logger;

        public OutageTraceService(ILogger<OutageTraceService> logger)
        {
            _logger = logger;
            NetworkTraceNativeBridge.LogAvailability(_logger);
        }

        public Task<DistributionNetworkDto> GetNetworkAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new DistributionNetworkDto
            {
                NetworkName = RedlandsDistributionNetwork.NetworkName,
                SourceNodeId = RedlandsDistributionNetwork.SubstationNodeId,
                Elements = [.. RedlandsDistributionNetwork.Elements],
                TotalCustomers = RedlandsDistributionNetwork.TotalCustomers,
                FeederNames = [.. RedlandsDistributionNetwork.FeederNames]
            });
        }

        // Traces a fault three ways — downstream (who is out), upstream (what feeds the
        // fault), and the isolation frontier — preferring the native kernel and falling
        // back to managed code.
        public Task<TraceResultDto> TraceAsync(
            TraceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateNetwork(request.Elements, request.SourceNodeId, nameof(request));
            if (!ContainsElement(request.Elements, request.FaultElementId))
                throw new ArgumentException("The faulted element was not found in the network.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var elements = request.Elements;
            var source = request.SourceNodeId;
            var fault = request.FaultElementId;

            int[] downstream;
            int[] upstream;
            int[] isolation;
            int customersAffected;
            bool nativeAccelerated;

            // All three traces have to come from the same engine: mixing a native
            // downstream set with a managed isolation frontier would make the reported
            // NativeAccelerated flag a half-truth.
            if (NetworkTraceNativeBridge.TryDownstream(elements, source, fault, _logger, out var nativeDown, out var nativeAffected)
                && NetworkTraceNativeBridge.TryUpstream(elements, source, fault, _logger, out var nativeUp)
                && NetworkTraceNativeBridge.TryFindIsolationDevices(elements, source, fault, _logger, out var nativeIso))
            {
                downstream = nativeDown!;
                upstream = nativeUp!;
                isolation = nativeIso!;
                customersAffected = nativeAffected;
                nativeAccelerated = true;
            }
            else
            {
                (downstream, customersAffected) = DownstreamManaged(elements, fault, cancellationToken);
                upstream = UpstreamManaged(elements, source, fault);
                isolation = IsolationManaged(elements, source, fault);
                nativeAccelerated = false;
            }

            return Task.FromResult(BuildTraceResult(request, downstream, upstream, isolation, customersAffected, nativeAccelerated));
        }

        // Evaluates every open tie one at a time and keeps the switching plan that serves
        // the most customers. The search loop is managed and readable; each evaluation is
        // a full connectivity sweep, which is the work worth pushing into the kernel.
        public Task<RestoreResultDto> ProposeRestorationAsync(
            RestoreRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateNetwork(request.Elements, request.SourceNodeId, nameof(request));
            if (!ContainsElement(request.Elements, request.FaultElementId))
                throw new ArgumentException("The faulted element was not found in the network.", nameof(request));

            var isolationDevices = request.IsolationDeviceIds ?? [];
            if (isolationDevices.Count > MaxOverrides)
                throw new ArgumentException($"Switching plans are limited to {MaxOverrides} device operations.", nameof(request));
            foreach (var deviceId in isolationDevices)
            {
                if (!ContainsElement(request.Elements, deviceId))
                    throw new ArgumentException("An isolation device was not found in the network.", nameof(request));
            }

            if (double.IsNaN(request.AssumedRepairMinutes) || double.IsInfinity(request.AssumedRepairMinutes)
                || request.AssumedRepairMinutes < 0 || request.AssumedRepairMinutes > MaxRepairMinutes)
                throw new ArgumentException($"Assumed repair minutes must be between 0 and {MaxRepairMinutes}.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var elements = request.Elements;
            var source = request.SourceNodeId;
            var totalCustomers = elements.Sum(e => e.CustomerCount);

            // Step 1 — apply the isolation devices as OPEN overrides and measure the
            // post-isolation baseline. Everything beyond this point is upside.
            var baseIds = isolationDevices.Distinct().ToArray();
            var baseStates = new int[baseIds.Length];
            Array.Fill(baseStates, 1);

            var nativeAccelerated = true;
            var (baseEnergized, baseServed) = Energize(elements, source, baseIds, baseStates, ref nativeAccelerated, cancellationToken);

            // Step 2 — try each normally-open tie in turn.
            var bestServed = baseServed;
            var bestEnergized = baseEnergized;
            NetworkElementDto? bestTie = null;

            foreach (var tie in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (tie.DeviceType != DeviceTypes.TieSwitch || !tie.IsOpen)
                    continue;
                if (Array.IndexOf(baseIds, tie.Id) >= 0)
                    continue;

                var candidateIds = new int[baseIds.Length + 1];
                var candidateStates = new int[baseIds.Length + 1];
                Array.Copy(baseIds, candidateIds, baseIds.Length);
                Array.Copy(baseStates, candidateStates, baseStates.Length);
                candidateIds[^1] = tie.Id;
                candidateStates[^1] = 0;

                var (energized, served) = Energize(elements, source, candidateIds, candidateStates, ref nativeAccelerated, cancellationToken);

                // Never accept a plan that re-energizes the faulted section — restoring
                // customers by backfeeding into a fault is the one outcome worse than
                // leaving them out.
                if (Array.IndexOf(energized, request.FaultElementId) >= 0)
                    continue;

                if (served > bestServed)
                {
                    bestServed = served;
                    bestEnergized = energized;
                    bestTie = tie;
                }
            }

            return Task.FromResult(BuildRestoreResult(
                request, elements, baseIds, bestTie, bestEnergized, baseServed, bestServed, totalCustomers, nativeAccelerated));
        }

        // ── Dispatch helper ─────────────────────────────────────────────────────

        // One energization sweep, native when the kernel is loaded. `nativeAccelerated`
        // latches to false the first time any sweep falls back, so a partially managed
        // search never reports itself as accelerated.
        private (int[] Energized, int Served) Energize(
            List<NetworkElementDto> elements,
            int sourceNodeId,
            int[] overrideIds,
            int[] overrideStates,
            ref bool nativeAccelerated,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (nativeAccelerated && NetworkTraceNativeBridge.TryComputeEnergizedSet(
                elements, sourceNodeId, overrideIds, overrideStates, _logger, out var energized, out var served))
            {
                return (energized!, served);
            }

            nativeAccelerated = false;
            return EnergizedManaged(elements, sourceNodeId, overrideIds, overrideStates);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static void ValidateNetwork(List<NetworkElementDto> elements, int sourceNodeId, string paramName)
        {
            if (elements is null || elements.Count == 0)
                throw new ArgumentException("At least one network element is required.", paramName);
            if (elements.Count > MaxElements)
                throw new ArgumentException($"Networks are limited to {MaxElements} elements.", paramName);

            var sourceFound = false;
            foreach (var element in elements)
            {
                if (element.CustomerCount < 0)
                    throw new ArgumentException("Customer count cannot be negative.", paramName);
                if (element.DeviceType < 0 || element.DeviceType > DeviceTypes.Max)
                    throw new ArgumentException($"Device type must be between 0 and {DeviceTypes.Max}.", paramName);

                if (element.FromNodeId == sourceNodeId || element.ToNodeId == sourceNodeId)
                    sourceFound = true;
            }

            if (!sourceFound)
                throw new ArgumentException("The source node was not found in the network.", paramName);
        }

        private static bool ContainsElement(List<NetworkElementDto> elements, int elementId)
        {
            foreach (var element in elements)
            {
                if (element.Id == elementId)
                    return true;
            }
            return false;
        }

        // ── Managed fallbacks (mirror the native kernel exactly) ────────────────

        private static Dictionary<int, List<int>> BuildAdjacency(List<NetworkElementDto> elements)
        {
            var adjacency = new Dictionary<int, List<int>>(elements.Count * 2);
            for (var i = 0; i < elements.Count; i++)
            {
                AddIncident(adjacency, elements[i].FromNodeId, i);
                AddIncident(adjacency, elements[i].ToNodeId, i);
            }
            return adjacency;
        }

        private static void AddIncident(Dictionary<int, List<int>> adjacency, int nodeId, int index)
        {
            if (!adjacency.TryGetValue(nodeId, out var list))
            {
                list = [];
                adjacency[nodeId] = list;
            }
            list.Add(index);
        }

        private static int IndexOfElement(List<NetworkElementDto> elements, int elementId)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (elements[i].Id == elementId)
                    return i;
            }
            return -1;
        }

        private static int OtherEnd(NetworkElementDto element, int nodeId) =>
            element.FromNodeId == nodeId ? element.ToNodeId : element.FromNodeId;

        private static bool IsProtective(int deviceType) =>
            deviceType is DeviceTypes.Fuse or DeviceTypes.Recloser or DeviceTypes.Breaker;

        private static bool IsSwitch(int deviceType) =>
            deviceType is DeviceTypes.Switch or DeviceTypes.TieSwitch;

        // Breadth-first sweep over closed elements from `startNode`, recording for every
        // reached node the element that reached it. `blockedIndex` is never traversed.
        private static Dictionary<int, int> Sweep(
            List<NetworkElementDto> elements,
            Dictionary<int, List<int>> adjacency,
            int startNode,
            int blockedIndex,
            List<int>? visitOrder)
        {
            var reachedBy = new Dictionary<int, int> { [startNode] = -1 };
            var queue = new List<int> { startNode };

            for (var head = 0; head < queue.Count; head++)
            {
                if (!adjacency.TryGetValue(queue[head], out var incident))
                    continue;

                foreach (var index in incident)
                {
                    if (index == blockedIndex || elements[index].IsOpen)
                        continue;

                    var next = OtherEnd(elements[index], queue[head]);
                    if (reachedBy.ContainsKey(next))
                        continue;

                    reachedBy[next] = index;
                    visitOrder?.Add(index);
                    queue.Add(next);
                }
            }

            return reachedBy;
        }

        private static (int[] Ids, int Customers) DownstreamManaged(
            List<NetworkElementDto> elements,
            int faultElementId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var faultIndex = IndexOfElement(elements, faultElementId);
            var adjacency = BuildAdjacency(elements);

            var visitOrder = new List<int>();
            Sweep(elements, adjacency, elements[faultIndex].ToNodeId, faultIndex, visitOrder);

            var ids = new int[visitOrder.Count + 1];
            var customers = elements[faultIndex].CustomerCount;
            ids[0] = elements[faultIndex].Id;

            for (var i = 0; i < visitOrder.Count; i++)
            {
                var element = elements[visitOrder[i]];
                ids[i + 1] = element.Id;
                customers += element.CustomerCount;
            }

            return (ids, customers);
        }

        private static int[] UpstreamManaged(List<NetworkElementDto> elements, int sourceNodeId, int faultElementId)
        {
            var faultIndex = IndexOfElement(elements, faultElementId);
            var adjacency = BuildAdjacency(elements);
            var reachedBy = Sweep(elements, adjacency, sourceNodeId, faultIndex, null);

            var path = new List<int> { elements[faultIndex].Id };
            var node = elements[faultIndex].FromNodeId;

            while (reachedBy.TryGetValue(node, out var index) && index >= 0)
            {
                path.Add(elements[index].Id);
                node = OtherEnd(elements[index], node);
            }

            return [.. path];
        }

        private static int[] IsolationManaged(List<NetworkElementDto> elements, int sourceNodeId, int faultElementId)
        {
            var faultIndex = IndexOfElement(elements, faultElementId);
            var adjacency = BuildAdjacency(elements);
            var devices = new List<int>();

            // Upstream: the first protective device between the fault and the source.
            var reachedBy = Sweep(elements, adjacency, sourceNodeId, faultIndex, null);
            var node = elements[faultIndex].FromNodeId;
            while (reachedBy.TryGetValue(node, out var index) && index >= 0)
            {
                if (IsProtective(elements[index].DeviceType))
                {
                    devices.Add(elements[index].Id);
                    break;
                }
                node = OtherEnd(elements[index], node);
            }

            // Downstream: the frontier of closed switches. Traversal stops at each one, so
            // only the switches that actually bound the de-energized section are returned.
            var seenElement = new bool[elements.Count];
            seenElement[faultIndex] = true;
            var visitedNodes = new HashSet<int> { elements[faultIndex].ToNodeId };
            var queue = new List<int> { elements[faultIndex].ToNodeId };

            for (var head = 0; head < queue.Count; head++)
            {
                if (!adjacency.TryGetValue(queue[head], out var incident))
                    continue;

                foreach (var index in incident)
                {
                    if (seenElement[index])
                        continue;
                    seenElement[index] = true;

                    var element = elements[index];
                    if (element.IsOpen)
                        continue;

                    if (IsSwitch(element.DeviceType))
                    {
                        devices.Add(element.Id);
                        continue;
                    }

                    var next = OtherEnd(element, queue[head]);
                    if (visitedNodes.Add(next))
                        queue.Add(next);
                }
            }

            return [.. devices];
        }

        private static (int[] Energized, int Served) EnergizedManaged(
            List<NetworkElementDto> elements,
            int sourceNodeId,
            int[] overrideIds,
            int[] overrideStates)
        {
            var open = new bool[elements.Count];
            for (var i = 0; i < elements.Count; i++)
                open[i] = elements[i].IsOpen;

            for (var o = 0; o < overrideIds.Length; o++)
            {
                var index = IndexOfElement(elements, overrideIds[o]);
                if (index < 0)
                    continue;
                open[index] = overrideStates[o] != 0;
            }

            var adjacency = BuildAdjacency(elements);
            var visitedNodes = new HashSet<int> { sourceNodeId };
            var queue = new List<int> { sourceNodeId };

            for (var head = 0; head < queue.Count; head++)
            {
                if (!adjacency.TryGetValue(queue[head], out var incident))
                    continue;

                foreach (var index in incident)
                {
                    if (open[index])
                        continue;

                    var next = OtherEnd(elements[index], queue[head]);
                    if (visitedNodes.Add(next))
                        queue.Add(next);
                }
            }

            var energized = new List<int>();
            var served = 0;
            for (var i = 0; i < elements.Count; i++)
            {
                if (open[i])
                    continue;
                if (!visitedNodes.Contains(elements[i].FromNodeId) && !visitedNodes.Contains(elements[i].ToNodeId))
                    continue;

                energized.Add(elements[i].Id);
                served += elements[i].CustomerCount;
            }

            return ([.. energized], served);
        }

        // ── Result shaping ──────────────────────────────────────────────────────

        private static TraceResultDto BuildTraceResult(
            TraceRequestDto request,
            int[] downstream,
            int[] upstream,
            int[] isolation,
            int customersAffected,
            bool nativeAccelerated)
        {
            var total = request.Elements.Sum(e => e.CustomerCount);

            return new TraceResultDto
            {
                NativeAccelerated = nativeAccelerated,
                FaultElementId = request.FaultElementId,
                DownstreamElementIds = [.. downstream],
                UpstreamElementIds = [.. upstream],
                IsolationDeviceIds = [.. isolation],
                CustomersAffected = customersAffected,
                CustomersTotal = total,
                PercentAffected = total > 0 ? Math.Round(customersAffected * 100.0 / total, 2) : 0
            };
        }

        private static RestoreResultDto BuildRestoreResult(
            RestoreRequestDto request,
            List<NetworkElementDto> elements,
            int[] isolationIds,
            NetworkElementDto? tie,
            int[] energized,
            int baseServed,
            int bestServed,
            int totalCustomers,
            bool nativeAccelerated)
        {
            var plan = new List<SwitchingStepDto>();
            foreach (var deviceId in isolationIds)
            {
                var device = elements.FirstOrDefault(e => e.Id == deviceId);
                plan.Add(new SwitchingStepDto
                {
                    ElementId = deviceId,
                    Label = device?.Label ?? string.Empty,
                    Action = "OPEN"
                });
            }

            if (tie is not null)
            {
                plan.Add(new SwitchingStepDto
                {
                    ElementId = tie.Id,
                    Label = tie.Label,
                    Action = "CLOSE"
                });
            }

            var restored = bestServed - baseServed;
            var affected = totalCustomers - baseServed;

            return new RestoreResultDto
            {
                NativeAccelerated = nativeAccelerated,
                Plan = plan,
                CustomersRestored = restored,
                CustomersStillOut = totalCustomers - bestServed,
                CustomersAffected = affected,
                EnergizedElementIds = [.. energized],
                // SAIDI is customer-minutes lost divided by total customers, so the minutes
                // avoided by a restoration is restored customers x repair duration / system size.
                EstimatedSaidiMinutesAvoided = totalCustomers > 0
                    ? Math.Round(restored * request.AssumedRepairMinutes / totalCustomers, 2)
                    : 0,
                RestorationFound = tie is not null,
                TieElementId = tie?.Id ?? 0
            };
        }
    }
}
