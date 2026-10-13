using Microsoft.AspNetCore.Http;
using Portfolio.Web.Middleware;

namespace Portfolio.Tests.Middleware
{
    public class SecurityHeadersMiddlewareTests
    {
        private const string ExpectedCsp = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

        private static async Task<(DefaultHttpContext Context, bool NextCalled)> RunAsync(DefaultHttpContext? context = null)
        {
            context ??= new DefaultHttpContext();
            var nextCalled = false;
            var middleware = new SecurityHeadersMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

            await middleware.InvokeAsync(context);

            return (context, nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_SetsExactlyTheSecurityHeaders()
        {
            // Act
            var (context, _) = await RunAsync();

            // Assert
            var headers = context.Response.Headers;
            Assert.Equal(9, headers.Count);
            Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
            Assert.Equal("DENY", headers["X-Frame-Options"]);
            Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
            Assert.Equal(ExpectedCsp, headers["Content-Security-Policy"]);
        }

        [Fact]
        public async Task InvokeAsync_CspIsTheMinimalNonScriptRestrictingPolicy()
        {
            // Act
            var (context, _) = await RunAsync();

            // Assert
            var csp = context.Response.Headers["Content-Security-Policy"].ToString();
            Assert.DoesNotContain("script-src", csp);
            Assert.DoesNotContain("style-src", csp);
            Assert.DoesNotContain("default-src", csp);
        }

        [Fact]
        public async Task InvokeAsync_CallsNext()
        {
            // Act
            var (_, nextCalled) = await RunAsync();

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_OverwritesHeadersAlreadyPresent()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            context.Response.Headers["Content-Security-Policy"] = "default-src *";

            // Act
            await RunAsync(context);

            // Assert
            Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
            Assert.Equal(ExpectedCsp, context.Response.Headers["Content-Security-Policy"]);
        }

        [Fact]
        public async Task InvokeAsync_HeadersAreVisibleToTheNextDelegate()
        {
            // Arrange
            var context = new DefaultHttpContext();
            string? seenByNext = null;
            var middleware = new SecurityHeadersMiddleware(c =>
            {
                seenByNext = c.Response.Headers["X-Content-Type-Options"];
                return Task.CompletedTask;
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal("nosniff", seenByNext);
        }

        [Fact]
        public async Task InvokeAsync_AddsReportOnlyPolicyReportingAndIsolationHeaders()
        {
            // Act
            var (context, _) = await RunAsync();

            // Assert
            var headers = context.Response.Headers;
            var nonce = (string)context.Items["csp-nonce"]!;
            var reportOnly = headers["Content-Security-Policy-Report-Only"].ToString();
            Assert.Contains($"script-src 'self' 'nonce-{nonce}' https://js.arcgis.com https://unpkg.com/leaflet@1.9.4/;", reportOnly);
            Assert.Contains($"style-src 'self' 'nonce-{nonce}' ", reportOnly);
            Assert.Contains("script-src-attr 'none';", reportOnly);
            Assert.EndsWith("report-uri /csp-report; report-to csp", reportOnly);
            Assert.Equal("csp=\"/csp-report\"", headers["Reporting-Endpoints"]);
            Assert.Equal("same-origin", headers["Cross-Origin-Opener-Policy"]);
            Assert.Equal("same-origin", headers["Cross-Origin-Resource-Policy"]);
            Assert.StartsWith("accelerometer=(), camera=(), geolocation=()", headers["Permissions-Policy"].ToString());
        }

        [Fact]
        public async Task InvokeAsync_NonceIs32LowercaseHexCharsAndUniquePerRequest()
        {
            // Act
            var (first, _) = await RunAsync();
            var (second, _) = await RunAsync();

            // Assert
            var a = (string)first.Items["csp-nonce"]!;
            var b = (string)second.Items["csp-nonce"]!;
            Assert.Matches("^[0-9a-f]{32}$", a);
            Assert.Matches("^[0-9a-f]{32}$", b);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public async Task InvokeAsync_ApiResponseWithoutCacheControl_GetsNoStore()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v1/profile";
            var feature = new TestStartingFeature();
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(feature);

            // Act
            await RunAsync(context);
            await feature.StartAsync();

            // Assert
            Assert.Equal("no-store", context.Response.Headers.CacheControl);
        }

        [Fact]
        public async Task InvokeAsync_ApiResponseWithCacheControl_KeepsIt()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v1/catrisk/book";
            var feature = new TestStartingFeature();
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(feature);
            await RunAsync(context);
            context.Response.Headers.CacheControl = "public,max-age=3600";

            // Act
            await feature.StartAsync();

            // Assert
            Assert.Equal("public,max-age=3600", context.Response.Headers.CacheControl);
        }

        [Fact]
        public async Task InvokeAsync_NonApiResponse_GetsNoCacheControl()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/Privacy";
            var feature = new TestStartingFeature();
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(feature);

            // Act
            await RunAsync(context);
            await feature.StartAsync();

            // Assert
            Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
        }

        /// <summary>Response feature that lets a test fire the OnStarting callbacks.</summary>
        private sealed class TestStartingFeature : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
        {
            private readonly List<(Func<object, Task> Callback, object State)> _starting = [];
            public int StatusCode { get; set; } = 200;
            public string? ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = Stream.Null;
            public bool HasStarted { get; private set; }
            public void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));
            public void OnCompleted(Func<object, Task> callback, object state) { }
            public async Task StartAsync()
            {
                HasStarted = true;
                foreach (var (cb, state) in _starting) await cb(state);
            }
        }
    }
}
