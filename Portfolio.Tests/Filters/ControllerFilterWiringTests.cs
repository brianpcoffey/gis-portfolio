using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.Enums;
using Portfolio.Web.Controllers.Api;
using Portfolio.Web.Filters;

namespace Portfolio.Tests.Filters;

/// <summary>
/// WP-43 / D-17: the filters are applied at action level, exactly where a controller used to
/// catch the exception itself. These sets are the 27 <c>catch (ArgumentException)</c> and 6
/// <c>catch (BrokenCircuitException)</c> clauses the filters replaced; an action that is added
/// to or dropped from them must be a deliberate edit here, because a missing attribute silently
/// turns a 400 <c>{ error }</c> / 503 into the middleware's ProblemDetails.
/// </summary>
public class ControllerFilterWiringTests
{
    private static IEnumerable<MethodInfo> Actions() =>
        typeof(FeaturesController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";

    [Fact]
    public void BadRequestOnArgumentException_IsOnTheActionsThatCaughtArgumentException()
    {
        // Arrange
        string[] expected =
        [
            "AddressStandardizationController.Parse",
            "AddressStandardizationController.Validate",
            "BatchGeocodingController.GeocodeAddresses",
            "BatchGeocodingController.SubmitAsync",
            "CatRiskController.Accumulation",
            "CatRiskController.Simulate",
            "ChangeDetectionController.Detect",
            "ChangeDetectionController.Scene",
            "CollectionsController.Create",
            "FleetRoutingController.Optimize",
            "FleetRoutingController.Scenario",
            "GeoStreamController.ProcessEvents",
            "OutageController.Restore",
            "OutageController.Trace",
            "RasterTerrainController.Heatmap",
            "RasterTerrainController.Hillshade",
            "ResponseCoverageController.Isochrone",
            "ResponseCoverageController.Optimize",
            "ReverseGeocodingController.GetPlaceData",
            "SavedFeaturesController.Create",
            "SpatialClusterController.RunDbscan",
            "SpatialGeometryController.Clip",
            "SpatialGeometryController.Triangulate",
            "SpatialNetworkController.Route",
            "SpatialNetworkController.ServiceArea",
            "SpatialOverlayController.SpatialJoin",
            "ViewshedController.Compute"
        ];

        // Act
        var actual = Actions()
            .Where(a => a.IsDefined(typeof(BadRequestOnArgumentExceptionAttribute), inherit: false))
            .Select(Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ArcGisUnavailable_IsOnTheActionsThatCaughtBrokenCircuit_WithTheirUpstream()
    {
        // Arrange
        string[] expected =
        [
            "AddressStandardizationController.Parse=Geocoding",
            "AddressStandardizationController.Validate=Geocoding",
            "BatchGeocodingController.GeocodeAddresses=Geocoding",
            "BatchGeocodingController.SubmitAsync=Geocoding",
            "FeaturesController.GetFeatures=Features",
            "ReverseGeocodingController.GetPlaceData=Geocoding"
        ];

        // Act
        var actual = Actions()
            .SelectMany(a => a.GetCustomAttributesData()
                .Where(d => d.AttributeType == typeof(ArcGisUnavailableAttribute))
                .Select(d => $"{Name(a)}={(ArcGisUpstream)(int)d.ConstructorArguments[0].Value!}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NoControllerClassCarriesEitherFilter()
    {
        // A class-level attribute would also reshape the ArgumentExceptions of actions that let the
        // middleware answer (and the AttributeUsage already forbids it; this guards that rule).
        var controllers = typeof(FeaturesController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t));

        Assert.All(controllers, t =>
        {
            Assert.False(t.IsDefined(typeof(BadRequestOnArgumentExceptionAttribute), inherit: true), t.Name);
            Assert.False(t.IsDefined(typeof(ArcGisUnavailableAttribute), inherit: true), t.Name);
        });
    }
}
