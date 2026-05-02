using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Portfolio.Common.ArcGis;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Common.Serialization;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

namespace Portfolio.Services.Services
{
    public class BatchGeocodingService : IBatchGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<BatchGeocodingService> _logger;
        private readonly IBatchJobStore _jobStore;
        private readonly int _maxConcurrency;
        private readonly double _minMatchScore;
        private readonly int _cacheTtlMinutes;

        private const string GeocodeUrl =
            "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates";

        public BatchGeocodingService(
            HttpClient httpClient,
            IDistributedCache cache,
            ILogger<BatchGeocodingService> logger,
            IBatchJobStore jobStore,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _jobStore = jobStore;
            _maxConcurrency = configuration.GetValue<int>("BatchGeocoding:MaxConcurrency", 4);
            _minMatchScore = configuration.GetValue<double>("BatchGeocoding:MinMatchScore", 80.0);
            _cacheTtlMinutes = configuration.GetValue<int>("BatchGeocoding:CacheTtlMinutes", 60);
        }

        // Public overload: delegates to the byte[] overload after reading the stream on the request thread.
        // Kept for the legacy [Obsolete] sync endpoint and existing tests.
        public Task<List<BatchGeocodingResultDto>> GeocodeAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("A non-empty CSV file is required.", nameof(file));

