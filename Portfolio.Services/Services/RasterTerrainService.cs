using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class RasterTerrainService : IRasterTerrainService
    {
        private const int MaxRasterCells = 250000;
        private const int MaxHeatmapPoints = 10000;
        private readonly ILogger<RasterTerrainService> _logger;

        public RasterTerrainService(ILogger<RasterTerrainService> logger)
        {
            _logger = logger;
            RasterTerrainNativeBridge.LogAvailability(_logger);
        }

        // Generates an 8-bit hillshade intensity grid from an elevation raster.
        public Task<RasterHillshadeResultDto> GenerateHillshadeAsync(
            RasterHillshadeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Elevation is null)
                throw new ArgumentException("Elevation values are required.", nameof(request));
            ValidateRasterShape(request.Width, request.Height, request.Elevation.Count);
            if (request.CellSize <= 0 || double.IsNaN(request.CellSize) || double.IsInfinity(request.CellSize))
                throw new ArgumentException("Cell size must be a finite value greater than zero.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            if (RasterTerrainNativeBridge.TryGenerateHillshade(request, _logger, out var nativeResult))
                return Task.FromResult(nativeResult!);

            return Task.FromResult(GenerateHillshadeManaged(request, cancellationToken));
        }

        // Generates a normalized heatmap over the requested raster extent.
        public Task<HeatmapResultDto> GenerateHeatmapAsync(
            HeatmapRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateRasterShape(request.Width, request.Height, request.Width * request.Height);
            if (request.Points is null)
                throw new ArgumentException("Heatmap points are required.", nameof(request));
            if (request.Points.Count > MaxHeatmapPoints)
                throw new ArgumentException($"Heatmaps are limited to {MaxHeatmapPoints} weighted points.", nameof(request));
            if (request.MinX >= request.MaxX || request.MinY >= request.MaxY)
                throw new ArgumentException("Extent minimums must be less than maximums.", nameof(request));
            if (request.Radius <= 0 || double.IsNaN(request.Radius) || double.IsInfinity(request.Radius))
                throw new ArgumentException("Heatmap radius must be a finite value greater than zero.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            if (RasterTerrainNativeBridge.TryGenerateHeatmap(request, _logger, out var nativeResult))
                return Task.FromResult(NormalizeHeatmap(nativeResult!));

            return Task.FromResult(NormalizeHeatmap(GenerateHeatmapManaged(request, cancellationToken)));
        }

        private static RasterHillshadeResultDto GenerateHillshadeManaged(RasterHillshadeRequestDto request, CancellationToken cancellationToken)
        {
            var output = new byte[request.Width * request.Height];
            var azimuth = DegreesToRadians(360.0 - request.AzimuthDegrees + 90.0);
            var altitude = DegreesToRadians(request.AltitudeDegrees);

            for (var y = 0; y < request.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < request.Width; x++)
                {
                    var dzdx = Sample(request, x + 1, y) - Sample(request, x - 1, y);
                    var dzdy = Sample(request, x, y + 1) - Sample(request, x, y - 1);
                    var slope = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy) / (2.0 * request.CellSize));
                    var aspect = Math.Atan2(dzdy, -dzdx);
                    var shaded = Math.Sin(altitude) * Math.Cos(slope)
                        + Math.Cos(altitude) * Math.Sin(slope) * Math.Cos(azimuth - aspect);
                    output[y * request.Width + x] = (byte)Math.Clamp(Math.Round(255.0 * Math.Max(0, shaded)), 0, 255);
                }
            }

            return new RasterHillshadeResultDto
            {
                Width = request.Width,
                Height = request.Height,
                NativeAccelerated = false,
                Intensities = output.ToList()
            };
        }

        private static HeatmapResultDto GenerateHeatmapManaged(HeatmapRequestDto request, CancellationToken cancellationToken)
        {
            var values = new double[request.Width * request.Height];
            var max = 0.0;
            var xStep = (request.MaxX - request.MinX) / request.Width;
            var yStep = (request.MaxY - request.MinY) / request.Height;

            for (var y = 0; y < request.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var py = request.MinY + (y + 0.5) * yStep;
                for (var x = 0; x < request.Width; x++)
                {
                    var px = request.MinX + (x + 0.5) * xStep;
                    var value = 0.0;
                    foreach (var point in request.Points)
                    {
                        var dx = px - point.X;
                        var dy = py - point.Y;
                        var distanceSquared = dx * dx + dy * dy;
                        value += point.Weight * Math.Exp(-distanceSquared / (2.0 * request.Radius * request.Radius));
                    }

                    values[y * request.Width + x] = value;
                    max = Math.Max(max, value);
                }
            }

            if (max > 0)
            {
                for (var i = 0; i < values.Length; i++)
                    values[i] /= max;
            }

            return new HeatmapResultDto
            {
                Width = request.Width,
                Height = request.Height,
                NativeAccelerated = false,
                Values = values.ToList()
            };
        }

        private static HeatmapResultDto NormalizeHeatmap(HeatmapResultDto result)
        {
            result.Values = result.Values.Select(value => Math.Round(value, 4)).ToList();
            return result;
        }

        private static void ValidateRasterShape(int width, int height, int valueCount)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Raster dimensions must be greater than zero.");
            if (width > MaxRasterCells / height)
                throw new ArgumentException($"Raster operations are limited to {MaxRasterCells} cells.");
            if (valueCount != width * height)
                throw new ArgumentException("Elevation value count must equal width multiplied by height.");
        }

        private static double Sample(RasterHillshadeRequestDto request, int x, int y)
        {
            var clampedX = Math.Clamp(x, 0, request.Width - 1);
            var clampedY = Math.Clamp(y, 0, request.Height - 1);
            return request.Elevation[clampedY * request.Width + clampedX];
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
