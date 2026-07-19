using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace Portfolio.Web.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions on all routes and returns an RFC 7807
    /// ProblemDetails JSON response. UseExceptionHandler("/Error") remains as
    /// a final fallback but this middleware handles the vast majority of cases.
    /// </summary>
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
                else
                    _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

                await WriteProblemDetailsAsync(context, ex);
            }
        }

        private static async Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
        {
            // Detail is only safe to surface for the explicitly-mapped client (4xx)
            // errors, whose messages are validation/lookup text. For anything
            // unmapped (5xx) the raw exception message may leak schema/internal
            // details, so return a generic detail instead of ex.Message.
            var (status, title, detail) = ex switch
            {
                ArgumentNullException
                or ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", ex.Message),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message),
                OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled", "The request was cancelled."),
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
