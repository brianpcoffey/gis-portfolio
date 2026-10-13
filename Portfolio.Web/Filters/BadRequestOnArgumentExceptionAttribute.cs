using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Portfolio.Web.Middleware;

namespace Portfolio.Web.Filters;

/// <summary>
/// Turns an <see cref="ArgumentException"/> thrown by the action (a service's input validation) into
/// a 400 <c>{ "error": "..." }</c> body. The public JS clients read only <c>body.error</c>, so this
/// shape stays instead of falling through to <see cref="ApiExceptionMiddleware"/>'s RFC 7807 body.
/// </summary>
/// <remarks>
/// Apply it to an action, never a controller: a class-level attribute would also reshape the
/// <see cref="ArgumentException"/>s of actions that deliberately let the middleware answer. The
/// message is surfaced only for exceptions thrown from Portfolio code
/// (<see cref="ApiExceptionMiddleware.IsOwn"/>); an <see cref="ArgumentException"/> raised inside the
/// framework gets the middleware's generic text, so no framework detail leaks. A warning is logged
/// under the controller's category.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class BadRequestOnArgumentExceptionAttribute : ExceptionFilterAttribute
{
    /// <summary>The text <see cref="ApiExceptionMiddleware"/> uses for a non-Portfolio <see cref="ArgumentException"/>.</summary>
    internal const string GenericMessage = "The request was invalid.";

    /// <inheritdoc />
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not ArgumentException ex)
            return;

        var request = context.HttpContext.Request;
        LoggerFor(context).LogWarning(ex, "Invalid request on {Method} {Path}", request.Method, request.Path);

        context.Result = new BadRequestObjectResult(new { error = ApiExceptionMiddleware.IsOwn(ex) ? ex.Message : GenericMessage });
        context.ExceptionHandled = true;
    }

    /// <summary>A logger in the category of the controller that owns the action (what <c>ILogger&lt;TController&gt;</c> would give).</summary>
    internal static ILogger LoggerFor(ExceptionContext context)
    {
        var category = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerTypeInfo.AsType()
            ?? typeof(BadRequestOnArgumentExceptionAttribute);
        return context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(category);
    }
}
