namespace Portfolio.Services.Native
{
    /// <summary>
    /// Result of one native p-median solve: the chosen candidate indices and the objective
    /// trace. The kernel takes flat primitive buffers rather than structs, so this type
    /// exists only to keep the bridge's out-parameter list readable.
    /// </summary>
    internal sealed class FacilitySolveNativeResult
    {
        public int[] ChosenCandidateIndices { get; init; } = [];

        public double[] IterationObjectives { get; init; } = [];
    }

    /// <summary>
    /// Result of one native coverage evaluation: the nearest-facility assignment, the
    /// per-demand response times, and the weighted distribution statistics.
    /// </summary>
    internal sealed class FacilityCoverageNativeResult
    {
        public int[] Assignment { get; init; } = [];

        public double[] ResponseTimes { get; init; } = [];

        public double Mean { get; init; }

        public double P50 { get; init; }

        public double P90 { get; init; }

        public double PercentWithinFirstThreshold { get; init; }

        public double PercentWithinSecondThreshold { get; init; }
    }
}
