using System.Net;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Pins today's behaviour: the canonical link in _Layout.cshtml is built from the request's Host
/// header, so an attacker-supplied Host is reflected (no AllowedHosts enforcement, no fixed origin).
/// </summary>
public class HostHeaderTests
{
    [Fact]
    public async Task EvilHost_IsReflectedInTheCanonicalLink()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var req = new HttpRequestMessage(HttpMethod.Get, "/Privacy");
        req.Headers.Accept.ParseAdd("text/html");
        req.Headers.Host = "evil.example";

        // Act
        var r = await f.Client().SendAsync(req, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<link rel=\"canonical\" href=\"http://evil.example/Privacy\"", html, StringComparison.Ordinal);
    }
}
