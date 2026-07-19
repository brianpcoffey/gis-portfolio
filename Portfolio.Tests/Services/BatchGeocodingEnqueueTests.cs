using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portfolio.Common.Configuration;
using Portfolio.Common.Models;
using Portfolio.Services;
using Portfolio.Services.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Portfolio.Tests.Services
{
    /// <summary>
    /// Tests for BatchGeocodingService.EnqueueAsync — the async job pattern introduced in Change 4.
    /// Each test that inspects background-task results polls the store until the job reaches a
    /// terminal status, capped at five seconds to prevent an infinite wait in CI.
    /// </summary>
    public class BatchGeocodingEnqueueTests
    {
        private readonly Mock<ILogger<BatchGeocodingService>> _loggerMock = new();
        private readonly IDistributedCache _memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        private static IOptions<BatchGeocodingOptions> MakeOptions(int maxConcurrency = 2, double minScore = 80.0) =>
            Options.Create(new BatchGeocodingOptions
            {
                MaxConcurrency = maxConcurrency,
                MinMatchScore = minScore
            });

        private BatchGeocodingService CreateService(HttpMessageHandler handler, InMemoryBatchJobStore store) =>
            new(httpClient: new HttpClient(handler),
                cache:      _memoryCache,
                logger:     _loggerMock.Object,
                jobStore:   store,
                options:    MakeOptions());

        private static IFormFile BuildCsvFile(string csvContent)
        {
            var bytes  = Encoding.UTF8.GetBytes(csvContent);
            var stream = new MemoryStream(bytes);
            var mock   = new Mock<IFormFile>();
            mock.Setup(f => f.OpenReadStream()).Returns(stream);
            mock.Setup(f => f.Length).Returns(bytes.Length);
            mock.Setup(f => f.FileName).Returns("test.csv");
            return mock.Object;
        }

        private static string BuildMatchJson(double score = 95.0) =>
            JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new { address = "123 Main St, Denver, CO 80201", score, location = new { x = -104.9, y = 39.7 } }
                }
            });

        // Polls the store until the job leaves Queued/Processing, then returns it.
        private static async Task<BatchJob> WaitForTerminalStatusAsync(InMemoryBatchJobStore store, string jobId)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var job = await store.GetAsync(jobId);
                if (job is not null &&
                    job.Status is BatchJobStatus.Completed or BatchJobStatus.Failed)
                {
                    return job;
                }
                await Task.Delay(25);
            }
            throw new TimeoutException($"Job {jobId} did not reach a terminal status within 5 seconds.");
        }

        // EnqueueAsync returns a non-empty, 32-character hex job ID (Guid "N" format).
        [Fact]
        public async Task EnqueueAsync_ReturnsNonEmptyHexJobId()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson()), store);
            var file    = BuildCsvFile("Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201");

            // Act
            var jobId = await service.EnqueueAsync(file);

            // Assert — Guid.ToString("N") produces exactly 32 hex characters
            Assert.False(string.IsNullOrWhiteSpace(jobId));
            Assert.Equal(32, jobId.Length);
            Assert.True(jobId.All(char.IsAsciiHexDigit));
        }

        // Immediately after EnqueueAsync the job is visible in the store with Queued or Processing status.
        [Fact]
        public async Task EnqueueAsync_JobIsInStoreWithQueuedOrProcessingStatus()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new SlowHttpMessageHandler(delayMs: 500, BuildMatchJson()), store);
            var file    = BuildCsvFile("Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201");

            // Act
            var jobId = await service.EnqueueAsync(file);
            var job   = await store.GetAsync(jobId);

            // Assert — must exist immediately; status may advance before we read it
            Assert.NotNull(job);
            Assert.Contains(job.Status, new[] { BatchJobStatus.Queued, BatchJobStatus.Processing, BatchJobStatus.Completed });
        }

        // FileName on the stored job matches the IFormFile.FileName.
        [Fact]
        public async Task EnqueueAsync_StoresCorrectFileName()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson()), store);
            var file    = BuildCsvFile("Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201");

            // Act
            var jobId = await service.EnqueueAsync(file);
            await WaitForTerminalStatusAsync(store, jobId);
            var job = await store.GetAsync(jobId);

            // Assert
            Assert.NotNull(job);
            Assert.Equal("test.csv", job.FileName);
        }

        // After background geocoding completes the job reaches Completed with the correct metrics.
        [Fact]
        public async Task EnqueueAsync_BackgroundTask_SetsCompletedStatusAndMetrics()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson(95.0)), store);
            var csv     = "Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201\n2,456 Oak Ave,Boulder,CO,80302";
            var file    = BuildCsvFile(csv);

            // Act
            var jobId = await service.EnqueueAsync(file);
            var job   = await WaitForTerminalStatusAsync(store, jobId);

            // Assert
            Assert.Equal(BatchJobStatus.Completed, job.Status);
            Assert.NotNull(job.CompletedAt);
            Assert.Equal(2, job.TotalRows);
            Assert.Equal(2, job.ProcessedRows);
            Assert.Equal(2, job.Results.Count);
            Assert.True(job.AverageScore > 0);
            Assert.True(job.ThroughputPerSecond > 0);
        }

        // If the underlying GeocodeAsync throws, the job is marked Failed rather than left as Processing.
        [Fact]
        public async Task EnqueueAsync_BackgroundTask_SetsFailedStatusOnException()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new ErrorHttpMessageHandler(), store);
            // CSV has one valid row — the HTTP error will surface during geocoding
            var file    = BuildCsvFile("Id,Address,City,State,Zip\n1,Bad Address,Nowhere,XX,00000");

            // Act — EnqueueAsync itself must not throw; failure is async
            var jobId = await service.EnqueueAsync(file);
            // GeocodeAsync catches HttpRequestException and returns an unmatched row, so the
            // job still completes successfully — just with Matched=false. To force a real
            // failure we use a CSV that will cause an ArgumentException inside GeocodeAsync.
            var badFile = BuildCsvFile("Id,Address,City,State,Zip"); // header-only → 0 data rows
            var badStore = new InMemoryBatchJobStore();
            var badService = CreateService(new ErrorHttpMessageHandler(), badStore);
            var badJobId = await badService.EnqueueAsync(badFile);

            var badJob = await WaitForTerminalStatusAsync(badStore, badJobId);

            // Assert
            Assert.Equal(BatchJobStatus.Failed, badJob.Status);
            Assert.NotNull(badJob.CompletedAt);
        }

        // EnqueueAsync returns a fresh unique job ID on every call.
        [Fact]
        public async Task EnqueueAsync_CalledTwice_ReturnsDifferentJobIds()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson()), store);
            var file1   = BuildCsvFile("Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201");
            var file2   = BuildCsvFile("Id,Address,City,State,Zip\n2,456 Oak Ave,Boulder,CO,80302");

            // Act
            var id1 = await service.EnqueueAsync(file1);
            var id2 = await service.EnqueueAsync(file2);

            // Assert
            Assert.NotEqual(id1, id2);
        }

        // A 600-row CSV exercises the bounded channel's Wait backpressure mode.
        // The test verifies that all rows are processed without deadlock or data loss.
        [Fact]
        public async Task EnqueueAsync_LargeCsv_BoundedChannelProcessesAllRows()
        {
            // Arrange — build a 600-row CSV (above the channel capacity of 500)
            const int rowCount = 600;
            var sb = new StringBuilder("Id,Address,City,State,Zip\n");
            for (int i = 1; i <= rowCount; i++)
                sb.AppendLine($"{i},{i} Main St,Denver,CO,8020{i % 10}");

            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson(90.0)), store);
            var file    = BuildCsvFile(sb.ToString());

            // Act
            var jobId = await service.EnqueueAsync(file);
            var job   = await WaitForTerminalStatusAsync(store, jobId);

            // Assert — no rows lost despite 600 > channel capacity of 500
            Assert.Equal(BatchJobStatus.Completed, job.Status);
            Assert.Equal(rowCount, job.TotalRows);
            Assert.Equal(rowCount, job.Results.Count);
        }

        // ── Fake HTTP handlers ───────────────────────────────────────────────

        private sealed class FakeHttpMessageHandler(string json) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
        }

        private sealed class SlowHttpMessageHandler(int delayMs, string json) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(delayMs, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }

        // Returns HTTP 500 so the service's HttpRequestException path is exercised.
        private sealed class ErrorHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
