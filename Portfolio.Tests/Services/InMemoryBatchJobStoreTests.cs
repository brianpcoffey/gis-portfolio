using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services;

namespace Portfolio.Tests.Services
{
    public class InMemoryBatchJobStoreTests
    {
        private static BatchJob MakeJob(string jobId = "abc123") => new()
        {
            JobId = jobId,
            Status = BatchJobStatus.Queued,
            SubmittedAt = DateTimeOffset.UtcNow,
            FileName = "test.csv"
        };

        [Fact]
        public async Task CreateAsync_ThenGetAsync_ReturnsSameJob()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job = MakeJob("job-1");

            // Act
            await store.CreateAsync(job);
            var retrieved = await store.GetAsync("job-1");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("job-1", retrieved.JobId);
            Assert.Equal(BatchJobStatus.Queued, retrieved.Status);
            Assert.Equal("test.csv", retrieved.FileName);
        }

        [Fact]
        public async Task GetAsync_UnknownJobId_ReturnsNull()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();

            // Act
            var result = await store.GetAsync("does-not-exist");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsync_OverwritesExistingRecord()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job = MakeJob("job-u");
            await store.CreateAsync(job);

            // Act
            job.Status = BatchJobStatus.Completed;
            job.ProcessedRows = 5;
            await store.UpdateAsync(job);
            var retrieved = await store.GetAsync("job-u");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(BatchJobStatus.Completed, retrieved.Status);
            Assert.Equal(5, retrieved.ProcessedRows);
        }

        [Fact]
        public async Task UpdateAsync_WithoutPriorCreate_StoresJob()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job = MakeJob("job-upsert");

            // Act
            await store.UpdateAsync(job);
            var retrieved = await store.GetAsync("job-upsert");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("job-upsert", retrieved.JobId);
        }

        [Fact]
        public async Task MultipleJobs_StoredAndRetrievedIndependently()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var jobA = MakeJob("aaa");
            var jobB = MakeJob("bbb");

            // Act
            await store.CreateAsync(jobA);
            await store.CreateAsync(jobB);
            var retA = await store.GetAsync("aaa");
            var retB = await store.GetAsync("bbb");

            // Assert
            Assert.NotNull(retA);
            Assert.NotNull(retB);
            Assert.Equal("aaa", retA.JobId);
            Assert.Equal("bbb", retB.JobId);
        }

        // A job is gone 24 h after its last write; just before that it is still served.
        [Fact]
        public async Task GetAsync_AfterTtl_ReturnsNull()
        {
            // Arrange
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var store = new InMemoryBatchJobStore(clock);
            await store.CreateAsync(MakeJob("ttl"));

            // Act & Assert
            clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
            Assert.NotNull(await store.GetAsync("ttl"));
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Null(await store.GetAsync("ttl"));
        }

        // An update restarts the 24 h window, like the Redis store's sliding expiry.
        [Fact]
        public async Task UpdateAsync_ResetsTheTtlWindow()
        {
            // Arrange
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var store = new InMemoryBatchJobStore(clock);
            var job = MakeJob("slide");
            await store.CreateAsync(job);
            clock.Advance(TimeSpan.FromHours(20));
            await store.UpdateAsync(job);

            // Act
            clock.Advance(TimeSpan.FromHours(20));

            // Assert
            Assert.NotNull(await store.GetAsync("slide"));
        }

        // Writes prune expired jobs: after the old job expires, a write for another job evicts it
        // even if the old one is never read again.
        [Fact]
        public async Task Write_PrunesExpiredJobs()
        {
            // Arrange
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var store = new InMemoryBatchJobStore(clock);
            await store.CreateAsync(MakeJob("old"));
            clock.Advance(TimeSpan.FromHours(25));

            // Act
            await store.CreateAsync(MakeJob("new"));
            clock.Advance(TimeSpan.FromHours(-25)); // rewind: "old" would be live again if it were still stored

            // Assert
            Assert.Null(await store.GetAsync("old"));
            Assert.NotNull(await store.GetAsync("new"));
        }

        [Fact]
        public async Task UpdateAsync_WithResults_ResultsAreRetrievable()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job = MakeJob("job-r");
            await store.CreateAsync(job);

            // Act
            job.Results = new List<BatchGeocodingResultDto>
            {
                new() { OriginalAddress = "123 Main St", Matched = true, Score = 95.0 }
            };
            job.Status = BatchJobStatus.Completed;
            await store.UpdateAsync(job);
            var retrieved = await store.GetAsync("job-r");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Single(retrieved.Results);
            Assert.Equal("123 Main St", retrieved.Results[0].OriginalAddress);
        }
    }
}
