using System.Text.RegularExpressions;

namespace Portfolio.Tests.Contracts;

/// <summary>
/// WP-46 (SEC-09): the ArcGIS API script and widget themes are pinned with Subresource Integrity.
/// The digests live in wwwroot/js/esri-theme.js (the loader three pages use) and, for the page that
/// loads the API from markup, in the Plant Operations Dashboard's Index.cshtml. That page is
/// [Authorize] and static JS is not otherwise covered, so these tests read the source files: the two
/// copies of the init.js digest cannot drift apart and no ArcGIS script or stylesheet loses its
/// integrity unnoticed. They cannot tell whether a digest matches Esri's current bytes; that is
/// checked in a browser.
/// </summary>
public class ArcGisSriTests
{
    private static readonly string WebProject =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Portfolio.Web"));

    private const string Sha384 = "sha384-[A-Za-z0-9+/]{64}";

    private static string Read(params string[] relative) =>
        File.ReadAllText(Path.Combine(relative.Prepend(WebProject).ToArray()));

    private static IEnumerable<string> Files(string folder, string pattern) =>
        Directory.EnumerateFiles(Path.Combine(WebProject, folder), pattern, SearchOption.AllDirectories);

    [Fact]
    public void EsriThemeLoader_PinsTheApiScriptAndBothThemes_WithDistinctDigests()
    {
        // Arrange
        var js = Read("wwwroot", "js", "esri-theme.js");

        // Act
        var script = Regex.Match(js, $"integrity\\[SCRIPT_URL\\] = \"({Sha384})\"");
        var light = Regex.Match(js, $"integrity\\[themeHref\\(false\\)\\] = \"({Sha384})\"");
        var dark = Regex.Match(js, $"integrity\\[themeHref\\(true\\)\\] = \"({Sha384})\"");

        // Assert
        Assert.Contains("var SCRIPT_URL = BASE + \"init.js\";", js, StringComparison.Ordinal);
        Assert.True(script.Success && light.Success && dark.Success, "esri-theme.js must pin init.js and both themes");
        Assert.Equal(3, new[] { script.Groups[1].Value, light.Groups[1].Value, dark.Groups[1].Value }.Distinct().Count());
    }

    [Theory]
    [InlineData("applyIntegrity(s, SCRIPT_URL);", "s.src = SCRIPT_URL;")]
    [InlineData("applyIntegrity(themeLink, href);", "themeLink.href = href;")]
    [InlineData("applyIntegrity(themeLink, next);", "themeLink.href = next;")]
    public void EsriThemeLoader_SetsIntegrityBeforeTheUrl_SoTheFetchIsCheckedFromTheFirstByte(string integrityCall, string urlAssignment)
    {
        // Arrange
        var js = Read("wwwroot", "js", "esri-theme.js");

        // Act
        var integrityAt = js.IndexOf(integrityCall, StringComparison.Ordinal);
        var urlAt = js.IndexOf(urlAssignment, StringComparison.Ordinal);

        // Assert
        Assert.True(integrityAt >= 0, $"missing: {integrityCall}");
        Assert.True(urlAt > integrityAt, $"{urlAssignment} must come after {integrityCall}");
    }

    [Fact]
    public void PlantOperationsDashboard_LoadsInitJsWithTheLoadersDigest()
    {
        // Arrange
        var js = Read("wwwroot", "js", "esri-theme.js");
        var page = Read("Pages", "Projects", "PlantOperationsDashboard", "Index.cshtml");
        var loaderDigest = Regex.Match(js, $"integrity\\[SCRIPT_URL\\] = \"({Sha384})\"").Groups[1].Value;

        // Act
        var tags = Regex.Matches(page, @"<script\b[^>]*js\.arcgis\.com[^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value).ToList();

        // Assert
        var tag = Assert.Single(tags);
        Assert.Contains("src=\"https://js.arcgis.com/4.29/init.js\"", tag, StringComparison.Ordinal);
        Assert.Contains($"integrity=\"{loaderDigest}\"", tag, StringComparison.Ordinal);
        Assert.Contains("crossorigin=\"anonymous\"", tag, StringComparison.Ordinal);
        Assert.Contains(" defer", tag, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcGisCdn_IsReferencedOnlyBySriPinnedTags_OrByTheLoader()
    {
        // Arrange
        var cdnTags = new List<string>();
        var strayJs = new List<string>();

        // Act
        foreach (var cshtml in Files("Pages", "*.cshtml"))
        {
            cdnTags.AddRange(Regex.Matches(File.ReadAllText(cshtml), @"<(?:script|link)\b[^>]*js\.arcgis\.com[^>]*>", RegexOptions.IgnoreCase)
                .Select(m => m.Value));
        }
        foreach (var file in Files(Path.Combine("wwwroot", "js"), "*.js")
                     .Where(f => !string.Equals(Path.GetFileName(f), "esri-theme.js", StringComparison.Ordinal)))
        {
            if (File.ReadAllText(file).Contains("js.arcgis.com", StringComparison.OrdinalIgnoreCase))
            {
                strayJs.Add(Path.GetFileName(file));
            }
        }

        // Assert
        Assert.All(cdnTags, t =>
        {
            Assert.Matches($"integrity=\"{Sha384}\"", t);
            Assert.Contains("crossorigin=\"anonymous\"", t, StringComparison.Ordinal);
        });
        Assert.Empty(strayJs);
    }
}
