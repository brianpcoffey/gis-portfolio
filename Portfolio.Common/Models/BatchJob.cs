using System.Text.Json.Serialization;
using Portfolio.Common.DTOs;

namespace Portfolio.Common.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BatchJobStatus { Queued, Processing, Completed, Failed }

/// <summary>
/// Represents an async batch geocoding job, including status, progress metrics, and results.
/// </summary>
public sealed class BatchJob
{
    /// <summary>Unique job identifier (GUID, no hyphens).</summary>
    public string JobId { get; init; } = default!;

    /// <summary>Current lifecycle status of the job.</summary>
    public BatchJobStatus Status { get; set; }

    /// <summary>UTC timestamp when the job was submitted.</summary>
    public DateTimeOffset SubmittedAt { get; init; }

    /// <summary>UTC timestamp when the job finished (completed or failed).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Original file name of the uploaded CSV.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Total number of address rows parsed from the CSV.</summary>
    public int TotalRows { get; set; }

    /// <summary>Number of rows that have been processed so far.</summary>
    public int ProcessedRows { get; set; }

    /// <summary>Number of results served from the in-memory cache (no upstream HTTP call made).</summary>
    public int CacheHits { get; set; }

    /// <summary>Number of rows that could not be geocoded.</summary>
    public int FailedRows { get; set; }

    /// <summary>Mean ArcGIS match score across all matched results.</summary>
    public double AverageScore { get; set; }

    /// <summary>Rows processed per second over the full job duration.</summary>
    public double ThroughputPerSecond { get; set; }

    /// <summary>Full geocoding result set, populated when the job reaches Completed status.</summary>
    public List<BatchGeocodingResultDto> Results { get; set; } = [];
}
