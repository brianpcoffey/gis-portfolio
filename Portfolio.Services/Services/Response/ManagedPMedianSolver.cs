namespace Portfolio.Services.Services.Response
{
    internal sealed record CoverageEvaluation(
        int[] Assignment,
        double[] ResponseTimes,
        double Mean,
        double P50,
        double P90,
        double PercentWithinFirst,
        double PercentWithinSecond);

    // Managed p-median solver and coverage evaluation, mirroring the native
    // facility-location kernel (Facility_SolvePMedian / Facility_EvaluateCoverage).
    internal static class ManagedPMedianSolver
    {
        // An improvement must beat the incumbent by more than this to be accepted; mirrors
        // kImprovementEpsilon in native/facility_location_kernel/src/facility_location_kernel.cpp.
        private const double ImprovementEpsilon = 1e-12;

        // Mutable working set shared by the seeding and substitution phases.
        private sealed class SearchState
        {
            public required double[] CostMatrix { get; init; }
            public required int CandidateCount { get; init; }
            public required int DemandCount { get; init; }
            public required double[] Weights { get; init; }
            public required double TotalWeight { get; init; }
            public required int ObjectiveMode { get; init; }
            public required double CoverageThreshold { get; init; }
            public required int[] OrderScratch { get; init; }
            public required double[] Nearest { get; init; }
            public required double[] Second { get; init; }
            public required int[] NearestSlot { get; init; }
            public required double[] Trial { get; init; }
            public required bool[] IsChosen { get; init; }
            public required List<int> Chosen { get; init; }

            public double TrialObjective() => EvaluateObjective(
                Trial, Weights, DemandCount, TotalWeight, ObjectiveMode, CoverageThreshold, OrderScratch);
        }

        // Greedy seeding followed by Teitz-Bart vertex substitution. Mirrors
        // Facility_SolvePMedian line for line, including the tie-breaking comparator, so
        // both paths choose the same stations.
        internal static (int[] Chosen, double[] Objectives) SolveManaged(
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

            var state = new SearchState
            {
                CostMatrix = costMatrix,
                CandidateCount = candidateCount,
                DemandCount = demandCount,
                Weights = weights,
                TotalWeight = totalWeight,
                ObjectiveMode = objectiveMode,
                CoverageThreshold = coverageThreshold,
                OrderScratch = new int[demandCount],
                Nearest = new double[demandCount],
                Second = new double[demandCount],
                NearestSlot = new int[demandCount],
                Trial = new double[demandCount],
                IsChosen = new bool[candidateCount],
                Chosen = new List<int>(facilityCount),
            };

            Array.Fill(state.Nearest, double.PositiveInfinity);

            GreedySeed(state, facilityCount, cancellationToken);

            RebuildNearest(costMatrix, demandCount, state.Chosen, state.Nearest, state.NearestSlot, state.Second);

            var currentObjective = EvaluateObjective(
                state.Nearest, weights, demandCount, totalWeight, objectiveMode, coverageThreshold, state.OrderScratch);

            var objectives = new List<double>(maxIterations + 1) { currentObjective };

            var p = state.Chosen.Count;
            for (var iteration = 0; iteration < maxIterations && p < candidateCount; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (bestSlot, bestCandidate, bestObjective) = FindBestSwap(state, p, currentObjective);

                if (bestSlot < 0)
                    break;

                state.IsChosen[state.Chosen[bestSlot]] = false;
                state.IsChosen[bestCandidate] = true;
                state.Chosen[bestSlot] = bestCandidate;

                RebuildNearest(costMatrix, demandCount, state.Chosen, state.Nearest, state.NearestSlot, state.Second);
                currentObjective = bestObjective;
                objectives.Add(currentObjective);
            }

            state.Chosen.Sort();
            return ([.. state.Chosen], [.. objectives]);
        }

        // Greedy phase: repeatedly opens the candidate that most improves the objective.
        private static void GreedySeed(SearchState state, int facilityCount, CancellationToken cancellationToken)
        {
            for (var k = 0; k < facilityCount; k++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var best = FindBestGreedyCandidate(state);

                if (best < 0)
                    break;

                state.IsChosen[best] = true;
                state.Chosen.Add(best);

                var bestOffset = best * state.DemandCount;
                for (var d = 0; d < state.DemandCount; d++)
                {
                    if (state.CostMatrix[bestOffset + d] < state.Nearest[d])
                        state.Nearest[d] = state.CostMatrix[bestOffset + d];
                }
            }
        }

        private static int FindBestGreedyCandidate(SearchState state)
        {
            var best = -1;
            var bestObjective = double.PositiveInfinity;

            for (var c = 0; c < state.CandidateCount; c++)
            {
                if (state.IsChosen[c])
                    continue;

                var offset = c * state.DemandCount;
                for (var d = 0; d < state.DemandCount; d++)
                    state.Trial[d] = Math.Min(state.CostMatrix[offset + d], state.Nearest[d]);

                var objective = state.TrialObjective();

                if (best < 0 || objective < bestObjective - ImprovementEpsilon)
                {
                    bestObjective = objective;
                    best = c;
                }
            }

            return best;
        }

        // One substitution pass: finds the (slot, candidate) swap with the lowest objective
        // that beats the incumbent. Returns slot -1 when no swap improves. Iteration order
        // (slot-major, then candidate) and the strict-improvement comparator are load-bearing.
        private static (int Slot, int Candidate, double Objective) FindBestSwap(
            SearchState state, int p, double currentObjective)
        {
            var bestSlot = -1;
            var bestCandidate = -1;
            var bestObjective = currentObjective;

            for (var slot = 0; slot < p; slot++)
            {
                for (var c = 0; c < state.CandidateCount; c++)
                {
                    if (state.IsChosen[c])
                        continue;

                    FillSwapTrial(state, c, slot);

                    var objective = state.TrialObjective();

                    if (objective < bestObjective - ImprovementEpsilon)
                    {
                        bestObjective = objective;
                        bestSlot = slot;
                        bestCandidate = c;
                    }
                }
            }

            return (bestSlot, bestCandidate, bestObjective);
        }

        private static void FillSwapTrial(SearchState state, int candidate, int slot)
        {
            var offset = candidate * state.DemandCount;
            for (var d = 0; d < state.DemandCount; d++)
            {
                // Dropping the facility in `slot` leaves the demand points it
                // served on their second-nearest; everything else keeps its
                // current nearest. That is what turns each trial from
                // O(demand x p) into O(demand).
                var baseCost = state.NearestSlot[d] == slot ? state.Second[d] : state.Nearest[d];
                state.Trial[d] = Math.Min(state.CostMatrix[offset + d], baseCost);
            }
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

        // Mirrors Facility_EvaluateCoverage.
        internal static CoverageEvaluation EvaluateManaged(
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
    }
}
