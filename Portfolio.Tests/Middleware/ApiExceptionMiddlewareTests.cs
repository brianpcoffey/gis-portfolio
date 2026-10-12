using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Web.Middleware;

namespace Portfolio.Tests.Middleware
{
    public class ApiExceptionMiddlewareTests
    {
        private const string Generic500 = "An unexpected error occurred.";

        private static async Task<(DefaultHttpContext Context, JsonElement Body)> RunAsync(Exception toThrow, CancellationToken aborted = default)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v1/thing";
            context.RequestAborted = aborted;
            context.Response.Body = new MemoryStream();

            var middleware = new ApiExceptionMiddleware(_ => throw toThrow, NullLogger<ApiExceptionMiddleware>.Instance);
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
        public async Task FrameworkKeyNotFound_ReturnsFrameworkMessage_TodaysLeak()
        {
            var ex = Assert.Throws<KeyNotFoundException>(() => new Dictionary<string, int>()["missing-key"]);
            var (context, body) = await RunAsync(ex);

            Assert.Equal(404, context.Response.StatusCode);
            Assert.Equal(ex.Message, body.GetProperty("detail").GetString());
            Assert.Contains("missing-key", body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task BadHttpRequest413_ReturnsGeneric500_TodaysBug()
        {
            var ex = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            var (context, body) = await RunAsync(ex);

            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal(Generic500, body.GetProperty("detail").GetString());
        }
    }
}
