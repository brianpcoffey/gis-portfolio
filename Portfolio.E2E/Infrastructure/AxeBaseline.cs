using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Portfolio.E2E.Infrastructure;

/// <summary>One accessibility violation axe reported: the rule and the element it was reported on.</summary>
/// <param name="Rule">The axe rule id, e.g. <c>color-contrast</c>.</param>
/// <param name="Target">The element's CSS selector as axe reports it (frame selectors joined with <c> &gt;&gt; </c>).</param>
public readonly record struct AxeFinding(string Rule, string Target)
{
    public override string ToString() => $"{Rule}: {Target}";
}

/// <summary>What differs between the findings a run observed and the findings the baseline lists.</summary>
/// <param name="New">Observed but not in the baseline: a regression, or a page nobody has triaged. Fails the test.</param>
/// <param name="Stale">In the baseline but no longer observed: it was fixed. Fails the test, so the baseline only shrinks.</param>
public sealed record AxeComparison(IReadOnlyList<AxeFinding> New, IReadOnlyList<AxeFinding> Stale)
{
    public bool Matches => New.Count == 0 && Stale.Count == 0;
}

/// <summary>
/// The accessibility findings the site already has, kept in <c>Portfolio.E2E/axe-baseline.json</c> (decision D-07b):
/// a shrink-only list. A finding that is not in it fails the accessibility suite; a finding that is in it and no longer
/// happens fails the suite too, and the fix is to delete that line. Fixes for the findings themselves are separate PRs.
/// </summary>
/// <remarks>
/// The file is keyed by page, then by run configuration (<c>1280-light</c>, <c>1280-dark</c>, <c>360-light</c>), then by
/// axe rule id, and lists the element selectors. A (page, configuration) pair that has no entry has an empty baseline.
/// </remarks>
public sealed class AxeBaseline
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>> _cells;

    private AxeBaseline(SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>> cells) => _cells = cells;

    /// <summary>
    /// The run configurations the accessibility suite scans every page in, and so the only ones the file may hold: the viewport
    /// width in pixels, then the colour scheme.
    /// </summary>
    public static IReadOnlyList<string> Configurations { get; } = ["1280-light", "1280-dark", "360-light"];

    /// <summary>Every (page, configuration) the baseline has an entry for, empty ones included.</summary>
    public IEnumerable<(string Page, string Configuration)> Entries =>
        _cells.SelectMany(page => page.Value.Keys.Select(configuration => (page.Key, configuration)));

    /// <summary>The file's location in the source tree (found by walking up from the test binaries), or <c>null</c> when there is no source tree.</summary>
    public static string? SourcePath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.E2E.csproj")))
                return Path.Combine(dir.FullName, "axe-baseline.json");
        }

        return null;
    }

    /// <summary>Reads the baseline from <paramref name="path"/>; a missing file is an empty baseline.</summary>
    public static AxeBaseline Load(string? path) =>
        path is not null && File.Exists(path) ? Parse(File.ReadAllText(path)) : new AxeBaseline([]);

    /// <summary>Parses the JSON text of a baseline file.</summary>
    public static AxeBaseline Parse(string json)
    {
        var cells = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("pages", out var pages))
        {
            foreach (var page in pages.EnumerateObject())
            {
                foreach (var configuration in page.Value.EnumerateObject())
                {
                    // An empty object is a cell that was triaged and had nothing to report: keep it.
                    Touch(cells, page.Name, configuration.Name);
                    foreach (var rule in configuration.Value.EnumerateObject())
                    {
                        foreach (var target in rule.Value.EnumerateArray())
                            Add(cells, page.Name, configuration.Name, new AxeFinding(rule.Name, target.GetString() ?? string.Empty));
                    }
                }
            }
        }

        return new AxeBaseline(cells);
    }

    /// <summary>The baseline findings for one page in one configuration.</summary>
    public IReadOnlyList<AxeFinding> For(string page, string configuration)
    {
        if (!_cells.TryGetValue(page, out var configurations) || !configurations.TryGetValue(configuration, out var rules))
            return [];

        return [.. rules.SelectMany(rule => rule.Value.Select(target => new AxeFinding(rule.Key, target)))];
    }

    /// <summary>True when the baseline has an entry (even an empty one) for the page and configuration.</summary>
    public bool Has(string page, string configuration) =>
        _cells.TryGetValue(page, out var configurations) && configurations.ContainsKey(configuration);

    /// <summary>Compares what a run saw with what the baseline lists. Order and duplicates do not matter.</summary>
    public static AxeComparison Compare(IEnumerable<AxeFinding> baseline, IEnumerable<AxeFinding> observed)
    {
        var expected = new HashSet<AxeFinding>(baseline);
        var actual = new HashSet<AxeFinding>(observed);

        return new AxeComparison(
            [.. actual.Where(f => !expected.Contains(f)).OrderBy(f => f.Rule, StringComparer.Ordinal).ThenBy(f => f.Target, StringComparer.Ordinal)],
            [.. expected.Where(f => !actual.Contains(f)).OrderBy(f => f.Rule, StringComparer.Ordinal).ThenBy(f => f.Target, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Records what a run observed for one page and configuration, keeping the baseline shrink-only: where the baseline already has
    /// the cell, only findings still observed stay; where it does not (a page nobody has triaged), the observed findings become it.
    /// </summary>
    public void Record(string page, string configuration, IEnumerable<AxeFinding> observed)
    {
        var seen = observed.ToHashSet();
        var keep = Has(page, configuration) ? For(page, configuration).Where(seen.Contains).ToList() : [.. seen];

        // An empty cell is kept as an empty object: "triaged, nothing found" differs from "never recorded".
        Touch(_cells, page, configuration).Clear();
        foreach (var finding in keep)
            Add(_cells, page, configuration, finding);
    }

    /// <summary>The file text: stable key order, one selector per line, so a change shows up as a small diff.</summary>
    public string ToJson()
    {
        // "about" sorts before "pages", and the nested sorted collections serialize as objects and arrays in key order.
        var root = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["about"] = "Accessibility findings the site already has (axe, tags wcag2a/wcag2aa/wcag21a/wcag21aa). Shrink-only: a finding missing from "
                + "this file fails the accessibility suite, and so does a line here that no longer occurs. Fix the page in its own PR, then delete the "
                + "lines. See Portfolio.E2E/Tests/AccessibilityTests.cs.",
            ["pages"] = _cells
        };

        return JsonSerializer.Serialize(root, WriteOptions) + "\n";
    }

    // The rules of one (page, configuration) cell, created empty when it does not exist yet.
    private static SortedDictionary<string, SortedSet<string>> Touch(
        SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>> cells,
        string page,
        string configuration)
    {
        if (!cells.TryGetValue(page, out var configurations))
            cells[page] = configurations = new SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>(StringComparer.Ordinal);
        if (!configurations.TryGetValue(configuration, out var rules))
            configurations[configuration] = rules = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        return rules;
    }

    private static void Add(
        SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>> cells,
        string page,
        string configuration,
        AxeFinding finding)
    {
        var rules = Touch(cells, page, configuration);
        if (!rules.TryGetValue(finding.Rule, out var targets))
            rules[finding.Rule] = targets = new SortedSet<string>(StringComparer.Ordinal);
        targets.Add(finding.Target);
    }
}

/// <summary>
/// The xUnit assembly fixture behind <c>PORTFOLIO_E2E_AXE_RECORD=1</c>: while it is set, the accessibility suite records what it
/// saw instead of comparing, and this writes <c>axe-baseline.json</c> once the whole run is over. Recording never adds a finding to
/// a (page, configuration) the baseline already covers (see <see cref="AxeBaseline.Record"/>), so it can start a baseline or shrink
/// one but cannot grow it.
/// </summary>
public sealed class AxeRecorder : IAsyncDisposable
{
    /// <summary>Environment variable that switches the suite from comparing to recording.</summary>
    public const string Variable = "PORTFOLIO_E2E_AXE_RECORD";

    private readonly ConcurrentDictionary<(string Page, string Configuration), IReadOnlyList<AxeFinding>> _observed = new();

    /// <summary>True when <see cref="Variable"/> is <c>1</c>.</summary>
    public static bool Enabled { get; } = Environment.GetEnvironmentVariable(Variable) == "1";

    /// <summary>Remembers what one run saw.</summary>
    public void Observe(string page, string configuration, IReadOnlyList<AxeFinding> findings) =>
        _observed[(page, configuration)] = findings;

    public ValueTask DisposeAsync()
    {
        var path = AxeBaseline.SourcePath();
        if (!Enabled || _observed.IsEmpty || path is null)
            return ValueTask.CompletedTask;

        var baseline = AxeBaseline.Load(path);
        foreach (var (key, findings) in _observed)
            baseline.Record(key.Page, key.Configuration, findings);

        File.WriteAllText(path, baseline.ToJson());
        return ValueTask.CompletedTask;
    }
}
