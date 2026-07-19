using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class FleetRoutingControllerTests
    {
        private static FleetRoutingController NewController(Mock<IFleetRoutingService> service) =>
            new(service.Object, NullLogger<FleetRoutingController>.Instance);

        [Fact]
        public async Task FleetScenario_ValidPreset_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IFleetRoutingService>();
            service.Setup(s => s.GetScenarioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FleetScenarioDto { Preset = "fullday", VehicleCount = 5 });
            var controller = NewController(service);

            // Act
            var result = await controller.Scenario("fullday", CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var scenario = Assert.IsType<FleetScenarioDto>(ok.Value);
            Assert.Equal("fullday", scenario.Preset);
        }

        [Fact]
        public async Task FleetScenario_UnknownPreset_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IFleetRoutingService>();
            service.Setup(s => s.GetScenarioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Unknown scenario preset."));
            var controller = NewController(service);

            // Act
            var result = await controller.Scenario("nope", CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task FleetOptimize_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IFleetRoutingService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OptimizeResultDto { Feasible = true, VehiclesUsed = 3 });
            var controller = NewController(service);

            // Act
            var result = await controller.Optimize(new OptimizeRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var optimize = Assert.IsType<OptimizeResultDto>(ok.Value);
            Assert.Equal(3, optimize.VehiclesUsed);
        }

        [Fact]
        public async Task FleetOptimize_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IFleetRoutingService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("At least one stop is required."));
            var controller = NewController(service);

            // Act
            var result = await controller.Optimize(new OptimizeRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
