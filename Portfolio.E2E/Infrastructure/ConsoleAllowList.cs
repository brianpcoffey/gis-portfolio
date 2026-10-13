namespace Portfolio.E2E.Infrastructure;

/// <summary>One console message of type <c>error</c>, as the browser reported it.</summary>
/// <param name="Text">The message text.</param>
/// <param name="Location">The script location (<c>url:line:column</c>) that logged it; may be empty.</param>
public readonly record struct ConsoleEntry(string Text, string Location)
{
    /// <summary>The text followed by its location in brackets: the form test failures list.</summary>
    public override string ToString() => $"{Text} [{Location}]";
}

/// <summary>
/// The console errors a test accepts, each with the one-line reason it is accepted. The default is the empty
/// list: a page that logs an error fails its test.
/// </summary>
/// <remarks>
/// <para>
/// An entry matches only when ALL of its conditions hold (message text and script location), so it names one
/// known message from one known source instead of silencing a class of errors. Entries are added per test; the
/// only list that applies to the page-health, layout and accessibility suites is <see cref="ForPage"/>.
/// </para>
/// <para>
/// Chromium currently logs a Report-Only CSP violation at <c>info</c>, which <see cref="BrowserSession"/> does not
/// collect, so <see cref="ForPage"/> matches nothing today. It stays because it is the one message the site's
/// CSP soak already treats as expected: if a Chromium update raised the level to <c>error</c>, the suite would not
/// start failing on the ArcGIS API's own probe.
/// </para>
/// </remarks>
public sealed class ConsoleAllowList
{
    // The words every Chromium rendering of the eval violation shares: the Report-Only form at info level ("Evaluating a
    // string as JavaScript violates the following Content Security Policy directive because 'unsafe-eval' is not an
    // allowed source of script ...") and the enforced or older form ("Refused to evaluate a string as JavaScript because
    // 'unsafe-eval' is not an allowed source of script ...").
    private const string EvalViolationText = "'unsafe-eval' is not an allowed source of script";

    private readonly IReadOnlyList<Entry> _entries;

    private ConsoleAllowList(IReadOnlyList<Entry> entries) => _entries = entries;

    /// <summary>Accepts nothing.</summary>
    public static ConsoleAllowList None { get; } = new([]);

    /// <summary>The reason of every entry, for the PR description.</summary>
    public IReadOnlyList<string> Reasons => [.. _entries.Select(e => e.Reason)];

    /// <summary>
    /// The list for a page that may load the ArcGIS API: only the CSP probe the API runs itself
    /// (<c>new Function</c>, refused by the Report-Only policy because it has no <c>'unsafe-eval'</c>), and only
    /// when the script that triggered it lives under <paramref name="esriBaseUrl"/> (<c>PortfolioEsri.baseUrl</c>,
    /// read from the page). A page without ArcGIS passes <c>null</c> and gets <see cref="None"/>.
    /// </summary>
    public static ConsoleAllowList ForPage(string? esriBaseUrl) =>
        string.IsNullOrWhiteSpace(esriBaseUrl)
            ? None
            : None.Allow(
                "The ArcGIS API's own `new Function` probe, reported by the Report-Only CSP (no 'unsafe-eval' in script-src).",
                textContains: EvalViolationText,
                locationStartsWith: esriBaseUrl);

    /// <summary>
    /// Returns a copy of this list with one more entry. <paramref name="reason"/> says why the message is expected;
    /// at least one condition must be given, and a message must meet every condition that is.
    /// </summary>
    public ConsoleAllowList Allow(string reason, string? textContains = null, string? textStartsWith = null, string? locationStartsWith = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An allow-list entry needs a reason.", nameof(reason));
        if (textContains is null && textStartsWith is null && locationStartsWith is null)
            throw new ArgumentException("An allow-list entry needs at least one condition; an empty one would accept every error.");

        return new ConsoleAllowList([.. _entries, new Entry(reason, textContains, textStartsWith, locationStartsWith)]);
    }

    /// <summary>True when some entry accepts <paramref name="entry"/>.</summary>
    public bool IsAllowed(ConsoleEntry entry) => _entries.Any(e => e.Matches(entry));

    private sealed record Entry(string Reason, string? TextContains, string? TextStartsWith, string? LocationStartsWith)
    {
        public bool Matches(ConsoleEntry entry) =>
            (TextContains is null || entry.Text.Contains(TextContains, StringComparison.Ordinal)) &&
            (TextStartsWith is null || entry.Text.StartsWith(TextStartsWith, StringComparison.Ordinal)) &&
            (LocationStartsWith is null || entry.Location.StartsWith(LocationStartsWith, StringComparison.Ordinal));
    }
}
