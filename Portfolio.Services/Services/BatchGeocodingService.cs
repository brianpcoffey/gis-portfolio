using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Portfolio.Common.ArcGis;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;
using System.Threading.Channels;

namespace Portfolio.Services.Services
{
    public class BatchGeocodingService : IBatchGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BatchGeocodingService> _logger;
        private readonly int _maxConcurrency;
        private readonly double _minMatchScore;
        private readonly int _cacheTtlMinutes;

        private const string GeocodeUrl =
            "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates";

        public BatchGeocodingService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<BatchGeocodingService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _maxConcurrency = configuration.GetValue<int>("BatchGeocoding:MaxConcurrency", 4);
            _minMatchScore = configuration.GetValue<double>("BatchGeocoding:MinMatchScore", 80.0);
            _cacheTtlMinutes = configuration.GetValue<int>("BatchGeocoding:CacheTtlMinutes", 60);
        }

        // Parses the uploaded CSV, geocodes each row via producer/consumer channels, and returns results.
        // Duplicate addresses are served from IMemoryCache to avoid redundant HTTP calls.
        public async Task<List<BatchGeocodingResultDto>> GeocodeAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("A non-empty CSV file is required.", nameof(file));

            var rows = ParseCsv(file);

            if (rows.Count == 0)
                throw new ArgumentException("The CSV file contains no data rows.", nameof(file));

            _logger.LogInformation("Batch geocoding job started. Record count: {Count}", rows.Count);

            var channel = Channel.CreateUnbounded<CsvRow>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false
            });

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

        // Geocodes a single CSV row, using IMemoryCache to avoid duplicate HTTP calls.
        private async Task<BatchGeocodingResultDto> GeocodeRowAsync(CsvRow row, CancellationToken cancellationToken)
        {
            var normalizedKey = $"{row.Address}|{row.City}|{row.State}|{row.Zip}".Trim().ToLowerInvariant();
            var cacheKey = $"geocode:{normalizedKey}";

            if (!_cache.TryGetValue(cacheKey, out GeocodeCacheEntry? cached) || cached is null)
            {
                cached = await FetchGeocodeAsync(row, cancellationToken);
                _cache.Set(cacheKey, cached, TimeSpan.FromMinutes(_cacheTtlMinutes));
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
                    return new GeocodeCacheEntry();

                return new GeocodeCacheEntry
                {
                    MatchedAddress = top.Address ?? string.Empty,
                    Score = top.Score,
                    Latitude = top.Location?.Y ?? 0,
                    Longitude = top.Location?.X ?? 0
                };
                // ArcGisGeocodeResponse and ArcGisGeocodeCandidate are defined in Portfolio.Common.ArcGis.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error geocoding address for row {Id}", row.Id);
                return new GeocodeCacheEntry();
            }
        }

        // Parses the CSV stream into typed rows. Expects header: Id,Address,City,State,Zip
        private static List<CsvRow> ParseCsv(IFormFile file)
        {
            var rows = new List<CsvRow>();

            using var reader = new StreamReader(file.OpenReadStream());

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

        private sealed class GeocodeCacheEntry
        {
            public string MatchedAddress { get; set; } = string.Empty;
            public double Score { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }
    }
}
