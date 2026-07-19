using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;
using System.Diagnostics;

namespace Portfolio.Services.Services
{
    public class ResponseCoverageService : IResponseCoverageService
    {
        private const int MaxDemandPoints = 2_000;
        private const int MaxCandidates = 200;
        private const int MaxStations = 20;
        private const int MaxIterations = 500;
        private const int MaxIsochroneBands = 12;

        // An improvement must beat the incumbent by more than this to be accepted; mirrors
        // kImprovementEpsilon in native/facility_location_kernel/src/facility_location_kernel.cpp.
        private const double ImprovementEpsilon = 1e-12;

        // NFPA 1710 is written as "90% of the time"; the compliance flag is that literal.
        private const double Nfpa1710CompliancePercent = 90.0;

        private readonly ISpatialGraphService _graphService;
        private readonly ILogger<ResponseCoverageService> _logger;

        public ResponseCoverageService(
            ISpatialGraphService graphService,
            ILogger<ResponseCoverageService> logger)
        {
            _graphService = graphService;
            _logger = logger;
            FacilityLocationNativeBridge.LogAvailability(_logger);
        }

        // Returns the deterministic demo scenario, built once against the cached road graph.
        public async Task<ResponseScenarioDto> GetScenarioAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var graph = await _graphService.GetRedlandsGraphAsync(cancellationToken);
            return RedlandsResponseScenario.Build(graph);
        }

