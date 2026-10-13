using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions on all routes and returns an RFC 7807 ProblemDetails JSON response.
    /// </summary>
    /// <remarks>
    /// UseExceptionHandler("/Error") remains as a final fallback but this middleware handles the
    /// vast majority of cases.
    /// </remarks>
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // A client that disconnects mid-request surfaces as OperationCanceledException;
                // that is not a server fault, so log it at Debug rather than Error to avoid noise.
                if (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
                    _logger.LogDebug("Request aborted by client on {Method} {Path}", context.Request.Method, context.Request.Path);
                else if (ex is BadHttpRequestException bad)
                    // Kestrel rejections (oversized body, malformed request) are client faults: no stack trace.
                    _logger.LogInformation("Request rejected with {StatusCode} on {Method} {Path}: {Reason}", bad.StatusCode, context.Request.Method, context.Request.Path, bad.Message);
                else
                    _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

                // Once the response has started a ProblemDetails body can no longer be written;
                // let the server abort the connection rather than corrupt the partial response.
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("The response has already started; the exception middleware will not write ProblemDetails.");
                    throw;
                }

                await WriteProblemDetailsAsync(context, ex);
            }
        }

        /// <summary>
        /// True when the exception was thrown from Portfolio code (including async
        /// state machines), so its message is ours and safe to surface.
        /// </summary>
        /// <remarks>
        /// Framework throw helpers (Dictionary indexer, serializer, ThrowIfNull...) are not
        /// own; a null TargetSite counts as not own.
        /// </remarks>
        internal static bool IsOwn(Exception ex) =>
            ex.TargetSite?.DeclaringType?.Assembly.GetName().Name?.StartsWith("Portfolio.", StringComparison.Ordinal) == true;

        private static async Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
        {
            // Detail is only surfaced from Portfolio-originated exceptions, whose
            // messages are validation/lookup text. Framework-originated messages and
            // anything unmapped (5xx) may leak schema/internal details, so they get a
            // fixed generic detail per status instead of ex.Message.
            var own = IsOwn(ex);
            var (status, title, detail) = ex switch
            {
                ArgumentNullException
                or ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", own ? ex.Message : "The request was invalid."),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", own ? ex.Message : "The requested resource was not found."),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict", own ? ex.Message : "The request conflicts with the current state."),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", own ? ex.Message : "The request is not authorized."),
                OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled", "The request was cancelled."),
                BadHttpRequestException b => (b.StatusCode, "Bad Request", "The request was rejected."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "An unexpected error occurred.")
            };

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
