using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class SpatialComputeControllerTests
    {
        [Fact]
        public async Task GeoStreamProcessEvents_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IGeoStreamProcessorService>();
            service.Setup(s => s.ProcessBatchAsync(It.IsAny<GeoStreamBatchRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeoStreamBatchResultDto { TotalEvents = 1 });
            var controller = new GeoStreamController(service.Object, NullLogger<GeoStreamController>.Instance);

            // Act
            var result = await controller.ProcessEvents(new GeoStreamBatchRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<GeoStreamBatchResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.TriangulateAsync(It.IsAny<GeometryPointSetDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid geometry."));
            var controller = new SpatialGeometryController(service.Object, NullLogger<SpatialGeometryController>.Instance);

            // Act
            var result = await controller.Triangulate(new GeometryPointSetDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task RasterTerrainHillshade_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHillshadeAsync(It.IsAny<RasterHillshadeRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RasterHillshadeResultDto { Width = 1, Height = 1 });
            var controller = new RasterTerrainController(service.Object, NullLogger<RasterTerrainController>.Instance);

            // Act
            var result = await controller.Hillshade(new RasterHillshadeRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<RasterHillshadeResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialNetworkRoute_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.FindShortestPathAsync(It.IsAny<RouteRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RouteResultDto { Found = true });
            var controller = new SpatialNetworkController(service.Object, NullLogger<SpatialNetworkController>.Instance);

            // Act
            var result = await controller.Route(new RouteRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<RouteResultDto>(ok.Value);
        }

        [Fact]
        public async Task GeoStreamProcessEvents_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IGeoStreamProcessorService>();
            service.Setup(s => s.ProcessBatchAsync(It.IsAny<GeoStreamBatchRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid telemetry."));
            var controller = new GeoStreamController(service.Object, NullLogger<GeoStreamController>.Instance);

            // Act
            var result = await controller.ProcessEvents(new GeoStreamBatchRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task SpatialGeometryClip_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.ClipToBoundingBoxAsync(It.IsAny<PolygonClipRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PolygonOperationResultDto());
            var controller = new SpatialGeometryController(service.Object, NullLogger<SpatialGeometryController>.Instance);

            // Act
            var result = await controller.Clip(new PolygonClipRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<PolygonOperationResultDto>(ok.Value);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHeatmapAsync(It.IsAny<HeatmapRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid heatmap."));
            var controller = new RasterTerrainController(service.Object, NullLogger<RasterTerrainController>.Instance);

            // Act
            var result = await controller.Heatmap(new HeatmapRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task SpatialNetworkServiceArea_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.ComputeServiceAreaAsync(It.IsAny<ServiceAreaRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceAreaResultDto());
            var controller = new SpatialNetworkController(service.Object, NullLogger<SpatialNetworkController>.Instance);

            // Act
            var result = await controller.ServiceArea(new ServiceAreaRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<ServiceAreaResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialNetworkRoute_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.FindShortestPathAsync(It.IsAny<RouteRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid graph."));
            var controller = new SpatialNetworkController(service.Object, NullLogger<SpatialNetworkController>.Instance);

            // Act
            var result = await controller.Route(new RouteRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task SpatialNetworkServiceArea_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<ISpatialGraphService>();
            service.Setup(s => s.ComputeServiceAreaAsync(It.IsAny<ServiceAreaRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid service area."));
            var controller = new SpatialNetworkController(service.Object, NullLogger<SpatialNetworkController>.Instance);

            // Act
            var result = await controller.ServiceArea(new ServiceAreaRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task RasterTerrainHillshade_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHillshadeAsync(It.IsAny<RasterHillshadeRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid raster."));
            var controller = new RasterTerrainController(service.Object, NullLogger<RasterTerrainController>.Instance);

            // Act
            var result = await controller.Hillshade(new RasterHillshadeRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task RasterTerrainHeatmap_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<IRasterTerrainService>();
            service.Setup(s => s.GenerateHeatmapAsync(It.IsAny<HeatmapRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HeatmapResultDto { Width = 2, Height = 2 });
            var controller = new RasterTerrainController(service.Object, NullLogger<RasterTerrainController>.Instance);

            // Act
            var result = await controller.Heatmap(new HeatmapRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<HeatmapResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.TriangulateAsync(It.IsAny<GeometryPointSetDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TriangulationResultDto { Triangles = [] });
            var controller = new SpatialGeometryController(service.Object, NullLogger<SpatialGeometryController>.Instance);

            // Act
            var result = await controller.Triangulate(new GeometryPointSetDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<TriangulationResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialGeometryClip_ServiceArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.ClipToBoundingBoxAsync(It.IsAny<PolygonClipRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid bounding box."));
            var controller = new SpatialGeometryController(service.Object, NullLogger<SpatialGeometryController>.Instance);

            // Act
            var result = await controller.Clip(new PolygonClipRequestDto(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GeoStreamProcessEvents_ReturnsNativeAcceleratedFlag()
        {
            // Arrange — service reports native acceleration used
            var service = new Mock<IGeoStreamProcessorService>();
            service.Setup(s => s.ProcessBatchAsync(It.IsAny<GeoStreamBatchRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeoStreamBatchResultDto { TotalEvents = 5, NativeAccelerated = true });
            var controller = new GeoStreamController(service.Object, NullLogger<GeoStreamController>.Instance);

            // Act
            var result = await controller.ProcessEvents(new GeoStreamBatchRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<GeoStreamBatchResultDto>(ok.Value);
            Assert.True(dto.NativeAccelerated);
        }
    }
}
