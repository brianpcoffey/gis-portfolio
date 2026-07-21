using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;

namespace Portfolio.Web.Pages;

public class IndexModel : PageModel
{
    // Relative to wwwroot. Kept beside the code that describes it so the file and
    // its metadata cannot drift apart.
    internal const string ResumeRelativePath = "files/Brian-Coffey-Resume.pdf";

    private readonly IWebHostEnvironment _env;

    public IndexModel(IWebHostEnvironment env) => _env = env;

    /// <summary>
    /// Facts about the résumé download, e.g. "PDF · 1 page · 80 KB · Updated July 2026".
    /// Empty when the file is missing, so the view renders nothing rather than a lie.
    /// </summary>
    public string ResumeMeta { get; private set; } = string.Empty;

    public void OnGet()
    {
        var path = Path.Combine(_env.WebRootPath, ResumeRelativePath.Replace('/', Path.DirectorySeparatorChar));
        ResumeMeta = BuildResumeMeta(path);
    }

    /// <summary>
    /// Reads the file's real size and modified date rather than hard-coding them.
    /// A hard-coded "80 KB · Updated July 2026" silently becomes false the next time
    /// the PDF is replaced, and a stale date on a résumé is worse than no date.
    /// </summary>
    internal static string BuildResumeMeta(string path, Func<string, int?>? pageCounter = null)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists) return string.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return string.Empty;
        }

        var parts = new List<string> { "PDF" };

        var pages = (pageCounter ?? CountPdfPages)(path);
        // Only stated when the count is plausible. Claiming "0 pages", or a wild
        // number because the scan misfired, is worse than omitting it entirely.
        if (pages is >= 1 and <= 20)
        {
            parts.Add(pages == 1 ? "1 page" : $"{pages} pages");
        }

        parts.Add($"{Math.Max(1, (int)Math.Round(info.Length / 1024.0))} KB");
        parts.Add($"Updated {info.LastWriteTime:MMMM yyyy}");

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Counts page objects in a PDF without taking on a PDF library dependency for
    /// one line of UI text. Returns null when the file cannot be read.
    /// </summary>
    private static int? CountPdfPages(string path)
    {
        try
        {
            // System.IO.File spelled out: PageModel inherits a File(...) method that
            // otherwise wins name resolution here.
            // Latin1 maps every byte to a distinct char, so binary stream data
            // cannot decode into sequences that happen to look like page markers.
            var text = System.Text.Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(path));
            // "/Type /Page" but not "/Type /Pages" — the latter is the tree node.
            return Regex.Matches(text, @"/Type\s*/Page(?![s])", RegexOptions.None, TimeSpan.FromSeconds(2)).Count;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            return null;
        }
    }
}
