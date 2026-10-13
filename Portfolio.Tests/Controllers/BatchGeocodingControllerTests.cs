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
        public async Task GeocodeAddresses_ServiceThrowsArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var file = BuildFile(64);

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("The CSV file contains no data rows.", "file"));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.GeocodeAddresses(file, CancellationToken.None));
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
        public async Task SubmitAsync_ServiceThrowsArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var file = BuildFile(64);

            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("The CSV file contains no data rows.", "file"));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.SubmitAsync(file, CancellationToken.None));
        }

        [Fact]
        public async Task SubmitAsync_BrokenCircuit_Propagates()
        {
            // Arrange: [ArcGisUnavailable] turns it into the 503 body (see the filter tests).
            var file = BuildFile(128);

            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            // Act & Assert
            await Assert.ThrowsAsync<Polly.CircuitBreaker.BrokenCircuitException>(
                () => _controller.SubmitAsync(file, CancellationToken.None));
        }

        [Fact]
        public async Task SubmitAsync_QueueFull_Returns503WithRetryAfterHeaderAndProblemDetails()
        {
            // Arrange
            var file = BuildFile(128);
            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Portfolio.Services.Abstractions.BatchQueueFullException());

            // Act
            var result = await _controller.SubmitAsync(file, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(503, problem.Status);
            Assert.Equal(30, problem.Extensions["retryAfterSeconds"]);
            Assert.Equal("30", _controller.Response.Headers.RetryAfter.ToString());
        }

        [Fact]
        public async Task SubmitAsync_CsvOverRowCap_PropagatesTheServiceMessage()
        {
            // Arrange: the exception propagates from the action unchanged. The filter shows its message
            // only when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET,
            // so it gets the generic text.
            var file = BuildFile(128);
            _serviceMock
                .Setup(s => s.EnqueueAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("CSV exceeds the maximum of 1000 data rows."));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _controller.SubmitAsync(file, CancellationToken.None));

            // Assert
            Assert.Equal("CSV exceeds the maximum of 1000 data rows.", ex.Message);
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
        public async Task GeocodeAddresses_BrokenCircuit_Propagates()
        {
            // Arrange: [ArcGisUnavailable] turns it into the 503 body (see the filter tests).
            var file = BuildFile(128);

            _serviceMock
                .Setup(s => s.GeocodeAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Polly.CircuitBreaker.BrokenCircuitException("circuit open"));

            // Act & Assert
            await Assert.ThrowsAsync<Polly.CircuitBreaker.BrokenCircuitException>(
                () => _controller.GeocodeAddresses(file, CancellationToken.None));
        }

        // ── Exact error bodies ─────────────────────────────────────────────────

        private static IFormFile BuildNamedFile(string name, long length)
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(length);
            fileMock.Setup(f => f.FileName).Returns(name);
            return fileMock.Object;
        }

        [Fact]
        public async Task SubmitAsync_NoFile_PinsProblemDetailsBody()
        {
            // Act
            var result = await _controller.SubmitAsync(null!, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(bad.Value);
            Assert.Equal("File required", problem.Title);
            Assert.Equal(400, problem.Status);
            Assert.Null(problem.Detail);
        }

        [Fact]
        public async Task SubmitAsync_EmptyFile_PinsProblemDetailsBody()
        {
            // Act
            var result = await _controller.SubmitAsync(BuildNamedFile("a.csv", 0), CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("File required", Assert.IsType<ProblemDetails>(bad.Value).Title);
        }

        [Fact]
        public async Task SubmitAsync_NonCsvFile_PinsProblemDetailsBody()
        {
            // Act
            var result = await _controller.SubmitAsync(BuildNamedFile("a.txt", 10), CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(bad.Value);
            Assert.Equal("A .csv file is required", problem.Title);
            Assert.Equal(400, problem.Status);
        }

        [Fact]
        public async Task GeocodeAddresses_NoFile_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.GeocodeAddresses(null!, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "A non-empty CSV file is required.");
        }

        [Fact]
        public async Task GeocodeAddresses_NonCsvFile_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.GeocodeAddresses(BuildNamedFile("a.txt", 10), CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "A .csv file is required.");
        }
    }
}

#pragma warning restore CS0618
