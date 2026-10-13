using System.Text.RegularExpressions;

namespace Portfolio.Tests.Contracts;

/// <summary>EN-06: the site's own scripts and pages reach the API only through <c>apiFetch</c>.</summary>
/// <remarks>
/// <para>
/// A state-changing <c>/api</c> request that carries an identity cookie needs an <c>X-Requested-With</c> header
/// (<c>ApiRequestHeaderMiddleware</c>), and every HTML visitor holds the <c>AnonUserId</c> cookie. A raw
/// <c>fetch()</c>, an XHR, a jQuery call, a beacon or a plain form post to <c>/api</c> would therefore be answered
/// 403 for everyone, and nothing but a user clicking the button would notice. <c>wwwroot/js/api-fetch.js</c> adds
/// the header, so this test reads the sources (as <see cref="ArcGisSriTests"/> does) and fails on any other way
/// of making a request.
/// </para>
/// <para>
/// Scanned: every script under <c>wwwroot/js</c> except <c>api-fetch.js</c> itself and anything in a
/// <c>lib</c> folder, and every <c>.cshtml</c> under <c>Pages</c> and <c>Areas</c>. Comments are ignored. A GET
/// would get away with a raw call, but one rule is simpler to keep than "raw is fine unless it writes".
/// </para>
/// </remarks>
public class ClientApiCallTests
{
    private static readonly string WebProject =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Portfolio.Web"));

    private static readonly string ScriptRoot = Path.Combine(WebProject, "wwwroot", "js");

    private static readonly string ApiFetch = Path.Combine(ScriptRoot, "api-fetch.js");

    // (Not preceded by a word character, "$" or "-") keeps "apiFetch(" and "api-fetch (" out; ".fetch(" is caught.
    // The XMLHttpRequest rule skips a quoted name, the header value api-fetch.js and scalar-config.js send.
    private static readonly (string Kind, Regex Pattern)[] RawCalls =
    [
        ("fetch()", new Regex(@"(?<![\w$\-])fetch\s*\(")),
        ("XMLHttpRequest", new Regex(@"\bXMLHttpRequest\b(?![""'])")),
        ("jQuery request", new Regex(@"(?<![\w$])(?:\$|jQuery)\.(?:ajax|post|get|getJSON)\b")),
        ("sendBeacon", new Regex(@"\bsendBeacon\b")),
        ("form posting to /api", new Regex(@"<(?:form|button|input)\b[^>]*\b(?:form)?action\s*=\s*[""']~?/api(?=[/?#""'])", RegexOptions.IgnoreCase)),
    ];

    // Block comments (JS, CSS), HTML and Razor comments, and "//" comments that start a line or follow a space
    // (so "https://" survives). Replaced by blanks of the same shape, so line numbers stay right.
    private static readonly Regex BlockComments = new(
        @"(?<=^|\s)/\*.*?\*/|<!--.*?-->|@\*.*?\*@", RegexOptions.Singleline | RegexOptions.Multiline);

    private static readonly Regex LineComments = new(@"(?<=^|\s)//[^\r\n]*", RegexOptions.Multiline);

    private static string Blank(Match match) => Regex.Replace(match.Value, @"[^\r\n]", " ");

    private static List<(int Line, string Kind, string Text)> FindRawCalls(string source)
    {
        var code = LineComments.Replace(BlockComments.Replace(source, Blank), Blank);
        var lines = source.Split('\n');
        var found = new List<(int Line, string Kind, string Text)>();
        foreach (var (kind, pattern) in RawCalls)
        {
            foreach (Match match in pattern.Matches(code))
            {
                var line = code.AsSpan(0, match.Index).Count('\n') + 1;
                found.Add((line, kind, lines[line - 1].Trim()));
            }
        }
        return found;
    }

    private static IEnumerable<string> ClientSources()
    {
        var scripts = Directory.EnumerateFiles(ScriptRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".js" or ".mjs")
            .Where(f => !string.Equals(f, ApiFetch, StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetRelativePath(ScriptRoot, f)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains("lib", StringComparer.OrdinalIgnoreCase));
        var pages = new[] { "Pages", "Areas" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(WebProject, folder), "*.cshtml", SearchOption.AllDirectories));
        return scripts.Concat(pages);
    }

