using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;
using Portfolio.Services.Services.ChangeDetection;

namespace Portfolio.Services.Services
{
    /// <summary>
    /// Multitemporal raster change detection. The pipeline is the classic remote-sensing
    /// chain: CVA magnitude, Otsu threshold, morphological open, connected components,
    /// ranked detections.
    /// </summary>
    public class ChangeDetectionService : IChangeDetectionService
    {
        private const int MaxDimension = 512;
        private const int MaxBands = 8;
        private const int MaxPixels = 262_144;   // 512 * 512
        private const int MaxBlobs = ManagedChangeKernels.MaxBlobs;
        private const int MaxOpenIterations = 5;
        private const double MaxNoiseLevel = 0.30;

        // 128 bins over the magnitude range is fine enough for Otsu to land between the
        // modes and coarse enough to plot without downsampling.
        private const int HistogramBins = 128;

        private readonly ILogger<ChangeDetectionService> _logger;

        public ChangeDetectionService(ILogger<ChangeDetectionService> logger)
        {
            _logger = logger;
            ChangeDetectionNativeBridge.LogAvailability(_logger);
        }

        public Task<ChangeSceneDto> GetSceneAsync(
            int width,
            int height,
            double noiseLevel,
            CancellationToken cancellationToken = default)
        {
            ValidateDimensions(width, height, nameof(width));
            if (!double.IsFinite(noiseLevel) || noiseLevel < 0 || noiseLevel > MaxNoiseLevel)
                throw new ArgumentException($"Noise level must be between 0 and {MaxNoiseLevel}.", nameof(noiseLevel));

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(SyntheticChangeScene.Build(width, height, noiseLevel));
        }

