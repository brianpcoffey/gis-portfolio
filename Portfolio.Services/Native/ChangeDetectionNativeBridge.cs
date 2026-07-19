using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    // Raw per-blob statistics returned by the connected-component pass. The bridge hands
    // back primitives; DTO assembly and the NativeAccelerated decision live in the service,
    // so there is exactly one place where a result is shaped.
    internal sealed class ChangeComponentResult
    {
        internal int ComponentCount { get; init; }
        internal int[] Areas { get; init; } = [];
        internal double[] CentroidX { get; init; } = [];
        internal double[] CentroidY { get; init; } = [];
        internal double[] MeanMagnitude { get; init; } = [];
        internal int[] MinX { get; init; } = [];
        internal int[] MinY { get; init; } = [];
        internal int[] MaxX { get; init; } = [];
        internal int[] MaxY { get; init; } = [];
    }

    internal static class ChangeDetectionNativeBridge
    {
        private static readonly bool _available;

        static ChangeDetectionNativeBridge()
        {
            try
            {
                if (NativeToggle.Disabled)
                {
                    _available = false;
                    return;
                }

                _available = NativeLibrary.TryLoad(
                    "change_detection_kernel",
                    typeof(ChangeDetectionNativeBridge).Assembly,
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
                ? "Native change detection kernel loaded. CVA, Otsu, morphology and labelling will use the C++ fast path."
                : "Native change detection kernel unavailable; using managed change detection implementation.");
        }

        internal static bool TryComputeCvaMagnitude(
            double[] epochA,
            double[] epochB,
            int width,
            int height,
            int bandCount,
            ILogger logger,
            out double[]? magnitude)
        {
            magnitude = null;
            if (!_available)
                return false;

            try
            {
                var output = new double[width * height];

                var status = ChangeDetectionNativeInterop.ComputeCvaMagnitude(
                    epochA, epochB, width, height, bandCount, output, output.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native change detection kernel failed with status {status}.");

                magnitude = output;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native CVA magnitude failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryOtsuThreshold(
            double[] magnitude,
            int binCount,
            ILogger logger,
            out double threshold,
            out int[]? histogram)
        {
            threshold = 0;
            histogram = null;
            if (!_available)
                return false;

            try
            {
                var thresholdOutput = new double[1];
                var histogramOutput = new int[binCount];

                var status = ChangeDetectionNativeInterop.OtsuThreshold(
                    magnitude, magnitude.Length, binCount, thresholdOutput, histogramOutput, histogramOutput.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native change detection kernel failed with status {status}.");

                threshold = thresholdOutput[0];
                histogram = histogramOutput;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native Otsu threshold failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryMorphologicalOpen(
            byte[] mask,
            int width,
            int height,
            int iterations,
            ILogger logger,
            out byte[]? opened)
        {
            opened = null;
            if (!_available)
                return false;

            try
            {
                var output = new byte[width * height];

                var status = ChangeDetectionNativeInterop.MorphologicalOpen(
                    mask, width, height, iterations, output, output.Length);

                if (status < 0)
                    throw new InvalidOperationException($"Native change detection kernel failed with status {status}.");

                opened = output;
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native morphological open failed; falling back to managed implementation.");
                return false;
            }
        }

        internal static bool TryLabelComponents(
            byte[] mask,
            int width,
            int height,
            double[] magnitude,
            int blobCapacity,
            ILogger logger,
            out ChangeComponentResult? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                var labels = new int[width * height];
                var areas = new int[blobCapacity];
                var centroidX = new double[blobCapacity];
                var centroidY = new double[blobCapacity];
                var meanMagnitude = new double[blobCapacity];
                var minX = new int[blobCapacity];
                var minY = new int[blobCapacity];
                var maxX = new int[blobCapacity];
                var maxY = new int[blobCapacity];

                var status = ChangeDetectionNativeInterop.LabelComponents(
                    mask, width, height,
                    labels, labels.Length,
                    areas, centroidX, centroidY, meanMagnitude,
                    minX, minY, maxX, maxY,
                    magnitude, blobCapacity);

                if (status < 0)
                    throw new InvalidOperationException($"Native change detection kernel failed with status {status}.");

                result = new ChangeComponentResult
                {
                    ComponentCount = status,
                    Areas = areas,
                    CentroidX = centroidX,
                    CentroidY = centroidY,
                    MeanMagnitude = meanMagnitude,
                    MinX = minX,
                    MinY = minY,
                    MaxX = maxX,
                    MaxY = maxY
                };
                return true;
            }
            catch (Exception ex) when (IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native connected-component labelling failed; falling back to managed implementation.");
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
