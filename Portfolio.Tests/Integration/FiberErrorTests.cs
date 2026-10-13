using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Common.Models;
using Portfolio.Repositories;

namespace Portfolio.Tests.Integration;

/// <summary>
/// WP-34 (D-04a): the Fiber controllers no longer catch everything, so ApiExceptionMiddleware maps
/// the service exceptions.
/// </summary>
/// <remarks>
/// Drives the real host; the "Test" authentication scheme exists only here so the Authenticated policy
/// passes without Google. The user identity itself comes from the anonymous-cookie path (an unlinked
/// profile), exactly as the app resolves it.
/// </remarks>
public class FiberErrorTests
{
    private const string CookieName = "AnonUserId";
    private const string TestScheme = "Test";

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], TestScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
        }
    }

    // A host whose default scheme authenticates every request, and whose in-memory database
    // tolerates the (ignored) transaction ReceiveStock opens.
    private static (WebApplicationFactory<Program> Host, PortfolioAppFactory Inner) NewHost()
    {
        var inner = new PortfolioAppFactory();
        var dbName = "fiber-errors-" + Guid.NewGuid();
        var host = inner.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddAuthentication(o =>
            {
                o.DefaultScheme = TestScheme;
                o.DefaultAuthenticateScheme = TestScheme;
                o.DefaultChallengeScheme = TestScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });

            services.AddDbContext<PortfolioDbContext>(o => o
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        }));
        return (host, inner);
    }

    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static async Task<Guid> AddProfile(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var id = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile { UserId = id, CreatedDate = DateTime.UtcNow, LastActiveDate = DateTime.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }

    private static HttpRequestMessage Json(HttpMethod method, string path, object body, Guid? user)
    {
        var req = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (user is not null) req.Headers.TryAddWithoutValidation("Cookie", $"{CookieName}={user}");
        return req;
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage r, int status)
    {
        Assert.Equal((HttpStatusCode)status, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(status, doc.RootElement.GetProperty("status").GetInt32());
        return doc.RootElement;
    }

    private static readonly object Material = new { name = "Cable", sku = "C-1", category = "Cable", qtyOnHand = 1, unitCost = 1, reorderPoint = 1 };

    [Fact]
    public async Task ReceiveStock_ForUnknownMaterial_Returns404ProblemDetails()
    {
        // Arrange
        var (host, inner) = NewHost();
        await using var _ = inner;
        var user = await AddProfile(host);

        // Act
        var r = await Client(host).SendAsync(
            Json(HttpMethod.Post, "/api/v1/fiber/materials/9999/receive", new { quantity = 5 }, user),
            TestContext.Current.CancellationToken);

        // Assert
        var p = await Problem(r, 404);
        Assert.Equal("Not Found", p.GetProperty("title").GetString());
        Assert.Contains("9999", p.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UpdateMaterial_ForUnknownMaterial_Returns404ProblemDetails()
    {
        // Arrange
        var (host, inner) = NewHost();
        await using var _ = inner;
        var user = await AddProfile(host);

        // Act
        var r = await Client(host).SendAsync(
            Json(HttpMethod.Put, "/api/v1/fiber/materials/9999", Material, user),
            TestContext.Current.CancellationToken);

        // Assert
        var p = await Problem(r, 404);
        Assert.Contains("9999", p.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetMaterials_WithoutAnIdentity_Returns409ProblemDetails()
    {
        // Arrange
        var (host, inner) = NewHost();
        await using var _ = inner;

        // Act
        var r = await Client(host).GetAsync("/api/v1/fiber/materials", TestContext.Current.CancellationToken);

        // Assert
        var p = await Problem(r, 409);
        Assert.Equal("Conflict", p.GetProperty("title").GetString());
        Assert.Equal("User not identified", p.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetOrders_WithoutAnIdentity_Returns409ProblemDetails()
    {
        // Arrange
        var (host, inner) = NewHost();
        await using var _ = inner;

        // Act
        var r = await Client(host).GetAsync("/api/v1/fiber/orders", TestContext.Current.CancellationToken);

        // Assert
        await Problem(r, 409);
    }

    [Fact]
    public async Task ReceiveStock_WithInvalidQuantity_Returns400WithTheMessage()
    {
        // Arrange
        var (host, inner) = NewHost();
        await using var _ = inner;
        var user = await AddProfile(host);

        // Act
        var r = await Client(host).SendAsync(
            Json(HttpMethod.Post, "/api/v1/fiber/materials/1/receive", new { quantity = 0 }, user),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Received quantity must be greater than zero.", body);
    }
}
