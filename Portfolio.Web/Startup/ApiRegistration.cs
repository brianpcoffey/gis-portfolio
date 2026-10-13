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

        // MapOpenApi serves /openapi/v1.json; Scalar renders it at /scalar.
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        return services;
    }
}
