using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Repositories;
using Portfolio.Repositories.Diagnostics;
using Portfolio.Repositories.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Tests.Repositories
{
    /// <summary>An <see cref="ILogger{T}"/> that records every formatted message and its structured state.</summary>
    public sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            // Include the structured values too, so a raw id passed as an argument is caught
            // even if a message template were to drop it from the rendered text.
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                text += " | " + string.Join(", ", pairs.Select(p => $"{p.Key}={p.Value}"));
            Entries.Add((logLevel, text));
        }

        public string All => string.Join("\n", Entries.Select(e => e.Message));
    }

    public class LogRedactionTests
    {
        [Fact]
        public void UserId_ReturnsFirstEightHexCharsOfSha256()
        {
            var id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("3f2504e0-4f89-11d3-9a0c-0305e82c3301")))
                .Substring(0, 8).ToLowerInvariant();

            Assert.Equal(expected, LogRedaction.UserId(id));
        }

        [Fact]
        public void UserId_IsEightLowercaseHexCharsAndStable()
        {
            var id = Guid.NewGuid();

            var tag = LogRedaction.UserId(id);

            Assert.Equal(8, tag.Length);
            Assert.Matches("^[0-9a-f]{8}$", tag);
            Assert.Equal(tag, LogRedaction.UserId(id));
        }

        [Fact]
        public void UserId_DiffersBetweenUsersAndNeverContainsTheId()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            Assert.NotEqual(LogRedaction.UserId(a), LogRedaction.UserId(b));
            Assert.DoesNotContain(LogRedaction.UserId(a), a.ToString("D"), StringComparison.OrdinalIgnoreCase);
        }

        private static PortfolioDbContext NewDb() =>
            new(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        [Fact]
        public async Task CollectionRepository_DeleteMissing_LogsHashedOwnerOnly()
        {
            var owner = Guid.NewGuid();
            var logger = new CapturingLogger<CollectionRepository>();
            var repo = new CollectionRepository(NewDb(), logger);

            var deleted = await repo.DeleteAsync(99, owner);

            Assert.False(deleted);
            var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.Contains(LogRedaction.UserId(owner), warning.Message);
            Assert.DoesNotContain(owner.ToString(), logger.All, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SavedFeatureRepository_DeleteMissing_LogsHashedUserOnly()
        {
            var user = Guid.NewGuid();
            var logger = new CapturingLogger<SavedFeatureRepository>();
            var repo = new SavedFeatureRepository(NewDb(), logger);

            var deleted = await repo.DeleteAsync(99, user);

            Assert.False(deleted);
            var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.Contains(LogRedaction.UserId(user), warning.Message);
            Assert.DoesNotContain(user.ToString(), logger.All, StringComparison.OrdinalIgnoreCase);
        }
    }
}
