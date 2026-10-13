using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Portfolio.Web.Middleware;

namespace Portfolio.Tests.Middleware
{
    /// <summary>EN-06: the second CSRF layer, a required X-Requested-With header on cookie-bearing unsafe /api calls.</summary>
    public class ApiRequestHeaderMiddlewareTests
    {
        private const string RejectionBody = "{\"error\":\"This request must include the X-Requested-With header.\"}";
        private const string AnonCookie = "AnonUserId=3f2504e0-4f89-11d3-9a0c-0305e82c3301";
        private const string AuthCookie = ".AspNetCore.Cookies=chunked-ticket";

        private sealed class CapturingLogger : ILogger<ApiRequestHeaderMiddleware>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed record Outcome(DefaultHttpContext Context, bool NextCalled, string Body, CapturingLogger Log);

        // authCookieName: what IOptionsMonitor<CookieAuthenticationOptions>.Get("Cookies").Cookie.Name returns;
        // the real options get ".AspNetCore.Cookies" from their post-configuration, a bare options object has none (null).
        private static async Task<Outcome> RunAsync(
            string method,
            string path,
            string? cookie = null,
            StringValues? header = null,
            string? authCookieName = ".AspNetCore.Cookies")
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            if (cookie is not null)
                context.Request.Headers.Cookie = cookie;
            if (header is not null)
                context.Request.Headers["X-Requested-With"] = header.Value;
            context.Response.Body = new MemoryStream();

            var options = new CookieAuthenticationOptions();
            if (authCookieName is not null)
                options.Cookie.Name = authCookieName;
            var monitor = new Mock<IOptionsMonitor<CookieAuthenticationOptions>>();
            monitor.Setup(m => m.Get(CookieAuthenticationDefaults.AuthenticationScheme)).Returns(options);

