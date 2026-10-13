using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class SpatialNetworkControllerTests
    {
        [Fact]
        public async Task SpatialNetworkRoute_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.FindShortestPathAsync(It.IsAny<RouteRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RouteResultDto { Found = true });
            var controller = new SpatialNetworkController(service.Object);

            // Act
            var result = await controller.Route(new RouteRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<RouteResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialNetworkServiceArea_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.ComputeServiceAreaAsync(It.IsAny<ServiceAreaRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceAreaResultDto());
            var controller = new SpatialNetworkController(service.Object);

            // Act
            var result = await controller.ServiceArea(new ServiceAreaRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<ServiceAreaResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialNetworkRoute_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.FindShortestPathAsync(It.IsAny<RouteRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid graph."));
            var controller = new SpatialNetworkController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Route(new RouteRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task SpatialNetworkServiceArea_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.ComputeServiceAreaAsync(It.IsAny<ServiceAreaRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid service area."));
            var controller = new SpatialNetworkController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.ServiceArea(new ServiceAreaRequestDto(), CancellationToken.None));
        }

        [Fact]
        public async Task SpatialNetworkController_GetGraph_ReturnsOkWithRedlandsGraph()
        {
            // Arrange — use real service so the static cache path is exercised.
            var service = new SpatialGraphService(NullLogger<SpatialGraphService>.Instance);
            var controller = new Portfolio.Web.Controllers.Api.SpatialNetworkController(service);

            // Act
            var result = await controller.GetGraph(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var graph = Assert.IsType<RoadGraphDto>(ok.Value);
            Assert.True(graph.Nodes.Count >= 100, $"Expected a dense network but got {graph.Nodes.Count} nodes.");
            Assert.Contains(graph.Nodes, n => n.Id == graph.DestinationNodeId);
        }
    }
}
