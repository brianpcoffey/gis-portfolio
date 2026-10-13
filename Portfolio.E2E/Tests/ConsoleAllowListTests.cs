using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The console allow-list decides which browser errors a scenario may ignore. It needs no browser, so it always runs: a
/// rule that is too wide would hide real errors from every page-health test.
/// </summary>
public class ConsoleAllowListTests
{
    private const string EsriBase = "https://js.arcgis.com/4.29/";

    [Theory]
    [InlineData("Evaluating a string as JavaScript violates the following Content Security Policy directive because 'unsafe-eval' is not an allowed source of script: \"script-src 'self'\"")]
    [InlineData("Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source of script in the following Content Security Policy directive: \"script-src 'self'\"")]
    public void ForPage_ArcGisEvalProbeFromTheEsriFolder_IsAllowed(string text)
    {
        // Arrange
        var allowList = ConsoleAllowList.ForPage(EsriBase);

        // Act
        var allowed = allowList.IsAllowed(new ConsoleEntry(text, EsriBase + "init.js:79:0"));

        // Assert
        Assert.True(allowed);
    }

    [Theory]
    [InlineData("https://evil.example/init.js:1:1")]
    [InlineData("https://js.arcgis.com/4.30/init.js:79:0")]
    [InlineData("http://127.0.0.1:5000/js/HomeFinder/map.js:10:3")]
    [InlineData("")]
    public void ForPage_EvalProbeFromAnotherScript_IsNotAllowed(string location)
    {
        // Arrange: the same words, but not from the ArcGIS folder the page loaded.
        var allowList = ConsoleAllowList.ForPage(EsriBase);
        var entry = new ConsoleEntry("Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source of script", location);

        // Act
        var allowed = allowList.IsAllowed(entry);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void ForPage_OtherErrorFromTheEsriFolder_IsNotAllowed()
    {
        // Arrange
        var allowList = ConsoleAllowList.ForPage(EsriBase);
        var entry = new ConsoleEntry("Uncaught TypeError: x is not a function", EsriBase + "esri/views/MapView.js:323:0");

        // Act
        var allowed = allowList.IsAllowed(entry);

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ForPage_PageWithoutArcGis_AllowsNothing(string? esriBaseUrl)
    {
        // Arrange
        var allowList = ConsoleAllowList.ForPage(esriBaseUrl);
        var entry = new ConsoleEntry("Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source of script", EsriBase + "init.js:1:1");

        // Act
        var allowed = allowList.IsAllowed(entry);

        // Assert
        Assert.False(allowed);
        Assert.Empty(allowList.Reasons);
    }

    [Fact]
    public void None_AnyError_IsNotAllowed()
    {
        // Arrange
        var entry = new ConsoleEntry("anything", "anywhere:1:1");

        // Act
        var allowed = ConsoleAllowList.None.IsAllowed(entry);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void Allow_EntryWithTwoConditions_NeedsBothToMatch()
    {
        // Arrange
        var allowList = ConsoleAllowList.None.Allow("a known message from one script", textStartsWith: "Map failed", locationStartsWith: "http://app/");

        // Act and assert
        Assert.True(allowList.IsAllowed(new ConsoleEntry("Map failed to load: x", "http://app/js/map.js:1:1")));
        Assert.False(allowList.IsAllowed(new ConsoleEntry("Map failed to load: x", "http://other/js/map.js:1:1")));
        Assert.False(allowList.IsAllowed(new ConsoleEntry("Something else", "http://app/js/map.js:1:1")));
        Assert.Equal(["a known message from one script"], allowList.Reasons);
    }

    [Fact]
    public void Allow_DoesNotChangeTheListItWasCalledOn()
    {
        // Arrange
        var original = ConsoleAllowList.None;

        // Act
        var extended = original.Allow("a reason", textContains: "x");

        // Assert
        Assert.Empty(original.Reasons);
        Assert.Single(extended.Reasons);
    }

    [Fact]
    public void Allow_EntryWithoutConditions_Throws()
    {
        // Arrange and act
        var exception = Assert.Throws<ArgumentException>(() => ConsoleAllowList.None.Allow("accepts everything"));

        // Assert
        Assert.StartsWith("An allow-list entry needs at least one condition", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Allow_EntryWithoutReason_Throws(string reason)
    {
        // Arrange and act
        var exception = Assert.Throws<ArgumentException>(() => ConsoleAllowList.None.Allow(reason, textContains: "x"));

        // Assert
        Assert.StartsWith("An allow-list entry needs a reason.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_Entry_ListsTheTextThenTheLocationInBrackets()
    {
        // Arrange
        var entry = new ConsoleEntry("boom", "http://app/x.js:3:4");

        // Act
        var text = entry.ToString();

        // Assert
        Assert.Equal("boom [http://app/x.js:3:4]", text);
    }
}
