namespace Portfolio.Tests.Contracts;

/// <summary>EN-02a: the browser tests' sign-in shortcut exists only in the E2E project.</summary>
/// <remarks>
/// <c>Portfolio.E2E</c> signs a visitor in through a test-only endpoint (<c>/__e2e/signin</c>) that it wires
/// into its own host. A route like that in the shipped site would be a login bypass, so this test reads the
/// source of every project the Dockerfile publishes (Web, Services, Repositories and Common; as
/// <see cref="ArcGisSriTests"/> does for Web) and fails if any file or file name mentions it.
/// </remarks>
public class E2EIsolationTests
{
    private const string Marker = "__e2e";

    private static readonly string RepositoryRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    // Text formats only: build output, images, fonts and the résumé PDF cannot carry a hand-written route.
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cshtml", ".csproj", ".props", ".targets", ".json", ".js", ".mjs", ".css", ".html", ".htm",
        ".xml", ".config", ".txt", ".md", ".yml", ".yaml", ".svg", ".csv"
    };

    // Only the project's own top-level bin/ and obj/ (compiler output); vendored libraries are scanned.
    private static bool IsBuildOutput(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is "bin" or "obj";

    // The four projects `docker build` publishes (Web -> Services -> Repositories -> Common).
    [Theory]
    [InlineData("Portfolio.Web")]
    [InlineData("Portfolio.Services")]
    [InlineData("Portfolio.Repositories")]
    [InlineData("Portfolio.Common")]
    public void ShippedProject_AnyFile_DoesNotMentionTheE2ESignInHook(string project)
    {
        // Arrange
        var projectDirectory = Path.Combine(RepositoryRoot, project);
        var files = Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(Path.GetRelativePath(projectDirectory, f)))
            .ToList();

        // Act
        var offenders = files
            .Where(f => Path.GetFileName(f).Contains(Marker, StringComparison.OrdinalIgnoreCase) ||
                        (TextExtensions.Contains(Path.GetExtension(f)) &&
                         File.ReadAllText(f).Contains(Marker, StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetRelativePath(projectDirectory, f))
            .ToList();

        // Assert
        Assert.Contains(files, f => Path.GetFileName(f) == project + ".csproj"); // the scan really covered the project
        Assert.True(offenders.Count == 0, $"'{Marker}' must exist only in Portfolio.E2E, but found it in {project}: {string.Join(", ", offenders)}");
    }
}
