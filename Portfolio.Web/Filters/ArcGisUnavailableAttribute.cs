using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Polly.CircuitBreaker;
using Portfolio.Common.Enums;
using Portfolio.Web.Startup;

namespace Portfolio.Web.Filters;

/// <summary>
/// Turns a <see cref="BrokenCircuitException"/> (the ArcGIS circuit breaker is open) into a 503
/// ProblemDetails body carrying a <c>retryAfterSeconds</c> extension.
/// </summary>
/// <remarks>
/// The number is the pipeline's own <see cref="ArcGisResilienceProfile.BreakDuration"/>, so it cannot
/// drift from the breaker. Apply it to an action, never a controller. A warning is logged under the
/// controller's category.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ArcGisUnavailableAttribute : ExceptionFilterAttribute
{
    private readonly ArcGisUpstream _upstream;
    private readonly string _title;
    private readonly string _detail;
    private readonly int _retryAfterSeconds;

    /// <summary>Resolves the pipeline's profile and the response text once, so a request only builds the result.</summary>
    /// <param name="upstream">The pipeline family whose circuit breaker the action reports on.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="upstream"/> is not a defined <see cref="ArcGisUpstream"/>. This surfaces when the attribute
    /// is instantiated (while MVC discovers the action's filters), not on the first outage.
    /// </exception>
    public ArcGisUnavailableAttribute(ArcGisUpstream upstream)
    {
        var (profile, title, service) = upstream switch
        {
            ArcGisUpstream.Geocoding => (ArcGisResilienceProfile.Geocoding, "Geocoding Unavailable", "geocoding service"),
            ArcGisUpstream.Features => (ArcGisResilienceProfile.Features, "Feature Service Unavailable", "GIS feature service"),
            _ => throw new ArgumentOutOfRangeException(nameof(upstream), upstream, "Unknown ArcGIS upstream.")
        };

        _upstream = upstream;
        _title = title;
        _retryAfterSeconds = (int)profile.BreakDuration.TotalSeconds;
        _detail = $"The upstream {service} is temporarily unavailable. Retry after {_retryAfterSeconds.ToString(CultureInfo.InvariantCulture)} seconds.";
    }

    /// <inheritdoc />
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not BrokenCircuitException)
            return;

        FilterLogging.LoggerFor(context, GetType()).LogWarning("ArcGIS circuit breaker is open for the {Upstream} pipelines; responding 503.", _upstream);

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = _title,
            Detail = _detail,
            Extensions = { ["retryAfterSeconds"] = _retryAfterSeconds }
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
        context.ExceptionHandled = true;
    }
}
