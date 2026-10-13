using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Native
{
    internal static class ViewshedNativeBridge
    {
        // Loaded only when the library reports the ABI version this assembly was written against.
        private static readonly NativeLoadResult _load = NativeKernelLoader.Load(
            "viewshed_kernel", ViewshedNativeInterop.AbiVersion, ViewshedNativeInterop.ExpectedAbiVersion);

        private static readonly bool _available = _load == NativeLoadResult.Loaded;

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            if (_load == NativeLoadResult.AbiMismatch)
            {
                NativeKernelLoader.LogAbiMismatch(logger, "viewshed_kernel", ViewshedNativeInterop.ExpectedAbiVersion);
                return;
            }

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
                var elevation = request.Elevation.ToArray();
                var output = new byte[request.Width * request.Height];
                var status = ViewshedNativeInterop.Compute(
                    elevation,
                    elevation.Length,
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
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native viewshed computation failed; falling back to managed implementation.");
                return false;
            }
        }
    }
}
