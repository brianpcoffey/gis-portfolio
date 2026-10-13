using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    /// <summary>
    /// Shared assertions that pin the exact response body of controller error branches, not just the
    /// status code, for the controller test classes in this folder.
    /// </summary>
    internal static class ControllerBodyAssert
    {
        /// <summary>Asserts a <c>{ error = expected }</c> body (and nothing else) with the given status.</summary>
        public static void ErrorBody(IActionResult? result, int status, string expected)
        {
            var obj = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(status, obj.StatusCode);
            Assert.Equal(
                "{\"error\":" + JsonSerializer.Serialize(expected) + "}",
                JsonSerializer.Serialize(obj.Value));
        }

        /// <summary>Asserts a ProblemDetails body with the given status, title and detail.</summary>
        public static ProblemDetails Problem(IActionResult? result, int status, string title, string? detail)
        {
            var obj = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(status, obj.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(obj.Value);
            Assert.Equal(status, problem.Status);
            Assert.Equal(title, problem.Title);
            Assert.Equal(detail, problem.Detail);
            return problem;
        }

        /// <summary>Asserts a 503 ProblemDetails carrying exactly one <c>retryAfterSeconds</c> extension.</summary>
        public static void Unavailable(IActionResult? result, string title, string detail, int retryAfterSeconds)
        {
            var problem = Problem(result, 503, title, detail);
            var extension = Assert.Single(problem.Extensions);
            Assert.Equal("retryAfterSeconds", extension.Key);
            Assert.Equal(retryAfterSeconds, Assert.IsType<int>(extension.Value));
        }

        /// <summary>Asserts the 503 the geocoding controllers return when the ArcGIS breaker is open.</summary>
        public static void GeocodingUnavailable(IActionResult? result) =>
            Unavailable(result, "Geocoding Unavailable",
                "The upstream geocoding service is temporarily unavailable. Retry after 15 seconds.", 15);
    }

    public class SpatialClusterControllerTests
    {
        private readonly Mock<ISpatialClusterService> _service = new();
        private readonly SpatialClusterController _controller;

        public SpatialClusterControllerTests()
        {
            _controller = new SpatialClusterController(_service.Object);
        }

        [Fact]
        public async Task RunDbscan_ValidRequest_ReturnsOkWithResult()
        {
            // Arrange
            var expected = new DbscanResultDto { ClusterCount = 2, NoiseCount = 1 };
            _service.Setup(s => s.RunDbscanAsync(It.IsAny<DbscanRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await _controller.RunDbscan(new DbscanRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(expected, ok.Value);
        }

        [Fact]
        public async Task RunDbscan_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _service.Setup(s => s.RunDbscanAsync(It.IsAny<DbscanRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad epsilon."));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _controller.RunDbscan(new DbscanRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task RunDbscan_RealServiceWithNoPoints_MessageCarriesParameterSuffix()
        {
            // Arrange
            var controller = new SpatialClusterController(
                new SpatialClusterService(NullLogger<SpatialClusterService>.Instance));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => controller.RunDbscan(new DbscanRequestDto(), CancellationToken.None));

            // Assert: this is the text the filter puts in the 400 body.
            Assert.Equal("At least one point is required. (Parameter 'request')", ex.Message);
        }

        [Fact]
        public async Task RunDbscan_ServiceThrowsOtherException_Propagates()
        {
            // Arrange
            _service.Setup(s => s.RunDbscanAsync(It.IsAny<DbscanRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _controller.RunDbscan(new DbscanRequestDto(), CancellationToken.None));
        }
    }
}
