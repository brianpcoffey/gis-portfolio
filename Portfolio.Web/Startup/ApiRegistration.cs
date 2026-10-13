using Asp.Versioning;

namespace Portfolio.Web.Startup;

internal static class ApiRegistration
{
    internal static IServiceCollection AddPortfolioControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy        = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        return services;
    }

    // --------------------------
    // API Versioning
    // --------------------------
    // Versioning, the API Explorer and OpenAPI are registered together, in that order.
    internal static IServiceCollection AddPortfolioApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            // Require explicit URL-segment versions so missing or ambiguous API versions are detected.
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        // --------------------------
        // OpenAPI / API Explorer (the document is served by Scalar)
        // --------------------------
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        return services;
    }
}
