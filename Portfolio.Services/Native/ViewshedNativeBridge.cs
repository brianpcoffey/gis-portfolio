using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class ViewshedNativeBridge
    {
        private static readonly bool _available;

        static ViewshedNativeBridge()
        {
            try
            {
                _available = NativeLibrary.TryLoad(
                    "viewshed_kernel",
                    typeof(ViewshedNativeBridge).Assembly,
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
                ? "Native viewshed kernel loaded. Line-of-sight analysis will use the C++ fast path."
                : "Native viewshed kernel unavailable; using managed viewshed implementation.");
        }

        internal static bool TryCompute(
            ViewshedRequestDto request,
            ILogger logger,
            out ViewshedResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                var output = new byte[request.Width * request.Height];
                var status = ViewshedNativeInterop.Compute(
                    request.Elevation.ToArray(),
                    request.Width,
                    request.Height,
                    request.CellSize,
                    request.ObserverX,
                    request.ObserverY,
                    request.ObserverHeight,
                    output,
                    output.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native viewshed kernel failed with status {status}.");

                result = new ViewshedResultDto
                {
                    Width = request.Width,
                    Height = request.Height,
                    ObserverX = request.ObserverX,
                    ObserverY = request.ObserverY,
                    VisibleCells = status,
                    TotalCells = request.Width * request.Height,
                    NativeAccelerated = true,
                    Visibility = output.ToList()
                };
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native viewshed computation failed; falling back to managed implementation.");
                return false;
            }
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
