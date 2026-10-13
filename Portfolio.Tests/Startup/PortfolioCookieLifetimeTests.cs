using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Portfolio.Web.Startup;

namespace Portfolio.Tests.Startup;

public class PortfolioCookieLifetimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly AuthenticationScheme Scheme =
        new(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));

    private static ClaimsPrincipal Principal(string? authTime)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "g-1") };
        if (authTime is not null)
            claims.Add(new Claim(PortfolioCookieEvents.AuthTimeClaimType, authTime));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
    }

    private static string Unix(TimeSpan age) => (Now - age).ToUnixTimeSeconds().ToString();

    private static (CookieValidatePrincipalContext Context, Mock<IAuthenticationService> Auth) Validate(ClaimsPrincipal principal)
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name);
        return (new CookieValidatePrincipalContext(http, Scheme, new CookieAuthenticationOptions(), ticket), auth);
    }

    private static PortfolioCookieEvents Events() => new(new FakeTimeProvider(Now));

    [Fact]
    public async Task ValidatePrincipal_25HourOldPrincipal_IsRejectedAndSignedOut()
    {
        var (ctx, auth) = Validate(Principal(Unix(TimeSpan.FromHours(25))));

        await Events().ValidatePrincipal(ctx);

        Assert.Null(ctx.Principal);
        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), Scheme.Name, It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public async Task ValidatePrincipal_1HourOldPrincipal_IsAccepted()
    {
        var (ctx, auth) = Validate(Principal(Unix(TimeSpan.FromHours(1))));

        await Events().ValidatePrincipal(ctx);

        Assert.NotNull(ctx.Principal);
        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    [Fact]
    public async Task ValidatePrincipal_PrincipalWithoutAuthTime_IsAccepted()
    {
        var (ctx, auth) = Validate(Principal(null));

        await Events().ValidatePrincipal(ctx);

        Assert.NotNull(ctx.Principal);
        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    [Fact]
    public async Task ValidatePrincipal_UnparseableAuthTime_IsRejected()
    {
        var (ctx, _) = Validate(Principal("yesterday"));

        await Events().ValidatePrincipal(ctx);

        Assert.Null(ctx.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_ExactlyAtLimitIsAccepted_JustPastIsRejected()
    {
        var (atLimit, _) = Validate(Principal(Unix(TimeSpan.FromHours(24))));
        var (past, _) = Validate(Principal(Unix(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1))));

        await Events().ValidatePrincipal(atLimit);
        await Events().ValidatePrincipal(past);

        Assert.NotNull(atLimit.Principal);
        Assert.Null(past.Principal);
    }

    [Fact]
    public async Task SigningIn_StampsAuthTimeAsUnixSeconds_ReplacingAnyExistingClaim()
    {
        var principal = Principal("1");
        var http = new DefaultHttpContext();
        var ctx = new CookieSigningInContext(http, Scheme, new CookieAuthenticationOptions(), principal,
            new AuthenticationProperties(), new CookieOptions());

        await Events().SigningIn(ctx);

        var claims = principal.FindAll(PortfolioCookieEvents.AuthTimeClaimType).ToList();
        Assert.Single(claims);
        Assert.Equal(Now.ToUnixTimeSeconds().ToString(), claims[0].Value);
    }

    [Fact]
    public async Task Logout_SignOutStillWorks_AfterValidation()
    {
        var (ctx, auth) = Validate(Principal(Unix(TimeSpan.FromMinutes(5))));
        await Events().ValidatePrincipal(ctx);
        Assert.NotNull(ctx.Principal);

        await ctx.HttpContext.SignOutAsync(Scheme.Name);

        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), Scheme.Name, It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public void AddPortfolioAuthentication_WiresEventsWithRegisteredTimeProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Google:ClientId"] = "test-id",
            ["Authentication:Google:ClientSecret"] = "test-secret"
        }).Build();
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        services.AddPortfolioAuthentication(config, env.Object);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.IsType<PortfolioCookieEvents>(options.Events);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }
}
