using Portfolio.Services.Native;

namespace Portfolio.Services.Services.ChangeDetection
{
    /// <summary>
    /// Managed fallbacks for the change-detection pipeline. They mirror the native kernel exactly.
    /// </summary>
    internal static class ManagedChangeKernels
    {
        internal const int MaxBlobs = 5_000;

        // magnitude[i] = sqrt( sum_b (epochB[b][i] - epochA[b][i])^2 ), band-sequential.
        internal static double[] CvaMagnitudeManaged(
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
        internal static (double Threshold, int[] Histogram) OtsuManaged(double[] magnitude, int binCount)
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
            FillHistogram(magnitude, histogram, minValue, binWidth);

            var totalWeight = 0.0;
            var totalMean = 0.0;
            for (var b = 0; b < binCount; b++)
            {
                var centre = minValue + (b + 0.5) * binWidth;
                totalWeight += histogram[b];
                totalMean += histogram[b] * centre;
            }

            var bestBin = BestSplitBin(histogram, minValue, binWidth, totalWeight, totalMean);

            return (minValue + (bestBin + 1) * binWidth, histogram);
        }

        private static void FillHistogram(double[] magnitude, int[] histogram, double minValue, double binWidth)
        {
            var binCount = histogram.Length;
            for (var i = 0; i < magnitude.Length; i++)
            {
                var bin = (int)((magnitude[i] - minValue) / binWidth);
                if (bin < 0) bin = 0;
                if (bin >= binCount) bin = binCount - 1;
                histogram[bin]++;
            }
        }

        // Bin after which the between-class variance is largest (first wins on ties).
        private static int BestSplitBin(
            int[] histogram,
            double minValue,
            double binWidth,
            double totalWeight,
            double totalMean)
        {
            var binCount = histogram.Length;
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

            return bestBin;
        }

        // Erode x n then dilate x n — one open with an n-scaled structuring element, not
        // n consecutive opens. Out-of-bounds neighbours read as background.
        internal static byte[] MorphologicalOpenManaged(
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

        internal static void ErodeOnce(byte[] source, byte[] destination, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    destination[index] = source[index] != 0 && AllNeighboursSet(source, x, y, width, height)
                        ? (byte)1
                        : (byte)0;
                }
            }
        }

