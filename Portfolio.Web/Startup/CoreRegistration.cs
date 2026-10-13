namespace Portfolio.Web.Startup;

internal static class CoreRegistration
{
    // --------------------------
    // Kestrel limits
    // --------------------------
    // 1 MB is the default request-body ceiling for every endpoint; the compute and upload actions
    // that legitimately need more raise it per endpoint with [RequestSizeLimit] (a Kestrel-level
    // limit, so only a live run enforces it, not TestServer). The Server response header is dropped
    // so the stack is not advertised.
    internal static WebApplicationBuilder ConfigurePortfolioKestrel(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 1_000_000;
            options.AddServerHeader = false;
        });
        return builder;
    }

    // --------------------------
    // Razor Pages & Services
    // --------------------------
    internal static IServiceCollection AddPortfolioCore(this IServiceCollection services)
    {
        services.AddRazorPages();
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
