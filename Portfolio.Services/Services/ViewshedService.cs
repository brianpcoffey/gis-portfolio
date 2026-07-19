using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class ViewshedService : IViewshedService
    {
        private const int MaxRasterCells = 250000;
        private readonly ILogger<ViewshedService> _logger;

        public ViewshedService(ILogger<ViewshedService> logger)
        {
            _logger = logger;
            ViewshedNativeBridge.LogAvailability(_logger);
        }

        // Computes a line-of-sight viewshed, preferring the native kernel and falling back to managed code.
        public Task<ViewshedResultDto> ComputeAsync(
            ViewshedRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Elevation is null)
                throw new ArgumentException("Elevation values are required.", nameof(request));
            ValidateRasterShape(request.Width, request.Height, request.Elevation.Count);
            if (request.CellSize <= 0 || double.IsNaN(request.CellSize) || double.IsInfinity(request.CellSize))
                throw new ArgumentException("Cell size must be a finite value greater than zero.", nameof(request));
            if (request.ObserverX < 0 || request.ObserverX >= request.Width ||
                request.ObserverY < 0 || request.ObserverY >= request.Height)
                throw new ArgumentException("Observer position must fall inside the grid.", nameof(request));
            if (double.IsNaN(request.ObserverHeight) || double.IsInfinity(request.ObserverHeight))
                throw new ArgumentException("Observer height must be a finite value.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            if (ViewshedNativeBridge.TryCompute(request, _logger, out var nativeResult))
                return Task.FromResult(nativeResult!);

            return Task.FromResult(ComputeManaged(request, cancellationToken));
        }

        private static ViewshedResultDto ComputeManaged(ViewshedRequestDto request, CancellationToken cancellationToken)
        {
            var width = request.Width;
            var height = request.Height;
            var elevation = request.Elevation;
            var visibility = new byte[width * height];
            var observerElevation = elevation[request.ObserverY * width + request.ObserverX] + request.ObserverHeight;
            var visibleCount = 0;

            for (var ty = 0; ty < height; ty++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var tx = 0; tx < width; tx++)
                {
                    if (tx == request.ObserverX && ty == request.ObserverY)
                    {
                        visibility[ty * width + tx] = 1;
                        visibleCount++;
                        continue;
                    }

                    var dx = (double)(tx - request.ObserverX);
                    var dy = (double)(ty - request.ObserverY);
                    var steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dy));
                    var targetDistance = Math.Sqrt(dx * dx + dy * dy) * request.CellSize;
                    var targetAngle = (elevation[ty * width + tx] - observerElevation) / targetDistance;

                    var maxAngle = double.NegativeInfinity;
                    var blocked = false;
                    for (var i = 1; i < steps; i++)
                    {
                        var fraction = (double)i / steps;
                        // Half-up, stated explicitly. Math.Round rounds half to even and the
                        // native kernel's std::lround rounds half away from zero, so relying
                        // on either default made the two paths walk a different cell whenever
                        // a sample landed exactly on .5. Sample coordinates are always
                        // non-negative here, so Floor(v + 0.5) is unambiguous.
                        var cx = (int)Math.Floor(request.ObserverX + dx * fraction + 0.5);
                        var cy = (int)Math.Floor(request.ObserverY + dy * fraction + 0.5);
                        var distance = fraction * targetDistance;
                        if (distance <= 0)
                            continue;

                        var angle = (elevation[cy * width + cx] - observerElevation) / distance;
                        if (angle > maxAngle)
                            maxAngle = angle;
                        if (targetAngle < maxAngle)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    var visible = (byte)(blocked ? 0 : 1);
                    visibility[ty * width + tx] = visible;
                    visibleCount += visible;
                }
            }

            return new ViewshedResultDto
            {
                Width = width,
                Height = height,
                ObserverX = request.ObserverX,
                ObserverY = request.ObserverY,
                VisibleCells = visibleCount,
                TotalCells = width * height,
                NativeAccelerated = false,
                Visibility = visibility.ToList()
            };
        }

        private static void ValidateRasterShape(int width, int height, int valueCount)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Raster dimensions must be greater than zero.");
            if (width > MaxRasterCells / height)
                throw new ArgumentException($"Viewshed operations are limited to {MaxRasterCells} cells.");
            if (valueCount != width * height)
                throw new ArgumentException("Elevation value count must equal width multiplied by height.");
        }
    }
}
