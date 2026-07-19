using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portfolio.Benchmarks;

/// <summary>
/// Native-versus-managed benchmark harness for all thirteen kernels.
///
/// <para><b>Why it spawns children.</b> Every bridge probes availability exactly once, in a
/// static constructor. One process can therefore only ever measure one path. The parent runs
/// itself twice — once with <c>PORTFOLIO_DISABLE_NATIVE=0</c> and once with <c>=1</c> —
/// collects each child's JSON from stdout and joins the two on workload name.</para>
///
/// <para><b>Why the median.</b> Best-of-N rewards a single lucky run and systematically
/// flatters whichever path has the higher variance. A 1.66x figure previously published for
/// <c>spatial_graph_engine</c> came from a single timed run after one warmup and did not
/// survive re-measurement. Every number here is the median of nine timed iterations after
/// three discarded warmups, with min and max reported so the spread is visible.</para>
/// </summary>
internal static class Program
{
    private const int WarmupIterations = 3;
    private const int TimedIterations = 9;

    // Two doubles agreeing to within this relative distance are treated as parity. Summation
    // order differs between the two paths, so the last bits of a floating-point fold are not
    // required to be equal; anything above this is a real divergence and is reported as one.
    private const double ParityTolerance = 1e-9;

    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--diag"))
        {
            await Diagnostics.RunAsync(CancellationToken.None);
            return 0;
        }

        if (args.Contains("--run"))
            return await RunChildAsync();

