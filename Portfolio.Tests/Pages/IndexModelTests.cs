using Portfolio.Web.Pages;

namespace Portfolio.Tests.Pages;

/// <summary>
/// Covers the résumé download metadata. The point of reading these from the file
/// is that they cannot go stale, so the tests are mostly about what happens when
/// the file or the page count is not what we expect.
/// </summary>
public class IndexModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "resume-meta-" + Guid.NewGuid().ToString("N"));

    public IndexModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* temp dir */ }
        GC.SuppressFinalize(this);
    }

    private string WriteFile(string name, int bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public void BuildResumeMeta_MissingFile_ReturnsEmpty()
    {
        var meta = IndexModel.BuildResumeMeta(Path.Combine(_dir, "does-not-exist.pdf"));

        // Empty rather than "0 KB" — the view omits the line entirely instead of
        // stating something untrue about a file that is not there.
        Assert.Equal(string.Empty, meta);
    }

    [Fact]
    public void BuildResumeMeta_SinglePage_UsesSingularAndRealSize()
    {
        var path = WriteFile("one.pdf", 81_747);

        var meta = IndexModel.BuildResumeMeta(path, _ => 1);

        Assert.StartsWith("PDF · 1 page · ", meta);
        Assert.Contains("80 KB", meta);          // 81747 / 1024 rounds to 80
        Assert.DoesNotContain("1 pages", meta);
    }

    [Fact]
    public void BuildResumeMeta_MultiplePages_UsesPlural()
    {
        var path = WriteFile("three.pdf", 4096);

        var meta = IndexModel.BuildResumeMeta(path, _ => 3);

        Assert.Contains("3 pages", meta);
    }

    [Theory]
    [InlineData(0)]     // a scan that matched nothing
    [InlineData(21)]    // implausible for a résumé — the scan misfired
    [InlineData(null)]  // unreadable
    public void BuildResumeMeta_ImplausiblePageCount_OmitsPageCount(int? pages)
    {
        var path = WriteFile("odd.pdf", 2048);

        var meta = IndexModel.BuildResumeMeta(path, _ => pages);

        Assert.DoesNotContain("page", meta);
        Assert.StartsWith("PDF · ", meta);
        Assert.Contains("KB", meta);
    }

    [Fact]
    public void BuildResumeMeta_TinyFile_NeverReportsZeroKb()
    {
        var path = WriteFile("tiny.pdf", 12);

        var meta = IndexModel.BuildResumeMeta(path, _ => 1);

        Assert.Contains("1 KB", meta);
        Assert.DoesNotContain("0 KB", meta);
    }

    [Fact]
    public void BuildResumeMeta_IncludesMonthAndYearOfLastWrite()
    {
        var path = WriteFile("dated.pdf", 4096);
        var when = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(path, when);

        var meta = IndexModel.BuildResumeMeta(path, _ => 1);

        Assert.Contains("Updated March 2026", meta);
    }

    [Fact]
    public void ResumeRelativePath_PointsAtAFileThatShips()
    {
        // Guards the one string that ties the metadata to the actual download; if
        // the PDF is renamed, this fails here rather than silently blanking the line.
        var webroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Portfolio.Web", "wwwroot");
        var pdf = Path.GetFullPath(Path.Combine(webroot, IndexModel.ResumeRelativePath));

        Assert.True(File.Exists(pdf), $"Expected the résumé at {pdf}");
    }
}
