using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Common.ArcGis;
using Portfolio.Common.Configuration;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Common.Serialization;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class BatchGeocodingService : IBatchGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<BatchGeocodingService> _logger;
        private readonly IBatchJobStore _jobStore;
        private readonly IBatchJobQueue _queue;
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
            IBatchJobQueue queue,
            IOptions<BatchGeocodingOptions> options)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _jobStore = jobStore;
            _queue = queue;
            var opts = options.Value;
            _maxConcurrency = opts.MaxConcurrency;
            _minMatchScore = opts.MinMatchScore;
            _cacheTtlMinutes = opts.CacheTtlMinutes;
        }

        // Synchronous path (POST .../batch/sync): reads the stream on the request thread and geocodes
        // inline, without the job queue. Bounded by MaxRows.
        public async Task<List<BatchGeocodingResultDto>> GeocodeAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("A non-empty CSV file is required.", nameof(file));

            using var ms = new MemoryStream();
            file.OpenReadStream().CopyTo(ms);
            var run = await RunGeocodeAsync(ms.ToArray(), file.FileName, onTotal: null, onProgress: null, cancellationToken);
            return run.Results;
        }

        // Core geocoding over raw CSV bytes. All internal callers use this method
        // so the IFormFile stream is never captured across an async boundary. Optional callbacks
        // report the total row count (once the CSV is parsed) and per-row completion progress, so
        // the async job can surface live polling status. Returns the results plus the cache-hit tally.
        private async Task<(List<BatchGeocodingResultDto> Results, int CacheHits)> RunGeocodeAsync(
            byte[] csvBytes, string fileName,
            Func<int, Task>? onTotal, Func<int, Task>? onProgress,
            CancellationToken cancellationToken = default)
        {
            var started = DateTimeOffset.UtcNow;
            var rows = ParseCsv(csvBytes);

            if (rows.Count == 0)
                throw new ArgumentException("The CSV file contains no data rows.", fileName);

            if (onTotal is not null)
                await onTotal(rows.Count);

            // Rows are already in memory (at most MaxRows), so the channel holds them all and the
            // producer step cannot block: nothing here can wait on a consumer that has died.
            var channel = Channel.CreateBounded<CsvRow>(new BoundedChannelOptions(rows.Count)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = false
            });

            foreach (var row in rows)
                channel.Writer.TryWrite(row);
            channel.Writer.Complete();

            // A faulting worker cancels its siblings so the job ends promptly instead of the
            // remaining workers grinding through (or hanging on) the rest of the rows.
            using var faultCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var workerToken = faultCts.Token;
            Exception? firstFault = null;

            var results = new System.Collections.Concurrent.ConcurrentBag<(string Id, BatchGeocodingResultDto Dto)>();
            var processed = 0;
            var cacheHits = 0;
            var stats = new RunStats();

            var workers = Enumerable.Range(0, _maxConcurrency).Select(_ => Task.Run(async () =>
            {
                try
                {
                    await foreach (var row in channel.Reader.ReadAllAsync(workerToken))
                    {
                        var (dto, cacheHit) = await GeocodeRowAsync(row, stats, workerToken);
                        results.Add((row.Id, dto));
                        if (cacheHit)
                            Interlocked.Increment(ref cacheHits);
                        var done = Interlocked.Increment(ref processed);
                        if (onProgress is not null)
                            await onProgress(done);
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                           && !(ex is OperationCanceledException && workerToken.IsCancellationRequested))
                {
                    Interlocked.CompareExchange(ref firstFault, ex, null);
                    faultCts.Cancel();
                    throw;
                }
            }, workerToken)).ToArray();

            try
            {
                await Task.WhenAll(workers);
            }
            catch when (firstFault is not null)
            {
                // Surface the original fault, not a sibling's cancellation.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFault).Throw();
            }

            var ordered = results.OrderBy(r => r.Id).Select(r => r.Dto).ToList();

            // One summary per job. No per-row logging: rows carry user-entered addresses (SEC-16)
            // and a 1,000-row job would otherwise write 1,000 lines.
            var matchedCount = ordered.Count(r => r.Matched);
            _logger.LogInformation(
                "Batch geocoding summary: {Rows} rows, {Matched} matched, {Unmatched} unmatched, " +
                "{CacheHits} cache hits, {UpstreamErrors} upstream errors, {ElapsedMs} ms.",
                ordered.Count, matchedCount, ordered.Count - matchedCount, cacheHits,
                stats.UpstreamErrors, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);

            return (ordered, cacheHits);
        }

        // The row cap is enforced here, on the request thread, so an oversized CSV is a 400 rather than a failed
        // job. A full queue throws BatchQueueFullException (503 + Retry-After at the controller).
        // The stream is read here so the IFormFile lifetime need not extend into the background.
        public async Task<string> EnqueueAsync(IFormFile file, CancellationToken ct = default)
        {
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();
            }

            // Throws ArgumentException above MaxRows. A header-only CSV still enqueues and
            // fails as a job, as before.
            ParseCsv(fileBytes);

            var jobId = Guid.NewGuid().ToString("N");
            var job = new BatchJob
            {
                JobId = jobId,
                Status = BatchJobStatus.Queued,
                SubmittedAt = DateTimeOffset.UtcNow,
                FileName = file.FileName
            };

            await _jobStore.CreateAsync(job, ct);

            if (!_queue.TryEnqueue(new BatchWorkItem(job, fileBytes)))
            {
                // The caller never learns this id; mark the record so it is not left Queued until TTL.
                job.Status = BatchJobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                await _jobStore.UpdateAsync(job, CancellationToken.None);
                throw new BatchQueueFullException();
            }

            return jobId;
        }

        // The final store update is in finally, so a failure or cancellation still leaves the
        // job Completed/Failed rather than stuck in Processing.
        public async Task ProcessJobAsync(BatchWorkItem item, CancellationToken ct)
        {
            var job = item.Job;
            try
            {
                job.Status = BatchJobStatus.Processing;
                await _jobStore.UpdateAsync(job, ct);

                var started = DateTimeOffset.UtcNow;

                var (results, cacheHits) = await RunGeocodeAsync(
                    item.CsvBytes, job.FileName,
                    onTotal: async total =>
                    {
                        job.TotalRows = total;
                        await _jobStore.UpdateAsync(job, ct);
                    },
                    // Report incremental progress so pollers see ProcessedRows climb.
                    // Persist on every row for small demo batches; throttle for larger ones.
                    onProgress: async done =>
                    {
                        job.ProcessedRows = done;
                        if (job.TotalRows <= 50 || done % 5 == 0 || done == job.TotalRows)
                            await _jobStore.UpdateAsync(job, ct);
                    },
                    ct);

                var matchedScores = results.Where(r => r.Matched).Select(r => r.Score).ToList();

                job.Results = results;
                job.ProcessedRows = results.Count;
                job.FailedRows = results.Count(r => !r.Matched);
                job.CacheHits = cacheHits;
                // Mean match score across matched results only: unmatched rows (score 0)
                // must not dilute the reported quality.
                job.AverageScore = matchedScores.Count > 0 ? matchedScores.Average() : 0;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.ThroughputPerSecond = results.Count /
                    Math.Max((job.CompletedAt.Value - started).TotalSeconds, 0.001);
                job.Status = BatchJobStatus.Completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch job {JobId} failed.", job.JobId);
                job.Status = BatchJobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
            }
            finally
            {
                await _jobStore.UpdateAsync(job, CancellationToken.None);
            }
        }

        // Geocodes a single CSV row, using IDistributedCache to avoid duplicate HTTP calls across replicas.
        // Returns the result plus whether it was served from cache (for the job's CacheHits tally).
        private async Task<(BatchGeocodingResultDto Dto, bool CacheHit)> GeocodeRowAsync(CsvRow row, RunStats stats, CancellationToken cancellationToken)
        {
            var normalizedKey = $"{row.Address}|{row.City}|{row.State}|{row.Zip}".Trim().ToLowerInvariant();
            var cacheKey = $"geocode:{normalizedKey}";

            GeocodeCacheEntry? cached = null;
            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedBytes is not null)
            {
                cached = JsonSerializer.Deserialize<GeocodeCacheEntry>(cachedBytes, PortfolioJsonOptions.Default);
            }

            var cacheHit = cached is not null;

            if (cached is null)
            {
                cached = await FetchGeocodeAsync(row, stats, cancellationToken);
                var entryBytes = JsonSerializer.SerializeToUtf8Bytes(cached, PortfolioJsonOptions.Default);
                await _cache.SetAsync(cacheKey, entryBytes,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheTtlMinutes)
                    }, cancellationToken);
            }

            var matched = cached.Score >= _minMatchScore;

            var dto = new BatchGeocodingResultDto
            {
                OriginalAddress = $"{row.Address}, {row.City}, {row.State} {row.Zip}".Trim(),
                Matched = matched,
                MatchedAddress = matched ? cached.MatchedAddress : string.Empty,
                Score = cached.Score,
                Latitude = matched ? cached.Latitude : null,
                Longitude = matched ? cached.Longitude : null
            };

            return (dto, cacheHit);
        }

        private async Task<GeocodeCacheEntry> FetchGeocodeAsync(CsvRow row, RunStats stats, CancellationToken cancellationToken)
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
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                // Counted, not logged per row: the summary reports the total (SEC-16).
                Interlocked.Increment(ref stats.UpstreamErrors);
                return new GeocodeCacheEntry(string.Empty, 0, 0, 0);
            }
        }

        // Hard cap on data rows per upload, sync and async alike (decision D-10): bounds memory,
        // outbound geocoding volume and Redis commands.
        private const int MaxRows = 1_000;

        // Expects header: Id,Address,City,State,Zip
        private static List<CsvRow> ParseCsv(byte[] csvBytes)
        {
            var rows = new List<CsvRow>();

            using var reader = new StreamReader(new MemoryStream(csvBytes));

            var header = reader.ReadLine();
            if (header is null)
                return rows;

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (rows.Count >= MaxRows)
                    throw new ArgumentException(
                        $"CSV exceeds the maximum of {MaxRows} data rows.", nameof(csvBytes));

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

        // Per-run tally of upstream HTTP failures, reported once in the job summary.
        private sealed class RunStats
        {
            public int UpstreamErrors;
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
