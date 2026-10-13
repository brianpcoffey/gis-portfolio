using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Web.Middleware;

namespace Portfolio.Tests.Middleware
{
    public class ApiExceptionMiddlewareTests
    {
        private const string Generic500 = "An unexpected error occurred.";

        // preserveStack re-raises with the original stack (so TargetSite still names the throwing
        // method: framework code, or an async method here); otherwise it is thrown from this
        // (Portfolio.Tests) assembly, standing in for a Portfolio service.
        private static Task ThrowAsync(Exception ex, bool preserveStack)
        {
            if (preserveStack)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex);
            throw ex;
        }

        private static async Task<(DefaultHttpContext Context, JsonElement Body)> RunAsync(Exception toThrow, CancellationToken aborted = default, bool preserveStack = false)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v1/thing";
            context.RequestAborted = aborted;
            context.Response.Body = new MemoryStream();

            var middleware = new ApiExceptionMiddleware(_ => ThrowAsync(toThrow, preserveStack), NullLogger<ApiExceptionMiddleware>.Instance);
            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            using var doc = await JsonDocument.ParseAsync(context.Response.Body);
            return (context, doc.RootElement.Clone());
        }

        public static TheoryData<string, int, string> OwnMappings => new()
        {
            { "ArgumentException", 400, "Bad Request" },
            { "ArgumentNullException", 400, "Bad Request" },
            { "KeyNotFoundException", 404, "Not Found" },
            { "InvalidOperationException", 409, "Conflict" },
            { "UnauthorizedAccessException", 401, "Unauthorized" },
        };

        private static Exception Make(string kind) => kind switch
        {
            "ArgumentException" => new ArgumentException("own message"),
            "ArgumentNullException" => new ArgumentNullException("p", "own message"),
            "KeyNotFoundException" => new KeyNotFoundException("own message"),
            "InvalidOperationException" => new InvalidOperationException("own message"),
            "UnauthorizedAccessException" => new UnauthorizedAccessException("own message"),
            _ => throw new NotSupportedException(kind),
        };

        [Theory]
        [MemberData(nameof(OwnMappings))]
        public async Task MappedException_ReturnsStatusTitleAndOriginalMessage(string kind, int status, string title)
        {
            var ex = Make(kind);
            var (context, body) = await RunAsync(ex);

            Assert.Equal(status, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(status, body.GetProperty("status").GetInt32());
            Assert.Equal(title, body.GetProperty("title").GetString());
            Assert.Equal(ex.Message, body.GetProperty("detail").GetString());
            Assert.Equal("/api/v1/thing", body.GetProperty("instance").GetString());
        }

        [Fact]
        public async Task OperationCanceled_WithRequestAborted_Returns499()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var (context, body) = await RunAsync(new OperationCanceledException(), cts.Token);

            Assert.Equal(499, context.Response.StatusCode);
            Assert.Equal("Request Cancelled", body.GetProperty("title").GetString());
            Assert.Equal("The request was cancelled.", body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task UnmappedException_Returns500WithGenericDetail_NeverMessage()
        {
            var (context, body) = await RunAsync(new NotSupportedException("secret schema detail"));

            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal(Generic500, body.GetProperty("title").GetString());
            Assert.Equal(Generic500, body.GetProperty("detail").GetString());
            Assert.DoesNotContain("secret", body.GetRawText());
        }

        [Fact]
        public async Task FrameworkKeyNotFound_Returns404WithGenericDetail()
        {
            var ex = Assert.Throws<KeyNotFoundException>(() => new Dictionary<string, int>()["missing-key"]);
            var (context, body) = await RunAsync(ex, preserveStack: true);

            Assert.Equal(404, context.Response.StatusCode);
            Assert.Equal("The requested resource was not found.", body.GetProperty("detail").GetString());
            Assert.DoesNotContain("missing-key", body.GetRawText());
        }

        [Fact]
        public async Task FrameworkThrowHelper_Returns400WithGenericDetail()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => ArgumentNullException.ThrowIfNull((object?)null, "secretParam"));
            var (context, body) = await RunAsync(ex, preserveStack: true);

            Assert.Equal(400, context.Response.StatusCode);
            Assert.Equal("The request was invalid.", body.GetProperty("detail").GetString());
            Assert.DoesNotContain("secretParam", body.GetRawText());
        }

        private static async Task ServiceThrowsAsync()
        {
            await Task.Yield();
            throw new ArgumentException("service says no");
        }

        [Fact]
        public async Task OwnArgumentExceptionFromAsyncMethod_KeepsOriginalMessage()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(ServiceThrowsAsync);
            var (context, body) = await RunAsync(ex, preserveStack: true);

            Assert.Equal(400, context.Response.StatusCode);
            Assert.Equal("service says no", body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task BadHttpRequest413_Returns413()
        {
            var ex = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            var (context, body) = await RunAsync(ex);

            Assert.Equal(413, context.Response.StatusCode);
            Assert.Equal(413, body.GetProperty("status").GetInt32());
            Assert.Equal("Bad Request", body.GetProperty("title").GetString());
            Assert.Equal("The request was rejected.", body.GetProperty("detail").GetString());
        }

        private sealed class StartedResponseFeature : Microsoft.AspNetCore.Http.Features.HttpResponseFeature
        {
            public override bool HasStarted => true;
        }

        [Fact]
        public async Task ResponseAlreadyStarted_RethrowsAndWritesNothing()
        {
            var context = new DefaultHttpContext();
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(new StartedResponseFeature());
            var body = new MemoryStream();
            context.Response.Body = body;
            var boom = new NotSupportedException("boom");

            var middleware = new ApiExceptionMiddleware(_ => throw boom, NullLogger<ApiExceptionMiddleware>.Instance);
            var thrown = await Assert.ThrowsAsync<NotSupportedException>(() => middleware.InvokeAsync(context));

            Assert.Same(boom, thrown);
            Assert.Equal(0, body.Length);
        }
    }
}
