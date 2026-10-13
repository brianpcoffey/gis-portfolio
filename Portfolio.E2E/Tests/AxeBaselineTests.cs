using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The comparison behind the shrink-only axe baseline. It needs no browser, so it always runs: a mistake here would let new
/// accessibility violations in, or keep fixed ones listed.
/// </summary>
public class AxeBaselineTests
{
    private static readonly AxeFinding Contrast = new("color-contrast", "#a");
    private static readonly AxeFinding Label = new("label", "#b");

    [Fact]
    public void Compare_SameFindingsInAnyOrder_Matches()
    {
        // Arrange
        AxeFinding[] baseline = [Contrast, Label];
        AxeFinding[] observed = [Label, Contrast, Label];

        // Act
        var comparison = AxeBaseline.Compare(baseline, observed);

        // Assert
        Assert.True(comparison.Matches);
    }

    [Fact]
    public void Compare_FindingNotInTheBaseline_IsNew()
    {
        // Arrange and act
        var comparison = AxeBaseline.Compare([Contrast], [Contrast, Label]);

        // Assert
        Assert.False(comparison.Matches);
        Assert.Equal([Label], comparison.New);
        Assert.Empty(comparison.Stale);
    }

    [Fact]
    public void Compare_BaselineFindingThatNoLongerHappens_IsStale()
    {
        // Arrange and act
        var comparison = AxeBaseline.Compare([Contrast, Label], [Contrast]);

        // Assert
        Assert.False(comparison.Matches);
        Assert.Empty(comparison.New);
        Assert.Equal([Label], comparison.Stale);
    }

    [Fact]
    public void Compare_SameRuleOnAnotherElement_IsBothNewAndStale()
    {
        // Arrange and act
        var comparison = AxeBaseline.Compare([new AxeFinding("label", "#old")], [new AxeFinding("label", "#new")]);

        // Assert
        Assert.Equal([new AxeFinding("label", "#new")], comparison.New);
        Assert.Equal([new AxeFinding("label", "#old")], comparison.Stale);
    }

    [Fact]
    public void Parse_FileText_ReturnsTheFindingsPerPageAndConfiguration()
    {
        // Arrange
        const string json = """
            { "about": "x", "pages": {
                "/a": { "1280-light": { "label": ["#x", "#y"], "color-contrast": ["#z"] }, "360-light": {} } } }
            """;

        // Act
        var baseline = AxeBaseline.Parse(json);

        // Assert
        Assert.Equal(
            [new AxeFinding("color-contrast", "#z"), new AxeFinding("label", "#x"), new AxeFinding("label", "#y")],
            baseline.For("/a", "1280-light").OrderBy(f => f.Rule, StringComparer.Ordinal).ThenBy(f => f.Target, StringComparer.Ordinal));
        Assert.Empty(baseline.For("/a", "360-light"));
        Assert.True(baseline.Has("/a", "360-light"));
        Assert.False(baseline.Has("/a", "1280-dark"));
        Assert.Empty(baseline.For("/missing", "1280-light"));
    }

    [Fact]
    public void Entries_ParsedFile_ListsEveryPageAndConfigurationIncludingEmptyOnes()
    {
        // Arrange
        var baseline = AxeBaseline.Parse(
            """{ "pages": { "/b": { "360-light": {} }, "/a": { "1280-light": { "label": ["#x"] }, "1280-dark": {} } } }""");

        // Act
        var entries = baseline.Entries.ToList();

        // Assert
        Assert.Equal([("/a", "1280-dark"), ("/a", "1280-light"), ("/b", "360-light")], entries);
    }

    // A baseline entry is only compared when the accessibility suite visits its page and configuration, so an entry for a page that
    // was renamed or removed, or with a mistyped configuration, would never be compared and never fail as stale. This is the check
    // that does not wait for a visit.
    [Fact]
    public void CommittedFile_EveryEntry_IsForAPageAndConfigurationTheSuiteScans()
    {
        // Arrange
        var path = AxeBaseline.SourcePath();
        Assert.True(path is not null && File.Exists(path), "axe-baseline.json should be next to Portfolio.E2E.csproj.");
        var pages = PageCatalog.All.Select(page => page.Path).ToHashSet(StringComparer.Ordinal);

        // Act
        var entries = AxeBaseline.Load(path).Entries.ToList();
        var unknownPages = entries.Select(entry => entry.Page).Where(page => !pages.Contains(page)).Distinct(StringComparer.Ordinal).ToList();
        var unknownConfigurations = entries
            .Where(entry => !AxeBaseline.Configurations.Contains(entry.Configuration))
            .Select(entry => $"{entry.Page}: {entry.Configuration}")
            .ToList();

        // Assert
        Assert.True(
            unknownPages.Count == 0,
            $"axe-baseline.json lists pages that PageCatalog does not have (renamed or removed? delete or rename the entries): {string.Join(", ", unknownPages)}");
        Assert.True(
            unknownConfigurations.Count == 0,
            $"axe-baseline.json has configurations the suite does not scan (one of {string.Join(", ", AxeBaseline.Configurations)}): {string.Join("; ", unknownConfigurations)}");
    }

    [Fact]
    public void ToJson_ThenParse_RoundTripsAndKeepsEmptyCells()
    {
        // Arrange
        var baseline = AxeBaseline.Parse("{}");
        baseline.Record("/a", "1280-light", [Contrast, Label]);
        baseline.Record("/a", "360-light", []);

        // Act
        var copy = AxeBaseline.Parse(baseline.ToJson());

        // Assert
        Assert.Equal(2, copy.For("/a", "1280-light").Count);
        Assert.True(copy.Has("/a", "360-light"));
        Assert.Empty(copy.For("/a", "360-light"));
    }

    [Fact]
    public void Record_CellNotInTheBaselineYet_TakesWhatWasObserved()
    {
        // Arrange
        var baseline = AxeBaseline.Parse("{}");

        // Act
        baseline.Record("/a", "1280-light", [Contrast]);

        // Assert
        Assert.Equal([Contrast], baseline.For("/a", "1280-light"));
    }

    [Fact]
    public void Record_CellAlreadyInTheBaseline_OnlyShrinks()
    {
        // Arrange
        var baseline = AxeBaseline.Parse("""{ "pages": { "/a": { "1280-light": { "color-contrast": ["#a"], "label": ["#b"] } } } }""");

        // Act: the label violation was fixed and a different one appeared.
        baseline.Record("/a", "1280-light", [Contrast, new AxeFinding("image-alt", "#new")]);

        // Assert
        Assert.Equal([Contrast], baseline.For("/a", "1280-light"));
    }
}