        internal static void DilateOnce(byte[] source, byte[] destination, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    destination[index] = source[index] != 0 || AnyNeighbourSet(source, x, y, width, height)
                        ? (byte)1
                        : (byte)0;
                }
            }
        }

        // True when the whole 3x3 window is in bounds and set (out-of-bounds reads as background).
        private static bool AllNeighboursSet(byte[] source, int x, int y, int width, int height)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var ny = y + dy;
                if (ny < 0 || ny >= height)
                    return false;

                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    if (nx < 0 || nx >= width || source[ny * width + nx] == 0)
                        return false;
                }
            }

            return true;
        }

        // True when any in-bounds cell of the 3x3 window is set.
        private static bool AnyNeighbourSet(byte[] source, int x, int y, int width, int height)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var ny = y + dy;
                if (ny < 0 || ny >= height)
                    continue;

                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    if (nx >= 0 && nx < width && source[ny * width + nx] != 0)
                        return true;
                }
            }

            return false;
        }

        // Two-pass union-find labelling, 8-connectivity, over a flat parent array.
        internal static ChangeComponentResult LabelComponentsManaged(
            byte[] mask,
            int width,
            int height,
            double[] magnitude,
            CancellationToken cancellationToken)
        {
            var parent = new List<int> { 0 };
            var provisional = LabelFirstPass(mask, width, height, parent, cancellationToken);

            var compact = new int[parent.Count];
            var accumulator = new ComponentAccumulator();

            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var label = provisional[index];
                    if (label == 0)
                        continue;

                    var slot = ResolveSlot(parent, compact, accumulator, label, x, y);
                    accumulator.Add(slot, x, y, magnitude[index]);
                }
            }

            return accumulator.ToResult();
        }

        // First pass: assign provisional labels in raster order and union the neighbours.
        private static int[] LabelFirstPass(
            byte[] mask,
            int width,
            int height,
            List<int> parent,
            CancellationToken cancellationToken)
        {
            var provisional = new int[mask.Length];
            var neighbours = new int[4];

            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (mask[index] == 0)
                        continue;

                    var neighbourCount = CollectNeighbourLabels(provisional, x, y, width, neighbours);
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

            return provisional;
        }

        // Non-zero labels of the already-visited 8-connected neighbours (NW, N, NE, W), in that order.
        private static int CollectNeighbourLabels(int[] provisional, int x, int y, int width, int[] neighbours)
        {
            var count = 0;
            if (y > 0)
            {
                if (x > 0)
                    AddLabel(provisional[(y - 1) * width + (x - 1)], neighbours, ref count);
                AddLabel(provisional[(y - 1) * width + x], neighbours, ref count);
                if (x + 1 < width)
                    AddLabel(provisional[(y - 1) * width + (x + 1)], neighbours, ref count);
            }
            if (x > 0)
                AddLabel(provisional[y * width + (x - 1)], neighbours, ref count);
            return count;
        }

        private static void AddLabel(int label, int[] neighbours, ref int count)
        {
            if (label != 0)
                neighbours[count++] = label;
        }

        // Compact component slot (0-based) for a provisional label, allocating it on first sight.
        private static int ResolveSlot(
            List<int> parent,
            int[] compact,
            ComponentAccumulator accumulator,
            int label,
            int x,
            int y)
        {
            var root = FindRoot(parent, label);
            var id = compact[root];
            if (id == 0)
            {
                if (accumulator.Count + 1 > MaxBlobs)
                    throw new ArgumentException(
                        $"The change mask contains more than {MaxBlobs} connected components; raise the threshold or the open iterations.",
                        "request");

                id = accumulator.Start(x, y);
                compact[root] = id;
            }

            return id - 1;
        }

        // Per-component running totals, one slot per component in first-seen (raster) order.
        private sealed class ComponentAccumulator
        {
            private readonly List<double> _sumX = [];
            private readonly List<double> _sumY = [];
            private readonly List<double> _sumMagnitude = [];
            private readonly List<int> _areas = [];
            private readonly List<int> _minX = [];
            private readonly List<int> _minY = [];
            private readonly List<int> _maxX = [];
            private readonly List<int> _maxY = [];

            public int Count => _areas.Count;

            // Opens a new slot seeded at (x, y); returns its 1-based id.
            public int Start(int x, int y)
            {
                _sumX.Add(0);
                _sumY.Add(0);
                _sumMagnitude.Add(0);
                _areas.Add(0);
                _minX.Add(x);
                _minY.Add(y);
                _maxX.Add(x);
                _maxY.Add(y);
                return _areas.Count;
            }

            public void Add(int slot, int x, int y, double magnitude)
            {
                _sumX[slot] += x;
                _sumY[slot] += y;
                _sumMagnitude[slot] += magnitude;
                _areas[slot]++;
                if (x < _minX[slot]) _minX[slot] = x;
                if (y < _minY[slot]) _minY[slot] = y;
                if (x > _maxX[slot]) _maxX[slot] = x;
                if (y > _maxY[slot]) _maxY[slot] = y;
            }

            public ChangeComponentResult ToResult()
            {
                var componentCount = _areas.Count;
                var centroidX = new double[componentCount];
                var centroidY = new double[componentCount];
                var meanMagnitude = new double[componentCount];
                for (var c = 0; c < componentCount; c++)
                {
                    double count = _areas[c];
                    centroidX[c] = count > 0 ? _sumX[c] / count : 0;
                    centroidY[c] = count > 0 ? _sumY[c] / count : 0;
                    meanMagnitude[c] = count > 0 ? _sumMagnitude[c] / count : 0;
                }

                return new ChangeComponentResult
                {
                    ComponentCount = componentCount,
                    Areas = [.. _areas],
                    CentroidX = centroidX,
                    CentroidY = centroidY,
                    MeanMagnitude = meanMagnitude,
                    MinX = [.. _minX],
                    MinY = [.. _minY],
                    MaxX = [.. _maxX],
                    MaxY = [.. _maxY]
                };
            }
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
    }
}
