using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Portfolio.Web.Filters;

/// <summary>Logging helper shared by the exception-filter attributes in this folder.</summary>
internal static class FilterLogging
{
    /// <summary>
    /// A logger in the category of the controller that owns the action (what <c>ILogger&lt;TController&gt;</c>
    /// would give), or in <paramref name="fallbackCategory"/> when the action is not a controller action.
    /// </summary>
    /// <param name="context">The filter context of the failed action.</param>
    /// <param name="fallbackCategory">The calling attribute's own type.</param>
    internal static ILogger LoggerFor(ExceptionContext context, Type fallbackCategory)
    {
        var category = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerTypeInfo.AsType()
            ?? fallbackCategory;
        return context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(category);
    }
}
