using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Repositories;

namespace Portfolio.Tests.Integration;

/// <summary>Boots the real app in-process (TestServer) with no network and no database.</summary>
/// <remarks>
/// This runs <c>Program.cs</c> and the Startup/* registrations it calls. The Npgsql DbContext
/// registration is replaced by EF InMemory, migrations are skipped, and every
/// ArcGIS typed client gets a fake primary handler that returns 503 and counts attempts (so no test
/// can ever reach arcgis.com). Use ONE factory per test whenever rate-limit state matters.
/// </remarks>
public sealed class PortfolioAppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The typed ArcGIS clients registered by ArcGisClientRegistration.cs (name = typed-client service interface).
    /// </summary>
    public static readonly string[] ArcGisClientNames =
        ["IArcGisService", "IBatchGeocodingService", "IReverseGeocodingService", "IAddressStandardizationService"];

    private readonly string _db = "pipeline-" + Guid.NewGuid();
    private readonly ConcurrentDictionary<string, int> _arcGisAttempts = new();

    /// <summary>Every service descriptor visible at the end of service registration (before test overrides).</summary>
    public List<(Type Service, ServiceLifetime Lifetime, string Impl)> Registrations { get; } = [];

    /// <summary>Requests that reached the fake primary handler of the named ArcGIS client.</summary>
    public int ArcGisAttempts(string clientName) => _arcGisAttempts.GetValueOrDefault(clientName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Non-Development would create /app/DataProtection-Keys on the host disk.
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=unused;Username=u;Password=p");
        builder.UseSetting("RunMigrationsOnStartup", "false");
        // An empty value disables Redis (InfrastructureRegistration checks IsNullOrWhiteSpace) even if a
        // dev env var or appsettings enables it.
        builder.UseSetting("Redis__ConnectionString", "");
        builder.UseSetting("Redis:ConnectionString", "");
        // Telemetry stays off even on a machine that sets OTEL_EXPORTER_OTLP_ENDPOINT globally; the tests that want
        // it set the endpoint themselves, after this.
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "");
        builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services)
            {
                var impl = d.ImplementationType?.FullName
                    ?? (d.ImplementationFactory is not null ? "(factory)" : d.ImplementationInstance?.GetType().FullName ?? "?");
                Registrations.Add((d.ServiceType, d.Lifetime, impl));
            }

            var drop = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<PortfolioDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))).ToList();
            foreach (var d in drop) services.Remove(d);
            services.AddDbContext<PortfolioDbContext>(o => o.UseInMemoryDatabase(_db));

            // Later primary-handler configuration wins, so the resilience pipeline stays in place
            // and only the transport at the bottom is faked.
            foreach (var name in ArcGisClientNames)
            {
                services.AddHttpClient(name)
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeUnavailableHandler(() => _arcGisAttempts.AddOrUpdate(name, 1, (_, n) => n + 1)));
            }
        });
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public int ProfileCount()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().UserProfiles.Count();
    }

    private sealed class FakeUnavailableHandler(Action onSend) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onSend();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
