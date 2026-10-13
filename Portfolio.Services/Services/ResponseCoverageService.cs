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
                if (!double.IsFinite(cost))
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
            if (!double.IsFinite(request.FirstThresholdMinutes) || request.FirstThresholdMinutes <= 0)
                throw new ArgumentException("The first response threshold must be greater than zero.", nameof(request));
            if (!double.IsFinite(request.SecondThresholdMinutes) || request.SecondThresholdMinutes < request.FirstThresholdMinutes)
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
                (chosenIndices, iterationObjectives) = ManagedPMedianSolver.SolveManaged(
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
                if (!double.IsFinite(point.CallVolume) || point.CallVolume <= 0)
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
            if (!double.IsFinite(avgSpeedKmh) || avgSpeedKmh < 5 || avgSpeedKmh > 120)
                throw new ArgumentException("Average speed must be between 5 and 120 km/h.", "request");
        }

        private static void ValidateBands(List<double> bandMinutes)
        {
            if (bandMinutes is null || bandMinutes.Count == 0 || bandMinutes.Count > MaxIsochroneBands)
                throw new ArgumentException("Isochrone bands must be ascending positive values.", "request");

            var previous = 0.0;
            foreach (var band in bandMinutes)
            {
                if (!double.IsFinite(band) || band <= previous)
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

        // ── Coverage evaluation ─────────────────────────────────────────────────

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
            return ManagedPMedianSolver.EvaluateManaged(
                costMatrix, demandCount, weights, chosenIndices, firstThreshold, secondThreshold);
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
                    && double.IsFinite(evaluation.ResponseTimes[d]);

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

        private static double Round(double value) => double.IsFinite(value) ? Math.Round(value, 3) : 0;
    }
}
