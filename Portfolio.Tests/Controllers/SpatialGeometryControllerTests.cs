using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;

namespace Portfolio.Tests.Controllers
{
    public class SpatialGeometryControllerTests
    {
        [Fact]
        public async Task SpatialGeometryTriangulate_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.TriangulateAsync(It.IsAny<GeometryPointSetDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid geometry."));
            var controller = new SpatialGeometryController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Triangulate(new GeometryPointSetDto(), CancellationToken.None));
        }

        [Fact]
        public async Task SpatialGeometryClip_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.ClipToBoundingBoxAsync(It.IsAny<PolygonClipRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PolygonOperationResultDto());
            var controller = new SpatialGeometryController(service.Object);

            // Act
            var result = await controller.Clip(new PolygonClipRequestDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<PolygonOperationResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialGeometryTriangulate_ValidRequest_ReturnsOk()
        {
            // Arrange
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.TriangulateAsync(It.IsAny<GeometryPointSetDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TriangulationResultDto { Triangles = [] });
            var controller = new SpatialGeometryController(service.Object);

            // Act
            var result = await controller.Triangulate(new GeometryPointSetDto(), CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<TriangulationResultDto>(ok.Value);
        }

        [Fact]
        public async Task SpatialGeometryClip_ServiceArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            var service = new Mock<ISpatialGeometryService>();
            service.Setup(s => s.ClipToBoundingBoxAsync(It.IsAny<PolygonClipRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid bounding box."));
            var controller = new SpatialGeometryController(service.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => controller.Clip(new PolygonClipRequestDto(), CancellationToken.None));
        }
    }
}
