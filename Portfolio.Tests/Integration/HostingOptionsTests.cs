using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Portfolio.Tests.Integration;

/// <summary>Pins the hosting options registered at startup (WP-14).</summary>
/// <remarks>
/// The options come from ConfigurePortfolioKestrel (Startup/CoreRegistration.cs), AddPortfolioHsts
/// (Startup/InfrastructureRegistration.cs) and ForwardedHeadersSetup (Startup/ForwardedHeadersSetup.cs).
/// TestServer does not run Kestrel, so the Kestrel limits are asserted as configured options only; the
/// 413 and the absent Server header are checked against a live run.
/// </remarks>
public class HostingOptionsTests
{
    [Fact]
    public async Task Kestrel_DefaultBodyLimitIs1MB_AndServerHeaderIsOff()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var kestrel = f.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        // Assert
        Assert.Equal(1_000_000, kestrel.Limits.MaxRequestBodySize);
        Assert.False(kestrel.AddServerHeader);
    }

    [Fact]
    public async Task Hsts_IsOneYear_WithoutSubdomainsOrPreload()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var hsts = f.Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromDays(365), hsts.MaxAge);
        Assert.False(hsts.IncludeSubDomains);
        Assert.False(hsts.Preload);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void ForwardedHeaders_Default_TrustsAnyProxy_WithForwardLimit1()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();

        // Act
        ForwardedHeadersSetup.Apply(options, Config());

        // Assert
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
    }

    [Fact]
    public void ForwardedHeaders_NoTrustList_ForcesForwardLimit1_DespiteConfig()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();

        // Act
        ForwardedHeadersSetup.Apply(options, Config(("ForwardedHeaders:ForwardLimit", "3")));

        // Assert
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void ForwardedHeaders_WithNetworks_KeepsTheTrustListInsteadOfClearingIt()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();

        // Act
        ForwardedHeadersSetup.Apply(options, Config(
            ("ForwardedHeaders:ForwardLimit", "2"),
            ("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8"),
            ("ForwardedHeaders:KnownNetworks:1", "172.16.0.0/12")));

        // Assert
        Assert.Equal(2, options.ForwardLimit);
        Assert.Contains(options.KnownIPNetworks, n => n.ToString() == "10.0.0.0/8");
        Assert.Contains(options.KnownIPNetworks, n => n.ToString() == "172.16.0.0/12");
        Assert.NotEmpty(options.KnownProxies);
    }

    [Fact]
    public void ForwardedHeaders_CommaSeparatedSingleValue_IsAccepted()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();

        // Act
        ForwardedHeadersSetup.Apply(options, Config(("ForwardedHeaders:KnownNetworks", "10.0.0.0/8, 192.168.0.0/16")));

        // Assert
        Assert.Contains(options.KnownIPNetworks, n => n.ToString() == "10.0.0.0/8");
        Assert.Contains(options.KnownIPNetworks, n => n.ToString() == "192.168.0.0/16");
    }

    [Fact]
    public void ForwardedHeaders_InvalidCidr_FailsLoudly()
    {
        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
            ForwardedHeadersSetup.Apply(new ForwardedHeadersOptions(), Config(("ForwardedHeaders:KnownNetworks:0", "not-a-cidr"))));
    }

    [Fact]
    public async Task ForwardedHeaders_HostDefaults_AreUnchanged()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var options = f.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // Assert
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
    }

    [Fact]
    public async Task RateLimiter_RejectsWith429()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var limiter = f.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Assert
        Assert.Equal((int)HttpStatusCode.TooManyRequests, limiter.RejectionStatusCode);
        Assert.NotNull(limiter.GlobalLimiter);
    }
}
