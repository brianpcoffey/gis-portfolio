using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Portfolio.Services.Interfaces;
using Portfolio.Tests.Helpers;
using Portfolio.Tests.Integration;
using Portfolio.Web.Startup;

namespace Portfolio.Tests.Startup;

/// <summary>EN-03b: OpenTelemetry is registered only when OTEL_EXPORTER_OTLP_ENDPOINT is set, and exports no query values or user ids.</summary>
public class ObservabilityRegistrationTests
{
    // The discard port: nothing listens there, so an export attempt is refused at once and nothing leaves the machine.
    private const string UnreachableEndpoint = "http://127.0.0.1:9";

    private const string ClipRoute = "api/v{version:apiVersion}/geometry/clip";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IHostEnvironment Env(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    private static WebApplicationFactory<Program> WithEndpoint(PortfolioAppFactory factory, Action<IWebHostBuilder>? more = null) =>
        factory.WithWebHostBuilder(b =>
        {
            b.UseSetting(ObservabilityRegistration.EndpointKey, UnreachableEndpoint);
            more?.Invoke(b);
        });

    private static HttpRequestMessage Request(HttpMethod method, string url, ActivityTraceId traceId, string? json = null)
    {
        // A traceparent header puts the server span in a trace of its own, so a listener can pick out this test's
        // spans while other tests run in parallel.
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return request;
    }

    /// <summary>One stopped activity of one trace, with its tags as they were when it stopped.</summary>
    private sealed record Span(bool IsAllDataRequested, IReadOnlyDictionary<string, string?> Tags)
    {
        // Assertion failures print the span, so spell its tags out instead of the dictionary's type name.
        public override string ToString() =>
            $"[recorded={IsAllDataRequested}] " + string.Join(", ", Tags.Select(t => $"{t.Key}={t.Value}"));
    }

    private sealed class SpanCapture : IDisposable
    {
        private readonly ConcurrentQueue<Span> _spans = new();
        private readonly ActivityListener _listener;

        public SpanCapture(ActivityTraceId traceId, string activitySource, Func<Activity, bool>? include = null)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == activitySource || source.Name.StartsWith("Portfolio.Tests.", StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.TraceId == traceId && activity.Source.Name == activitySource && (include?.Invoke(activity) ?? true))
                    {
                        _spans.Enqueue(new Span(
                            activity.IsAllDataRequested,
                            activity.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString())));
                    }
                }
            };
            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>Waits (generously: a loaded CI machine is slow) until <paramref name="count"/> spans have stopped.</summary>
        public async Task<IReadOnlyList<Span>> WaitForAsync(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (_spans.Count < count && DateTime.UtcNow < deadline)
                await Task.Delay(20);
            return [.. _spans];
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed class WarningCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, Lines);
        public void Dispose() { }

        private sealed class Capture(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    lines.Enqueue($"{logLevel}|{category}|{formatter(state, exception)}");
            }
        }
    }

    private sealed class CapturingProcessor(string source, string spanName) : BaseProcessor<Activity>
    {
        public ConcurrentQueue<Dictionary<string, string?>> Spans { get; } = new();

        // The instrumentations are process-global, so a request from a test class running in parallel can reach this
        // processor too; only the span this test started (its source and its one-off name) is kept.
        public override void OnEnd(Activity data)
        {
            if (data.Source.Name == source && data.OperationName == spanName)
                Spans.Enqueue(data.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString()));
        }
    }

    private sealed class CapturingMetricExporter : BaseExporter<Metric>
    {
        public ConcurrentQueue<(string Meter, string Instrument, Dictionary<string, object?> Tags)> Points { get; } = new();

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                foreach (var point in metric.GetMetricPoints())
                {
                    var tags = new Dictionary<string, object?>();
                    foreach (var tag in point.Tags)
                        tags[tag.Key] = tag.Value;
                    Points.Enqueue((metric.MeterName, metric.Name, tags));
                }
            }

            return ExportResult.Success;
        }
    }

    // ---- registration ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPortfolioObservability_WithoutEndpoint_RegistersNothing(string? endpoint)
    {
        // Arrange
        var services = new ServiceCollection();
        var config = Config(
            (ObservabilityRegistration.EndpointKey, endpoint),
            ("OTEL_SERVICE_NAME", "custom"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));

        // Act
        var result = services.AddPortfolioObservability(config, Env("Production"));

        // Assert
        Assert.Same(services, result);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(TracerProvider));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(MeterProvider));
        Assert.Empty(services);
    }

    [Fact]
    public void AddPortfolioObservability_WithEndpoint_RegistersTracerMeterAndHostedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPortfolioObservability(Config((ObservabilityRegistration.EndpointKey, UnreachableEndpoint)), Env("Production"));

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(TracerProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(MeterProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Theory]
    [InlineData(null, null, "portfolio-web", false)]
    [InlineData("  ", "  ", "portfolio-web", false)]
    [InlineData("my-service", "abc1234", "my-service", true)]
    public void AddPortfolioObservability_ResourceCarriesServiceNameVersionAndEnvironment(
        string? serviceName, string? commit, string expectedName, bool expectCommitAsVersion)
    {
        // Arrange
        var config = Config(
            (ObservabilityRegistration.EndpointKey, UnreachableEndpoint),
            ("OTEL_SERVICE_NAME", serviceName),
            ("RENDER_GIT_COMMIT", commit));
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddPortfolioObservability(config, Env("Staging"));
        var assemblyVersion = typeof(ObservabilityRegistration).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        // Act
        using var provider = services.BuildServiceProvider();
        var resource = provider.GetRequiredService<TracerProvider>().GetResource().Attributes
            .ToDictionary(a => a.Key, a => a.Value);

        // Assert
        Assert.Equal(expectedName, resource["service.name"]);
        Assert.Equal(expectCommitAsVersion ? commit : assemblyVersion, resource["service.version"]);
        Assert.Equal("Staging", resource["deployment.environment.name"]);
    }

    // ---- what is left out, and what is redacted ----

    [Theory]
    [InlineData("/health/live", true)]
    [InlineData("/health/ready", true)]
    [InlineData("/Health", true)]
    [InlineData("/lib/bootstrap/dist/js/bootstrap.bundle.min.js", true)]
    [InlineData("/css/site.css", true)]
    [InlineData("/js/api-fetch.js", true)]
    [InlineData("/img/og-preview.jpg", true)]
    [InlineData("/files/Brian-Coffey-Resume.pdf", true)]
    [InlineData("/favicon.ico", true)]
    [InlineData("/", false)]
    [InlineData("/Projects/HomeFinder", false)]
    [InlineData("/api/v1/geometry/clip", false)]
    [InlineData("/api/v1/health-score", false)]
    [InlineData("/healthy", false)]
    [InlineData("/jsx/app.js", false)]
    [InlineData("/csp-report", false)]
    public void IsUntracedPath_HealthAndStaticAssetPaths_AreLeftOut(string path, bool expected)
    {
        // Arrange
        var requestPath = new PathString(path);

        // Act
        var untraced = ObservabilityRegistration.IsUntracedPath(requestPath);

        // Assert
        Assert.Equal(expected, untraced);
    }

    [Theory]
    [InlineData("/api/v1/users/0b1c2d3e-0000-4000-8000-000000000001", "/api/v1/users/Redacted")]
    [InlineData("/api/v1/users/0b1c2d3e-0000-4000-8000-000000000001/", "/api/v1/users/Redacted/")]
    [InlineData("/api/v1/users/google/1234567890", "/api/v1/users/google/Redacted")]
    [InlineData("/API/V1/Users/Google/1234567890", "/API/V1/Users/Google/Redacted")]
    [InlineData("/api/v2/users/google/1234567890", "/api/v2/users/google/Redacted")]
    [InlineData("/api/v1/users/me", "/api/v1/users/me")]
    [InlineData("/api/v1/users/me/claims", "/api/v1/users/me/claims")]
    [InlineData("/api/v1/users/me/claims/preferences", "/api/v1/users/me/claims/preferences")]
    [InlineData("/api/v1/users", "/api/v1/users")]
    [InlineData("/api/v1/users/google", "/api/v1/users/google")]
    [InlineData("/api/v1/fiber/orders/42", "/api/v1/fiber/orders/42")]
    [InlineData("/Account/Profile", "/Account/Profile")]
    public void RedactUserIdentifiers_OnlyTheUserIdentifierSegment_IsReplaced(string path, string expected)
    {
        // Act
        var redacted = ObservabilityRegistration.RedactUserIdentifiers(path);

        // Assert
        Assert.Equal(expected, redacted);
    }

    [Fact]
    public void RedactUserIdentifierTag_PathWithUserId_IsRewrittenOnTheActivity()
    {
        // Arrange
        using var activity = new Activity("test");
        activity.SetTag("url.path", "/api/v1/users/google/1234567890");

        // Act
        ObservabilityRegistration.RedactUserIdentifierTag(activity);

        // Assert
        Assert.Equal("/api/v1/users/google/Redacted", activity.GetTagItem("url.path"));
    }

    [Fact]
    public void RedactUserIdentifierTag_OrdinaryPathOrNoPath_IsLeftAlone()
    {
        // Arrange
        using var ordinary = new Activity("ordinary");
        ordinary.SetTag("url.path", "/api/v1/geometry/clip");
        using var none = new Activity("none");

        // Act
        ObservabilityRegistration.RedactUserIdentifierTag(ordinary);
        ObservabilityRegistration.RedactUserIdentifierTag(none);

        // Assert
        Assert.Equal("/api/v1/geometry/clip", ordinary.GetTagItem("url.path"));
        Assert.Null(none.GetTagItem("url.path"));
    }

    // ---- the Npgsql connection string, which would otherwise ride along as a span tag and a metric tag ----

    [Fact]
    public void NpgsqlDataSourceTagProcessor_OnEnd_RemovesOnlyTheDataSourceTag()
    {
        // Arrange
        using var activity = new Activity("postgresql");
        activity.SetTag(ObservabilityRegistration.DataSourceTag, "Host=db.example.test;Port=5432;Database=app;Username=app_owner");
        activity.SetTag("db.system.name", "postgresql");

        // Act
        new NpgsqlDataSourceTagProcessor().OnEnd(activity);

        // Assert
        Assert.Null(activity.GetTagItem(ObservabilityRegistration.DataSourceTag));
        Assert.Equal("postgresql", activity.GetTagItem("db.system.name"));
    }

    [Fact]
    public void NpgsqlMetricView_NpgsqlMeter_KeepsTheAllowListAndOtherMetersAreUntouched()
    {
        // Arrange
        using var npgsql = new Meter("Npgsql");
        using var other = new Meter("Portfolio.Tests.Other");
        var npgsqlCounter = npgsql.CreateCounter<long>("db.client.connection.count");
        var otherCounter = other.CreateCounter<long>("other.count");

        // Act
        var forNpgsql = ObservabilityRegistration.NpgsqlMetricView(npgsqlCounter);
        var forOther = ObservabilityRegistration.NpgsqlMetricView(otherCounter);

        // Assert
        Assert.NotNull(forNpgsql);
        Assert.Equal(ObservabilityRegistration.NpgsqlMetricTagKeys, forNpgsql.TagKeys);
        Assert.DoesNotContain("db.client.connection.pool.name", forNpgsql.TagKeys!);
        Assert.Null(forOther);
    }

    [Fact]
    public void TracerPipeline_NpgsqlSpan_ReachesLaterProcessorsWithoutTheDataSourceTag()
    {
        // Arrange
        var spanName = "portfolio.tests." + Guid.NewGuid().ToString("N");
        var captured = new CapturingProcessor("Npgsql", spanName);
        var config = Config((ObservabilityRegistration.EndpointKey, UnreachableEndpoint));
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddPortfolioObservability(config, Env("Production"));
        services.ConfigureOpenTelemetryTracerProvider(builder => builder.AddProcessor(captured));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("Npgsql");

        // Act
        using (var span = source.StartActivity(spanName, ActivityKind.Client))
        {
            Assert.NotNull(span);
            span.SetTag(ObservabilityRegistration.DataSourceTag, "Host=db.example.test;Port=5432;Database=app;Username=app_owner");
            span.SetTag("db.system.name", "postgresql");
            span.SetTag("db.query.text", "SELECT 1");
        }

        // Stands in for a span from a test class running in parallel, which the capture must not pick up.
        using (source.StartActivity("portfolio.tests.other", ActivityKind.Client))
        {
        }

        // Assert
        var tags = Assert.Single(captured.Spans);
        Assert.False(tags.ContainsKey(ObservabilityRegistration.DataSourceTag));
        Assert.Equal("postgresql", tags["db.system.name"]);
        Assert.Equal("SELECT 1", tags["db.query.text"]);
    }

    [Fact]
    public void MeterPipeline_NpgsqlMetric_ReachesTheReaderWithoutThePoolName()
    {
        // Arrange
        var exporter = new CapturingMetricExporter();
        var config = Config((ObservabilityRegistration.EndpointKey, UnreachableEndpoint));
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddPortfolioObservability(config, Env("Production"));
        services.ConfigureOpenTelemetryMeterProvider(builder => builder.AddReader(new BaseExportingMetricReader(exporter)));
        using var provider = services.BuildServiceProvider();
        var meterProvider = provider.GetRequiredService<MeterProvider>();
        // A name of its own, because Npgsql's static meter also reports db.client.connection.count for any pool the
        // process has opened (other tests open one against an unreachable database), and those points are not ours.
        var instrumentName = "portfolio.tests." + Guid.NewGuid().ToString("N");
        using var meter = new Meter("Npgsql");
        var connections = meter.CreateCounter<long>(instrumentName);

        // Act
        connections.Add(
            1,
            new KeyValuePair<string, object?>("db.client.connection.pool.name", "Host=db.example.test;Port=5432;Database=app;Username=app_owner"),
            new KeyValuePair<string, object?>("db.client.connection.state", "idle"));
        meterProvider.ForceFlush(10_000);

        // Assert
        var point = Assert.Single(exporter.Points, p => p.Instrument == instrumentName);
        Assert.Equal("Npgsql", point.Meter);
        Assert.False(point.Tags.ContainsKey("db.client.connection.pool.name"));
        Assert.Equal("idle", point.Tags["db.client.connection.state"]);
        // The view strips the pool name from every Npgsql instrument, Npgsql's own included.
        Assert.All(
            exporter.Points.Where(p => p.Meter == "Npgsql"),
            p => Assert.False(p.Tags.ContainsKey("db.client.connection.pool.name"), $"{p.Instrument} kept the pool name"));
    }

    // ---- the real host, with an endpoint nothing listens on ----

    [Fact]
    public async Task TracedHost_ApiRequestIsRecorded_HealthAndStaticAssetRequestsAreNot()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom();
        using var capture = new SpanCapture(traceId, "Microsoft.AspNetCore");
        await using var factory = new PortfolioAppFactory();
        await using var traced = WithEndpoint(factory);
        using var client = traced.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var health = await client.SendAsync(Request(HttpMethod.Get, "/health/live", traceId), TestContext.Current.CancellationToken);
        var asset = await client.SendAsync(Request(HttpMethod.Get, "/js/api-config.js", traceId), TestContext.Current.CancellationToken);
        var api = await client.SendAsync(Request(HttpMethod.Post, "/api/v1/geometry/clip", traceId, "{}"), TestContext.Current.CancellationToken);
        var spans = await capture.WaitForAsync(3);

        // Assert
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, api.StatusCode);
        Assert.Equal(3, spans.Count);
        var recorded = Assert.Single(spans, s => s.IsAllDataRequested);
        Assert.Equal(ClipRoute, recorded.Tags["http.route"]);
        Assert.Equal("/api/v1/geometry/clip", recorded.Tags["url.path"]);
        Assert.All(spans.Where(s => !s.IsAllDataRequested), s => Assert.Empty(s.Tags));
    }

    [Fact]
    public async Task TracedHost_ServerSpans_CarryNoQueryValuesAndNoUserIdentifiers()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom();
        var userId = Guid.NewGuid();
        var googleId = "117" + Guid.NewGuid().ToString("N");
        using var capture = new SpanCapture(traceId, "Microsoft.AspNetCore");
        await using var factory = new PortfolioAppFactory();
        await using var traced = WithEndpoint(factory);
        using var client = traced.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        await client.SendAsync(Request(HttpMethod.Post, "/api/v1/geometry/clip?address=1+Secret+Street&location=-117.1825,34.0555", traceId, "{}"), TestContext.Current.CancellationToken);
        var byGuid = await client.SendAsync(Request(HttpMethod.Get, $"/api/v1/users/{userId}?x=1", traceId), TestContext.Current.CancellationToken);
        var byGoogleId = await client.SendAsync(Request(HttpMethod.Get, $"/api/v1/users/google/{googleId}", traceId), TestContext.Current.CancellationToken);
        var spans = await capture.WaitForAsync(3);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, byGuid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byGoogleId.StatusCode);
        Assert.Equal(3, spans.Count);
        Assert.Contains(spans, s => s.Tags["url.query"] == "?address=Redacted&location=Redacted");
        Assert.Contains(spans, s => s.Tags["url.path"] == "/api/v1/users/Redacted" && s.Tags["url.query"] == "?x=Redacted");
        Assert.Contains(spans, s => s.Tags["url.path"] == "/api/v1/users/google/Redacted");
        foreach (var value in spans.SelectMany(s => s.Tags.Values))
        {
            Assert.DoesNotContain("Secret", value);
            Assert.DoesNotContain("117.18", value);
            Assert.DoesNotContain(userId.ToString(), value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(googleId, value);
        }
    }

    [Fact]
    public async Task TracedHost_TypedArcGisClientSpans_CarryNoQueryValues()
    {
        // Arrange
        using var source = new ActivitySource("Portfolio.Tests.ObservabilityRegistration");
        using var all = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(all);
        using var parent = source.StartActivity("reverse-geocode")!;
        using var capture = new SpanCapture(parent.TraceId, "System.Net.Http", a => a.Kind == ActivityKind.Client);
        await using var factory = new PortfolioAppFactory();
        // The fake 503 handler never builds a System.Net.Http span, so give the typed client a real handler whose
        // connect step fails at once: the request is described by the instrumentation but nothing leaves the process.
        await using var traced = WithEndpoint(factory, b => b.ConfigureTestServices(services =>
            services.AddHttpClient("IReverseGeocodingService").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectCallback = (_, _) => throw new IOException("blocked in test")
            })));
        var service = traced.Services.GetRequiredService<IReverseGeocodingService>();

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseGeocodeAsync(34.0555, -117.1825));
        var spans = await capture.WaitForAsync(1);

        // Assert
        Assert.StartsWith("Upstream geocoding service failed", ex.Message);
        Assert.NotEmpty(spans);
        foreach (var span in spans)
        {
            var url = span.Tags["url.full"]!;
            Assert.StartsWith("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/reverseGeocode", url);
            Assert.DoesNotContain("location", url);
            Assert.DoesNotContain("117", url);
            Assert.DoesNotContain("34.05", url);
            Assert.DoesNotContain(span.Tags.Values, v => v is not null && (v.Contains("117.18") || v.Contains("34.05")));
        }
    }

    [Fact]
    public async Task TracedHost_WithUnreachableEndpoint_AddsNoWarningOrErrorLogLines()
    {
        // Arrange
        // The host already logs a few Warnings of its own on these requests (HTTPS redirection port, a 400 from
        // the clip action), so the claim is that an unreachable collector adds nothing to them.
        var baselineLogs = new WarningCapture();
        var tracedLogs = new WarningCapture();
        await using var plainFactory = new PortfolioAppFactory();
        await using var plain = plainFactory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(baselineLogs)));
        await using var tracedFactory = new PortfolioAppFactory();
        await using var traced = WithEndpoint(tracedFactory, b => b.ConfigureLogging(l => l.AddProvider(tracedLogs)));

        // Act
        var baselineStatuses = await RunScenarioAsync(plain);
        var tracedStatuses = await RunScenarioAsync(traced);
        // A flush forces an export attempt at the closed port now instead of at the next batch; whether it reports
        // success is beside the point, what matters is that the failure stays out of the log.
        _ = traced.Services.GetRequiredService<TracerProvider>().ForceFlush(10_000);
        _ = traced.Services.GetRequiredService<MeterProvider>().ForceFlush(10_000);

        // Assert
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.BadRequest], baselineStatuses);
        Assert.Equal(baselineStatuses, tracedStatuses);
        var baseline = WithoutDataProtection(baselineLogs.Lines);
        Assert.NotEmpty(baseline);
        Assert.Equal(baseline, WithoutDataProtection(tracedLogs.Lines));
    }

    // Data Protection keys live in one on-disk folder shared by every host on the machine, so which host happens to
    // create the first key (and log its warning) is a race, and a fresh CI runner starts with an empty folder.
    // Lines read "Level|Category|Message"; the category is the second field.
    private static List<string> WithoutDataProtection(IEnumerable<string> lines) => lines
        .Where(l => !l.Split('|', 3)[1].StartsWith("Microsoft.AspNetCore.DataProtection", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToList();

    private static async Task<HttpStatusCode[]> RunScenarioAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var health = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        var api = await client.PostAsync("/api/v1/geometry/clip", new StringContent("{}", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        return [health.StatusCode, api.StatusCode];
    }

    [Fact]
    public async Task TracedHost_AddsNoPortfolioRegistrations_OnlyOpenTelemetryOnes()
    {
        // Arrange
        await using var plain = new PortfolioAppFactory();
        _ = plain.Client();
        await using var withEndpoint = new PortfolioAppFactory();
        await using var traced = WithEndpoint(withEndpoint);
        _ = traced.CreateClient();

        static List<string> PortfolioRegistrations(PortfolioAppFactory f) => f.Registrations
            .Where(r => r.Service.FullName!.StartsWith("Portfolio.", StringComparison.Ordinal) ||
                        (r.Service.Name == "IHostedService" && r.Impl.StartsWith("Portfolio.", StringComparison.Ordinal)))
            .Select(r => $"{r.Service.FullName} | {r.Lifetime} | {r.Impl}")
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act
        var baseline = PortfolioRegistrations(plain);
        var enabled = PortfolioRegistrations(withEndpoint);

        // Assert
        GoldenAssert.Equal(baseline, enabled);
        Assert.DoesNotContain(plain.Registrations, r => r.Impl.StartsWith("OpenTelemetry.", StringComparison.Ordinal));
        Assert.Contains(withEndpoint.Registrations, r =>
            r.Service == typeof(IHostedService) && r.Impl.StartsWith("OpenTelemetry.", StringComparison.Ordinal));
        // No OTLP logs (D-13): the console stays the only log sink, so no logger provider is added.
        Assert.Equal(
            plain.Registrations.Count(r => r.Service == typeof(ILoggerProvider)),
            withEndpoint.Registrations.Count(r => r.Service == typeof(ILoggerProvider)));
    }
}
