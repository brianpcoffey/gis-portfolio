using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class FleetRoutingControllerTests
    {
        private static FleetRoutingController NewController(Mock<IFleetRoutingService> service) =>
            new(service.Object);

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
        public async Task FleetScenario_UnknownPreset_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IFleetRoutingService>();
            service.Setup(s => s.GetScenarioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Unknown preset.", "presetName"));
            var controller = NewController(service);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => controller.Scenario("nope", CancellationToken.None));

            // Assert: the exception propagates from the action unchanged. The filter shows its message only
            // when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET, so it gets the generic text.
            Assert.Equal("Unknown preset. (Parameter 'presetName')", ex.Message);
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
        public async Task FleetOptimize_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<IFleetRoutingService>();
            service.Setup(s => s.OptimizeAsync(It.IsAny<OptimizeRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Bad fleet.", "request"));
            var controller = NewController(service);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => controller.Optimize(new OptimizeRequestDto(), CancellationToken.None));

            // Assert: the exception propagates from the action unchanged. The filter shows its message only
            // when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET, so it gets the generic text.
            Assert.Equal("Bad fleet. (Parameter 'request')", ex.Message);
        }
    }
}