            var log = new CapturingLogger();
            var nextCalled = false;
            var middleware = new ApiRequestHeaderMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor.Object, log);

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
            return new Outcome(context, nextCalled, body, log);
        }

        private static void AssertPassedThrough(Outcome outcome)
        {
            Assert.True(outcome.NextCalled);
            Assert.Equal(StatusCodes.Status200OK, outcome.Context.Response.StatusCode);
            Assert.Empty(outcome.Body);
            Assert.Empty(outcome.Log.Entries);
        }

        private static void AssertRejected(Outcome outcome)
        {
            Assert.False(outcome.NextCalled);
            Assert.Equal(StatusCodes.Status403Forbidden, outcome.Context.Response.StatusCode);
            Assert.StartsWith("application/json", outcome.Context.Response.ContentType, StringComparison.Ordinal);
            Assert.Equal(RejectionBody, outcome.Body);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public async Task SafeMethod_WithIdentityCookieAndNoHeader_PassesThrough(string method)
        {
            // Act
            var outcome = await RunAsync(method, "/api/v1/features/saved", cookie: AnonCookie);

            // Assert
            AssertPassedThrough(outcome);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task UnsafeMethod_WithNoCookiesAndNoHeader_PassesThrough(string method)
        {
            // Act
            var outcome = await RunAsync(method, "/api/v1/geometry/triangulate");

            // Assert
            AssertPassedThrough(outcome);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task UnsafeMethod_WithAnonymousCookieAndNoHeader_Returns403WithTheExactJsonBody(string method)
        {
            // Act
            var outcome = await RunAsync(method, "/api/v1/features/saved", cookie: AnonCookie);

            // Assert
            AssertRejected(outcome);
        }

        [Fact]
        public async Task UnsafeMethod_WithAnonymousCookieAmongOthers_Returns403()
        {
            // Act
            var outcome = await RunAsync("POST", "/api/v1/features/saved", cookie: "theme=dark; " + AnonCookie + "; other=1");

            // Assert
            AssertRejected(outcome);
        }

        [Theory]
        [InlineData(".AspNetCore.Cookies")]
        [InlineData(".AspNetCore.CookiesC1")]
        [InlineData(".AspNetCore.CookiesC2")]
        public async Task UnsafeMethod_WithAuthCookieOrItsChunksAndNoHeader_Returns403(string cookieName)
        {
            // Act
            var outcome = await RunAsync("POST", "/api/v1/fiber/orders", cookie: $"{cookieName}=ticket-part");

            // Assert
            AssertRejected(outcome);
        }

        [Theory]
        [InlineData("XMLHttpRequest")]
        [InlineData("x")]
        [InlineData("1")]
        public async Task UnsafeMethod_WithIdentityCookieAndAnyNonEmptyHeader_PassesThrough(string value)
        {
            // Act
            var withAnon = await RunAsync("POST", "/api/v1/features/saved", cookie: AnonCookie, header: value);
            var withAuth = await RunAsync("DELETE", "/api/v1/fiber/orders/5", cookie: AuthCookie, header: value);

            // Assert
            AssertPassedThrough(withAnon);
            AssertPassedThrough(withAuth);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("   ")]
        public async Task UnsafeMethod_WithIdentityCookieAndAnEmptyOrBlankHeader_Returns403(string value)
        {
            // Act
            var outcome = await RunAsync("POST", "/api/v1/features/saved", cookie: AnonCookie, header: value);

            // Assert
            AssertRejected(outcome);
        }

        [Fact]
        public async Task UnsafeMethod_WithRepeatedHeaderWhereOnlyOneValueIsBlank_PassesThrough()
        {
            // Act
            var outcome = await RunAsync(
                "POST", "/api/v1/features/saved", cookie: AnonCookie, header: new StringValues(["", "XMLHttpRequest"]));

            // Assert
            AssertPassedThrough(outcome);
        }

        [Fact]
        public async Task UnsafeMethod_WithRepeatedHeaderWhereEveryValueIsBlank_Returns403()
        {
            // Act
            var outcome = await RunAsync(
                "POST", "/api/v1/features/saved", cookie: AnonCookie, header: new StringValues(["", " "]));

            // Assert
            AssertRejected(outcome);
        }

        [Theory]
        [InlineData("/csp-report")]
        [InlineData("/Logout")]
        [InlineData("/Account/Profile")]
        [InlineData("/apix")]
        [InlineData("/apix/v1/features/saved")]
        [InlineData("/")]
        [InlineData("/health/ready")]
        public async Task UnsafeMethod_OutsideTheApiPath_WithIdentityCookieAndNoHeader_IsNotMatched(string path)
        {
            // Act
            var outcome = await RunAsync("POST", path, cookie: AnonCookie + "; " + AuthCookie);

            // Assert
            AssertPassedThrough(outcome);
        }

        [Theory]
        [InlineData("/api")]
        [InlineData("/api/v1/features/saved")]
        [InlineData("/API/v1/features/saved")]
        [InlineData("/Api/V1/Features/Saved")]
        public async Task UnsafeMethod_OnAnApiPath_MatchesWhateverTheCase(string path)
        {
            // Act
            var outcome = await RunAsync("POST", path, cookie: AnonCookie);

            // Assert
            AssertRejected(outcome);
        }

        [Fact]
        public async Task UnsafeMethod_WithOnlyUnrelatedCookies_PassesThrough()
        {
            // Act
            var outcome = await RunAsync(
                "POST",
                "/api/v1/geometry/triangulate",
                cookie: ".AspNetCore.Session=abc; .AspNetCore.Antiforgery.x=y; theme=dark; AnonUserIdentity=1; Cookies=2");

            // Assert
            AssertPassedThrough(outcome);
        }

        [Fact]
        public async Task UnsafeMethod_WithAConfiguredAuthCookieName_MatchesThatNameAndNotTheDefault()
        {
            // Act
            var custom = await RunAsync("POST", "/api/v1/fiber/orders", cookie: "MyAuthC1=ticket", authCookieName: "MyAuth");
            var defaultName = await RunAsync("POST", "/api/v1/fiber/orders", cookie: AuthCookie, authCookieName: "MyAuth");

            // Assert
            AssertRejected(custom);
            AssertPassedThrough(defaultName);
        }

        [Fact]
        public async Task UnsafeMethod_WithNoConfiguredAuthCookieName_FallsBackToTheDefaultName()
        {
            // Act
            var auth = await RunAsync("POST", "/api/v1/fiber/orders", cookie: AuthCookie, authCookieName: null);
            var unrelated = await RunAsync("POST", "/api/v1/fiber/orders", cookie: "theme=dark", authCookieName: null);

            // Assert
            AssertRejected(auth);
            AssertPassedThrough(unrelated);
        }

        [Fact]
        public async Task Rejection_LogsOneWarning_WithMethodAndPathOnASingleLine()
        {
            // Act
            var outcome = await RunAsync("POST", "/api/v1/x\r\nFORGED entry" + char.ConvertFromUtf32(0x2028) + "here", cookie: AnonCookie);

            // Assert
            var entry = Assert.Single(outcome.Log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal("Rejected POST /api/v1/x  FORGED entry here: identity cookie without X-Requested-With", entry.Message);
            Assert.DoesNotContain('\r', entry.Message);
            Assert.DoesNotContain('\n', entry.Message);
        }

        [Fact]
        public async Task Rejection_DoesNotLogTheCookieValue()
        {
            // Act
            var outcome = await RunAsync("POST", "/api/v1/features/saved", cookie: AnonCookie);

            // Assert
            Assert.DoesNotContain("3f2504e0", Assert.Single(outcome.Log.Entries).Message, StringComparison.Ordinal);
        }
    }
}
