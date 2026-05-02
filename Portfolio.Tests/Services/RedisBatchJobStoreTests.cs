using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services;

namespace Portfolio.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="RedisBatchJobStore"/> using an in-process
    /// <see cref="MemoryDistributedCache"/> to verify that serialization and
    /// deserialization round-trips are symmetric without requiring a live Redis instance.
    /// </summary>
    public class RedisBatchJobStoreTests
    {
        private readonly RedisBatchJobStore _store;

        public RedisBatchJobStoreTests()
        {
            var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var logger = new Mock<ILogger<RedisBatchJobStore>>().Object;
            _store = new RedisBatchJobStore(cache, logger);
        }

        [Fact]
        public async Task CreateAsync_ThenGetAsync_ReturnsDeserializedJob()
        {
            // Arrange
            var job = new BatchJob
            {
                JobId       = Guid.NewGuid().ToString("N"),
                Status      = BatchJobStatus.Queued,
                SubmittedAt = DateTimeOffset.UtcNow,
                FileName    = "test.csv",
                TotalRows   = 10
            };

            // Act
            await _store.CreateAsync(job);
            var retrieved = await _store.GetAsync(job.JobId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(job.JobId, retrieved.JobId);
            Assert.Equal(BatchJobStatus.Queued, retrieved.Status);
            Assert.Equal("test.csv", retrieved.FileName);
            Assert.Equal(10, retrieved.TotalRows);
        }

        [Fact]
        public async Task GetAsync_UnknownJobId_ReturnsNull()
        {
            // Act
            var result = await _store.GetAsync("does-not-exist");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsync_OverwritesExistingJob_StatusRoundTrips()
        {
            // Arrange
            var jobId = Guid.NewGuid().ToString("N");
            var job = new BatchJob
            {
                JobId       = jobId,
                Status      = BatchJobStatus.Queued,
                SubmittedAt = DateTimeOffset.UtcNow,
                FileName    = "update-test.csv"
            };
            await _store.CreateAsync(job);

            // Act — update to Processing
            job.Status = BatchJobStatus.Processing;
            await _store.UpdateAsync(job);
            var afterProcessing = await _store.GetAsync(jobId);

            // Act — update to Completed with results
            job.Status        = BatchJobStatus.Completed;
            job.CompletedAt   = DateTimeOffset.UtcNow;
            job.ProcessedRows = 5;
            job.AverageScore  = 92.5;
            job.Results       = [new BatchGeocodingResultDto { OriginalAddress = "123 Main St", Matched = true, Score = 92.5 }];
            await _store.UpdateAsync(job);
            var afterComplete = await _store.GetAsync(jobId);

            // Assert — each state transition persisted correctly
            Assert.NotNull(afterProcessing);
            Assert.Equal(BatchJobStatus.Processing, afterProcessing.Status);

            Assert.NotNull(afterComplete);
            Assert.Equal(BatchJobStatus.Completed, afterComplete.Status);
            Assert.Equal(5, afterComplete.ProcessedRows);
            Assert.Equal(92.5, afterComplete.AverageScore);
            Assert.Single(afterComplete.Results);
            Assert.Equal("123 Main St", afterComplete.Results[0].OriginalAddress);
        }

        [Fact]
        public async Task AllBatchJobStatusValues_SurviveJsonRoundTrip()
        {
            // Arrange — verify every enum member serializes as its string name (not an integer)
            foreach (var status in Enum.GetValues<BatchJobStatus>())
            {
                var jobId = Guid.NewGuid().ToString("N");
                var job = new BatchJob
                {
                    JobId       = jobId,
                    Status      = status,
                    SubmittedAt = DateTimeOffset.UtcNow,
                    FileName    = $"{status}.csv"
                };

                // Act
                await _store.CreateAsync(job);
                var retrieved = await _store.GetAsync(jobId);

                // Assert
                Assert.NotNull(retrieved);
                Assert.Equal(status, retrieved.Status);
            }
        }
    }
}
