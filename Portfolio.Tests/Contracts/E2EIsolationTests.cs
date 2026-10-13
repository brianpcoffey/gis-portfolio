namespace Portfolio.Tests.Contracts;

/// <summary>EN-02a: the browser tests' sign-in shortcut exists only in the E2E project.</summary>
/// <remarks>
/// <c>Portfolio.E2E</c> signs a visitor in through a test-only endpoint (<c>/__e2e/signin</c>) that it wires
/// into its own host. A route like that in the shipped site would be a login bypass, so this test reads the
/// Web project's source (as <see cref="ArcGisSriTests"/> does) and fails if any file or file name mentions it.
/// </remarks>
public class E2EIsolationTests
{
    private const string Marker = "__e2e";

    private static readonly string WebProject =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Portfolio.Web"));

    // Text formats only: build output, images, fonts and the résumé PDF cannot carry a hand-written route.
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cshtml", ".csproj", ".props", ".targets", ".json", ".js", ".mjs", ".css", ".html", ".htm",
        ".xml", ".config", ".txt", ".md", ".yml", ".yaml", ".svg", ".csv"
    };

    // Only the project's own top-level bin/ and obj/ (compiler output); vendored libraries are scanned.
    private static bool IsBuildOutput(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is "bin" or "obj";

    [Fact]
    public void WebProject_AnyFile_DoesNotMentionTheE2ESignInHook()
    {
        // Arrange
        var files = Directory.EnumerateFiles(WebProject, "*", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(Path.GetRelativePath(WebProject, f)))
            .ToList();

        // Act
        var offenders = files
            .Where(f => Path.GetFileName(f).Contains(Marker, StringComparison.OrdinalIgnoreCase) ||
                        (TextExtensions.Contains(Path.GetExtension(f)) &&
                         File.ReadAllText(f).Contains(Marker, StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetRelativePath(WebProject, f))
            .ToList();

        // Assert
        Assert.Contains(files, f => Path.GetFileName(f) == "Program.cs"); // the scan really covered the project
        Assert.True(offenders.Count == 0, $"'{Marker}' must exist only in Portfolio.E2E, but found it in: {string.Join(", ", offenders)}");
    }
}
