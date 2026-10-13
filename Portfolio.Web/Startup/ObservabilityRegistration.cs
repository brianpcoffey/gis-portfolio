using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.RegularExpressions;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Portfolio.Web.Startup;

/// <summary>
/// OpenTelemetry traces and metrics, exported over OTLP, and only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
/// </summary>
/// <remarks>
/// With the variable blank (the default, and today's production) nothing is registered: no providers, no
/// listeners, no hosted service, so the app behaves exactly as it did before this file existed. When it is set,
/// each signal gets its own <c>AddOtlpExporter()</c>; <c>UseOtlpExporter()</c> is avoided because it would also
/// export logs, which stay on the console. Standard <c>OTEL_EXPORTER_OTLP_*</c> variables (protocol, headers)
/// are read by the exporter itself, so a backend key lives in the host's environment only and is never logged.
/// There is no EF Core instrumentation (still a prerelease): <c>Npgsql.OpenTelemetry</c> supplies the database
/// spans and metrics. No enrichment adds user ids, coordinates or addresses. The ASP.NET Core instrumentation does
/// not record exceptions on server spans, but a failed span can still carry an exception event (message and stack
/// trace): Npgsql adds one to a failed database span and .NET's HttpClient to a failed outgoing call. The worst
/// realistic content is a host name or IP, or a <c>password authentication failed for user "name"</c> message;
/// never a password, a query string or a row value ("Include Error Detail" is off). Query strings are redacted by
/// the instrumentations themselves (ObservabilityRegistrationTests pins it).
/// Two things the defaults would leak are removed here: the user identifier in <c>/users/{userId}</c> and
/// <c>/users/google/{googleId}</c> paths, and the Npgsql data-source / pool name, which is the connection string
/// minus its password (host, port, database, user name).
/// </remarks>
internal static partial class ObservabilityRegistration
{
    /// <summary>The variable that switches telemetry on. A blank or missing value leaves it off.</summary>
    internal const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    internal const string DefaultServiceName = "portfolio-web";

    internal const string NpgsqlMeterName = "Npgsql";

    /// <summary>The span tag Npgsql fills with the connection string minus its password.</summary>
    internal const string DataSourceTag = "db.npgsql.data_source";

    // Every other tag Npgsql puts on its metrics. The connection-pool name tag, which is the same password-less
    // connection string, is the one left out; a tag not on this list is dropped rather than exported.
    internal static readonly string[] NpgsqlMetricTagKeys =
    [
        "db.client.connection.state",
        "db.system.name",
        "server.address",
        "server.port",
        "error.type",
        "db.response.status_code"
    ];

    // Request paths that only add noise to a trace backend: health probes (Render hits /health/live every few
    // seconds) and static assets: every top-level folder and file under wwwroot. Segment-aware: "/js" matches
    // "/js/site.js" but not "/jsx".
    private static readonly string[] UntracedPaths =
        ["/health", "/lib", "/css", "/js", "/img", "/files", "/samples", "/favicon.ico", "/favicon.svg"];

    internal static IServiceCollection AddPortfolioObservability(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configuration[EndpointKey]))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: NonBlank(configuration["OTEL_SERVICE_NAME"]) ?? DefaultServiceName,
                    serviceVersion: NonBlank(configuration["RENDER_GIT_COMMIT"]) ?? AssemblyVersion())
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IsUntracedPath(context.Request.Path);
                    options.EnrichWithHttpRequest = (activity, _) => RedactUserIdentifierTag(activity);
                    options.RecordException = false;
                })
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddProcessor(new NpgsqlDataSourceTagProcessor())
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddView(NpgsqlMetricView)
                .AddOtlpExporter());

        return services;
    }

    /// <summary>True for health probes and static-asset paths, which are left out of traces.</summary>
    internal static bool IsUntracedPath(PathString path)
    {
        foreach (var prefix in UntracedPaths)
        {
            if (path.StartsWithSegments(prefix))
                return true;
        }

        return false;
    }

    /// <summary>Limits the Npgsql metric streams to <see cref="NpgsqlMetricTagKeys"/>, which leaves out the pool name.</summary>
    internal static MetricStreamConfiguration? NpgsqlMetricView(Instrument instrument) =>
        instrument.Meter.Name == NpgsqlMeterName ? new MetricStreamConfiguration { TagKeys = NpgsqlMetricTagKeys } : null;

    /// <summary>Replaces the user identifier in a <c>url.path</c> tag, so it never leaves the process.</summary>
    internal static void RedactUserIdentifierTag(Activity activity)
    {
        if (activity.GetTagItem("url.path") is string path)
        {
            var redacted = RedactUserIdentifiers(path);
            if (!string.Equals(redacted, path, StringComparison.Ordinal))
                activity.SetTag("url.path", redacted);
        }
    }

    /// <summary>
    /// <c>/api/v1/users/{userId}</c> and <c>/api/v1/users/google/{googleId}</c> become <c>.../users/Redacted</c> and
    /// <c>.../users/google/Redacted</c>. <c>/users/me</c> and everything under it carry no identifier and are kept.
    /// </summary>
    internal static string RedactUserIdentifiers(string path) =>
        UserIdentifierPath().Replace(path, "$1Redacted");

    [GeneratedRegex(@"^(/api/v[^/]+/users/(?:google/)?)(?!me(?:/|$))(?!google(?:/|$))[^/]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserIdentifierPath();

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string AssemblyVersion() =>
        typeof(ObservabilityRegistration).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}

/// <summary>
/// Removes <see cref="ObservabilityRegistration.DataSourceTag"/> from finished spans, before the exporter sees them.
/// Setting a tag to null removes it.
/// </summary>
internal sealed class NpgsqlDataSourceTagProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data) => data.SetTag(ObservabilityRegistration.DataSourceTag, null);
}
