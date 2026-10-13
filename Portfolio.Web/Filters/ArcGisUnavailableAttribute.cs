using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Polly.CircuitBreaker;
using Portfolio.Common.Enums;
using Portfolio.Web.Startup;

namespace Portfolio.Web.Filters;

/// <summary>
/// Turns a <see cref="BrokenCircuitException"/> (the ArcGIS circuit breaker is open) into a 503
/// ProblemDetails body carrying a <c>retryAfterSeconds</c> extension. The number is the pipeline's
/// own <see cref="ArcGisResilienceProfile.BreakDuration"/>, so it cannot drift from the breaker.
/// Apply it to an action, never a controller. A warning is logged under the controller's category.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ArcGisUnavailableAttribute(ArcGisUpstream upstream) : ExceptionFilterAttribute
{
    /// <inheritdoc />
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not BrokenCircuitException)
            return;

        var (profile, title, service) = upstream switch
        {
            ArcGisUpstream.Geocoding => (ArcGisResilienceProfile.Geocoding, "Geocoding Unavailable", "geocoding service"),
            ArcGisUpstream.Features => (ArcGisResilienceProfile.Features, "Feature Service Unavailable", "GIS feature service"),
            _ => throw new ArgumentOutOfRangeException(nameof(upstream), upstream, "Unknown ArcGIS upstream.")
        };
        var retryAfterSeconds = (int)profile.BreakDuration.TotalSeconds;

        FilterLogging.LoggerFor(context, GetType()).LogWarning("ArcGIS circuit breaker is open for the {Upstream} pipelines; responding 503.", upstream);

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = title,
            Detail = $"The upstream {service} is temporarily unavailable. Retry after {retryAfterSeconds.ToString(CultureInfo.InvariantCulture)} seconds.",
            Extensions = { ["retryAfterSeconds"] = retryAfterSeconds }
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
        context.ExceptionHandled = true;
    }
}
