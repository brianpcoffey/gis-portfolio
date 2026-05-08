using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class RasterTerrainNativeBridge
    {
        private static readonly bool _available;

        static RasterTerrainNativeBridge()
        {
            try
            {
                _available = NativeLibrary.TryLoad(
                    "raster_terrain_kernel",
                    typeof(RasterTerrainNativeBridge).Assembly,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory,
                    out _);
            }
            catch
            {
                _available = false;
            }
        }

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native raster terrain kernel loaded. Raster operations will use the C++ fast path."
                : "Native raster terrain kernel unavailable; using managed raster implementation.");
        }

        internal static bool TryGenerateHillshade(
            RasterHillshadeRequestDto request,
            ILogger logger,
            out RasterHillshadeResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = GenerateHillshade(request);
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native raster hillshade generation failed; falling back to managed raster implementation.");
                return false;
            }
        }

        internal static bool TryGenerateHeatmap(
            HeatmapRequestDto request,
            ILogger logger,
            out HeatmapResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = GenerateHeatmap(request);
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native raster heatmap generation failed; falling back to managed raster implementation.");
                return false;
            }
        }

        internal static RasterHillshadeResultDto GenerateHillshade(RasterHillshadeRequestDto request)
        {
            var output = new byte[request.Width * request.Height];
            var status = RasterTerrainNativeInterop.GenerateHillshade(
                request.Elevation.ToArray(),
                request.Width,
                request.Height,
                request.CellSize,
                request.AzimuthDegrees,
                request.AltitudeDegrees,
                output,
                output.Length);
            ThrowIfFailed(status);

            return new RasterHillshadeResultDto
            {
                Width = request.Width,
                Height = request.Height,
                NativeAccelerated = true,
                Intensities = output.ToList()
            };
        }

        internal static HeatmapResultDto GenerateHeatmap(HeatmapRequestDto request)
        {
            var points = request.Points.Select(p => new WeightedPointNative
            {
                X = p.X,
                Y = p.Y,
                Weight = p.Weight
            }).ToArray();
            var extent = new RasterExtentNative
            {
                MinX = request.MinX,
                MinY = request.MinY,
                MaxX = request.MaxX,
                MaxY = request.MaxY
            };
            var output = new double[request.Width * request.Height];
            var status = RasterTerrainNativeInterop.GenerateHeatmap(points, points.Length, in extent, request.Width, request.Height, request.Radius, output, output.Length);
            ThrowIfFailed(status);

            return new HeatmapResultDto
            {
                Width = request.Width,
                Height = request.Height,
                NativeAccelerated = true,
                Values = output.ToList()
            };
        }

        private static void ThrowIfFailed(int status)
        {
            if (status != 0)
                throw new InvalidOperationException($"Native raster terrain kernel failed with status {status}.");
        }

        private static bool IsNativeInvocationException(Exception exception)
        {
            return exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or SEHException
                or InvalidOperationException;
        }
    }
}
