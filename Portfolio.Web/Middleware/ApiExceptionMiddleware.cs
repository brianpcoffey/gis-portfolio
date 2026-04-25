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
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
                await WriteProblemDetailsAsync(context, ex);
            }
        }

        private static async Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
        {
            var (status, title) = ex switch
            {
                ArgumentNullException
                or ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled"),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = ex.Message,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
