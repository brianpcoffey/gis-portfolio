using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

[assembly: AssemblyFixture(typeof(AxeRecorder))]

namespace Portfolio.E2E.Tests;

/// <summary>
/// Runs axe (WCAG 2.0/2.1 A and AA) on every page and compares the violations with <c>Portfolio.E2E/axe-baseline.json</c>
/// (decision D-07b). The baseline only shrinks: a violation that is not in it fails the test, and a violation that is in it
/// and no longer happens fails the test too, so a fix has to remove its lines. The findings themselves are fixed in separate
/// pull requests.
/// </summary>
/// <remarks>
/// <para>
/// Three configurations per page: 1280 px wide in light, 1280 px in dark, and 360 px in light. The map libraries' own DOM
/// (<c>.esri-view-root</c>, <c>.leaflet-pane</c>) is excluded: it is third-party markup this site cannot change.
/// </para>
/// <para>
/// To start a baseline or to drop fixed lines, run the suite once with <c>PORTFOLIO_E2E_AXE_RECORD=1</c>. That records instead
/// of comparing and never adds a finding to a page and configuration the file already covers (see <see cref="AxeBaseline.Record"/>).
/// </para>
/// </remarks>
public sealed class AccessibilityTests(E2EHost host, AxeRecorder recorder) : BrowserTestBase(host)
{
    private const int Height = 800;
    private static readonly string[] Tags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];
    private static readonly string[][] ThirdPartyDom = [[".esri-view-root"], [".leaflet-pane"]];
    private static readonly Lazy<AxeBaseline> Baseline = new(() => AxeBaseline.Load(AxeBaseline.SourcePath()));

    /// <summary>Every page in each of the three configurations.</summary>
    public static TheoryData<string, string> PagesAndConfigurations =>
        [.. PageCatalog.All.SelectMany(page => AxeBaseline.Configurations.Select(configuration => (page.Path, configuration)))];

    [E2ETheory]
    [MemberData(nameof(PagesAndConfigurations))]
    public async Task Page_ScannedWithAxe_HasOnlyTheBaselinedViolations(string path, string configuration)
    {
        // Arrange
        var entry = PageCatalog.Find(path);
        var (width, scheme) = ParseConfiguration(configuration);
        await StartAsync(new BrowserSessionOptions(scheme, width, Height));
        // Reduced motion switches the scroll fade-in off (preferences.css). The loader also waits for the remaining
        // entrance animations to end, so axe reads final colours, not a frame of an animation. Set before the first
        // navigation: the page reads it while it starts.
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await Loader.LoadAsync(entry);

        // Act
        var result = await Page.RunAxe(
            new AxeRunContext { Exclude = [.. ThirdPartyDom.Select(selector => new AxeSelector(selector[0]))] },
            new AxeRunOptions { RunOnly = new RunOnlyOptions { Type = "tag", Values = [.. Tags] } });
        var observed = result.Violations
            .SelectMany(violation => violation.Nodes.Select(node => new AxeFinding(violation.Id, Describe(node.Target))))
            .ToList();

        // Assert
        if (AxeRecorder.Enabled)
        {
            recorder.Observe(path, configuration, observed);
            return;
        }

        var comparison = AxeBaseline.Compare(Baseline.Value.For(path, configuration), observed);
        Assert.True(comparison.Matches, Explain(path, configuration, comparison));
    }

    private static (int Width, ColorScheme Scheme) ParseConfiguration(string configuration)
    {
        var parts = configuration.Split('-');
        return (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts[1] == "dark" ? ColorScheme.Dark : ColorScheme.Light);
    }

    // FrameSelectors is the whole path to the element (the frames' selectors, then the element's own), which for an element
    // in the top document is just its selector.
    private static string Describe(AxeSelector target)
    {
        var path = target.FrameSelectors?.ToList() ?? [];
        return path.Count > 0 ? string.Join(" >> ", path) : target.Selector;
    }

    private static string Explain(string path, string configuration, AxeComparison comparison)
    {
        var lines = new List<string> { $"{path} ({configuration}) differs from axe-baseline.json." };
        if (comparison.New.Count > 0)
        {
            lines.Add($"New violations ({comparison.New.Count}): fix the page, or, if the finding is accepted for now, add it to the baseline with a reason in the PR.");
            lines.AddRange(comparison.New.Select(f => "  + " + f));
        }

        if (comparison.Stale.Count > 0)
        {
            lines.Add($"Fixed since the baseline was written ({comparison.Stale.Count}): delete these lines from axe-baseline.json (the baseline only shrinks).");
            lines.AddRange(comparison.Stale.Select(f => "  - " + f));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
