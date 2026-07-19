using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Benchmarks;

/// <summary>
/// <c>--diag</c>: prints the raw shape of the results that the parity checksums fold, for the
/// workloads where native and managed disagree. Run it once per path and diff the output —
/// the checksum tells you that something differs, this tells you what.
/// </summary>
internal static class Diagnostics
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var disabled = Environment.GetEnvironmentVariable("PORTFOLIO_DISABLE_NATIVE");
        Console.WriteLine($"# PORTFOLIO_DISABLE_NATIVE={disabled}");

        // ---- portfolio_scoring
        var repo = new DeterministicPropertyRepository(count: 200_000, seed: 20_260_719u);
        var scoring = new HomeScoringService(repo, NullLogger<HomeScoringService>.Instance, TimeProvider.System);
        var top = await scoring.GetTopPropertiesAsync(new HomeSearchPreferencesDto
        {
            MaxPrice = 2_000_000,
            MaxMonthlyBudget = 9_000,
            MinBedrooms = 1,
            MinBathrooms = 1,
            MinSqft = 400,
            MaxCommuteMin = 120
        }, 50, ct);

        Console.WriteLine($"scoring.count={top.Count}");
        foreach (var p in top.Take(10))
            Console.WriteLine($"scoring[{p.Rank}] id={p.PropertyId} composite={p.CompositeScore:R} afford={p.AffordabilityScore:R} monthly={p.EstimatedMonthlyCost}");
        Console.WriteLine($"scoring.compositeSum={top.Sum(p => p.CompositeScore):R}");
        Console.WriteLine("scoring.ids=" + string.Join(",", top.Select(p => p.PropertyId)));
        Console.WriteLine("scoring.composites=" + string.Join(",", top.Select(p => p.CompositeScore.ToString("R"))));

        // ---- spatial_geometry_kernel clip
        var geometry = new SpatialGeometryService(NullLogger<SpatialGeometryService>.Instance);
        var subject = new List<CoordinateDto>(5_000);
        for (var i = 0; i < 5_000; i++)
        {
            var theta = i * 2.0 * Math.PI / 5_000;
            var r = 1.0 + 0.6 * Math.Sin(theta * 9.0);
            subject.Add(new CoordinateDto { X = r * Math.Cos(theta), Y = r * Math.Sin(theta) });
        }
        var clipped = await geometry.ClipToBoundingBoxAsync(new PolygonClipRequestDto
        {
            Subject = subject,
            MinX = -0.9,
            MinY = -0.9,
            MaxX = 0.9,
            MaxY = 0.9
        }, ct);
        Console.WriteLine($"clip.vertices={clipped.Vertices.Count} sum={clipped.Vertices.Sum(v => v.X * 0.5 + v.Y):R}");
        foreach (var v in clipped.Vertices.Take(6))
            Console.WriteLine($"clip[] x={v.X:R} y={v.Y:R}");

        // ---- viewshed_kernel
        var viewshed = new ViewshedService(NullLogger<ViewshedService>.Instance);
        const int dim = 500;
        var elevation = new List<double>(dim * dim);
        for (var y = 0; y < dim; y++)
        {
            for (var x = 0; x < dim; x++)
            {
                var fx = x / (double)dim;
                var fy = y / (double)dim;
                elevation.Add(500.0 + 300.0 * Math.Sin(fx * 5.0) * Math.Cos(fy * 7.0) + 120.0 * Math.Sin((fx + fy) * 19.0));
            }
        }
        var vs = await viewshed.ComputeAsync(new ViewshedRequestDto
        {
            Width = dim,
            Height = dim,
            CellSize = 30,
            ObserverX = dim / 2,
            ObserverY = dim / 2,
            ObserverHeight = 25,
            Elevation = elevation
        }, ct);
        Console.WriteLine($"viewshed.visible={vs.VisibleCells} total={vs.TotalCells}");
        var firstDifferences = new List<int>();
        for (var i = 0; i < vs.Visibility.Count && firstDifferences.Count < 20; i++)
            if (vs.Visibility[i] != 0) firstDifferences.Add(i);
        Console.WriteLine("viewshed.firstVisibleIndices=" + string.Join(",", firstDifferences));
    }
}
