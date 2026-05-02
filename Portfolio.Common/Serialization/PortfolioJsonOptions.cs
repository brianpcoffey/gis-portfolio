using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portfolio.Common.Serialization;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used for all Redis serialization
/// across the solution. Centralizing options prevents silent deserialization
/// failures caused by mismatched write/read settings.
/// </summary>
public static class PortfolioJsonOptions
{
    /// <summary>
    /// Default options: camelCase property names, null values omitted,
    /// enums serialized as strings.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy       = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
        Converters                 = { new JsonStringEnumConverter() }
    };
}