            using var ms = new MemoryStream();
            file.OpenReadStream().CopyTo(ms);
            return GeocodeAsync(ms.ToArray(), file.FileName, cancellationToken);
        }

        // Core geocoding implementation over raw CSV bytes. All internal callers use this overload
        // so the IFormFile stream is never captured across an async boundary.
        private async Task<List<BatchGeocodingResultDto>> GeocodeAsync(
            byte[] csvBytes, string fileName, CancellationToken cancellationToken = default)
        {
            var rows = ParseCsv(csvBytes);

            if (rows.Count == 0)
                throw new ArgumentException("The CSV file contains no data rows.", fileName);

            _logger.LogInformation("Batch geocoding job started. Record count: {Count}", rows.Count);

            var channel = Channel.CreateBounded<CsvRow>(new BoundedChannelOptions(500)
            {
                FullMode                      = BoundedChannelFullMode.Wait,
                SingleWriter                  = true,
                SingleReader                  = false,
                AllowSynchronousContinuations = false
            });

            _logger.LogInformation(
                "Batch geocoding channel created with capacity {Capacity} and {Workers} workers.",
                500, _maxConcurrency);

            // Producer: write all rows into the channel.
            var producer = Task.Run(async () =>
            {
                foreach (var row in rows)
                    await channel.Writer.WriteAsync(row, cancellationToken);

                channel.Writer.Complete();
            }, cancellationToken);

            // Consumer: spin up MaxConcurrency workers.
            var results = new System.Collections.Concurrent.ConcurrentBag<(string Id, BatchGeocodingResultDto Dto)>();

            var workers = Enumerable.Range(0, _maxConcurrency).Select(_ => Task.Run(async () =>
            {
                await foreach (var row in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    var result = await GeocodeRowAsync(row, cancellationToken);
                    results.Add((row.Id, result));
                }
            }, cancellationToken)).ToArray();

            await producer;
            await Task.WhenAll(workers);

            return [.. results.OrderBy(r => r.Id).Select(r => r.Dto)];
        }

        // Enqueues a CSV file for background processing and returns a job ID immediately.
        // The file stream is read on the request thread before Task.Run so the IFormFile
        // lifetime does not need to extend into the background closure.
        public async Task<string> EnqueueAsync(IFormFile file, CancellationToken ct = default)
        {
            // Read the entire stream on the request thread, before Task.Run.
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();
            }
            string originalFileName = file.FileName;

            var jobId = Guid.NewGuid().ToString("N");
            var job = new BatchJob
            {
                JobId       = jobId,
                Status      = BatchJobStatus.Queued,
                SubmittedAt = DateTimeOffset.UtcNow,
                FileName    = originalFileName
            };

            await _jobStore.CreateAsync(job, ct);

            // Fire and forget — captures only value-type-safe locals (byte[], string, BatchJob).
            _ = Task.Run(async () =>
            {
                job.Status = BatchJobStatus.Processing;
                await _jobStore.UpdateAsync(job);

                try
                {
                    var started = DateTimeOffset.UtcNow;
                    var results = await GeocodeAsync(fileBytes, originalFileName, CancellationToken.None);

                    job.TotalRows           = results.Count;
                    await _jobStore.UpdateAsync(job);

                    job.Results             = results;
                    job.ProcessedRows       = results.Count;
                    job.AverageScore        = results.Count > 0
                        ? results.Average(r => r.Score) : 0;
                    job.CompletedAt         = DateTimeOffset.UtcNow;
                    job.ThroughputPerSecond = results.Count /
                        Math.Max((job.CompletedAt.Value - started).TotalSeconds, 0.001);
                    job.Status              = BatchJobStatus.Completed;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Batch job {JobId} failed.", jobId);
                    job.Status      = BatchJobStatus.Failed;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                }

                await _jobStore.UpdateAsync(job);
            }, CancellationToken.None);

            return jobId;
        }

        // Geocodes a single CSV row, using IDistributedCache to avoid duplicate HTTP calls across replicas.
        private async Task<BatchGeocodingResultDto> GeocodeRowAsync(CsvRow row, CancellationToken cancellationToken)
        {
            var normalizedKey = $"{row.Address}|{row.City}|{row.State}|{row.Zip}".Trim().ToLowerInvariant();
            var cacheKey = $"geocode:{normalizedKey}";

            GeocodeCacheEntry? cached = null;
            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedBytes is not null)
            {
                cached = JsonSerializer.Deserialize<GeocodeCacheEntry>(cachedBytes, PortfolioJsonOptions.Default);
            }

            if (cached is null)
            {
                cached = await FetchGeocodeAsync(row, cancellationToken);
                var entryBytes = JsonSerializer.SerializeToUtf8Bytes(cached, PortfolioJsonOptions.Default);
                await _cache.SetAsync(cacheKey, entryBytes,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheTtlMinutes)
                    }, cancellationToken);
            }

            var matched = cached.Score >= _minMatchScore;

            if (!matched)
                _logger.LogWarning("Unmatched address for Id {Id}: {Address}", row.Id, row.Address);

            return new BatchGeocodingResultDto
            {
                OriginalAddress = $"{row.Address}, {row.City}, {row.State} {row.Zip}".Trim(),
                Matched = matched,
                MatchedAddress = matched ? cached.MatchedAddress : string.Empty,
                Score = cached.Score,
                Latitude = matched ? cached.Latitude : null,
                Longitude = matched ? cached.Longitude : null
            };
        }

        // Makes the HTTP call to the ArcGIS World Geocoding REST API.
        private async Task<GeocodeCacheEntry> FetchGeocodeAsync(CsvRow row, CancellationToken cancellationToken)
        {
            var address = Uri.EscapeDataString(row.Address);
            var city = Uri.EscapeDataString(row.City);
            var state = Uri.EscapeDataString(row.State);
            var zip = Uri.EscapeDataString(row.Zip);

            var url = $"{GeocodeUrl}?address={address}&city={city}&region={state}&postal={zip}&f=json&maxLocations=1";

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var parsed = await response.Content.ReadFromJsonAsync<ArcGisGeocodeResponse>(
                    cancellationToken: cancellationToken);

                var top = parsed?.Candidates?.FirstOrDefault();
                if (top is null)
                    return new GeocodeCacheEntry(string.Empty, 0, 0, 0);

                return new GeocodeCacheEntry(
                    top.Address ?? string.Empty,
                    top.Score,
                    top.Location?.Y ?? 0,
                    top.Location?.X ?? 0);
                // ArcGisGeocodeResponse and ArcGisGeocodeCandidate are defined in Portfolio.Common.ArcGis.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error geocoding address for row {Id}", row.Id);
                return new GeocodeCacheEntry(string.Empty, 0, 0, 0);
            }
        }

        // Parses the CSV bytes into typed rows. Expects header: Id,Address,City,State,Zip
        private static List<CsvRow> ParseCsv(byte[] csvBytes)
        {
            var rows = new List<CsvRow>();

            using var reader = new StreamReader(new MemoryStream(csvBytes));

            // Skip header line.
            var header = reader.ReadLine();
            if (header is null)
                return rows;

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',', 5);
                if (parts.Length < 5)
                    continue;

                rows.Add(new CsvRow
                {
                    Id = parts[0].Trim(),
                    Address = parts[1].Trim(),
                    City = parts[2].Trim(),
                    State = parts[3].Trim(),
                    Zip = parts[4].Trim()
                });
            }

            return rows;
        }

        private sealed class CsvRow
        {
            public string Id { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public string Zip { get; set; } = string.Empty;
        }

        private sealed record GeocodeCacheEntry(
            string MatchedAddress, double Score, double Latitude, double Longitude);
    }
}
