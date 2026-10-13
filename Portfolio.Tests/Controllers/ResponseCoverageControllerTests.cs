using Microsoft.AspNetCore.Mvc;
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
            var controller = new ResponseCoverageController(service.Object);

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
            var controller = new ResponseCoverageController(service.Object);

            // Act
            var result = await controller.Isochrone(new IsochroneRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<IsochroneResultDto>(ok.Value);
        }

        [Fact]
        public async Task ResponseIsochrone_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.ComputeIsochroneAsync(It.IsAny<IsochroneRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad isochrone.", "request"));
            var controller = new ResponseCoverageController(service.Object);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => controller.Isochrone(new IsochroneRequestDto(), CancellationToken.None));

            // Assert: the exception propagates from the action unchanged. The filter shows its message only
            // when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET, so it gets the generic text.
            Assert.Equal("Bad isochrone. (Parameter 'request')", ex.Message);
        }

        [Fact]
        public async Task ResponseOptimize_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeCoverageRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OptimizeCoverageResultDto { ChosenCandidateIds = [1, 2], MeetsNfpa1710 = true });
            var controller = new ResponseCoverageController(service.Object);

            // Act
            var result = await controller.Optimize(new OptimizeCoverageRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<OptimizeCoverageResultDto>(ok.Value);
        }

        [Fact]
        public async Task ResponseOptimize_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IResponseCoverageService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeCoverageRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad coverage.", "request"));
            var controller = new ResponseCoverageController(service.Object);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => controller.Optimize(new OptimizeCoverageRequestDto(), CancellationToken.None));

            // Assert: the exception propagates from the action unchanged. The filter shows its message only
            // when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET, so it gets the generic text.
            Assert.Equal("Bad coverage. (Parameter 'request')", ex.Message);
        }
    }
}
