using Microsoft.Playwright;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// No page scrolls sideways at the three widths the site is designed for (a phone, a tablet and a desktop), in the light and
/// the dark theme. Each page is loaded once at desktop width and then resized, which is how a visitor rotating a phone or
/// resizing a window meets it.
/// </summary>
public sealed class LayoutTests(E2EHost host) : BrowserTestBase(host)
{
    internal static readonly (int Width, int Height)[] Viewports = [(360, 800), (768, 1024), (1280, 800)];
    private static readonly string[] Schemes = ["light", "dark"];

    // Overflows the site has today, so a suite like this can land before their fixes (those are separate pull requests). Like the
    // axe baseline it only shrinks: an overflow that is not listed fails, and a listed one that no longer happens fails too, so
    // whoever fixes the page deletes its line. Each entry says what overflows and why. Empty: every page fits at every width.
    internal static readonly Dictionary<(string Path, int Width), string> KnownOverflows = new();

    // Runs in the page. After the viewport changes it waits (in animation frames, not milliseconds) for the new width
    // to arrive and for the page's resize handlers and reflow to finish, then measures. A page that overflows only
    // until a deferred reflow has run therefore passes; one that keeps overflowing does not. The frame counts are
    // ceilings for a slow machine: 20 frames always elapse, up to 120 more are used while the page still overflows. A page that is
    // not being rendered (hidden or throttled) fires no frames, so a frame also ends after 250 ms and all the waiting after 15 s:
    // the script then measures what is there, instead of hanging until the test run's own limit.
    private const string MeasureScript =
        """
        async (expectedWidth) => {
            const deadline = Date.now() + 15000;
            const waiting = () => Date.now() < deadline;
            const frame = () => new Promise(resolve => {
                const timer = setTimeout(resolve, 250);
                requestAnimationFrame(() => { clearTimeout(timer); resolve(); });
            });
            const frames = async n => { for (let i = 0; i < n && waiting(); i++) await frame(); };
            const fits = () => document.documentElement.scrollWidth <= window.innerWidth && document.body.scrollWidth <= window.innerWidth;

            for (let i = 0; i < 120 && window.innerWidth !== expectedWidth && waiting(); i++) await frame();
            await frames(20);
            for (let i = 0; i < 120 && !fits() && waiting(); i++) await frame();

            const describe = el => {
                const classes = typeof el.className === 'string' ? el.className.trim().split(/\s+/).filter(Boolean).slice(0, 3) : [];
                return el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') + classes.map(c => '.' + c).join('');
            };
            // The widest element sticking out of the viewport that is not clipped by a scroll or overflow container.
            let widest = null;
            if (!fits()) {
                for (const el of document.body.querySelectorAll('*')) {
                    const rect = el.getBoundingClientRect();
                    if (rect.width === 0 || rect.right <= window.innerWidth + 0.5) continue;
                    let clipped = false;
                    for (let a = el.parentElement; a && a !== document.body; a = a.parentElement) {
                        if (/hidden|auto|scroll|clip/.test(getComputedStyle(a).overflowX)) { clipped = true; break; }
                    }
                    if (!clipped && (widest === null || rect.right > widest.right)) widest = { right: rect.right, text: describe(el) };
                }
            }

            return {
                fits: fits(),
                innerWidth: window.innerWidth,
                documentWidth: document.documentElement.scrollWidth,
                bodyWidth: document.body.scrollWidth,
                widest: widest ? `${widest.text} (right edge ${Math.round(widest.right)} px)` : null
            };
        }
        """;

    /// <summary>Every page in both themes.</summary>
    public static TheoryData<string, string> PagesAndSchemes =>
        [.. PageCatalog.All.SelectMany(page => Schemes.Select(scheme => (page.Path, scheme)))];

    [E2ETheory]
    [MemberData(nameof(PagesAndSchemes))]
    public async Task Page_ResizedAcrossBreakpoints_HasNoHorizontalScroll(string path, string scheme)
    {
        // Arrange
        var entry = PageCatalog.Find(path);
        await StartAsync(new BrowserSessionOptions(scheme == "dark" ? ColorScheme.Dark : ColorScheme.Light, 1280, 800));
        await Loader.LoadAsync(entry);
        var problems = new List<string>();

        // Act
        foreach (var (width, height) in Viewports)
        {
            await Page.SetViewportSizeAsync(width, height);
            var measured = await Page.EvaluateAsync<LayoutMeasurement>(MeasureScript, width);
            var known = KnownOverflows.ContainsKey((path, width));

            if (!measured.Fits && !known)
            {
                problems.Add(
                    $"{width} px: the page is {Math.Max(measured.DocumentWidth, measured.BodyWidth)} px wide in a {measured.InnerWidth} px window " +
                    $"(document {measured.DocumentWidth}, body {measured.BodyWidth}); widest element: {measured.Widest ?? "none found (the root itself overflows)"}");
            }
            else if (measured.Fits && known)
            {
                problems.Add($"{width} px: the page fits now, so remove its entry from KnownOverflows (it only shrinks). The entry said: {KnownOverflows[(path, width)]}");
            }
        }

        // Assert
        Assert.True(problems.Count == 0, $"{path} ({scheme}) has layout problems:{Environment.NewLine}  - {string.Join(Environment.NewLine + "  - ", problems)}");
        Session.AssertClean(ConsoleAllowList.ForPage(await Page.EvaluateAsync<string?>("() => window.PortfolioEsri?.baseUrl ?? null")));
    }

    /// <summary>What the measuring script returns (property names match its camelCase fields; Playwright needs a settable class to fill in).</summary>
    private sealed class LayoutMeasurement
    {
        public bool Fits { get; set; }

        public int InnerWidth { get; set; }

        public int DocumentWidth { get; set; }

        public int BodyWidth { get; set; }

        public string? Widest { get; set; }
    }
}
