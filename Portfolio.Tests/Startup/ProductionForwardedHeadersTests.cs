using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Portfolio.Tests.Startup;

/// <summary>
/// WP-52: pins what the production settings file (<c>Portfolio.Web/appsettings.Production.json</c>) does to
/// forwarded headers and host filtering. The file is read from the source tree, so the tests see exactly
/// the values Render deploys.
/// </summary>
/// <remarks>
/// Production traffic (measured with a one-off probe, 2026-10-13) is client -> Cloudflare edge -> Render
/// load balancer (10.x) -> a local proxy on the loopback address -> the app. The connecting peer is
/// therefore loopback and X-Forwarded-For is "client, cloudflare, render". The middleware needs
/// ForwardLimit 3 and a trust list covering the Render and Cloudflare hops to reach the client entry.
/// The Development and test settings are untouched and are pinned elsewhere (HostingOptionsTests,
/// HostHeaderTests).
/// </remarks>
public class ProductionForwardedHeadersTests
{
    private static readonly string ProductionSettings = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "Portfolio.Web", "appsettings.Production.json"));

    // Cloudflare's published ranges (https://www.cloudflare.com/ips-v4 and /ips-v6), fetched 2026-10-13.
    private static readonly string[] CloudflareRanges =
    [
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18",
        "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17",
        "162.158.0.0/15", "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32",
        "2a06:98c0::/29", "2c0f:f248::/32"
    ];

    private static IConfiguration ProductionConfig() =>
        new ConfigurationBuilder().AddJsonFile(ProductionSettings, optional: false).Build();

    private static ForwardedHeadersOptions ProductionOptions()
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Apply(options, ProductionConfig());
        return options;
    }

    /// <summary>Runs the real ForwardedHeadersMiddleware, configured from the production file, on one request.</summary>
    private static async Task<HttpContext> Forward(string peer, string? forwardedFor, string? forwardedProto = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        if (forwardedFor is not null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        if (forwardedProto is not null) context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(ProductionOptions()));
        await middleware.Invoke(context);
        return context;
    }

    [Fact]
    public void Production_ForwardLimitIs3_AndTrustsRenderAndCloudflare()
    {
        // Act
        var options = ProductionOptions();

        // Assert
        Assert.Equal(3, options.ForwardLimit);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        var configured = options.KnownIPNetworks.Select(n => n.ToString()).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("10.0.0.0/8", configured);
        foreach (var range in CloudflareRanges)
            Assert.Contains(range, configured);
    }

    [Fact]
    public void Production_LoopbackStaysTrusted_BesideTheConfiguredNetworks()
    {
        // Act: the default loopback entries must survive Apply when networks are configured.
        var options = ProductionOptions();

        // Assert
        Assert.Contains(IPAddress.IPv6Loopback, options.KnownProxies);
        Assert.Contains(options.KnownIPNetworks, n => n.ToString() == "127.0.0.0/8");
        Assert.True(options.KnownIPNetworks.Count > 1);
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("127.0.0.1")]
    public async Task Forward_ThreeHopChain_ResolvesTheClient(string loopbackPeer)
    {
        // Act: client, Cloudflare edge, Render load balancer (the measured production shape).
        var context = await Forward(loopbackPeer, "198.51.100.10, 104.23.160.155, 10.193.217.5");

        // Assert
        Assert.Equal(IPAddress.Parse("198.51.100.10"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Forward_SpoofedEntryOnTheLeft_IsNeverReached()
    {
        // Act: the client prepended its own entry, so the real client is the second one from the left.
        var context = await Forward("::1", "203.0.113.7, 198.51.100.10, 172.69.34.212, 10.194.229.146");

        // Assert: the spoof stays unconsumed and cannot become the rate-limit key.
        Assert.Equal(IPAddress.Parse("198.51.100.10"), context.Connection.RemoteIpAddress);
        Assert.Equal("203.0.113.7", context.Request.Headers["X-Forwarded-For"].ToString());
    }

    [Fact]
    public async Task Forward_Ipv6ClientThroughIpv6CloudflareEdge_ResolvesTheClient()
    {
        // Act
        var context = await Forward("::1", "2001:db8::1, 2606:4700::6810:1, 10.1.2.3");

        // Assert
        Assert.Equal(IPAddress.Parse("2001:db8::1"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Forward_UnlistedEdge_StopsAtTheEdge_NeverAtTheClientSuppliedEntry()
    {
        // Act: 8.8.8.8 is not a Cloudflare range, so the walk stops there. That degrades to a shared
        // edge key for the request (the pre-WP-52 behaviour); it must never become the left-hand entry.
        var context = await Forward("::1", "198.51.100.10, 8.8.8.8, 10.1.2.3");

        // Assert
        Assert.Equal(IPAddress.Parse("8.8.8.8"), context.Connection.RemoteIpAddress);
        Assert.Equal("198.51.100.10", context.Request.Headers["X-Forwarded-For"].ToString());
    }

    [Fact]
    public async Task Forward_DirectConnectionFromOutsideTheTrustList_CannotInjectHeaders()
    {
        // Act: a caller reaching the container directly (compose, k8s) is not a known proxy.
        var context = await Forward("203.0.113.50", "198.51.100.10, 104.23.160.155, 10.193.217.5", "https");

        // Assert: the headers are ignored and the connecting address stays the key.
        Assert.Equal(IPAddress.Parse("203.0.113.50"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task Forward_ProtoHeader_StillSetsHttps_WithTheThreeHopLimit()
    {
        // Act: HSTS, secure cookies and absolute URLs depend on the scheme surviving the higher limit.
        var context = await Forward("::1", "198.51.100.10, 104.23.160.155, 10.193.217.5", "https");

        // Assert
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("198.51.100.10"), context.Connection.RemoteIpAddress);
    }

    /// <summary>Sends one GET with the given Host header to a host that carries only the production settings.</summary>
    private static async Task<HttpStatusCode> StatusForHost(string host)
    {
        // The web defaults register the host-filtering startup filter and bind AllowedHosts from
        // configuration, which is the path the real app takes (Program.cs has no explicit call).
        using var server = await new HostBuilder()
            .ConfigureWebHostDefaults(web => web
                .UseTestServer()
                .ConfigureAppConfiguration((_, config) => config.AddJsonFile(ProductionSettings, optional: false))
                .Configure(app => app.Run(context => context.Response.WriteAsync("ok"))))
            .StartAsync(TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = host;
        using var response = await server.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    [Theory]
    [InlineData("brianpcoffey.com")]
    [InlineData("www.brianpcoffey.com")]
    [InlineData("portfolio-uscj.onrender.com")]
    [InlineData("brianpcoffey.com:443")]
    public async Task HostFiltering_ListedHosts_Pass(string host)
    {
        // Act
        var status = await StatusForHost(host);

        // Assert
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("brianpcoffey.com.evil.example")]
    [InlineData("admin.brianpcoffey.com")]
    public async Task HostFiltering_UnlistedHosts_AreRejectedWith400(string host)
    {
        // Act
        var status = await StatusForHost(host);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}
