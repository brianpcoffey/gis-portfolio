using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services;

namespace Portfolio.Tests.Services
{
    public class InMemoryBatchJobStoreTests
    {
        private static BatchJob MakeJob(string jobId = "abc123") => new()
        {
            JobId       = jobId,
            Status      = BatchJobStatus.Queued,
            SubmittedAt = DateTimeOffset.UtcNow,
            FileName    = "test.csv"
        };

        // CreateAsync followed by GetAsync returns the same object.
        [Fact]
        public async Task CreateAsync_ThenGetAsync_ReturnsSameJob()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job   = MakeJob("job-1");

            // Act
            await store.CreateAsync(job);
            var retrieved = await store.GetAsync("job-1");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("job-1",              retrieved.JobId);
            Assert.Equal(BatchJobStatus.Queued, retrieved.Status);
            Assert.Equal("test.csv",           retrieved.FileName);
        }

        // GetAsync for an unknown ID returns null without throwing.
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

        // UpdateAsync replaces the stored record; the updated state is visible via GetAsync.
        [Fact]
        public async Task UpdateAsync_OverwritesExistingRecord()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job   = MakeJob("job-u");
            await store.CreateAsync(job);

            // Act
            job.Status       = BatchJobStatus.Completed;
            job.ProcessedRows = 5;
            await store.UpdateAsync(job);
            var retrieved = await store.GetAsync("job-u");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(BatchJobStatus.Completed, retrieved.Status);
            Assert.Equal(5, retrieved.ProcessedRows);
        }

        // UpdateAsync can upsert a job that was never explicitly created.
        [Fact]
        public async Task UpdateAsync_WithoutPriorCreate_StoresJob()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job   = MakeJob("job-upsert");

            // Act
            await store.UpdateAsync(job);
            var retrieved = await store.GetAsync("job-upsert");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("job-upsert", retrieved.JobId);
        }

        // Multiple distinct job IDs are stored and retrieved independently.
        [Fact]
        public async Task MultipleJobs_StoredAndRetrievedIndependently()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var jobA  = MakeJob("aaa");
            var jobB  = MakeJob("bbb");

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

        // Results list is stored and retrievable after UpdateAsync.
        [Fact]
        public async Task UpdateAsync_WithResults_ResultsAreRetrievable()
        {
            // Arrange
            var store = new InMemoryBatchJobStore();
            var job   = MakeJob("job-r");
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
