using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Pins the hosting options Program.cs registers (WP-14). TestServer does not run Kestrel, so the
/// Kestrel limits are asserted as configured options only; the 413 and the absent Server header
/// are checked against a live run.
/// </summary>
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