        public Task<DetectResultDto> DetectAsync(
            DetectRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var (mode, epochA, epochB) = ValidateDetectRequest(request);
            var width = request.Width;
            var height = request.Height;

            cancellationToken.ThrowIfCancellationRequested();

            // ── 1. Change Vector Analysis magnitude ─────────────────────────
            var nativeAccelerated = true;
            var magnitude = ComputeMagnitude(request, epochA, epochB, ref nativeAccelerated, cancellationToken);
            var (histogramMin, histogramMax) = MagnitudeRange(magnitude);

            cancellationToken.ThrowIfCancellationRequested();

            // ── 2. Threshold. The histogram is always computed, even in manual ──
            // ── mode, because the UI plots it next to the chosen threshold. ──
            var (otsuThreshold, histogram) = ComputeOtsu(magnitude, ref nativeAccelerated);
            var threshold = mode == "manual" ? request.ManualThreshold : otsuThreshold;

            // ── 3. Binarize ─────────────────────────────────────────────────
            var mask = new byte[magnitude.Length];
            for (var i = 0; i < magnitude.Length; i++)
                mask[i] = magnitude[i] > threshold ? (byte)1 : (byte)0;

            cancellationToken.ThrowIfCancellationRequested();

            // ── 4. Morphological open ───────────────────────────────────────
            if (request.OpenIterations > 0)
                mask = ComputeOpen(mask, width, height, request.OpenIterations, ref nativeAccelerated, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // ── 5. Connected components ─────────────────────────────────────
            var components = ComputeComponents(mask, width, height, magnitude, ref nativeAccelerated, cancellationToken);

            return Task.FromResult(BuildResult(
                request, mode, threshold, magnitude, mask, histogram,
                histogramMin, histogramMax, components, nativeAccelerated));
        }

        private double[] ComputeMagnitude(
            DetectRequestDto request,
            double[] epochA,
            double[] epochB,
            ref bool nativeAccelerated,
            CancellationToken cancellationToken)
        {
            if (ChangeDetectionNativeBridge.TryComputeCvaMagnitude(
                    epochA, epochB, request.Width, request.Height, request.BandCount, _logger, out var nativeMagnitude))
            {
                return nativeMagnitude!;
            }

            nativeAccelerated = false;
            return ManagedChangeKernels.CvaMagnitudeManaged(
                epochA, epochB, request.Width * request.Height, request.BandCount, cancellationToken);
        }

        private static (double Min, double Max) MagnitudeRange(double[] magnitude)
        {
            var histogramMin = magnitude[0];
            var histogramMax = magnitude[0];
            for (var i = 1; i < magnitude.Length; i++)
            {
                if (magnitude[i] < histogramMin) histogramMin = magnitude[i];
                if (magnitude[i] > histogramMax) histogramMax = magnitude[i];
            }
            return (histogramMin, histogramMax);
        }

        private (double Threshold, int[] Histogram) ComputeOtsu(double[] magnitude, ref bool nativeAccelerated)
        {
            if (ChangeDetectionNativeBridge.TryOtsuThreshold(
                    magnitude, HistogramBins, _logger, out var nativeThreshold, out var nativeHistogram))
            {
                return (nativeThreshold, nativeHistogram!);
            }

            nativeAccelerated = false;
            return ManagedChangeKernels.OtsuManaged(magnitude, HistogramBins);
        }

        private byte[] ComputeOpen(
            byte[] mask,
            int width,
            int height,
            int iterations,
            ref bool nativeAccelerated,
            CancellationToken cancellationToken)
        {
            if (ChangeDetectionNativeBridge.TryMorphologicalOpen(
                    mask, width, height, iterations, _logger, out var nativeOpened))
            {
                return nativeOpened!;
            }

            nativeAccelerated = false;
            return ManagedChangeKernels.MorphologicalOpenManaged(mask, width, height, iterations, cancellationToken);
        }

        private ChangeComponentResult ComputeComponents(
            byte[] mask,
            int width,
            int height,
            double[] magnitude,
            ref bool nativeAccelerated,
            CancellationToken cancellationToken)
        {
            if (ChangeDetectionNativeBridge.TryLabelComponents(
                    mask, width, height, magnitude, MaxBlobs, _logger, out var nativeComponents))
            {
                return nativeComponents!;
            }

            nativeAccelerated = false;
            return ManagedChangeKernels.LabelComponentsManaged(mask, width, height, magnitude, cancellationToken);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static (string Mode, double[] EpochA, double[] EpochB) ValidateDetectRequest(DetectRequestDto request)
        {
            ValidateDimensions(request.Width, request.Height, nameof(request));
            ValidateBandsAndEpochs(request);

            if (request.OpenIterations < 0 || request.OpenIterations > MaxOpenIterations)
                throw new ArgumentException($"Open iterations must be between 0 and {MaxOpenIterations}.", nameof(request));
            if (request.MinBlobArea < 0)
                throw new ArgumentException("Minimum blob area cannot be negative.", nameof(request));

            var mode = ValidateThresholdMode(request);

            var epochA = ToFiniteArray(request.EpochA!, nameof(request));
            var epochB = ToFiniteArray(request.EpochB!, nameof(request));
            return (mode, epochA, epochB);
        }

        private static void ValidateBandsAndEpochs(DetectRequestDto request)
        {
            if (request.BandCount < 1 || request.BandCount > MaxBands)
                throw new ArgumentException($"Band count must be between 1 and {MaxBands}.", nameof(request));

            var expected = request.Width * request.Height * request.BandCount;
            if (request.EpochA is null || request.EpochB is null)
                throw new ArgumentException("Both epochs are required.", nameof(request));
            if (request.EpochA.Count != request.EpochB.Count)
                throw new ArgumentException("Both epochs must have identical dimensions.", nameof(request));
            if (request.EpochA.Count != expected)
                throw new ArgumentException("Epoch raster length must equal width * height * bandCount.", nameof(request));
        }

        private static string ValidateThresholdMode(DetectRequestDto request)
        {
            var mode = (request.ThresholdMode ?? string.Empty).Trim().ToLowerInvariant();
            if (mode.Length == 0)
                mode = "otsu";
            if (mode != "otsu" && mode != "manual")
                throw new ArgumentException("Threshold mode must be 'otsu' or 'manual'.", nameof(request));
            if (mode == "manual" && (!double.IsFinite(request.ManualThreshold) || request.ManualThreshold < 0))
                throw new ArgumentException("Manual threshold must be a finite non-negative value.", nameof(request));
            return mode;
        }

        private static void ValidateDimensions(int width, int height, string paramName)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Raster dimensions must be greater than zero.", paramName);
            if (width > MaxDimension || height > MaxDimension)
                throw new ArgumentException($"Raster dimensions are limited to {MaxDimension}.", paramName);
            if ((long)width * height > MaxPixels)
                throw new ArgumentException($"Rasters are limited to {MaxPixels} pixels.", paramName);
        }

        private static double[] ToFiniteArray(List<double> values, string paramName)
        {
            var array = new double[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                if (!double.IsFinite(values[i]))
                    throw new ArgumentException("Raster values must be finite.", paramName);
                array[i] = values[i];
            }
            return array;
        }

        // ── Result shaping ──────────────────────────────────────────────────────

        private static DetectResultDto BuildResult(
            DetectRequestDto request,
            string mode,
            double threshold,
            double[] magnitude,
            byte[] mask,
            int[] histogram,
            double histogramMin,
            double histogramMax,
            ChangeComponentResult components,
            bool nativeAccelerated)
        {
            var changedPixels = 0;
            for (var i = 0; i < mask.Length; i++)
            {
                if (mask[i] != 0)
                    changedPixels++;
            }

            var maxMagnitude = histogramMax > 0 ? histogramMax : 1.0;
            var blobs = new List<ChangeBlobDto>(components.ComponentCount);

            for (var c = 0; c < components.ComponentCount; c++)
            {
                // Never index native output past what the caller allocated.
                if (c >= components.Areas.Length)
                    break;
                if (components.Areas[c] < request.MinBlobArea)
                    continue;

                var mean = c < components.MeanMagnitude.Length ? components.MeanMagnitude[c] : 0;
                var confidence = mean / maxMagnitude;
                if (confidence < 0) confidence = 0;
                if (confidence > 1) confidence = 1;

                blobs.Add(new ChangeBlobDto
                {
                    Area = components.Areas[c],
                    CentroidX = c < components.CentroidX.Length ? components.CentroidX[c] : 0,
                    CentroidY = c < components.CentroidY.Length ? components.CentroidY[c] : 0,
                    MeanMagnitude = mean,
                    Confidence = confidence,
                    MinX = c < components.MinX.Length ? components.MinX[c] : 0,
                    MinY = c < components.MinY.Length ? components.MinY[c] : 0,
                    MaxX = c < components.MaxX.Length ? components.MaxX[c] : 0,
                    MaxY = c < components.MaxY.Length ? components.MaxY[c] : 0
                });
            }

            // Rank by area descending, then by mean magnitude so ties are deterministic.
            blobs.Sort((a, b) =>
            {
                var byArea = b.Area.CompareTo(a.Area);
                return byArea != 0 ? byArea : b.MeanMagnitude.CompareTo(a.MeanMagnitude);
            });
            for (var i = 0; i < blobs.Count; i++)
                blobs[i].Id = i + 1;

            return new DetectResultDto
            {
                NativeAccelerated = nativeAccelerated,
                Width = request.Width,
                Height = request.Height,
                Threshold = threshold,
                ThresholdMode = mode,
                Magnitude = [.. magnitude],
                Mask = [.. mask],
                Histogram = [.. histogram],
                HistogramMin = histogramMin,
                HistogramMax = histogramMax,
                Blobs = blobs,
                ChangedPixels = changedPixels,
                ChangedPercent = mask.Length > 0 ? 100.0 * changedPixels / mask.Length : 0,
                BlobsBeforeFiltering = components.ComponentCount
            };
        }
    }
}
