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
        public async Task InvokeAsync_SetsExactlyTheFourSecurityHeaders()
        {
            var (context, _) = await RunAsync();

            var headers = context.Response.Headers;
            Assert.Equal(4, headers.Count);
            Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
            Assert.Equal("DENY", headers["X-Frame-Options"]);
            Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
            Assert.Equal(ExpectedCsp, headers["Content-Security-Policy"]);
        }

        [Fact]
        public async Task InvokeAsync_CspIsTheMinimalNonScriptRestrictingPolicy()
        {
            var (context, _) = await RunAsync();

            var csp = context.Response.Headers["Content-Security-Policy"].ToString();
            Assert.DoesNotContain("script-src", csp);
            Assert.DoesNotContain("style-src", csp);
            Assert.DoesNotContain("default-src", csp);
        }

        [Fact]
        public async Task InvokeAsync_CallsNext()
        {
            var (_, nextCalled) = await RunAsync();

            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_OverwritesHeadersAlreadyPresent()
        {
            var context = new DefaultHttpContext();
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            context.Response.Headers["Content-Security-Policy"] = "default-src *";

            await RunAsync(context);

            Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
            Assert.Equal(ExpectedCsp, context.Response.Headers["Content-Security-Policy"]);
        }

        [Fact]
        public async Task InvokeAsync_HeadersAreVisibleToTheNextDelegate()
        {
            var context = new DefaultHttpContext();
            string? seenByNext = null;
            var middleware = new SecurityHeadersMiddleware(c =>
            {
                seenByNext = c.Response.Headers["X-Content-Type-Options"];
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);

            Assert.Equal("nosniff", seenByNext);
        }
    }
}
