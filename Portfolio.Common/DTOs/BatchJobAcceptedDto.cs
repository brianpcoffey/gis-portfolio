namespace Portfolio.Common.DTOs;

/// <summary>
/// Response body returned by the 202 Accepted response when a batch geocoding job is enqueued.
/// </summary>
/// <param name="JobId">Opaque identifier for the submitted job.</param>
/// <param name="StatusUrl">Absolute URL the client should poll to retrieve job status and results.</param>
public sealed record BatchJobAcceptedDto(string JobId, string StatusUrl);
