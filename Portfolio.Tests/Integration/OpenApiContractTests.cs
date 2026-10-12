using System.Net;
using System.Text.Json;
using Portfolio.Tests.Helpers;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Characterizes the public OpenAPI document served at /openapi/v1.json (anonymous in every
/// environment): the sorted operation list, the schema names and each operation's request-body
/// description. The descriptions below are TODAY's text, and several are wrong: the XML-doc
/// "cancellation token" param text leaks into the body description (WP-23 fixes it and updates
/// this golden). "-" means the operation has no requestBody; "(none)" means it has one without a
/// description.
/// </summary>
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
        POST /api/v1/addresses/parse | Cancellation token.
        POST /api/v1/addresses/validate | Cancellation token.
        POST /api/v1/catrisk/accumulation | Request cancellation token.
        POST /api/v1/catrisk/simulate | Request cancellation token.
        POST /api/v1/change/detect | Request cancellation token.
        POST /api/v1/clustering/dbscan | Request cancellation token.
        POST /api/v1/collections | Cancellation token for the request.
        POST /api/v1/features/saved | Cancellation token.
        POST /api/v1/fiber/materials | (none)
        POST /api/v1/fiber/materials/{id}/receive | (none)
        POST /api/v1/fiber/orders | (none)
        POST /api/v1/fiber/shipments | (none)
        POST /api/v1/fleet/optimize | Request cancellation token.
        POST /api/v1/geocoding/batch | Cancellation token.
        POST /api/v1/geometry/clip | Request cancellation token.
        POST /api/v1/geometry/triangulate | Request cancellation token.
        POST /api/v1/geostream/events | Request cancellation token.
        POST /api/v1/homefinder/search | (none)
        POST /api/v1/homefinder/searches | (none)
        POST /api/v1/network/route | Request cancellation token.
        POST /api/v1/network/service-area | Request cancellation token.
        POST /api/v1/outage/restore | Request cancellation token.
        POST /api/v1/outage/trace | Request cancellation token.
        POST /api/v1/overlay/spatial-join | Request cancellation token.
        POST /api/v1/profile/claims | (none)
        POST /api/v1/raster/heatmap | Request cancellation token.
        POST /api/v1/raster/hillshade | Request cancellation token.
        POST /api/v1/response/isochrone | Request cancellation token.
        POST /api/v1/response/optimize | Request cancellation token.
        POST /api/v1/viewshed/compute | Request cancellation token.
        PUT /api/v1/collections/{id} | Cancellation token for the request.
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
