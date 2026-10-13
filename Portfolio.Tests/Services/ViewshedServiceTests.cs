using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class ViewshedServiceTests
    {
        [Fact]
        public async Task Viewshed_FlatTerrain_AllCellsVisible()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 4,
                Height = 4,
                CellSize = 30,
                ObserverX = 0,
                ObserverY = 0,
                ObserverHeight = 2,
                Elevation = Enumerable.Repeat(100.0, 16).ToList()
            };

            var result = await service.ComputeAsync(request);

            Assert.Equal(16, result.VisibleCells);
            Assert.All(result.Visibility, v => Assert.Equal(1, v));
        }

        [Fact]
        public async Task Viewshed_PeakBlocksCellsBehindIt()
        {
            // 1x5 row, observer at x=0, a tall peak at x=1 blocks x=2,3,4.
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 5,
                Height = 1,
                CellSize = 1,
                ObserverX = 0,
                ObserverY = 0,
                ObserverHeight = 0,
                Elevation = [0, 10, 0, 0, 0]
            };

            var result = await service.ComputeAsync(request);

            Assert.Equal(1, result.Visibility[0]); // observer
            Assert.Equal(1, result.Visibility[1]); // the peak itself
            Assert.Equal(0, result.Visibility[2]); // behind the peak
            Assert.Equal(0, result.Visibility[3]);
            Assert.Equal(0, result.Visibility[4]);
            Assert.Equal(2, result.VisibleCells);
        }

        [Fact]
        public async Task Viewshed_ObserverCellAlwaysVisible()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 3,
                Height = 3,
                CellSize = 30,
                ObserverX = 1,
                ObserverY = 1,
                ObserverHeight = 0,
                Elevation = [0, 0, 0, 0, 0, 0, 0, 0, 0]
            };

            var result = await service.ComputeAsync(request);

            Assert.Equal(1, result.Visibility[1 * 3 + 1]);
        }

        [Fact]
        public async Task Viewshed_NullRequest_ThrowsArgumentNullException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ComputeAsync(null!));
        }

        [Fact]
        public async Task Viewshed_ObserverOutOfBounds_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 2,
                Height = 2,
                CellSize = 30,
                ObserverX = 5,
                ObserverY = 0,
                Elevation = [1, 2, 3, 4]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        [Fact]
        public async Task Viewshed_ElevationLengthMismatch_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 2,
                Height = 2,
                CellSize = 30,
                ObserverX = 0,
                ObserverY = 0,
                Elevation = [1, 2, 3]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        [Fact]
        public async Task Viewshed_ZeroCellSize_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = new ViewshedRequestDto
            {
                Width = 2,
                Height = 2,
                CellSize = 0,
                ObserverX = 0,
                ObserverY = 0,
                Elevation = [1, 2, 3, 4]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        private static ViewshedRequestDto FlatViewshed(int width, int height, int observerX = 0, int observerY = 0) => new()
        {
            Width = width,
            Height = height,
            CellSize = 30,
            ObserverX = observerX,
            ObserverY = observerY,
            Elevation = Enumerable.Repeat(0.0, width * height).ToList()
        };

        [Theory]
        [InlineData(513, 1)]
        [InlineData(1, 513)]
        [InlineData(250000, 1)]
        public async Task Viewshed_SideOverCap_ThrowsArgumentException(int width, int height)
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(FlatViewshed(width, height)));
            Assert.StartsWith("Viewshed width and height are each limited to 512 cells.", ex.Message);
        }

        [Fact]
        public async Task Viewshed_WidthOf250000_IsRejectedQuickly()
        {
            // findings-security 5.2: 237 s of CPU before the side cap.
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = FlatViewshed(250000, 1);
            var timer = System.Diagnostics.Stopwatch.StartNew();

            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));

            Assert.True(timer.ElapsedMilliseconds < 5000, $"took {timer.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData(512, 488)]
        [InlineData(40, 40)]
        [InlineData(512, 1)]
        public async Task Viewshed_AtOrBelowBoundary_Succeeds(int width, int height)
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);

            var result = await service.ComputeAsync(FlatViewshed(width, height, width / 2, height / 2));

            Assert.Equal(width * height, result.TotalCells);
            Assert.Equal(width * height, result.VisibleCells);
        }

        [Fact]
        public async Task Viewshed_CellCountMessageStillWinsOverSideCap()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = FlatViewshed(1, 1);
            request.Width = 300000;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
            Assert.Equal("Viewshed operations are limited to 250000 cells.", ex.Message);
        }

        [Fact]
        public async Task Viewshed_NonFiniteElevation_ThrowsArgumentException()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            var request = FlatViewshed(3, 3, 1, 1);
            request.Elevation[8] = double.NaN;

            await Assert.ThrowsAsync<ArgumentException>(() => service.ComputeAsync(request));
        }

        [Fact]
        public async Task Viewshed_CancelledToken_ThrowsOperationCanceled()
        {
            var service = new ViewshedService(NullLogger<ViewshedService>.Instance);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ComputeAsync(FlatViewshed(64, 64, 32, 32), cts.Token));
        }
    }
}
