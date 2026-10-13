using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Portfolio.Common.Configuration;
using Portfolio.Common.Models;
using Portfolio.Services;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.BackgroundServices;
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
    public class BatchGeocodingEnqueueTests : IAsyncDisposable
    {
        private readonly Mock<ILogger<BatchGeocodingService>> _loggerMock = new();
        private readonly IDistributedCache _memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        private readonly BoundedBatchJobQueue _queue = new();
        private readonly List<BatchGeocodingWorker> _workers = [];

        public async ValueTask DisposeAsync()
        {
            foreach (var worker in _workers)
                await worker.StopAsync(CancellationToken.None);
        }

        // Starts the real hosted worker over the service, as Program.cs does, so the queued job runs end to end.
        private void StartWorker(BatchGeocodingService service)
        {
            var provider = new ServiceCollection()
                .AddSingleton<IBatchGeocodingService>(service)
                .BuildServiceProvider();
            var worker = new BatchGeocodingWorker(
                _queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BatchGeocodingWorker>.Instance);
            _workers.Add(worker);
            _ = worker.StartAsync(CancellationToken.None);
        }

        private static IOptions<BatchGeocodingOptions> MakeOptions(int maxConcurrency = 2, double minScore = 80.0) =>
            Options.Create(new BatchGeocodingOptions
            {
                MaxConcurrency = maxConcurrency,
                MinMatchScore = minScore
            });

        private BatchGeocodingService CreateService(HttpMessageHandler handler, IBatchJobStore store, bool startWorker = true)
        {
            var service = new BatchGeocodingService(
                httpClient: new HttpClient(handler),
                cache:      _memoryCache,
                logger:     _loggerMock.Object,
                jobStore:   store,
                queue:      _queue,
                options:    MakeOptions());
            if (startWorker)
                StartWorker(service);
            return service;
        }

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

        // With no worker draining it, the queue accepts exactly its capacity (8) and then rejects.
        [Fact]
        public async Task EnqueueAsync_QueueFull_ThrowsBatchQueueFullException()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson()), store, startWorker: false);
            const string csv = "Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201";
            for (var i = 0; i < BoundedBatchJobQueue.DefaultCapacity; i++)
                await service.EnqueueAsync(BuildCsvFile(csv));

            // Act & Assert
            await Assert.ThrowsAsync<BatchQueueFullException>(() => service.EnqueueAsync(BuildCsvFile(csv)));
        }

        // 1,001 rows is one over the D-10 cap: rejected on the request thread, before any job exists.
        [Fact]
        public async Task EnqueueAsync_CsvOverRowCap_ThrowsArgumentExceptionAndCreatesNoJob()
        {
            // Arrange
            var store   = new Mock<IBatchJobStore>();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson()), store.Object, startWorker: false);
            var sb = new StringBuilder("Id,Address,City,State,Zip\n");
            for (var i = 1; i <= 1001; i++)
                sb.Append(i).Append(',').Append(i).Append(" Main St,Denver,CO,80201\n");

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.EnqueueAsync(BuildCsvFile(sb.ToString())));

            // Assert
            Assert.StartsWith("CSV exceeds the maximum of 1000 data rows.", ex.Message);
            store.Verify(s => s.CreateAsync(It.IsAny<BatchJob>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // The worker takes a queued job through Processing to Completed and logs one summary for it.
        [Fact]
        public async Task Worker_ProcessesQueuedJobEndToEnd_WithOneSummaryLog()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new FakeHttpMessageHandler(BuildMatchJson(95.0)), store);
            var sb = new StringBuilder("Id,Address,City,State,Zip\n");
            for (var i = 1; i <= 30; i++)
                sb.Append(i).Append(',').Append(i).Append(" Private Rd,Denver,CO,80201\n");

            // Act
            var jobId = await service.EnqueueAsync(BuildCsvFile(sb.ToString()));
            var job   = await WaitForTerminalStatusAsync(store, jobId);

            // Assert
            Assert.Equal(BatchJobStatus.Completed, job.Status);
            Assert.Equal(30, job.Results.Count);
            var messages = _loggerMock.Invocations
                .Where(i => i.Method.Name == "Log")
                .Select(i => i.Arguments[2]?.ToString() ?? string.Empty)
                .ToList();
            var summary = Assert.Single(messages, m => m.Contains("summary"));
            Assert.Contains("30 rows", summary);
            Assert.DoesNotContain(messages, m => m.Contains("Private"));
        }

        // Stopping the host mid-job still writes a terminal status (the final update is in finally).
        [Fact]
        public async Task Worker_StoppedMidJob_LeavesJobFailedNotProcessing()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var service = CreateService(new BlockingHandler(), store);
            var jobId   = await service.EnqueueAsync(BuildCsvFile("Id,Address,City,State,Zip\n1,123 Main St,Denver,CO,80201"));
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while ((await store.GetAsync(jobId))!.Status != BatchJobStatus.Processing && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(10);

            // Act
            await _workers[0].StopAsync(CancellationToken.None);

            // Assert
            var job = await store.GetAsync(jobId);
            Assert.Equal(BatchJobStatus.Failed, job!.Status);
            Assert.NotNull(job.CompletedAt);
        }

        // Two workers: with more queued jobs than workers, exactly two run at once.
        [Fact]
        public async Task Worker_RunsAtMostTwoJobsConcurrently()
        {
            // Arrange
            var store   = new InMemoryBatchJobStore();
            var handler = new ConcurrencyProbeHandler(BuildMatchJson());
            var service = CreateService(handler, store);
            var ids = new List<string>();
            for (var i = 1; i <= 5; i++)
                ids.Add(await service.EnqueueAsync(BuildCsvFile($"Id,Address,City,State,Zip\n1,{i} Main St,Denver,CO,80201")));

            // Act
            foreach (var id in ids)
                await WaitForTerminalStatusAsync(store, id);

            // Assert
            Assert.Equal(BatchGeocodingWorker.WorkerCount, handler.MaxConcurrent);
        }

        private static BatchWorkItem MakeItem(string id) =>
            new(new BatchJob { JobId = id, Status = BatchJobStatus.Queued, FileName = id + ".csv" }, []);

        private (BatchGeocodingWorker Worker, Mock<IBatchGeocodingService> Service, Mock<IBatchJobStore> Store) BuildWorker()
        {
            var service = new Mock<IBatchGeocodingService>();
            var store   = new Mock<IBatchJobStore>();
            var provider = new ServiceCollection()
                .AddSingleton(service.Object)
                .AddSingleton(store.Object)
                .BuildServiceProvider();
            var worker = new BatchGeocodingWorker(
                _queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BatchGeocodingWorker>.Instance);
            _workers.Add(worker);
            return (worker, service, store);
        }

        // A cancellation that is not the host stopping must fail that job only; if it escaped
        // RunAsync the default BackgroundServiceExceptionBehavior.StopHost would stop the host.
        [Fact]
        public async Task Worker_NonHostCancellation_DoesNotStopTheLoop()
        {
            // Arrange
            var (worker, service, _) = BuildWorker();
            var second = new TaskCompletionSource();
            service.Setup(s => s.ProcessJobAsync(It.Is<BatchWorkItem>(i => i.Job.JobId == "a"), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new OperationCanceledException("upstream timeout"));
            service.Setup(s => s.ProcessJobAsync(It.Is<BatchWorkItem>(i => i.Job.JobId == "b"), It.IsAny<CancellationToken>()))
                   .Returns(() => { second.SetResult(); return Task.CompletedTask; });

            // Act
            await worker.StartAsync(CancellationToken.None);
            _queue.TryEnqueue(MakeItem("a"));
            _queue.TryEnqueue(MakeItem("b"));
            var finished = await Task.WhenAny(second.Task, Task.Delay(TimeSpan.FromSeconds(30)));

            // Assert
            Assert.Same(second.Task, finished);
            Assert.False(worker.ExecuteTask!.IsCompleted);
        }

        // On shutdown, jobs still waiting in the queue will never run: they are marked Failed.
        [Fact]
        public async Task Worker_Stopped_MarksStillQueuedJobsFailed()
        {
            // Arrange: both loops are busy with blocking jobs, so c and d stay queued.
            var (worker, service, store) = BuildWorker();
            var started = 0;
            var bothBusy = new TaskCompletionSource();
            service.Setup(s => s.ProcessJobAsync(It.IsAny<BatchWorkItem>(), It.IsAny<CancellationToken>()))
                   .Returns<BatchWorkItem, CancellationToken>(async (_, ct) =>
                   {
                       if (Interlocked.Increment(ref started) == BatchGeocodingWorker.WorkerCount)
                           bothBusy.SetResult();
                       await Task.Delay(Timeout.Infinite, ct);
                   });
            var updated = new List<BatchJob>();
            store.Setup(s => s.UpdateAsync(It.IsAny<BatchJob>(), It.IsAny<CancellationToken>()))
                 .Returns<BatchJob, CancellationToken>((j, _) => { lock (updated) updated.Add(j); return Task.CompletedTask; });
            await worker.StartAsync(CancellationToken.None);
            _queue.TryEnqueue(MakeItem("a"));
            _queue.TryEnqueue(MakeItem("b"));
            await bothBusy.Task.WaitAsync(TimeSpan.FromSeconds(30));
            _queue.TryEnqueue(MakeItem("c"));
            _queue.TryEnqueue(MakeItem("d"));

            // Act
            await worker.StopAsync(CancellationToken.None);

            // Assert
            lock (updated)
            {
                Assert.Equal(["c", "d"], updated.Select(j => j.JobId).OrderBy(x => x));
                Assert.All(updated, j =>
                {
                    Assert.Equal(BatchJobStatus.Failed, j.Status);
                    Assert.NotNull(j.CompletedAt);
                });
            }
        }

        // ── Fake HTTP handlers ───────────────────────────────────────────────

        private sealed class BlockingHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        // Each request holds for 150 ms and records the peak number in flight. Every job here has one
        // row, so in-flight requests equal running jobs.
        private sealed class ConcurrencyProbeHandler(string json) : HttpMessageHandler
        {
            private int _current;
            private int _max;
            public int MaxConcurrent => Volatile.Read(ref _max);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var now = Interlocked.Increment(ref _current);
                int seen;
                while ((seen = Volatile.Read(ref _max)) < now &&
                       Interlocked.CompareExchange(ref _max, now, seen) != seen) { }
                await Task.Delay(150, cancellationToken);
                Interlocked.Decrement(ref _current);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }

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
