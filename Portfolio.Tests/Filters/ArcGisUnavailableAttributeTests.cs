using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Portfolio.Common.Enums;
using Portfolio.Tests.Controllers;
using Portfolio.Web.Controllers.Api;
using Portfolio.Web.Filters;
using Portfolio.Web.Startup;
using static Portfolio.Tests.Filters.BadRequestOnArgumentExceptionAttributeTests;

namespace Portfolio.Tests.Filters;

/// <summary>
/// WP-43 / D-17 and CX-19: <c>[ArcGisUnavailable]</c> unit tests. The same bodies are pinned through the
/// real host in <c>ControllerErrorBodyTests</c>.
/// </summary>
public class ArcGisUnavailableAttributeTests
{
    [Fact]
    public void Geocoding_BreakerOpen_Returns503WithTheGeocodingBody()
    {
        // Arrange
        var (context, _) = NewContext(new BrokenCircuitException("open"), typeof(ReverseGeocodingController));

        // Act
        new ArcGisUnavailableAttribute(ArcGisUpstream.Geocoding).OnException(context);

        // Assert
        ControllerBodyAssert.GeocodingUnavailable(context.Result);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void Features_BreakerOpen_Returns503WithTheFeatureServiceBody()
    {
        // Arrange
        var (context, _) = NewContext(new BrokenCircuitException("open"), typeof(FeaturesController));

        // Act
        new ArcGisUnavailableAttribute(ArcGisUpstream.Features).OnException(context);

        // Assert
        ControllerBodyAssert.Unavailable(context.Result, "Feature Service Unavailable",
            "The upstream GIS feature service is temporarily unavailable. Retry after 30 seconds.", 30);
        Assert.True(context.ExceptionHandled);
    }

    [Theory]
    [InlineData(ArcGisUpstream.Geocoding)]
    [InlineData(ArcGisUpstream.Features)]
    public void RetryAfter_IsThePipelinesBreakDuration_InTheExtensionAndTheDetail(ArcGisUpstream upstream)
    {
        // Arrange
        var profile = upstream == ArcGisUpstream.Geocoding ? ArcGisResilienceProfile.Geocoding : ArcGisResilienceProfile.Features;
        var expected = (int)profile.BreakDuration.TotalSeconds;
        var (context, _) = NewContext(new BrokenCircuitException("open"), typeof(ReverseGeocodingController));

        // Act
        new ArcGisUnavailableAttribute(upstream).OnException(context);

        // Assert
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(context.Result).Value);
        Assert.Equal(expected, Assert.IsType<int>(problem.Extensions["retryAfterSeconds"]));
        Assert.EndsWith($"Retry after {expected} seconds.", problem.Detail);
    }

    [Fact]
    public void ABreakerThatWasIsolatedByHand_IsAlsoABrokenCircuit()
    {
        // Arrange: IsolatedCircuitException derives from BrokenCircuitException, as the old catch clause saw it.
        var (context, _) = NewContext(new IsolatedCircuitException("isolated"), typeof(ReverseGeocodingController));

        // Act
        new ArcGisUnavailableAttribute(ArcGisUpstream.Geocoding).OnException(context);

        // Assert
        ControllerBodyAssert.GeocodingUnavailable(context.Result);
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(HttpRequestException))]
    public void OtherExceptions_AreLeftForTheMiddleware(Type exceptionType)
    {
        // Arrange
        var (context, log) = NewContext((Exception)Activator.CreateInstance(exceptionType, "boom")!, typeof(ReverseGeocodingController));

        // Act
        new ArcGisUnavailableAttribute(ArcGisUpstream.Geocoding).OnException(context);

        // Assert
        Assert.Null(context.Result);
        Assert.False(context.ExceptionHandled);
        Assert.Empty(log.Entries);
    }

    [Theory]
    [InlineData(ArcGisUpstream.Geocoding)]
    [InlineData(ArcGisUpstream.Features)]
    public void LogsOneWarning_UnderTheControllersCategory(ArcGisUpstream upstream)
    {
        // Arrange
        var (context, log) = NewContext(new BrokenCircuitException("open"), typeof(FeaturesController));

        // Act
        new ArcGisUnavailableAttribute(upstream).OnException(context);

        // Assert
        var entry = Assert.Single(log.Entries);
        Assert.Equal(typeof(FeaturesController).FullName, entry.Category);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(upstream.ToString(), entry.Message);
    }

    [Fact]
    public void ForAnActionThatIsNotOnAController_LogsUnderTheAttributesOwnCategory()
    {
        // Arrange
        var (context, log) = NewContext(new BrokenCircuitException("open"), controller: null);

        // Act
        new ArcGisUnavailableAttribute(ArcGisUpstream.Geocoding).OnException(context);

        // Assert
        Assert.Equal(typeof(ArcGisUnavailableAttribute).FullName, Assert.Single(log.Entries).Category);
        ControllerBodyAssert.GeocodingUnavailable(context.Result);
    }

    [Fact]
    public void AnUnknownUpstream_IsRejected()
    {
        // Arrange
        var (context, _) = NewContext(new BrokenCircuitException("open"), typeof(FeaturesController));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArcGisUnavailableAttribute((ArcGisUpstream)99).OnException(context));
    }

    [Fact]
    public void IsActionLevelOnly()
    {
        var usage = typeof(ArcGisUnavailableAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
