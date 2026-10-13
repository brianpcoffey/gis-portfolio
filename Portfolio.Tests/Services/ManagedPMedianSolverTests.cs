using Portfolio.Services.Services;
using Portfolio.Services.Services.Response;

namespace Portfolio.Tests.Services
{
    // Direct tests of the managed p-median solver on a hand-computable instance
    // (3 candidates, 2 unit-weight demand points; cost is candidate-major).
    public class ManagedPMedianSolverTests
    {
        private static readonly double[] Weights = [1, 1];

        private static readonly double[] Costs =
        [
            1, 10,   // candidate 0
            10, 1,   // candidate 1
            5, 5,    // candidate 2
        ];

        private static (int[] Chosen, double[] Objectives) Solve(
            double[] costs, int candidates, int stations, int mode, double threshold = 0,
            int iterations = 10, CancellationToken ct = default) =>
            ManagedPMedianSolver.SolveManaged(costs, candidates, 2, Weights, stations, mode, threshold, iterations, ct);

        [Fact]
        public void SingleStation_MeanMode_PicksCentralCandidate()
        {
            var (chosen, objectives) = Solve(Costs, 3, 1, 0);

            Assert.Equal([2], chosen);
            Assert.Equal([5.0], objectives);
        }

        [Fact]
        public void TwoStations_MeanMode_SubstitutionImprovesOnGreedySeed()
        {
            // Greedy opens candidate 2 then candidate 0 (objective 3); swapping slot 0 for
            // candidate 1 reaches {0, 1} (objective 1).
            var (chosen, objectives) = Solve(Costs, 3, 2, 0);

            Assert.Equal([0, 1], chosen);
            Assert.Equal([3.0, 1.0], objectives);
        }

        [Fact]
        public void TiedCandidates_LowestIndexWins()
        {
            double[] tied = [4, 4, 4, 4, 9, 9];

            var (chosen, objectives) = Solve(tied, 3, 1, 0);

            Assert.Equal([0], chosen);
            Assert.Equal([4.0], objectives);
        }

        [Fact]
        public void CoverageMode_ReportsUncoveredFraction()
        {
            var (chosen, objectives) = Solve(Costs, 3, 1, 2, threshold: 2);

            Assert.Equal([0], chosen);
            Assert.Equal([0.5], objectives);
        }

        [Fact]
        public void PercentileMode_ReturnsOneObjectiveForSeedAndNeverIncreases()
        {
            var (chosen, objectives) = Solve(Costs, 3, 2, 1);

            Assert.Equal(2, chosen.Length);
            Assert.NotEmpty(objectives);
            for (var i = 1; i < objectives.Length; i++)
                Assert.True(objectives[i] < objectives[i - 1]);
        }

        [Fact]
        public void ZeroIterations_ReturnsGreedySeedOnly()
        {
            var (chosen, objectives) = Solve(Costs, 3, 2, 0, iterations: 0);

            Assert.Equal([0, 2], chosen);
            Assert.Equal([3.0], objectives);
        }

        [Fact]
        public void CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => Solve(Costs, 3, 1, 0, ct: cts.Token));
        }

        [Fact]
        public void EvaluateManaged_AssignsNearestChosenCandidate()
        {
            var evaluation = ManagedPMedianSolver.EvaluateManaged(Costs, 2, Weights, [0, 1], 2, 8);

            Assert.Equal([0, 1], evaluation.Assignment);
            Assert.Equal([1.0, 1.0], evaluation.ResponseTimes);
            Assert.Equal(1.0, evaluation.Mean);
            Assert.Equal(100.0, evaluation.PercentWithinFirst);
            Assert.Equal(100.0, evaluation.PercentWithinSecond);
        }
    }
}