        return await RunOrchestratorAsync();
    }

    // ------------------------------------------------------------------ child

    private static async Task<int> RunChildAsync()
    {
        var pinned = PinToFirstCore();

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var workloads = await Workloads.BuildAsync(ct);
        var results = new List<WorkloadResult>(workloads.Count);

        foreach (var workload in workloads)
        {
            // Setup is outside the timed region for every workload; only the service call is
            // inside it. GC is settled between workloads, not between iterations, so a
            // collection triggered by the workload itself still counts against it.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            for (var i = 0; i < WarmupIterations; i++)
                await workload.Run(ct);

            var samples = new double[TimedIterations];
            var checksum = 0.0;
            bool? nativeFlag = null;

            for (var i = 0; i < TimedIterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var outcome = await workload.Run(ct);
                sw.Stop();

                samples[i] = outcome.StageMilliseconds ?? sw.Elapsed.TotalMilliseconds;
                checksum = outcome.Checksum;
                nativeFlag = outcome.NativeAccelerated;
            }

            Array.Sort(samples);

            results.Add(new WorkloadResult
            {
                Kernel = workload.Kernel,
                Workload = workload.Name,
                Note = workload.Note,
                MedianMs = samples[TimedIterations / 2],
                MinMs = samples[0],
                MaxMs = samples[^1],
                Checksum = checksum,
                NativeAccelerated = nativeFlag
            });
        }

        var report = new ChildReport
        {
            Pinned = pinned,
            NativeEnabled = !IsDisabledByEnvironment(),
            RuntimeVersion = Environment.Version.ToString(),
            FrameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Results = results
        };

        Console.Out.Write(JsonSerializer.Serialize(report, JsonContext.Default.ChildReport));
        Console.Out.Flush();
        return 0;
    }

    /// <summary>
    /// Pins the measurement process to the first logical processor and raises its priority.
    ///
    /// <para>This is not tuning, it is a correctness fix for the measurement. On a hybrid
    /// CPU the scheduler will migrate the benchmark thread between performance and efficiency
    /// cores, and the same workload then measures bimodally — one raster workload here read
    /// 184 ms on one run and 454 ms on the next from an identical binary. Without pinning,
    /// which core a run landed on is a bigger effect than native versus managed, and the
    /// comparison is meaningless.</para>
    /// </summary>
    private static bool PinToFirstCore()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;

            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                // Logical processor 2 rather than 0: CPU 0 services a disproportionate share
                // of driver interrupts and DPCs on Windows, which shows up as an occasional
                // outlier iteration. On a hybrid part the low-numbered logical processors are
                // the performance cores, so this stays off the efficiency cluster.
                process.ProcessorAffinity = Environment.ProcessorCount > 2 ? 4 : 1;
                return true;
            }
        }
        catch
        {
            // Affinity and priority are both best-effort: a restricted environment simply
            // gets noisier numbers, which the reported min-max spread will show.
        }

        return false;
    }

    private static bool IsDisabledByEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("PORTFOLIO_DISABLE_NATIVE");
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        return value.Equals("1", StringComparison.Ordinal)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ orchestrator

    private static async Task<int> RunOrchestratorAsync()
    {
        Console.Error.WriteLine("Portfolio native-kernel benchmark harness");
        Console.Error.WriteLine($"  {WarmupIterations} warmup iterations discarded, median of {TimedIterations} timed iterations reported");
        Console.Error.WriteLine();

        Console.Error.WriteLine("  [1/2] running with native kernels enabled ...");
        var native = await RunChildProcessAsync(disableNative: false);
        if (native is null) return 1;

        Console.Error.WriteLine("  [2/2] running with PORTFOLIO_DISABLE_NATIVE=1 ...");
        var managed = await RunChildProcessAsync(disableNative: true);
        if (managed is null) return 1;

        Console.Error.WriteLine();
        Console.Out.Write(Render(native, managed));
        return 0;
    }

    private static async Task<ChildReport?> RunChildProcessAsync(bool disableNative)
    {
        var host = Environment.ProcessPath;
        var assembly = Environment.GetCommandLineArgs()[0];

        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // `dotnet run` normally launches the apphost directly, but running through the muxer
        // (`dotnet Portfolio.Benchmarks.dll`) is equally valid and must re-enter the same way.
        if (host is not null && !Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = host;
        }
        else
        {
            psi.FileName = host ?? "dotnet";
            psi.ArgumentList.Add(Path.ChangeExtension(assembly, ".dll"));
        }

        psi.ArgumentList.Add("--run");
        psi.Environment["PORTFOLIO_DISABLE_NATIVE"] = disableNative ? "1" : "0";

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start the benchmark child process.");

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await stdout;
        var error = await stderr;

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine($"Child process failed (exit {process.ExitCode}).");
            if (!string.IsNullOrWhiteSpace(error))
                Console.Error.WriteLine(error);
            return null;
        }

        return JsonSerializer.Deserialize(output, JsonContext.Default.ChildReport);
    }

    // ------------------------------------------------------------------ rendering

    private static string Render(ChildReport native, ChildReport managed)
    {
        var sb = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;

        var byName = managed.Results.ToDictionary(r => r.Kernel + "|" + r.Workload);

        sb.AppendLine("### Native vs managed, median of 9 timed iterations after 3 warmups");
        sb.AppendLine();
        sb.AppendLine(string.Create(culture, $"Machine: {Environment.ProcessorCount} logical cores, {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}"));
        sb.AppendLine(string.Create(culture, $"Runtime: {native.FrameworkDescription} (CLR {native.RuntimeVersion})"));
        sb.AppendLine(string.Create(culture, $"Measured: {DateTime.UtcNow:yyyy-MM-dd} UTC"));
        sb.AppendLine(native.Pinned && managed.Pinned
            ? "Both children pinned to one logical processor at high priority (hybrid-core migration otherwise dominates the signal)."
            : "Processor affinity could not be set; treat the min-max spread as the confidence interval.");
        sb.AppendLine();
        sb.AppendLine("| Kernel | Workload | Native median | Managed median | Speedup | Native min–max | Managed min–max | Parity |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---|");

        var mismatches = new List<string>();
        var unavailable = new List<string>();

        foreach (var n in native.Results)
        {
            if (!byName.TryGetValue(n.Kernel + "|" + n.Workload, out var m))
                continue;

            var speedup = n.MedianMs <= 0 ? double.NaN : m.MedianMs / n.MedianMs;
            var parity = Parity(n.Checksum, m.Checksum);

            if (parity == ParityKind.Mismatch)
                mismatches.Add($"{n.Kernel} — {n.Workload}: native {n.Checksum:R} vs managed {m.Checksum:R}");

            if (n.NativeAccelerated == false)
                unavailable.Add($"{n.Kernel} — {n.Workload}");

            var speedupCell = double.IsNaN(speedup)
                ? "n/a"
                : speedup >= 1.0
                    ? string.Create(culture, $"**{speedup:F2}x**")
                    : string.Create(culture, $"{speedup:F2}x");

            sb.AppendLine(string.Create(culture,
                $"| `{n.Kernel}` | {n.Workload} | {Ms(n.MedianMs)} | {Ms(m.MedianMs)} | {speedupCell} | {Ms(n.MinMs)}–{Ms(n.MaxMs)} | {Ms(m.MinMs)}–{Ms(m.MaxMs)} | {ParityText(parity)} |"));
        }

        sb.AppendLine();

        // Notes are per-workload caveats; deduplicated so the table does not repeat itself.
        var notes = native.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.Note))
            .Select(r => r.Note!)
            .Distinct()
            .ToList();

        if (notes.Count > 0)
        {
            sb.AppendLine("Notes:");
            foreach (var note in notes)
                sb.AppendLine($"- {note}");
            sb.AppendLine();
        }

        sb.AppendLine("Parity legend: `exact` = bit-identical checksum; `~ULP` = agrees to within 1e-9 relative "
            + "(floating-point summation order differs between the two paths); `MISMATCH` = the two paths "
            + "produced different results and the speedup is meaningless.");
        sb.AppendLine();

        if (mismatches.Count > 0)
        {
            sb.AppendLine("**CHECKSUM MISMATCHES — these are correctness bugs, not performance results:**");
            foreach (var mismatch in mismatches)
                sb.AppendLine($"- {mismatch}");
        }
        else
        {
            sb.AppendLine("All parity checks passed: every workload produced the same result on both paths.");
        }

        if (unavailable.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**The following workloads reported `NativeAccelerated == false` in the native-enabled child** "
                + "— the shared library did not load, so the row compares managed against managed:");
            foreach (var u in unavailable)
                sb.AppendLine($"- {u}");
        }

        return sb.ToString();
    }

    private static string Ms(double value) => value switch
    {
        >= 100 => value.ToString("F0", CultureInfo.InvariantCulture) + " ms",
        >= 10 => value.ToString("F1", CultureInfo.InvariantCulture) + " ms",
        _ => value.ToString("F2", CultureInfo.InvariantCulture) + " ms"
    };

    private enum ParityKind { Exact, Close, Mismatch }

    private static ParityKind Parity(double a, double b)
    {
        if (a.Equals(b)) return ParityKind.Exact;
        if (double.IsNaN(a) || double.IsNaN(b)) return ParityKind.Mismatch;

        var scale = Math.Max(Math.Abs(a), Math.Abs(b));
        if (scale == 0) return ParityKind.Exact;

        return Math.Abs(a - b) / scale <= ParityTolerance ? ParityKind.Close : ParityKind.Mismatch;
    }

    private static string ParityText(ParityKind kind) => kind switch
    {
        ParityKind.Exact => "exact",
        ParityKind.Close => "~ULP",
        _ => "**MISMATCH**"
    };
}

internal sealed class WorkloadResult
{
    public required string Kernel { get; init; }
    public required string Workload { get; init; }
    public string? Note { get; init; }
    public required double MedianMs { get; init; }
    public required double MinMs { get; init; }
    public required double MaxMs { get; init; }
    public required double Checksum { get; init; }
    public bool? NativeAccelerated { get; init; }
}

internal sealed class ChildReport
{
    public bool Pinned { get; init; }
    public required bool NativeEnabled { get; init; }
    public required string RuntimeVersion { get; init; }
    public required string FrameworkDescription { get; init; }
    public required List<WorkloadResult> Results { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(ChildReport))]
internal partial class JsonContext : JsonSerializerContext;
