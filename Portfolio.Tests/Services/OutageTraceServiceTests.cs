using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Outage management over an electric distribution circuit: fault tracing, isolation
    // device selection, and tie-switch restoration planning.
    //
    // These tests assert computation, not which path produced it: they pass whether or
    // not the native shared libraries have been built. Native/managed equivalence is
    // covered by NativeParityTests and by `dotnet run --project Portfolio.Benchmarks`.
    // native/network_trace_kernel/src/network_trace_kernel.cpp, so these also pin the
    // traversal semantics the native kernel must reproduce.
    public class OutageTraceServiceTests
    {
        private const int SourceNode = 1;

        private static OutageTraceService NewService() =>
            new(NullLogger<OutageTraceService>.Instance);

        // A single radial feeder: breaker, trunk with a recloser, one fused lateral, and
        // two service transformers.
        //
        //   (1)--BRK--(2)--TRUNK1--(3)--REC--(4)--TRUNK2--(5)--XFMR(20)--(6)
        //                            \--FUSE--(7)--LATSEG--(8)--XFMR(10)--(9)
        private static List<NetworkElementDto> RadialFeeder() =>
        [
            Element(1, "BRK A", 1, 2, DeviceTypes.Breaker),
            Element(2, "A-TRUNK-1", 2, 3, DeviceTypes.Conductor),
            Element(3, "REC A-1", 3, 4, DeviceTypes.Recloser),
            Element(4, "A-TRUNK-2", 4, 5, DeviceTypes.Conductor),
            Element(5, "XFMR A-2", 5, 6, DeviceTypes.Transformer, customerCount: 20),
            Element(6, "FUSE A-L1", 3, 7, DeviceTypes.Fuse),
            Element(7, "A-L1-SEG-1", 7, 8, DeviceTypes.Conductor),
            Element(8, "XFMR A-L1-1", 8, 9, DeviceTypes.Transformer, customerCount: 10)
        ];

        // Two feeders off one substation bus with a normally-open tie between their tails.
        //
        //   Feeder A: (1)--BRK A--(2)--A-T1--(3)--SW A--(4)--A-T2--(5)--XFMR(40)--(6)
        //                                      \--XFMR(25)--(7)
        //   Feeder B: (1)--BRK B--(8)--B-T1--(9)--XFMR(30)--(10)
        //   Tie:      (9)==TIE T-1==(5)   normally open
        private static List<NetworkElementDto> TwoFeedersWithTie() =>
        [
            Element(1, "BRK A", 1, 2, DeviceTypes.Breaker),
            Element(2, "A-T1", 2, 3, DeviceTypes.Conductor),
            Element(3, "SW A", 3, 4, DeviceTypes.Switch),
            Element(4, "A-T2", 4, 5, DeviceTypes.Conductor),
            Element(5, "XFMR A-2", 5, 6, DeviceTypes.Transformer, customerCount: 40),
            Element(6, "XFMR A-1", 3, 7, DeviceTypes.Transformer, customerCount: 25),
            Element(7, "BRK B", 1, 8, DeviceTypes.Breaker),
            Element(8, "B-T1", 8, 9, DeviceTypes.Conductor),
            Element(9, "XFMR B-1", 9, 10, DeviceTypes.Transformer, customerCount: 30),
            Element(10, "TIE T-1", 9, 5, DeviceTypes.TieSwitch, isOpen: true)
        ];

        private static NetworkElementDto Element(
            int id,
            string label,
            int fromNodeId,
            int toNodeId,
            int deviceType,
            bool isOpen = false,
            int customerCount = 0) => new()
            {
                Id = id,
                Label = label,
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                DeviceType = deviceType,
                IsOpen = isOpen,
                CustomerCount = customerCount
            };

        private static TraceRequestDto TraceOf(List<NetworkElementDto> elements, int faultElementId) => new()
        {
            Elements = elements,
            SourceNodeId = SourceNode,
            FaultElementId = faultElementId
        };

        // ── Fault tracing ───────────────────────────────────────────────────────

        [Fact]
        public async Task Trace_FaultOnLateral_AffectsOnlyThatLateral()
        {
            var service = NewService();

            var result = await service.TraceAsync(TraceOf(RadialFeeder(), faultElementId: 7));

            Assert.Equal(new[] { 7, 8 }, result.DownstreamElementIds.Order().ToArray());
            Assert.Equal(10, result.CustomersAffected);
            Assert.Equal(30, result.CustomersTotal);
        }

        [Fact]
        public async Task Trace_FaultOnTrunk_AffectsEverythingDownstream()
        {
            var service = NewService();

            var result = await service.TraceAsync(TraceOf(RadialFeeder(), faultElementId: 2));

            Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8 }, result.DownstreamElementIds.Order().ToArray());
            Assert.Equal(30, result.CustomersAffected);
            Assert.Equal(100, result.PercentAffected);
        }

        [Fact]
        public async Task Trace_StopsAtOpenDevice()
        {
            var service = NewService();
            var elements = RadialFeeder();
            elements[5].IsOpen = true; // the lateral fuse is already blown

            var result = await service.TraceAsync(TraceOf(elements, faultElementId: 2));

            Assert.DoesNotContain(6, result.DownstreamElementIds);
            Assert.DoesNotContain(7, result.DownstreamElementIds);
            Assert.DoesNotContain(8, result.DownstreamElementIds);
            Assert.Equal(20, result.CustomersAffected);
        }

        [Fact]
        public async Task Trace_CustomersAffected_SumsDownstreamCustomers()
        {
            var service = NewService();

            var result = await service.TraceAsync(TraceOf(RadialFeeder(), faultElementId: 4));

            Assert.Equal(new[] { 4, 5 }, result.DownstreamElementIds.Order().ToArray());
            Assert.Equal(20, result.CustomersAffected);
        }

        [Fact]
        public async Task Trace_UpstreamPath_ReachesSource()
        {
            var service = NewService();

            var result = await service.TraceAsync(TraceOf(RadialFeeder(), faultElementId: 7));

            Assert.Equal(new[] { 7, 6, 2, 1 }, result.UpstreamElementIds.ToArray());
            Assert.Equal(1, result.UpstreamElementIds[^1]); // the substation breaker
        }

        [Fact]
        public async Task Trace_IsolationDevices_ReturnsNearestUpstreamProtective()
        {
            var service = NewService();
            var elements = RadialFeeder();

            // On the lateral the fuse clears first; on the trunk beyond it the recloser does.
            var onLateral = await service.TraceAsync(TraceOf(elements, faultElementId: 7));
            var onTrunk = await service.TraceAsync(TraceOf(elements, faultElementId: 4));
            var atHead = await service.TraceAsync(TraceOf(elements, faultElementId: 2));

            Assert.Equal(new[] { 6 }, onLateral.IsolationDeviceIds.ToArray());
            Assert.Equal(new[] { 3 }, onTrunk.IsolationDeviceIds.ToArray());
            Assert.Equal(new[] { 1 }, atHead.IsolationDeviceIds.ToArray());
        }

        [Fact]
        public async Task Trace_FaultElementItselfIsIncludedDownstream()
        {
            var service = NewService();

            var result = await service.TraceAsync(TraceOf(RadialFeeder(), faultElementId: 4));

            Assert.Equal(4, result.DownstreamElementIds[0]);
        }

        [Fact]
        public async Task Trace_UnknownFaultElement_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.TraceAsync(TraceOf(RadialFeeder(), faultElementId: 999)));
        }

        [Fact]
        public async Task Trace_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.TraceAsync(null!));
        }

        [Fact]
        public async Task Trace_NoElements_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.TraceAsync(new TraceRequestDto { Elements = [], SourceNodeId = 1, FaultElementId = 1 }));
        }

        [Fact]
        public async Task Trace_TooManyElements_ThrowsArgumentException()
        {
            var service = NewService();
            var elements = new List<NetworkElementDto>(5_001);
            for (var i = 1; i <= 5_001; i++)
                elements.Add(Element(i, $"SEG-{i}", i, i + 1, DeviceTypes.Conductor));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.TraceAsync(TraceOf(elements, faultElementId: 1)));
        }

        [Fact]
        public async Task Trace_UnknownSourceNode_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.TraceAsync(new TraceRequestDto
            {
                Elements = RadialFeeder(),
                SourceNodeId = 9_999,
                FaultElementId = 1
            }));
        }

        [Fact]
        public async Task Trace_NegativeCustomerCount_ThrowsArgumentException()
        {
            var service = NewService();
            var elements = RadialFeeder();
            elements[4].CustomerCount = -1;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.TraceAsync(TraceOf(elements, faultElementId: 2)));
        }

        [Fact]
        public async Task Trace_UnknownDeviceType_ThrowsArgumentException()
        {
            var service = NewService();
            var elements = RadialFeeder();
            elements[4].DeviceType = 7;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.TraceAsync(TraceOf(elements, faultElementId: 2)));
        }

        // ── Restoration ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Restore_ClosingTie_RestoresHealthySections()
        {
            var service = NewService();
            var elements = TwoFeedersWithTie();

            var trace = await service.TraceAsync(TraceOf(elements, faultElementId: 2));
            var result = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = elements,
                SourceNodeId = SourceNode,
                FaultElementId = 2,
                IsolationDeviceIds = trace.IsolationDeviceIds
            });

            Assert.True(result.RestorationFound);
            Assert.Equal(10, result.TieElementId);
            Assert.Equal(40, result.CustomersRestored);
            Assert.True(result.EstimatedSaidiMinutesAvoided > 0);
        }

        [Fact]
        public async Task Restore_NoTieAvailable_ReturnsNoRestoration()
        {
            var service = NewService();
            var elements = RadialFeeder();

            var trace = await service.TraceAsync(TraceOf(elements, faultElementId: 4));
            var result = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = elements,
                SourceNodeId = SourceNode,
                FaultElementId = 4,
                IsolationDeviceIds = trace.IsolationDeviceIds
            });

            Assert.False(result.RestorationFound);
            Assert.Equal(0, result.CustomersRestored);
            Assert.Equal(0, result.TieElementId);
        }

        [Fact]
        public async Task Restore_PlanOpensIsolationBeforeClosingTie()
        {
            var service = NewService();
            var elements = TwoFeedersWithTie();

            var result = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = elements,
                SourceNodeId = SourceNode,
                FaultElementId = 2,
                IsolationDeviceIds = [1, 3]
            });

            Assert.Equal(3, result.Plan.Count);
            Assert.Equal("OPEN", result.Plan[0].Action);
            Assert.Equal("OPEN", result.Plan[1].Action);
            Assert.Equal("CLOSE", result.Plan[2].Action);
            Assert.Equal("TIE T-1", result.Plan[2].Label);
        }

        [Fact]
        public async Task Restore_DoesNotReenergizeFaultedSection()
        {
            var service = NewService();
            var elements = TwoFeedersWithTie();

            var result = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = elements,
                SourceNodeId = SourceNode,
                FaultElementId = 2,
                IsolationDeviceIds = [1, 3]
            });

            Assert.DoesNotContain(2, result.EnergizedElementIds);
        }

        [Fact]
        public async Task Restore_CustomersRestoredPlusStillOut_EqualsAffected()
        {
            var service = NewService();
            var elements = TwoFeedersWithTie();

            var result = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = elements,
                SourceNodeId = SourceNode,
                FaultElementId = 2,
                IsolationDeviceIds = [1, 3]
            });

            Assert.Equal(result.CustomersAffected, result.CustomersRestored + result.CustomersStillOut);
        }

        [Fact]
        public async Task EnergizedSet_TraversesTieAgainstNominalDirection()
        {
            var service = NewService();
            var elements = TwoFeedersWithTie();

            var result = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = elements,
                SourceNodeId = SourceNode,
                FaultElementId = 2,
                IsolationDeviceIds = [1, 3]
            });

            // A-T2 is nominally 4 -> 5. Backfeeding through the tie energizes it from node
            // 5, against that nominal direction, which only works because energization is
            // computed as undirected connectivity.
            Assert.Contains(4, result.EnergizedElementIds);
            Assert.Contains(5, result.EnergizedElementIds);
        }

        [Fact]
        public async Task Restore_UnknownIsolationDevice_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ProposeRestorationAsync(new RestoreRequestDto
                {
                    Elements = TwoFeedersWithTie(),
                    SourceNodeId = SourceNode,
                    FaultElementId = 2,
                    IsolationDeviceIds = [999]
                }));
        }

        [Fact]
        public async Task Restore_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ProposeRestorationAsync(null!));
        }

        [Fact]
        public async Task Restore_NegativeRepairMinutes_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ProposeRestorationAsync(new RestoreRequestDto
                {
                    Elements = TwoFeedersWithTie(),
                    SourceNodeId = SourceNode,
                    FaultElementId = 2,
                    IsolationDeviceIds = [1, 3],
                    AssumedRepairMinutes = -5
                }));
        }

        // ── The shipped demo network ────────────────────────────────────────────

        [Fact]
        public async Task Network_IsDeterministic()
        {
            var service = NewService();

            var first = await service.GetNetworkAsync();
            var second = await service.GetNetworkAsync();

            Assert.Equal(first.Elements.Count, second.Elements.Count);
            Assert.Equal(first.TotalCustomers, second.TotalCustomers);
            for (var i = 0; i < first.Elements.Count; i++)
            {
                Assert.Equal(first.Elements[i].Id, second.Elements[i].Id);
                Assert.Equal(first.Elements[i].Label, second.Elements[i].Label);
                Assert.Equal(first.Elements[i].CustomerCount, second.Elements[i].CustomerCount);
                Assert.Equal(first.Elements[i].FromNodeId, second.Elements[i].FromNodeId);
                Assert.Equal(first.Elements[i].ToNodeId, second.Elements[i].ToNodeId);
            }
        }

        [Fact]
        public async Task Network_IsRadial_WithTiesOpen()
        {
            var service = NewService();
            var network = await service.GetNetworkAsync();

            var closed = network.Elements.Where(e => !e.IsOpen).ToList();
            var nodes = new HashSet<int>();
            foreach (var element in closed)
            {
                nodes.Add(element.FromNodeId);
                nodes.Add(element.ToNodeId);
            }

            // Everything closed is energized from the substation with no switching, so the
            // closed sub-network is connected...
            var energized = await service.ProposeRestorationAsync(new RestoreRequestDto
            {
                Elements = network.Elements,
                SourceNodeId = network.SourceNodeId,
                FaultElementId = network.Elements[0].Id,
                IsolationDeviceIds = []
            });
            Assert.Equal(closed.Count, energized.EnergizedElementIds.Count);

            // ...and a connected graph with |E| = |V| - 1 is a tree, which is exactly what
            // "radial in operation" means electrically: one path from the source to any point.
            Assert.Equal(closed.Count + 1, nodes.Count);
        }

        [Fact]
        public async Task Network_TraceOnDemoNetwork_ProducesSensibleImpact()
        {
            var service = NewService();
            var network = await service.GetNetworkAsync();
            var trunkFault = network.Elements.First(e => e.Label == "A-TRUNK-07");

            var trace = await service.TraceAsync(new TraceRequestDto
            {
                Elements = network.Elements,
                SourceNodeId = network.SourceNodeId,
                FaultElementId = trunkFault.Id
            });

            Assert.True(trace.CustomersAffected > 0);
            Assert.True(trace.CustomersAffected < network.TotalCustomers);
            Assert.NotEmpty(trace.IsolationDeviceIds);
            Assert.Equal(network.TotalCustomers, trace.CustomersTotal);
        }
    }
}
