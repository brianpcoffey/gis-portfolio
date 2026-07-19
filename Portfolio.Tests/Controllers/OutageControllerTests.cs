using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class OutageControllerTests
    {
        private static OutageController NewController(Mock<IOutageTraceService> service) =>
            new(service.Object, NullLogger<OutageController>.Instance);

        [Fact]
        public async Task OutageGetNetwork_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IOutageTraceService>();
            service.Setup(s => s.GetNetworkAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DistributionNetworkDto { NetworkName = "Test", TotalCustomers = 100 });
            var controller = NewController(service);

            // Act
            var result = await controller.GetNetwork(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var network = Assert.IsType<DistributionNetworkDto>(ok.Value);
            Assert.Equal(100, network.TotalCustomers);
        }

        [Fact]
        public async Task OutageTrace_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IOutageTraceService>();
            service.Setup(s => s.TraceAsync(It.IsAny<TraceRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TraceResultDto { CustomersAffected = 42 });
            var controller = NewController(service);

            // Act
            var result = await controller.Trace(new TraceRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var trace = Assert.IsType<TraceResultDto>(ok.Value);
            Assert.Equal(42, trace.CustomersAffected);
        }

        [Fact]
        public async Task OutageTrace_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IOutageTraceService>();
            service.Setup(s => s.TraceAsync(It.IsAny<TraceRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("The faulted element was not found in the network."));
            var controller = NewController(service);

            // Act
            var result = await controller.Trace(new TraceRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task OutageRestore_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IOutageTraceService>();
            service.Setup(s => s.ProposeRestorationAsync(It.IsAny<RestoreRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestoreResultDto { RestorationFound = true, CustomersRestored = 7 });
            var controller = NewController(service);

            // Act
            var result = await controller.Restore(new RestoreRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var restore = Assert.IsType<RestoreResultDto>(ok.Value);
            Assert.True(restore.RestorationFound);
            Assert.Equal(7, restore.CustomersRestored);
        }

        [Fact]
        public async Task OutageRestore_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IOutageTraceService>();
            service.Setup(s => s.ProposeRestorationAsync(It.IsAny<RestoreRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Networks are limited to 5000 elements."));
            var controller = NewController(service);

            // Act
            var result = await controller.Restore(new RestoreRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
