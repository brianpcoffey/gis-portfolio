using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class BatchGeocodingControllerTests
    {
        private readonly Mock<IBatchGeocodingService> _serviceMock;
        private readonly Mock<IBatchJobStore> _jobStoreMock;
        private readonly BatchGeocodingController _controller;

        public BatchGeocodingControllerTests()
        {
            _serviceMock  = new Mock<IBatchGeocodingService>();
            _jobStoreMock = new Mock<IBatchJobStore>();
            _controller   = new BatchGeocodingController(
                _serviceMock.Object,
                _jobStoreMock.Object,
                new Mock<ILogger<BatchGeocodingController>>().Object);

            // Wire up an HttpContext so SubmitAsync can read Request.Scheme / Request.Host.
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host   = new HostString("api.example.com");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private static IFormFile BuildFile(long length)
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(length);
            fileMock.Setup(f => f.FileName).Returns("addresses.csv");
            return fileMock.Object;
        }

        [Fact]
        public async Task GeocodeAddresses_ValidFile_ReturnsOkWithResults()
        {
            // Arrange
            var file = BuildFile(128);
            var results = new List<BatchGeocodingResultDto>
            {
                new()
                {
                    OriginalAddress = "123 Main St, Denver, CO 80201",
                    Matched = true,
                    MatchedAddress = "123 Main St, Denver, CO 80201",
                    Score = 95.0,
                    Latitude = 39.7,
                    Longitude = -104.9
                }
            };

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(results, ok.Value);
        }

        [Fact]
        public async Task GeocodeAddresses_NullFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GeocodeAddresses(null!, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task GeocodeAddresses_EmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var file = BuildFile(0);

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task GeocodeAddresses_ServiceThrowsArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var file = BuildFile(64);

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("The CSV file contains no data rows.", "file"));

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        // ── SubmitAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task SubmitAsync_NullFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.SubmitAsync(null!, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task SubmitAsync_EmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var file = BuildFile(0);

            // Act
            var result = await _controller.SubmitAsync(file, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task SubmitAsync_ValidFile_Returns202AcceptedWithJobIdAndStatusUrl()
        {
            // Arrange
            var file  = BuildFile(256);
            var jobId = "abc123def456abc123def456abc12345";

            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobId);

            // Act
            var result = await _controller.SubmitAsync(file, CancellationToken.None);

            // Assert
            var accepted = Assert.IsType<AcceptedResult>(result);
            var dto = Assert.IsType<BatchJobAcceptedDto>(accepted.Value);
            Assert.Equal(jobId, dto.JobId);
            Assert.Contains(jobId, dto.StatusUrl);
            Assert.StartsWith("https://api.example.com", dto.StatusUrl);
        }

        [Fact]
        public async Task SubmitAsync_ServiceThrowsArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var file = BuildFile(64);

            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("The CSV file contains no data rows.", "file"));

            // Act
            var result = await _controller.SubmitAsync(file, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task SubmitAsync_BrokenCircuit_Returns503WithProblemDetails()
        {
            // Arrange
            var file = BuildFile(128);

            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            // Act
            var result = await _controller.SubmitAsync(file, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(503, problem.Status);
            Assert.Contains("15", problem.Extensions["retryAfterSeconds"]?.ToString());
        }

        // ── GetStatusAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatusAsync_JobExists_ReturnsOkWithJob()
        {
            // Arrange
            var job = new BatchJob
            {
                JobId       = "job999",
                Status      = BatchJobStatus.Completed,
                SubmittedAt = DateTimeOffset.UtcNow,
                FileName    = "results.csv",
                TotalRows   = 3,
                ProcessedRows = 3
            };

            _jobStoreMock
                .Setup(s => s.GetAsync("job999", It.IsAny<CancellationToken>()))
                .ReturnsAsync(job);

            // Act
            var result = await _controller.GetStatusAsync("job999", CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(job, ok.Value);
        }

        [Fact]
        public async Task GetStatusAsync_UnknownJobId_ReturnsNotFound()
        {
            // Arrange
            _jobStoreMock
                .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BatchJob?)null);

            // Act
            var result = await _controller.GetStatusAsync("missing-id", CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── BrokenCircuitException on legacy sync action ───────────────────────

        [Fact]
        public async Task GeocodeAddresses_BrokenCircuit_Returns503WithProblemDetails()
        {
            // Arrange
            var file = BuildFile(128);

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            // Act
            var result = await _controller.GeocodeAddresses(file, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(503, problem.Status);
        }
    }
}