    [Fact]
    public void ClientScriptsAndPages_MakeNoRawRequests_SoEveryApiCallCarriesTheCsrfHeader()
    {
        // Arrange
        var files = ClientSources().ToList();

        // Act
        var violations = files
            .SelectMany(file => FindRawCalls(File.ReadAllText(file))
                .Select(call => $"{Path.GetRelativePath(WebProject, file).Replace('\\', '/')}:{call.Line}: {call.Kind}: {call.Text}"))
            .ToList();

        // Assert
        Assert.Contains(files, f => Path.GetFileName(f) == "site.js"); // the scan really covered the scripts
        Assert.Contains(files, f => Path.GetExtension(f) == ".cshtml"); // and the pages
        Assert.True(
            violations.Count == 0,
            "Call the API through apiFetch, apiGet, apiPost, apiPut, apiDelete or apiPostForm (wwwroot/js/api-fetch.js), which add " +
            "the X-Requested-With header the API requires on state-changing calls; a raw request is answered 403 for any visitor " +
            "who holds the AnonUserId cookie. Found:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ApiFetch_IsTheOneFileThatMakesTheRawRequest_AndTheScanSeesIt()
    {
        // Act
        var calls = FindRawCalls(File.ReadAllText(ApiFetch));

        // Assert
        Assert.Contains(calls, c => c.Kind == "fetch()" && c.Text.StartsWith("return fetch(url, opts)", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("fetch('/api/v1/x', { method: 'POST' });", 1)]
    [InlineData("const r = await fetch ( url );", 1)]
    [InlineData("window.fetch(\"/api/v1/x\")", 1)]
    [InlineData("fetch\n(url)", 1)]
    [InlineData("var x = new XMLHttpRequest();", 1)]
    [InlineData("$.ajax({ url: '/api/v1/x' });", 1)]
    [InlineData("$.post('/api/v1/x', body);", 1)]
    [InlineData("$.get('/api/v1/x');", 1)]
    [InlineData("jQuery.getJSON('/api/v1/x');", 1)]
    [InlineData("navigator.sendBeacon('/api/v1/x', data);", 1)]
    [InlineData("<form method=\"post\" action=\"/api/v1/x\">", 1)]
    [InlineData("<FORM method='post' ACTION='~/api/v1/x'>", 1)]
    [InlineData("<form\n  method=\"post\"\n  action=\"/api/v1/x\">", 1)]
    [InlineData("<button formaction=\"/api/v1/x\">Go</button>", 1)]
    [InlineData("apiFetch('/api/v1/x', { method: 'POST' });", 0)]
    [InlineData("window.apiPost('/api/v1/x', body);", 0)]
    [InlineData("return fetchWithAuth(url);", 0)]
    [InlineData("// uses api-fetch (apiGet/apiPost); never fetch(url) directly", 0)]
    [InlineData("var a = 1; // fetch('/api/v1/x')", 0)]
    [InlineData("/* fetch('/api/v1/x')\n   $.post('/api/v1/x') */", 0)]
    [InlineData("<!-- <form action=\"/api/v1/x\"> -->", 0)]
    [InlineData("@* fetch('/api/v1/x') *@", 0)]
    [InlineData("headers.set('X-Requested-With', 'XMLHttpRequest');", 0)]
    [InlineData("<form method=\"post\" action=\"/Account/Logout\">", 0)]
    [InlineData("<form method=\"post\" action=\"/apix\">", 0)]
    [InlineData("var u = \"https://example.com/\"; fetch(u);", 1)]
    [InlineData("<a href=\"https://example.com/x\">fetch</a>", 0)]
    public void FindRawCalls_FlagsEveryRawWayToRequestTheApi_AndNothingElse(string source, int expected)
    {
        // Act
        var calls = FindRawCalls(source);

        // Assert
        Assert.Equal(expected, calls.Count);
    }

    [Fact]
    public void FindRawCalls_ReportsTheLineOfEachCall_AcrossCommentsThatSpanLines()
    {
        // Arrange
        var source = "var a = 1;\n/* one\n   two */\nfetch('/api/v1/x');\n\n$.post('/api/v1/y');";

        // Act
        var calls = FindRawCalls(source);

        // Assert
        Assert.Equal(new[] { (4, "fetch()"), (6, "jQuery request") }, calls.Select(c => (c.Line, c.Kind)).OrderBy(c => c.Line));
    }
}
