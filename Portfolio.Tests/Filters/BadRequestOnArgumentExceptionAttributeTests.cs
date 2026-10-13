using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Portfolio.Tests.Controllers;
using Portfolio.Web.Controllers.Api;
using Portfolio.Web.Filters;

namespace Portfolio.Tests.Filters;

/// <summary>
/// WP-43 / D-17: <c>[BadRequestOnArgumentException]</c> unit tests. The filter is driven directly with an
/// <see cref="ExceptionContext"/>; the same bodies are pinned through the real host in
/// <c>ControllerErrorBodyTests</c>.
/// </summary>
public class BadRequestOnArgumentExceptionAttributeTests
{
    internal sealed class CapturingProvider : ILoggerProvider
    {
        public List<(string Category, LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, Entries);

        public void Dispose() { }

        private sealed class Capture(string category, List<(string, LogLevel, string, Exception?)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Add((category, logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>An <see cref="ExceptionContext"/> for an action of <paramref name="controller"/> (or a non-controller action when null).</summary>
    internal static (ExceptionContext Context, CapturingProvider Log) NewContext(Exception exception, Type? controller)
    {
        var log = new CapturingProvider();
        var services = new ServiceCollection().AddLogging(b => b.AddProvider(log)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = "POST";
        http.Request.Path = "/api/v1/clustering/dbscan";
        ActionDescriptor descriptor = controller is null
            ? new ActionDescriptor()
            : new ControllerActionDescriptor { ControllerTypeInfo = controller.GetTypeInfo() };
        var context = new ExceptionContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>())
        {
            Exception = exception
        };
        return (context, log);
    }

    // Thrown from this (Portfolio.Tests) assembly, standing in for a Portfolio service: ApiExceptionMiddleware.IsOwn
    // looks at the throwing method's assembly, which only exists once the exception has actually been thrown.
    internal static T Thrown<T>(T exception) where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T caught)
        {
            return caught;
        }
    }

    [Fact]
    public void ArgumentException_FromPortfolioCode_Returns400WithTheMessage()
    {
        // Arrange
        var (context, _) = NewContext(Thrown(new ArgumentException("Bad epsilon.", "request")), typeof(SpatialClusterController));

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        Assert.IsType<BadRequestObjectResult>(context.Result);
        ControllerBodyAssert.ErrorBody(context.Result, 400, "Bad epsilon. (Parameter 'request')");
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void ArgumentNullException_IsAnArgumentException_AndIsHandledToo()
    {
        // Arrange
        var (context, _) = NewContext(Thrown(new ArgumentNullException("request")), typeof(SpatialClusterController));

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        ControllerBodyAssert.ErrorBody(context.Result, 400, "Value cannot be null. (Parameter 'request')");
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void ArgumentException_RaisedInsideTheFramework_GetsTheGenericMessage()
    {
        // Arrange: Enum.Parse throws from System.Private.CoreLib, so its text is not ours to surface.
        var frameworkException = Assert.Throws<ArgumentException>(() => Enum.Parse<DayOfWeek>("not-a-day"));
        Assert.Contains("not-a-day", frameworkException.Message);
        var (context, _) = NewContext(frameworkException, typeof(SpatialClusterController));

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        ControllerBodyAssert.ErrorBody(context.Result, 400, "The request was invalid.");
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void ArgumentException_NeverThrown_HasNoOrigin_AndGetsTheGenericMessage()
    {
        // Arrange: a null TargetSite counts as not ours (a faulted Task carries such an exception).
        var (context, _) = NewContext(new ArgumentException("detail from nowhere"), typeof(SpatialClusterController));

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        ControllerBodyAssert.ErrorBody(context.Result, 400, "The request was invalid.");
    }

    [Fact]
    public void GenericMessage_IsTheTextTheExceptionMiddlewareUses()
    {
        Assert.Equal("The request was invalid.", BadRequestOnArgumentExceptionAttribute.GenericMessage);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(KeyNotFoundException))]
    [InlineData(typeof(Exception))]
    public void OtherExceptions_AreLeftForTheMiddleware(Type exceptionType)
    {
        // Arrange
        var (context, log) = NewContext(Thrown((Exception)Activator.CreateInstance(exceptionType, "boom")!), typeof(SpatialClusterController));

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        Assert.Null(context.Result);
        Assert.False(context.ExceptionHandled);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void LogsOneWarning_WithTheException_UnderTheControllersCategory()
    {
        // Arrange
        var exception = Thrown(new ArgumentException("Bad epsilon.", "request"));
        var (context, log) = NewContext(exception, typeof(SpatialClusterController));

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        var entry = Assert.Single(log.Entries);
        Assert.Equal(typeof(SpatialClusterController).FullName, entry.Category);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Equal("Invalid request on POST /api/v1/clustering/dbscan", entry.Message);
    }

    [Fact]
    public void ForAnActionThatIsNotOnAController_LogsUnderTheAttributesOwnCategory()
    {
        // Arrange
        var (context, log) = NewContext(Thrown(new ArgumentException("Bad.")), controller: null);

        // Act
        new BadRequestOnArgumentExceptionAttribute().OnException(context);

        // Assert
        Assert.Equal(typeof(BadRequestOnArgumentExceptionAttribute).FullName, Assert.Single(log.Entries).Category);
        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact]
    public void IsActionLevelOnly()
    {
        // A class-level attribute would also reshape the ArgumentExceptions of actions that deliberately let
        // ApiExceptionMiddleware answer, so the attribute cannot be put on a controller.
        var usage = typeof(BadRequestOnArgumentExceptionAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