        // Buckets every reachable road-network node into a drive-time band. The graph
        // engine already computes the full single-source cost vector; this keeps it,
        // which is exactly what ComputeServiceAreaAsync throws away.
        public async Task<IsochroneResultDto> ComputeIsochroneAsync(
            IsochroneRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateSpeed(request.AvgSpeedKmh);
            ValidateBands(request.BandMinutes);

            var graph = await _graphService.GetRedlandsGraphAsync(cancellationToken);
            if (!graph.Nodes.Any(n => n.Id == request.OriginNodeId))
                throw new ArgumentException("The origin node was not found in the road network.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            // Costs are along-road kilometres and come back parallel to graph.Nodes —
            // index i is the cost to Nodes[i], not to node id i.
            var costs = await _graphService.ComputeDistancesAsync(graph, request.OriginNodeId, cancellationToken);

            var bands = request.BandMinutes;
            var bandCounts = new int[bands.Count + 1];
            var nodes = new List<IsochroneNodeDto>(costs.Length);
            var unreachable = 0;

            for (var i = 0; i < graph.Nodes.Count && i < costs.Length; i++)
            {
                var cost = costs[i];
                if (!IsFinite(cost))
                {
                    unreachable++;
                    continue;
                }

                var minutes = cost / request.AvgSpeedKmh * 60.0;
                var bandIndex = BandFor(minutes, bands);
                bandCounts[bandIndex]++;

                nodes.Add(new IsochroneNodeDto
                {
                    NodeId = graph.Nodes[i].Id,
                    Latitude = graph.Nodes[i].Latitude,
                    Longitude = graph.Nodes[i].Longitude,
                    Minutes = Math.Round(minutes, 3),
                    BandIndex = bandIndex
                });
            }

            return new IsochroneResultDto
            {
                // The isochrone rides on the shared graph engine rather than the facility
                // kernel, so it reports that engine's availability.
                NativeAccelerated = SpatialGraphNativeBridge.IsAvailable,
                OriginNodeId = request.OriginNodeId,
                Nodes = nodes,
                BandCounts = [.. bandCounts],
                ReachableNodes = nodes.Count,
                UnreachableNodes = unreachable
            };
        }

        // Builds the candidate-to-demand travel-time matrix over the road network, sites
        // the requested number of stations, and reports the result against the stations
        // operating today.
        public async Task<OptimizeCoverageResultDto> OptimizeAsync(
            OptimizeCoverageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateDemandPoints(request.DemandPoints);
            ValidateCandidates(request.Candidates);
            ValidateStationCount(request.StationCount, request.Candidates.Count);
            ValidateSpeed(request.AvgSpeedKmh);

            if (request.ObjectiveMode is < 0 or > 2)
                throw new ArgumentException("Objective mode must be 0 (mean), 1 (p90), or 2 (coverage).", nameof(request));
            if (!IsFinite(request.FirstThresholdMinutes) || request.FirstThresholdMinutes <= 0)
                throw new ArgumentException("The first response threshold must be greater than zero.", nameof(request));
            if (!IsFinite(request.SecondThresholdMinutes) || request.SecondThresholdMinutes < request.FirstThresholdMinutes)
                throw new ArgumentException("The second response threshold must be at least the first.", nameof(request));

            var iterations = request.MaxIterations <= 0 ? 1 : Math.Min(request.MaxIterations, MaxIterations);

            cancellationToken.ThrowIfCancellationRequested();

            var candidateNodeIds = request.Candidates.Select(c => c.NodeId).ToList();
            var demandNodeIds = request.DemandPoints.Select(d => d.NodeId).ToList();

            var matrixWatch = Stopwatch.StartNew();
            var graph = await _graphService.GetRedlandsGraphAsync(cancellationToken);
            var distanceKm = await _graphService.ComputeDistanceMatrixAsync(
                graph, candidateNodeIds, demandNodeIds, cancellationToken);
            matrixWatch.Stop();

            var candidateCount = request.Candidates.Count;
            var demandCount = request.DemandPoints.Count;
            if (distanceKm.Length < (long)candidateCount * demandCount)
                throw new ArgumentException("The travel-time matrix could not be built for the supplied sites.", nameof(request));

            // Distance to time happens outside the graph search, never inside it: the A*
            // heuristic is admissible only while edge costs stay in kilometres.
            var minutesPerKm = 60.0 / request.AvgSpeedKmh;
            var costMatrix = new double[candidateCount * demandCount];
            for (var i = 0; i < costMatrix.Length; i++)
                costMatrix[i] = distanceKm[i] * minutesPerKm;

            var weights = new double[demandCount];
            for (var d = 0; d < demandCount; d++)
                weights[d] = request.DemandPoints[d].CallVolume;

            cancellationToken.ThrowIfCancellationRequested();

            var solveWatch = Stopwatch.StartNew();

            bool nativeAccelerated;
            int[] chosenIndices;
            double[] iterationObjectives;
            if (FacilityLocationNativeBridge.TrySolvePMedian(
                    costMatrix, candidateCount, demandCount, weights,
                    request.StationCount, request.ObjectiveMode,
                    request.FirstThresholdMinutes, iterations, _logger, out var nativeSolve))
            {
                chosenIndices = nativeSolve!.ChosenCandidateIndices;
                iterationObjectives = nativeSolve.IterationObjectives;
                nativeAccelerated = true;
            }
            else
            {
                (chosenIndices, iterationObjectives) = SolveManaged(
                    costMatrix, candidateCount, demandCount, weights,
                    request.StationCount, request.ObjectiveMode,
                    request.FirstThresholdMinutes, iterations, cancellationToken);
                nativeAccelerated = false;
            }

            solveWatch.Stop();

            var optimized = Evaluate(
                costMatrix, candidateCount, demandCount, weights, chosenIndices,
                request.FirstThresholdMinutes, request.SecondThresholdMinutes, ref nativeAccelerated);

            var baselineIndices = ResolveBaselineIndices(request);
            CoverageEvaluation? baseline = null;
            if (baselineIndices.Length > 0)
            {
                var baselineNative = nativeAccelerated;
                baseline = Evaluate(
                    costMatrix, candidateCount, demandCount, weights, baselineIndices,
                    request.FirstThresholdMinutes, request.SecondThresholdMinutes, ref baselineNative);
            }

            return BuildResult(request, chosenIndices, iterationObjectives, optimized, baseline,
                matrixWatch.Elapsed.TotalMilliseconds, solveWatch.Elapsed.TotalMilliseconds, nativeAccelerated);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static void ValidateDemandPoints(List<DemandPointDto> demandPoints)
        {
            if (demandPoints is null || demandPoints.Count == 0)
                throw new ArgumentException("At least one demand point is required.", "request");
            if (demandPoints.Count > MaxDemandPoints)
                throw new ArgumentException($"Coverage analysis is limited to {MaxDemandPoints} demand points.", "request");

            foreach (var point in demandPoints)
            {
                if (!IsFinite(point.CallVolume) || point.CallVolume <= 0)
                    throw new ArgumentException("Call volume must be greater than zero.", "request");
            }
        }

        private static void ValidateCandidates(List<CandidateSiteDto> candidates)
        {
            if (candidates is null || candidates.Count == 0)
                throw new ArgumentException("At least one candidate site is required.", "request");
            if (candidates.Count > MaxCandidates)
                throw new ArgumentException($"Coverage analysis is limited to {MaxCandidates} candidate sites.", "request");
        }

        private static void ValidateStationCount(int stationCount, int candidateCount)
        {
            if (stationCount < 1)
                throw new ArgumentException("Station count must be at least one.", "request");
            if (stationCount > candidateCount)
                throw new ArgumentException("Station count cannot exceed the number of candidate sites.", "request");
            if (stationCount > MaxStations)
                throw new ArgumentException($"Station count is limited to {MaxStations}.", "request");
        }

        private static void ValidateSpeed(double avgSpeedKmh)
        {
            if (!IsFinite(avgSpeedKmh) || avgSpeedKmh < 5 || avgSpeedKmh > 120)
                throw new ArgumentException("Average speed must be between 5 and 120 km/h.", "request");
        }

        private static void ValidateBands(List<double> bandMinutes)
        {
            if (bandMinutes is null || bandMinutes.Count == 0 || bandMinutes.Count > MaxIsochroneBands)
                throw new ArgumentException("Isochrone bands must be ascending positive values.", "request");

            var previous = 0.0;
            foreach (var band in bandMinutes)
            {
                if (!IsFinite(band) || band <= previous)
                    throw new ArgumentException("Isochrone bands must be ascending positive values.", "request");
                previous = band;
            }
        }

        // ── Isochrone banding ───────────────────────────────────────────────────

        // Returns the index of the first band whose upper bound contains the travel time,
        // or bands.Count for anything past the last band. Bands are ascending and
        // half-open on the upper side, so every reachable node lands in exactly one.
        private static int BandFor(double minutes, List<double> bands)
        {
            for (var i = 0; i < bands.Count; i++)
            {
                if (minutes <= bands[i])
                    return i;
            }

            return bands.Count;
        }

        // ── Managed p-median (mirrors the native kernel) ────────────────────────

        // Greedy seeding followed by Teitz-Bart vertex substitution. Mirrors
        // Facility_SolvePMedian line for line, including the tie-breaking comparator, so
        // both paths choose the same stations.
        private static (int[] Chosen, double[] Objectives) SolveManaged(
            double[] costMatrix,
            int candidateCount,
            int demandCount,
            double[] weights,
            int facilityCount,
            int objectiveMode,
            double coverageThreshold,
            int maxIterations,
            CancellationToken cancellationToken)
        {
            var totalWeight = 0.0;
            for (var d = 0; d < demandCount; d++)
                totalWeight += weights[d];

            var orderScratch = new int[demandCount];
            var nearest = new double[demandCount];
            var second = new double[demandCount];
            var nearestSlot = new int[demandCount];
            var trial = new double[demandCount];
            var isChosen = new bool[candidateCount];
            var chosen = new List<int>(facilityCount);

            Array.Fill(nearest, double.PositiveInfinity);

            for (var k = 0; k < facilityCount; k++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var best = -1;
                var bestObjective = double.PositiveInfinity;

                for (var c = 0; c < candidateCount; c++)
                {
                    if (isChosen[c])
                        continue;

                    var offset = c * demandCount;
                    for (var d = 0; d < demandCount; d++)
                        trial[d] = Math.Min(costMatrix[offset + d], nearest[d]);

                    var objective = EvaluateObjective(
                        trial, weights, demandCount, totalWeight, objectiveMode, coverageThreshold, orderScratch);

                    if (best < 0 || objective < bestObjective - ImprovementEpsilon)
                    {
                        bestObjective = objective;
                        best = c;
                    }
                }

                if (best < 0)
                    break;

                isChosen[best] = true;
                chosen.Add(best);

                var bestOffset = best * demandCount;
                for (var d = 0; d < demandCount; d++)
                {
                    if (costMatrix[bestOffset + d] < nearest[d])
                        nearest[d] = costMatrix[bestOffset + d];
                }
            }

            RebuildNearest(costMatrix, demandCount, chosen, nearest, nearestSlot, second);

            var currentObjective = EvaluateObjective(
                nearest, weights, demandCount, totalWeight, objectiveMode, coverageThreshold, orderScratch);

            var objectives = new List<double>(maxIterations + 1) { currentObjective };

            var p = chosen.Count;
            for (var iteration = 0; iteration < maxIterations && p < candidateCount; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bestSlot = -1;
                var bestCandidate = -1;
                var bestObjective = currentObjective;

                for (var slot = 0; slot < p; slot++)
                {
                    for (var c = 0; c < candidateCount; c++)
                    {
                        if (isChosen[c])
                            continue;

                        var offset = c * demandCount;
                        for (var d = 0; d < demandCount; d++)
                        {
                            // Dropping the facility in `slot` leaves the demand points it
                            // served on their second-nearest; everything else keeps its
                            // current nearest. That is what turns each trial from
                            // O(demand x p) into O(demand).
                            var baseCost = nearestSlot[d] == slot ? second[d] : nearest[d];
                            trial[d] = Math.Min(costMatrix[offset + d], baseCost);
                        }

                        var objective = EvaluateObjective(
                            trial, weights, demandCount, totalWeight, objectiveMode, coverageThreshold, orderScratch);

                        if (objective < bestObjective - ImprovementEpsilon)
                        {
                            bestObjective = objective;
                            bestSlot = slot;
                            bestCandidate = c;
                        }
                    }
                }

                if (bestSlot < 0)
                    break;

                isChosen[chosen[bestSlot]] = false;
                isChosen[bestCandidate] = true;
                chosen[bestSlot] = bestCandidate;

                RebuildNearest(costMatrix, demandCount, chosen, nearest, nearestSlot, second);
                currentObjective = bestObjective;
                objectives.Add(currentObjective);
            }

            chosen.Sort();
            return ([.. chosen], [.. objectives]);
        }

        // Recomputes nearest and second-nearest open facility per demand point.
        // nearestSlot holds the position within `chosen`, not the candidate index, because
        // substitution removes facilities by position.
        private static void RebuildNearest(
            double[] costMatrix,
            int demandCount,
            List<int> chosen,
            double[] nearest,
            int[] nearestSlot,
            double[] second)
        {
            Array.Fill(nearest, double.PositiveInfinity);
            Array.Fill(second, double.PositiveInfinity);
            Array.Fill(nearestSlot, -1);

            for (var slot = 0; slot < chosen.Count; slot++)
            {
                var offset = chosen[slot] * demandCount;
                for (var d = 0; d < demandCount; d++)
                {
                    var cost = costMatrix[offset + d];
                    if (cost < nearest[d])
                    {
                        second[d] = nearest[d];
                        nearest[d] = cost;
                        nearestSlot[d] = slot;
                    }
                    else if (cost < second[d])
                    {
                        second[d] = cost;
                    }
                }
            }
        }

        // Scores an assignment. Lower is better in every mode, so the search treats them
        // uniformly — including coverage mode, which is expressed as uncovered demand.
        private static double EvaluateObjective(
            double[] distances,
            double[] weights,
            int count,
            double totalWeight,
            int objectiveMode,
            double coverageThreshold,
            int[] orderScratch)
        {
            if (count <= 0 || totalWeight <= 0)
                return 0;

            if (objectiveMode == 0)
            {
                var sum = 0.0;
                for (var d = 0; d < count; d++)
                    sum += weights[d] * distances[d];
                return sum / totalWeight;
            }

            if (objectiveMode == 2)
            {
                var uncovered = 0.0;
                for (var d = 0; d < count; d++)
                {
                    if (!(distances[d] <= coverageThreshold))
                        uncovered += weights[d];
                }
                return uncovered / totalWeight;
            }

            BuildResponseOrder(distances, count, orderScratch);
            return WeightedPercentile(distances, weights, orderScratch, count, totalWeight, 0.90);
        }

        // Orders demand points by response time, breaking ties on index so the ordering is
        // a total order — that is what makes the managed and native percentiles identical
        // despite different sort implementations.
        private static void BuildResponseOrder(double[] distances, int count, int[] order)
        {
            for (var d = 0; d < count; d++)
                order[d] = d;

            Array.Sort(order, 0, count, Comparer<int>.Create((a, b) =>
            {
                var da = distances[a];
                var db = distances[b];
                if (da < db) return -1;
                if (db < da) return 1;
                return a.CompareTo(b);
            }));
        }

        // Demand-weighted percentile: walk the response-time ordering accumulating call
        // volume and return the response time at which cumulative volume first reaches the
        // target fraction. This is NOT the percentile of the unweighted list — a single
        // heavily weighted neighbourhood can set the p90 on its own, which is precisely
        // the behaviour NFPA 1710 is written to capture.
        private static double WeightedPercentile(
            double[] distances,
            double[] weights,
            int[] order,
            int count,
            double totalWeight,
            double fraction)
        {
            if (count <= 0 || totalWeight <= 0)
                return 0;

            var target = totalWeight * fraction;
            var cumulative = 0.0;
            for (var i = 0; i < count; i++)
            {
                var d = order[i];
                cumulative += weights[d];
                if (cumulative >= target)
                    return distances[d];
            }

            return distances[order[count - 1]];
        }

        // ── Coverage evaluation ─────────────────────────────────────────────────

        private sealed record CoverageEvaluation(
            int[] Assignment,
            double[] ResponseTimes,
            double Mean,
            double P50,
            double P90,
            double PercentWithinFirst,
            double PercentWithinSecond);

        // Prefers the native kernel; clears the caller's native flag when it falls back so
        // the reported flag describes the whole computation, not just the solve.
        private CoverageEvaluation Evaluate(
            double[] costMatrix,
            int candidateCount,
            int demandCount,
            double[] weights,
            int[] chosenIndices,
            double firstThreshold,
            double secondThreshold,
            ref bool nativeAccelerated)
        {
            if (FacilityLocationNativeBridge.TryEvaluateCoverage(
                    costMatrix, candidateCount, demandCount, weights, chosenIndices,
                    firstThreshold, secondThreshold, _logger, out var native))
            {
                return new CoverageEvaluation(
                    native!.Assignment, native.ResponseTimes, native.Mean, native.P50, native.P90,
                    native.PercentWithinFirstThreshold, native.PercentWithinSecondThreshold);
            }

            nativeAccelerated = false;
            return EvaluateManaged(
                costMatrix, demandCount, weights, chosenIndices, firstThreshold, secondThreshold);
        }

        // Mirrors Facility_EvaluateCoverage.
        private static CoverageEvaluation EvaluateManaged(
            double[] costMatrix,
            int demandCount,
            double[] weights,
            int[] chosenIndices,
            double firstThreshold,
            double secondThreshold)
        {
            var responseTimes = new double[demandCount];
            var assignment = new int[demandCount];
            Array.Fill(responseTimes, double.PositiveInfinity);
            Array.Fill(assignment, -1);

            foreach (var candidate in chosenIndices)
            {
                var offset = candidate * demandCount;
                for (var d = 0; d < demandCount; d++)
                {
                    if (costMatrix[offset + d] < responseTimes[d])
                    {
                        responseTimes[d] = costMatrix[offset + d];
                        assignment[d] = candidate;
                    }
                }
            }

            var totalWeight = 0.0;
            var weightedSum = 0.0;
            var withinFirst = 0.0;
            var withinSecond = 0.0;
            for (var d = 0; d < demandCount; d++)
            {
                totalWeight += weights[d];
                weightedSum += weights[d] * responseTimes[d];
                if (responseTimes[d] <= firstThreshold)
                    withinFirst += weights[d];
                if (responseTimes[d] <= secondThreshold)
                    withinSecond += weights[d];
            }

            var order = new int[demandCount];
            BuildResponseOrder(responseTimes, demandCount, order);

            return new CoverageEvaluation(
                assignment,
                responseTimes,
                totalWeight > 0 ? weightedSum / totalWeight : 0,
                WeightedPercentile(responseTimes, weights, order, demandCount, totalWeight, 0.50),
                WeightedPercentile(responseTimes, weights, order, demandCount, totalWeight, 0.90),
                totalWeight > 0 ? 100.0 * withinFirst / totalWeight : 0,
                totalWeight > 0 ? 100.0 * withinSecond / totalWeight : 0);
        }

        // ── Result shaping ──────────────────────────────────────────────────────

        // Existing stations come from the request when supplied, otherwise from the
        // IsExisting flag on the candidate list. With neither, there is nothing to compare
        // against and the baseline block stays empty.
        private static int[] ResolveBaselineIndices(OptimizeCoverageRequestDto request)
        {
            var declared = new HashSet<int>(request.ExistingStationIds ?? []);
            var indices = new List<int>();

            for (var c = 0; c < request.Candidates.Count; c++)
            {
                var candidate = request.Candidates[c];
                if (declared.Contains(candidate.Id) || candidate.IsExisting)
                    indices.Add(c);
            }

            return [.. indices];
        }

        private static OptimizeCoverageResultDto BuildResult(
            OptimizeCoverageRequestDto request,
            int[] chosenIndices,
            double[] iterationObjectives,
            CoverageEvaluation optimized,
            CoverageEvaluation? baseline,
            double matrixBuildMs,
            double solveMs,
            bool nativeAccelerated)
        {
            var assignments = BuildAssignments(request, optimized);
            var baselineAssignments = baseline is null
                ? []
                : BuildAssignments(request, baseline);

            var chosenIds = chosenIndices
                .Where(i => i >= 0 && i < request.Candidates.Count)
                .Select(i => request.Candidates[i].Id)
                .OrderBy(id => id)
                .ToList();

            return new OptimizeCoverageResultDto
            {
                NativeAccelerated = nativeAccelerated,
                ChosenCandidateIds = chosenIds,
                Optimized = ToStats(optimized),
                Baseline = baseline is null ? new CoverageStatsDto() : ToStats(baseline),
                Assignments = assignments,
                BaselineAssignments = baselineAssignments,
                IterationObjectives = [.. iterationObjectives.Select(o => Math.Round(o, 6))],
                MatrixBuildMs = Math.Round(matrixBuildMs, 2),
                SolveMs = Math.Round(solveMs, 2),
                MeetsNfpa1710 = optimized.PercentWithinFirst >= Nfpa1710CompliancePercent
            };
        }

        // Projects the nearest-facility assignment onto request identifiers. Unreachable
        // demand points report candidate 0 and zero minutes rather than infinity, which
        // does not survive JSON serialization.
        private static List<DemandAssignmentDto> BuildAssignments(
            OptimizeCoverageRequestDto request,
            CoverageEvaluation evaluation)
        {
            var assignments = new List<DemandAssignmentDto>(request.DemandPoints.Count);
            for (var d = 0; d < request.DemandPoints.Count && d < evaluation.Assignment.Length; d++)
            {
                var candidateIndex = evaluation.Assignment[d];
                var reachable = candidateIndex >= 0
                    && candidateIndex < request.Candidates.Count
                    && IsFinite(evaluation.ResponseTimes[d]);

                assignments.Add(new DemandAssignmentDto
                {
                    DemandPointId = request.DemandPoints[d].Id,
                    AssignedCandidateId = reachable ? request.Candidates[candidateIndex].Id : 0,
                    ResponseMinutes = reachable ? Math.Round(evaluation.ResponseTimes[d], 3) : 0
                });
            }

            return assignments;
        }

        private static CoverageStatsDto ToStats(CoverageEvaluation evaluation) => new()
        {
            MeanMinutes = Round(evaluation.Mean),
            P50Minutes = Round(evaluation.P50),
            P90Minutes = Round(evaluation.P90),
            PercentWithinFirstThreshold = Round(evaluation.PercentWithinFirst),
            PercentWithinSecondThreshold = Round(evaluation.PercentWithinSecond)
        };

        private static double Round(double value) => IsFinite(value) ? Math.Round(value, 3) : 0;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
