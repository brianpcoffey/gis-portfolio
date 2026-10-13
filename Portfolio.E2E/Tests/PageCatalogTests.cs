using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The list of pages the browser suites visit must follow the site. These need no browser, so they always run: a demo
/// that is added to the site and not to <see cref="PageCatalog"/> would otherwise escape the health, layout and
/// accessibility suites without anyone noticing.
/// </summary>
public class PageCatalogTests
{
    [Fact]
    public void DemoNames_AgainstTheProjectsFolder_ListsEveryDemoPage()
    {
        // Arrange
        var projects = ProjectsFolder();

        // Act
        var demos = Directory.GetDirectories(projects)
            .Where(directory => File.Exists(Path.Combine(directory, "Index.cshtml")))
            .Select(directory => Path.GetFileName(directory))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.Equal(demos, PageCatalog.DemoNames.Order(StringComparer.Ordinal));
        Assert.All(demos, demo => Assert.True(File.Exists(Path.Combine(projects, demo, "Details.cshtml")), $"{demo} has no Details page."));
    }

    [Fact]
    public void All_Catalog_CoversEachDemoItsDetailsPageAndTheSitePages()
    {
        // Arrange
        var paths = PageCatalog.All.Select(p => p.Path).ToList();

        // Act and assert
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PageCatalog.DemoNames, demo =>
        {
            Assert.Contains($"/Projects/{demo}", paths);
            Assert.Contains($"/Projects/{demo}/Details", paths);
        });
        Assert.All(["/", "/Projects", "/Login", "/Privacy", "/Accessibility", "/Account/Profile"], page => Assert.Contains(page, paths));
        Assert.Equal(2 * PageCatalog.DemoNames.Count + 6, paths.Count);
    }

    [Fact]
    public void All_Catalog_SignsInOnlyForTheDashboardAndTheProfile()
    {
        // Arrange and act
        var signedIn = PageCatalog.All.Where(p => p.SignedIn).Select(p => p.Path).Order(StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(["/Account/Profile", "/Projects/PlantOperationsDashboard"], signedIn);
    }

    [Fact]
    public void All_Catalog_NamesFourArcGisAndThreeLeafletMaps()
    {
        // Arrange and act
        var arcGis = PageCatalog.All.Where(p => p.Kind == PageKind.ArcGis).Select(p => p.Path).Order(StringComparer.Ordinal).ToList();
        var leaflet = PageCatalog.All.Where(p => p.Kind == PageKind.Leaflet).Select(p => p.Path).Order(StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(
            ["/Projects/HomeFinder", "/Projects/PlantOperationsDashboard", "/Projects/ReverseGeocoding", "/Projects/StateExplorer"],
            arcGis);
        Assert.Equal(["/Projects/Fleet", "/Projects/Network", "/Projects/Response"], leaflet);
    }

    // An overflow is only compared when the layout suite visits its page and width, so an entry for a page that was renamed or
    // removed, or for a width the suite does not use, would never be compared and never fail as stale.
    [Fact]
    public void KnownOverflows_EveryKey_IsAPageAndWidthTheLayoutSuiteVisits()
    {
        // Arrange
        var paths = PageCatalog.All.Select(p => p.Path).ToHashSet(StringComparer.Ordinal);
        var widths = LayoutTests.Viewports.Select(viewport => viewport.Width).ToHashSet();

        // Act
        var unknown = LayoutTests.KnownOverflows.Keys
            .Where(key => !paths.Contains(key.Path) || !widths.Contains(key.Width))
            .Select(key => $"{key.Path} at {key.Width} px")
            .ToList();

        // Assert
        Assert.True(unknown.Count == 0, $"KnownOverflows has entries for pages or widths the layout suite does not visit: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Find_UnknownPath_Throws()
    {
        // Arrange and act
        var exception = Record.Exception(() => PageCatalog.Find("/Nope"));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    // Portfolio.Web/Pages/Projects, found by walking up from the test binaries to the solution.
    private static string ProjectsFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var projects = Path.Combine(dir.FullName, "Portfolio.Web", "Pages", "Projects");
            if (Directory.Exists(projects))
                return projects;
        }

        throw new InvalidOperationException("Portfolio.Web/Pages/Projects was not found above the test binaries.");
    }
}
