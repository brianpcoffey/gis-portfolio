using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class GeoStreamProcessorServiceTests
    {
        [Fact]
        public async Task GeoStreamProcessBatch_ValidEvents_ReturnsAggregates()
        {
            // Arrange
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                AnomalySpeedThresholdMetersPerSecond = 30,
                Events =
                [
                    new TelemetryEventDto { EntityId = 1, Latitude = 34.05, Longitude = -117.18, SpeedMetersPerSecond = 12 },
                    new TelemetryEventDto { EntityId = 2, Latitude = 34.10, Longitude = -117.20, SpeedMetersPerSecond = 40 },
                    new TelemetryEventDto { EntityId = 3, Latitude = 120, Longitude = -117.20, SpeedMetersPerSecond = 10 }
                ]
            };

            // Act
            var result = await service.ProcessBatchAsync(request);

            // Assert
            Assert.Equal(3, result.TotalEvents);
            Assert.Equal(2, result.ValidEvents);
            Assert.Equal(1, result.InvalidEvents);
            Assert.Equal(1, result.AnomalyCount);
            Assert.Single(result.Aggregates);
        }

        [Fact]
        public async Task GeoStreamProcessBatch_TooManyEvents_ThrowsArgumentException()
        {
            // Arrange
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                Events = Enumerable.Range(0, 10001)
                    .Select(i => new TelemetryEventDto { EntityId = i, Latitude = 34, Longitude = -117 })
                    .ToList()
            };

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_NullRequest_ThrowsArgumentNullException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ProcessBatchAsync(null!));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_EmptyEventList_ThrowsArgumentException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto { GridSizeDegrees = 1, Events = [] };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_ZeroGridSize_ThrowsArgumentException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 0,
                Events = [new TelemetryEventDto { EntityId = 1, Latitude = 34, Longitude = -117 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_NegativeAnomalyThreshold_ThrowsArgumentException()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                AnomalySpeedThresholdMetersPerSecond = -1,
                Events = [new TelemetryEventDto { EntityId = 1, Latitude = 34, Longitude = -117 }]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStreamProcessBatch_AllEventsInvalid_ReturnsZeroValidAndEmptyAggregates()
        {
            // Arrange — all events are out of range
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                Events =
                [
                    new TelemetryEventDto { EntityId = 1, Latitude = 200, Longitude = 0 },
                    new TelemetryEventDto { EntityId = 2, Latitude = 0,   Longitude = 500 }
                ]
            };

            // Act
            var result = await service.ProcessBatchAsync(request);

            // Assert
            Assert.Equal(0, result.ValidEvents);
            Assert.Equal(2, result.InvalidEvents);
            Assert.Empty(result.Aggregates);
        }

        [Fact]
        public async Task GeoStreamProcessBatch_MultipleEventsInSameCell_AggregatesIntoOneCell()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 10,
                AnomalySpeedThresholdMetersPerSecond = 100,
                Events =
                [
                    new TelemetryEventDto { EntityId = 1, Latitude = 34.0, Longitude = -117.0, SpeedMetersPerSecond = 10 },
                    new TelemetryEventDto { EntityId = 2, Latitude = 34.1, Longitude = -117.1, SpeedMetersPerSecond = 20 },
                    new TelemetryEventDto { EntityId = 3, Latitude = 34.2, Longitude = -117.2, SpeedMetersPerSecond = 30 }
                ]
            };

            var result = await service.ProcessBatchAsync(request);

            Assert.Single(result.Aggregates);
            Assert.Equal(3, result.Aggregates[0].Count);
            Assert.InRange(result.Aggregates[0].AverageSpeedMetersPerSecond, 19.9, 20.1);
        }

        [Theory]
        [InlineData(0.0000009)]
        [InlineData(1e-9)]
        public async Task GeoStream_GridSizeBelowMinimum_ThrowsArgumentException(double grid)
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = grid,
                Events = [new() { EntityId = 1, Latitude = 1, Longitude = 1, SpeedMetersPerSecond = 1 }]
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
            Assert.StartsWith("Grid size must be at least 0.000001 degrees.", ex.Message);
        }

        [Fact]
        public async Task GeoStream_GridSizeExactlyMinimum_Succeeds()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1e-6,
                Events = [new() { EntityId = 1, Latitude = 34, Longitude = -117, SpeedMetersPerSecond = 1 }]
            };

            var result = await service.ProcessBatchAsync(request);

            Assert.Equal(1, result.ValidEvents);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public async Task GeoStream_NonFiniteSpeed_ThrowsArgumentException(double speed)
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                Events = [new() { EntityId = 1, Latitude = 34, Longitude = -117, SpeedMetersPerSecond = speed }]
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessBatchAsync(request));
        }

        [Fact]
        public async Task GeoStream_NonFiniteLatLon_AreCountedInvalidNotThrown()
        {
            var service = new GeoStreamProcessorService(NullLogger<GeoStreamProcessorService>.Instance);
            var request = new GeoStreamBatchRequestDto
            {
                GridSizeDegrees = 1,
                Events =
                [
                    new() { EntityId = 1, Latitude = 34.05, Longitude = -117.18, SpeedMetersPerSecond = 12 },
                    new() { EntityId = 2, Latitude = double.NaN, Longitude = -117.18, SpeedMetersPerSecond = 12 },
                    new() { EntityId = 3, Latitude = 34.05, Longitude = double.PositiveInfinity, SpeedMetersPerSecond = 12 }
                ]
            };

            var result = await service.ProcessBatchAsync(request);

            Assert.Equal(3, result.TotalEvents);
            Assert.Equal(1, result.ValidEvents);
            Assert.Equal(2, result.InvalidEvents);
            Assert.Equal(1, result.Aggregates.Sum(a => a.Count));
        }
    }
}
