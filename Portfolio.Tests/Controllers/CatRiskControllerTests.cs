using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class CatRiskControllerTests
    {
        [Fact]
        public async Task CatRiskGetBook_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ICatRiskService>();
            service.Setup(s => s.GetPolicyBookAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PolicyBookDto { BookName = "Test Book", LocationCount = 3 });
            var controller = new CatRiskController(service.Object);

            // Act
            var result = await controller.GetBook(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<PolicyBookDto>(ok.Value);
            Assert.Equal(3, dto.LocationCount);
        }

        [Fact]
        public async Task CatRiskAccumulation_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ICatRiskService>();
            service.Setup(s => s.ComputeAccumulationAsync(It.IsAny<AccumulationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccumulationResultDto { BreachCount = 2, RadiusKm = 5 });
            var controller = new CatRiskController(service.Object);

            // Act
            var result = await controller.Accumulation(new AccumulationRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AccumulationResultDto>(ok.Value);
            Assert.Equal(2, dto.BreachCount);
        }

        [Fact]
        public async Task CatRiskAccumulation_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<ICatRiskService>();
            service.Setup(s => s.ComputeAccumulationAsync(It.IsAny<AccumulationRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Ring radius must be between 0 and 200 km."));
            var controller = new CatRiskController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Accumulation(new AccumulationRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task CatRiskSimulate_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ICatRiskService>();
            service.Setup(s => s.SimulateAsync(It.IsAny<SimulationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SimulationResultDto { AverageAnnualLoss = 1234, ProbableMaximumLoss = 98765 });
            var controller = new CatRiskController(service.Object);

            // Act
            var result = await controller.Simulate(new SimulationRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SimulationResultDto>(ok.Value);
            Assert.Equal(1234, dto.AverageAnnualLoss);
        }

        [Fact]
        public async Task CatRiskSimulate_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<ICatRiskService>();
            service.Setup(s => s.SimulateAsync(It.IsAny<SimulationRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("At least one catastrophe event is required."));
            var controller = new CatRiskController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Simulate(new SimulationRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task CatRiskSimulate_ReturnsNativeAcceleratedFlag()
        {
            // Arrange — service reports native acceleration used
            var service = new Mock<ICatRiskService>();
            service.Setup(s => s.SimulateAsync(It.IsAny<SimulationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SimulationResultDto { NativeAccelerated = true });
            var controller = new CatRiskController(service.Object);

            // Act
            var result = await controller.Simulate(new SimulationRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SimulationResultDto>(ok.Value);
            Assert.True(dto.NativeAccelerated);
        }
    }
}
