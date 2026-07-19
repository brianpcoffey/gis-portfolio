using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

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
        private const int MaxBlobs = 5_000;
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
            if (!IsFinite(noiseLevel) || noiseLevel < 0 || noiseLevel > MaxNoiseLevel)
                throw new ArgumentException($"Noise level must be between 0 and {MaxNoiseLevel}.", nameof(noiseLevel));

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(SyntheticChangeScene.Build(width, height, noiseLevel));
        }

        public Task<DetectResultDto> DetectAsync(
            DetectRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var width = request.Width;
            var height = request.Height;
            ValidateDimensions(width, height, nameof(request));

            if (request.BandCount < 1 || request.BandCount > MaxBands)
                throw new ArgumentException($"Band count must be between 1 and {MaxBands}.", nameof(request));

            var expected = width * height * request.BandCount;
            if (request.EpochA is null || request.EpochB is null)
                throw new ArgumentException("Both epochs are required.", nameof(request));
            if (request.EpochA.Count != request.EpochB.Count)
                throw new ArgumentException("Both epochs must have identical dimensions.", nameof(request));
            if (request.EpochA.Count != expected)
                throw new ArgumentException("Epoch raster length must equal width * height * bandCount.", nameof(request));

            if (request.OpenIterations < 0 || request.OpenIterations > MaxOpenIterations)
                throw new ArgumentException($"Open iterations must be between 0 and {MaxOpenIterations}.", nameof(request));
            if (request.MinBlobArea < 0)
                throw new ArgumentException("Minimum blob area cannot be negative.", nameof(request));

            var mode = (request.ThresholdMode ?? string.Empty).Trim().ToLowerInvariant();
            if (mode.Length == 0)
                mode = "otsu";
            if (mode != "otsu" && mode != "manual")
                throw new ArgumentException("Threshold mode must be 'otsu' or 'manual'.", nameof(request));
            if (mode == "manual" && (!IsFinite(request.ManualThreshold) || request.ManualThreshold < 0))
                throw new ArgumentException("Manual threshold must be a finite non-negative value.", nameof(request));

            var epochA = ToFiniteArray(request.EpochA, nameof(request));
            var epochB = ToFiniteArray(request.EpochB, nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            // ── 1. Change Vector Analysis magnitude ─────────────────────────
            var nativeAccelerated = true;
            double[] magnitude;
            if (ChangeDetectionNativeBridge.TryComputeCvaMagnitude(
                    epochA, epochB, width, height, request.BandCount, _logger, out var nativeMagnitude))
            {
                magnitude = nativeMagnitude!;
            }
            else
            {
                magnitude = CvaMagnitudeManaged(epochA, epochB, width * height, request.BandCount, cancellationToken);
                nativeAccelerated = false;
            }

            var histogramMin = magnitude[0];
            var histogramMax = magnitude[0];
            for (var i = 1; i < magnitude.Length; i++)
            {
                if (magnitude[i] < histogramMin) histogramMin = magnitude[i];
                if (magnitude[i] > histogramMax) histogramMax = magnitude[i];
            }

            cancellationToken.ThrowIfCancellationRequested();

            // ── 2. Threshold. The histogram is always computed, even in manual ──
            // ── mode, because the UI plots it next to the chosen threshold. ──
            int[] histogram;
            double otsuThreshold;
            if (ChangeDetectionNativeBridge.TryOtsuThreshold(
                    magnitude, HistogramBins, _logger, out var nativeThreshold, out var nativeHistogram))
            {
                otsuThreshold = nativeThreshold;
                histogram = nativeHistogram!;
            }
            else
            {
                (otsuThreshold, histogram) = OtsuManaged(magnitude, HistogramBins);
                nativeAccelerated = false;
            }

            var threshold = mode == "manual" ? request.ManualThreshold : otsuThreshold;

            // ── 3. Binarize ─────────────────────────────────────────────────
            var mask = new byte[magnitude.Length];
            for (var i = 0; i < magnitude.Length; i++)
                mask[i] = magnitude[i] > threshold ? (byte)1 : (byte)0;

            cancellationToken.ThrowIfCancellationRequested();

            // ── 4. Morphological open ───────────────────────────────────────
            if (request.OpenIterations > 0)
            {
                if (ChangeDetectionNativeBridge.TryMorphologicalOpen(
                        mask, width, height, request.OpenIterations, _logger, out var nativeOpened))
                {
                    mask = nativeOpened!;
                }
                else
                {
                    mask = MorphologicalOpenManaged(mask, width, height, request.OpenIterations, cancellationToken);
                    nativeAccelerated = false;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // ── 5. Connected components ─────────────────────────────────────
            ChangeComponentResult components;
            if (ChangeDetectionNativeBridge.TryLabelComponents(
                    mask, width, height, magnitude, MaxBlobs, _logger, out var nativeComponents))
            {
                components = nativeComponents!;
            }
            else
            {
                components = LabelComponentsManaged(mask, width, height, magnitude, cancellationToken);
                nativeAccelerated = false;
            }

            return Task.FromResult(BuildResult(
                request, mode, threshold, magnitude, mask, histogram,
                histogramMin, histogramMax, components, nativeAccelerated));
        }

        // ── Validation ──────────────────────────────────────────────────────────

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
                if (!IsFinite(values[i]))
                    throw new ArgumentException("Raster values must be finite.", paramName);
                array[i] = values[i];
            }
            return array;
        }

        // ── Managed fallbacks (mirror the native kernel exactly) ────────────────

        // magnitude[i] = sqrt( sum_b (epochB[b][i] - epochA[b][i])^2 ), band-sequential.
        private static double[] CvaMagnitudeManaged(
            double[] epochA,
            double[] epochB,
            int pixelCount,
            int bandCount,
            CancellationToken cancellationToken)
        {
            var magnitude = new double[pixelCount];

            for (var b = 0; b < bandCount; b++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var offset = b * pixelCount;
                for (var i = 0; i < pixelCount; i++)
                {
                    var delta = epochB[offset + i] - epochA[offset + i];
                    magnitude[i] += delta * delta;
                }
            }

            for (var i = 0; i < pixelCount; i++)
                magnitude[i] = Math.Sqrt(magnitude[i]);

            return magnitude;
        }

        // Otsu's method. One pass accumulates cumulative weight and cumulative weighted
        // mean; between-class variance for a split after bin b is w0*w1*(mu0-mu1)^2. The
        // returned threshold is the upper edge of the winning bin.
        private static (double Threshold, int[] Histogram) OtsuManaged(double[] magnitude, int binCount)
        {
            var histogram = new int[binCount];

            var minValue = magnitude[0];
            var maxValue = magnitude[0];
            for (var i = 1; i < magnitude.Length; i++)
            {
                if (magnitude[i] < minValue) minValue = magnitude[i];
                if (magnitude[i] > maxValue) maxValue = magnitude[i];
            }

            var span = maxValue - minValue;
            if (!(span > 0))
            {
                // Uniform magnitude: the threshold is the single value present, which
                // classifies nothing as changed rather than everything.
                histogram[0] = magnitude.Length;
                return (maxValue, histogram);
            }

            var binWidth = span / binCount;
            for (var i = 0; i < magnitude.Length; i++)
            {
                var bin = (int)((magnitude[i] - minValue) / binWidth);
                if (bin < 0) bin = 0;
                if (bin >= binCount) bin = binCount - 1;
                histogram[bin]++;
            }

            var totalWeight = 0.0;
            var totalMean = 0.0;
            for (var b = 0; b < binCount; b++)
            {
                var centre = minValue + (b + 0.5) * binWidth;
                totalWeight += histogram[b];
                totalMean += histogram[b] * centre;
            }

            var cumulativeWeight = 0.0;
            var cumulativeMean = 0.0;
            var bestVariance = -1.0;
            var bestBin = 0;

            for (var b = 0; b < binCount - 1; b++)
            {
                var centre = minValue + (b + 0.5) * binWidth;
                cumulativeWeight += histogram[b];
                cumulativeMean += histogram[b] * centre;

                var w0 = cumulativeWeight / totalWeight;
                var w1 = 1.0 - w0;
                if (w0 <= 0 || w1 <= 0)
                    continue;

                var mu0 = cumulativeMean / cumulativeWeight;
                var mu1 = (totalMean - cumulativeMean) / (totalWeight - cumulativeWeight);
                var diff = mu0 - mu1;
                var variance = w0 * w1 * diff * diff;

                if (variance > bestVariance)
                {
                    bestVariance = variance;
                    bestBin = b;
                }
            }

            return (minValue + (bestBin + 1) * binWidth, histogram);
        }

        // Erode x n then dilate x n — one open with an n-scaled structuring element, not
        // n consecutive opens. Out-of-bounds neighbours read as background.
        private static byte[] MorphologicalOpenManaged(
            byte[] mask,
            int width,
            int height,
            int iterations,
            CancellationToken cancellationToken)
        {
            var current = new byte[mask.Length];
            for (var i = 0; i < mask.Length; i++)
                current[i] = mask[i] != 0 ? (byte)1 : (byte)0;

            var scratch = new byte[mask.Length];

            for (var it = 0; it < iterations; it++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ErodeOnce(current, scratch, width, height);
                (current, scratch) = (scratch, current);
            }
            for (var it = 0; it < iterations; it++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DilateOnce(current, scratch, width, height);
                (current, scratch) = (scratch, current);
            }

            return current;
        }

        private static void ErodeOnce(byte[] source, byte[] destination, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (source[index] == 0)
                    {
                        destination[index] = 0;
                        continue;
                    }

                    byte keep = 1;
                    for (var dy = -1; dy <= 1 && keep != 0; dy++)
                    {
                        var ny = y + dy;
                        if (ny < 0 || ny >= height)
                        {
                            keep = 0;
                            break;
                        }

                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var nx = x + dx;
                            if (nx < 0 || nx >= width || source[ny * width + nx] == 0)
                            {
                                keep = 0;
                                break;
                            }
                        }
                    }

                    destination[index] = keep;
                }
            }
        }

        private static void DilateOnce(byte[] source, byte[] destination, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (source[index] != 0)
                    {
                        destination[index] = 1;
                        continue;
                    }

                    byte hit = 0;
                    for (var dy = -1; dy <= 1 && hit == 0; dy++)
                    {
                        var ny = y + dy;
                        if (ny < 0 || ny >= height)
                            continue;

                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var nx = x + dx;
                            if (nx < 0 || nx >= width)
                                continue;
                            if (source[ny * width + nx] != 0)
                            {
                                hit = 1;
                                break;
                            }
                        }
                    }

                    destination[index] = hit;
                }
            }
        }

        // Two-pass union-find labelling, 8-connectivity, over a flat parent array.
        private static ChangeComponentResult LabelComponentsManaged(
            byte[] mask,
            int width,
            int height,
            double[] magnitude,
            CancellationToken cancellationToken)
        {
            var provisional = new int[mask.Length];
            var parent = new List<int> { 0 };
            var neighbours = new int[4];

            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (mask[index] == 0)
                        continue;

                    var neighbourCount = 0;
                    if (y > 0)
                    {
                        if (x > 0)
                        {
                            var label = provisional[(y - 1) * width + (x - 1)];
                            if (label != 0) neighbours[neighbourCount++] = label;
                        }
                        {
                            var label = provisional[(y - 1) * width + x];
                            if (label != 0) neighbours[neighbourCount++] = label;
                        }
                        if (x + 1 < width)
                        {
                            var label = provisional[(y - 1) * width + (x + 1)];
                            if (label != 0) neighbours[neighbourCount++] = label;
                        }
                    }
                    if (x > 0)
                    {
                        var label = provisional[y * width + (x - 1)];
                        if (label != 0) neighbours[neighbourCount++] = label;
                    }

                    if (neighbourCount == 0)
                    {
                        var fresh = parent.Count;
                        parent.Add(fresh);
                        provisional[index] = fresh;
                        continue;
                    }

                    var smallest = neighbours[0];
                    for (var n = 1; n < neighbourCount; n++)
                    {
                        if (neighbours[n] < smallest)
                            smallest = neighbours[n];
                    }

                    provisional[index] = smallest;
                    for (var n = 0; n < neighbourCount; n++)
                        UnionLabels(parent, smallest, neighbours[n]);
                }
            }

            var compact = new int[parent.Count];
            var componentCount = 0;
            var sumX = new List<double>();
            var sumY = new List<double>();
            var sumMagnitude = new List<double>();
            var areas = new List<int>();
            var minX = new List<int>();
            var minY = new List<int>();
            var maxX = new List<int>();
            var maxY = new List<int>();

            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var label = provisional[index];
                    if (label == 0)
                        continue;

                    var root = FindRoot(parent, label);
                    var id = compact[root];
                    if (id == 0)
                    {
                        componentCount++;
                        if (componentCount > MaxBlobs)
                            throw new ArgumentException(
                                $"The change mask contains more than {MaxBlobs} connected components; raise the threshold or the open iterations.",
                                "request");

                        id = componentCount;
                        compact[root] = id;
                        sumX.Add(0);
                        sumY.Add(0);
                        sumMagnitude.Add(0);
                        areas.Add(0);
                        minX.Add(x);
                        minY.Add(y);
                        maxX.Add(x);
                        maxY.Add(y);
                    }

                    var slot = id - 1;
                    sumX[slot] += x;
                    sumY[slot] += y;
                    sumMagnitude[slot] += magnitude[index];
                    areas[slot]++;
                    if (x < minX[slot]) minX[slot] = x;
                    if (y < minY[slot]) minY[slot] = y;
                    if (x > maxX[slot]) maxX[slot] = x;
                    if (y > maxY[slot]) maxY[slot] = y;
                }
            }

            var centroidX = new double[componentCount];
            var centroidY = new double[componentCount];
            var meanMagnitude = new double[componentCount];
            for (var c = 0; c < componentCount; c++)
            {
                double count = areas[c];
                centroidX[c] = count > 0 ? sumX[c] / count : 0;
                centroidY[c] = count > 0 ? sumY[c] / count : 0;
                meanMagnitude[c] = count > 0 ? sumMagnitude[c] / count : 0;
            }

            return new ChangeComponentResult
            {
                ComponentCount = componentCount,
                Areas = [.. areas],
                CentroidX = centroidX,
                CentroidY = centroidY,
                MeanMagnitude = meanMagnitude,
                MinX = [.. minX],
                MinY = [.. minY],
                MaxX = [.. maxX],
                MaxY = [.. maxY]
            };
        }

        private static int FindRoot(List<int> parent, int label)
        {
            var root = label;
            while (parent[root] != root)
                root = parent[root];

            while (parent[label] != root)
            {
                var next = parent[label];
                parent[label] = root;
                label = next;
            }
            return root;
        }

        private static void UnionLabels(List<int> parent, int a, int b)
        {
            var rootA = FindRoot(parent, a);
            var rootB = FindRoot(parent, b);
            if (rootA == rootB)
                return;

            if (rootA < rootB)
                parent[rootB] = rootA;
            else
                parent[rootA] = rootB;
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

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
