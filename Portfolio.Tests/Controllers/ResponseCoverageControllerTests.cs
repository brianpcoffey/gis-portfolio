using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class ResponseCoverageControllerTests
    {
        [Fact]
        public async Task ResponseGetScenario_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.GetScenarioAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseScenarioDto { Name = "Redlands", TotalCallVolume = 100 });
            var controller = new ResponseCoverageController(service.Object, NullLogger<ResponseCoverageController>.Instance);

            // Act
            var result = await controller.GetScenario(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<ResponseScenarioDto>(ok.Value);
        }

        [Fact]
        public async Task ResponseIsochrone_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.ComputeIsochroneAsync(It.IsAny<IsochroneRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IsochroneResultDto { OriginNodeId = 1, ReachableNodes = 5 });
            var controller = new ResponseCoverageController(service.Object, NullLogger<ResponseCoverageController>.Instance);

            // Act
            var result = await controller.Isochrone(new IsochroneRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<IsochroneResultDto>(ok.Value);
        }

        [Fact]
        public async Task ResponseIsochrone_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.ComputeIsochroneAsync(It.IsAny<IsochroneRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Isochrone bands must be ascending positive values."));
            var controller = new ResponseCoverageController(service.Object, NullLogger<ResponseCoverageController>.Instance);

            // Act
            var result = await controller.Isochrone(new IsochroneRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task ResponseOptimize_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeCoverageRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OptimizeCoverageResultDto { ChosenCandidateIds = [1, 2], MeetsNfpa1710 = true });
            var controller = new ResponseCoverageController(service.Object, NullLogger<ResponseCoverageController>.Instance);

            // Act
            var result = await controller.Optimize(new OptimizeCoverageRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<OptimizeCoverageResultDto>(ok.Value);
        }

        [Fact]
        public async Task ResponseOptimize_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeCoverageRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Station count cannot exceed the number of candidate sites."));
            var controller = new ResponseCoverageController(service.Object, NullLogger<ResponseCoverageController>.Instance);

            // Act
            var result = await controller.Optimize(new OptimizeCoverageRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
