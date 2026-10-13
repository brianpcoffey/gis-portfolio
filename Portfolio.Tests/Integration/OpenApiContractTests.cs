using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Tests.Helpers;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Characterizes the public OpenAPI document served at /openapi/v1.json (anonymous in every
/// environment): the sorted operation list, the schema names and each operation's request-body
/// description.
/// </summary>
/// <remarks>
/// The request-body descriptions come from the XML-doc param of the body parameter (the
/// cancellation-token param docs were removed in WP-23, so they no longer leak into the body
/// description). "-" means the operation has no requestBody; "(none)" means it has one without a
/// description.
/// </remarks>
public class OpenApiContractTests
{
    private const string ExpectedOperations = """
        DELETE /api/v1/collections/{id} | -
        DELETE /api/v1/features/saved/{id} | -
        DELETE /api/v1/fiber/materials/{id} | -
        DELETE /api/v1/fiber/orders/{id} | -
        DELETE /api/v1/fiber/shipments/{id} | -
        DELETE /api/v1/homefinder/searches/{id} | -
        DELETE /api/v1/profile/claims/{type} | -
        DELETE /api/v1/users/me/claims/{type} | -
        DELETE /api/v1/users/{userId} | -
        GET /api/v1/catrisk/book | -
        GET /api/v1/change/scene | -
        GET /api/v1/collections | -
        GET /api/v1/collections/{id} | -
        GET /api/v1/features | -
        GET /api/v1/features/saved | -
        GET /api/v1/fiber/dashboard/stats | -
        GET /api/v1/fiber/materials | -
        GET /api/v1/fiber/materials/{id} | -
        GET /api/v1/fiber/orders | -
        GET /api/v1/fiber/orders/{id} | -
        GET /api/v1/fiber/shipments | -
        GET /api/v1/fiber/shipments/{id} | -
        GET /api/v1/fleet/scenario | -
        GET /api/v1/geocoding/batch/{jobId}/status | -
        GET /api/v1/geocoding/reverse | -
        GET /api/v1/homefinder/property/{id} | -
        GET /api/v1/homefinder/searches | -
        GET /api/v1/homefinder/searches/{id} | -
        GET /api/v1/network/graph | -
        GET /api/v1/outage/network | -
        GET /api/v1/profile | -
        GET /api/v1/response/scenario | -
        GET /api/v1/users/google/{googleId} | -
        GET /api/v1/users/me | -
        GET /api/v1/users/me/claims | -
        GET /api/v1/users/{userId} | -
        POST /api/v1/addresses/parse | Request body containing the raw address string.
        POST /api/v1/addresses/validate | Request body containing the raw address string.
        POST /api/v1/catrisk/accumulation | Policy locations, ring radius, and concentration limit.
        POST /api/v1/catrisk/simulate | Policy locations, event catalog, and vulnerability curve shape.
        POST /api/v1/change/detect | Both epochs plus threshold, morphology, and filtering settings.
        POST /api/v1/clustering/dbscan | Points plus epsilon and minimum-points parameters.
        POST /api/v1/collections | Collection data.
        POST /api/v1/features/saved | Feature data.
        POST /api/v1/fiber/materials | (none)
        POST /api/v1/fiber/materials/{id}/receive | (none)
        POST /api/v1/fiber/orders | (none)
        POST /api/v1/fiber/shipments | (none)
        POST /api/v1/fleet/optimize | Depot, stops, fleet parameters, and search budget.
        POST /api/v1/geocoding/batch | CSV file containing addresses to geocode.
        POST /api/v1/geometry/clip | Polygon and bounding box request.
        POST /api/v1/geometry/triangulate | Point set to triangulate.
        POST /api/v1/geostream/events | Telemetry batch and processing options.
        POST /api/v1/homefinder/search | (none)
        POST /api/v1/homefinder/searches | (none)
        POST /api/v1/network/route | Spatial graph and route endpoints.
        POST /api/v1/network/service-area | Spatial graph, origin, and maximum cost.
        POST /api/v1/outage/restore | Network elements, faulted element, and the isolation devices to open.
        POST /api/v1/outage/trace | Network elements, source node, and faulted element.
        POST /api/v1/overlay/spatial-join | Points and candidate zone polygons.
        POST /api/v1/profile/claims | (none)
        POST /api/v1/raster/heatmap | Weighted points and raster extent.
        POST /api/v1/raster/hillshade | Elevation raster and hillshade options.
        POST /api/v1/response/isochrone | Origin node, apparatus speed, and ascending band bounds in minutes.
        POST /api/v1/response/optimize | Demand points, candidate sites, station count, and objective.
        POST /api/v1/viewshed/compute | Elevation grid, observer position, and observer height.
        PUT /api/v1/collections/{id} | Update data.
        PUT /api/v1/fiber/materials/{id} | (none)
        PUT /api/v1/fiber/orders/{id} | (none)
        PUT /api/v1/fiber/shipments/{id} | (none)
        PUT /api/v1/fiber/shipments/{id}/status | (none)
        PUT /api/v1/profile/me/claims | (none)
        PUT /api/v1/users/me | (none)
        PUT /api/v1/users/me/claims | (none)
        """;

    private const string ExpectedSchemas = """
        AccumulationRequestDto
        AccumulationResultDto
        AddressParseRequestDto
        AddressParsedDto
        AddressValidationResultDto
        BatchGeocodingResultDto
        BatchJob
        BatchJobAcceptedDto
        BatchJobStatus
        CandidateSiteDto
        CatEventDto
        CatLocationDto
        ChangeBlobDto
        ChangeSceneDto
        ClaimCreateDto
        ClaimDto
        ClusterPointDto
        ClusteredPointDto
        CollectionCreateDto
        CollectionDto
        CollectionUpdateDto
        CommunityExposureDto
        ConfidenceTier
        CoordinateDto
        CoverageStatsDto
        CreateFiberMaterialDto
        CreateFiberOrderDto
        CreateSavedFeatureDto
        DbscanRequestDto
        DbscanResultDto
        DemandAssignmentDto
        DemandPointDto
        DetectRequestDto
        DetectResultDto
        DistributionNetworkDto
        EventLossDto
        ExceedancePointDto
        FeatureDto
        FiberShipmentDto
        FleetScenarioDto
        FleetStopDto
        GeoStreamBatchRequestDto
        GeoStreamBatchResultDto
        GeometryPointSetDto
        GraphEdgeDto
        GraphNodeDto
        GridAggregateDto
        GroundTruthBoxDto
        HeatmapRequestDto
        HeatmapResultDto
        HomeSearchPreferencesDto
        IFormFile
        IsochroneNodeDto
        IsochroneRequestDto
        IsochroneResultDto
        NetworkElementDto
        OptimizeCoverageRequestDto
        OptimizeCoverageResultDto
        OptimizeRequestDto
        OptimizeResultDto
        OverlayPointDto
        OverlayZoneDto
        PolicyBookDto
        PolygonClipRequestDto
        PolygonOperationResultDto
        ProblemDetails
        ProfileDto
        RasterHillshadeRequestDto
        RasterHillshadeResultDto
        ReceiveStockDto
        ResponseScenarioDto
        RestoreRequestDto
        RestoreResultDto
        ReturnPeriodLossDto
        ReverseGeocodingResultDto
        RingDto
        RoadGraphDto
        RoutePointDto
        RouteRequestDto
        RouteResultDto
        SaveSearchRequest
        SavedFeatureDto
        SavedSearchDto
        ScoredPropertyDto
        ServiceAreaRequestDto
        ServiceAreaResultDto
        SimulationRequestDto
        SimulationResultDto
        SpatialJoinRequestDto
        SpatialJoinResultDto
        SwitchingStepDto
        TaggedPointDto
        TelemetryEventDto
        TraceRequestDto
        TraceResultDto
        TriangleDto
        TriangulationResultDto
        UpdateFiberMaterialDto
        UpdateFiberOrderDto
        UpdateProfileDto
        UpdateShipmentStatusDto
        VehicleRouteDto
        ViewshedRequestDto
        ViewshedResultDto
        WeightedPointDto
        ZoneSummaryDto
        """;

    private static List<string> Lines(string golden) =>
        golden.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static async Task<JsonDocument> FetchAsync()
    {
        await using var f = new PortfolioAppFactory();
        var r = await f.Client().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenApiDocument_IsServed_AsJson()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Operations_AndRequestBodyDescriptions_MatchTheGoldenList()
    {
        // Arrange
        using var doc = await FetchAsync();

        // Act
        var actual = new List<string>();
        foreach (var path in doc.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var op in path.Value.EnumerateObject())
            {
                string desc;
                if (!op.Value.TryGetProperty("requestBody", out var body)) desc = "-";
                else if (body.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String) desc = d.GetString()!;
                else desc = "(none)";
                actual.Add($"{op.Name.ToUpperInvariant()} {path.Name} | {desc}");
            }
        }
        actual.Sort(StringComparer.Ordinal);

        // Assert
        GoldenAssert.Equal(Lines(ExpectedOperations), actual);
    }

    [Fact]
    public async Task RequestBodyDescriptions_DoNotMentionCancellation()
    {
        // Arrange
        using var doc = await FetchAsync();

        // Act
        var offenders = new List<string>();
        foreach (var path in doc.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var op in path.Value.EnumerateObject())
            {
                if (op.Value.TryGetProperty("requestBody", out var body)
                    && body.TryGetProperty("description", out var d)
                    && d.ValueKind == JsonValueKind.String
                    && d.GetString()!.Contains("cancellation", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{op.Name.ToUpperInvariant()} {path.Name} | {d.GetString()}");
                }
            }
        }

        // Assert
        Assert.True(offenders.Count == 0, "Request-body descriptions mention cancellation:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void EveryPublicControllerAction_HasAnXmlSummary_AndNoCancellationParamDocs()
    {
        // Arrange
        var assembly = typeof(Program).Assembly;
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Portfolio.Web.xml");
        Assert.True(File.Exists(xmlPath), "Portfolio.Web.xml was not copied next to the test assembly.");
        var members = XDocument.Load(xmlPath).Descendants("member")
            .Where(m => m.Attribute("name")?.Value.StartsWith("M:", StringComparison.Ordinal) == true)
            .ToDictionary(m => m.Attribute("name")!.Value, m => m, StringComparer.Ordinal);

        // Act
        var missingSummary = new List<string>();
        var tokenParamDocs = new List<string>();
        var actionCount = 0;
        var controllers = assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));
        foreach (var type in controllers)
        {
            var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);
            foreach (var method in actions)
            {
                var prefix = "M:" + type.FullName + "." + method.Name;
                var matches = members
                    .Where(kv => kv.Key == prefix || kv.Key.StartsWith(prefix + "(", StringComparison.Ordinal))
                    .Select(kv => kv.Value)
                    .ToList();
                actionCount++;
                var label = type.Name + "." + method.Name;
                if (!matches.Any(m => !string.IsNullOrWhiteSpace((string?)m.Element("summary"))))
                    missingSummary.Add(label);
                if (matches.Any(m => m.Elements("param").Any(p =>
                        (string?)p.Attribute("name") is "cancellationToken" or "ct")))
                    tokenParamDocs.Add(label);
            }
        }

        // Assert
        Assert.True(actionCount >= 70, $"Only {actionCount} controller actions were inspected.");
        Assert.True(missingSummary.Count == 0, "Actions without an XML <summary>:" + Environment.NewLine + string.Join(Environment.NewLine, missingSummary));
        Assert.True(tokenParamDocs.Count == 0, "Actions documenting a cancellation-token param:" + Environment.NewLine + string.Join(Environment.NewLine, tokenParamDocs));
    }

    [Fact]
    public async Task SchemaNames_MatchTheGoldenList()
    {
        // Arrange
        using var doc = await FetchAsync();

        // Act
        var actual = doc.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToList();

        // Assert
        GoldenAssert.Equal(Lines(ExpectedSchemas), actual);
    }
}
