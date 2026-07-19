using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Multitemporal raster change detection: CVA magnitude, Otsu thresholding,
    // morphological open, and connected-component detection extraction.
    //
    // The suite runs with no native shared library present, so every test exercises the
    // managed fallback and asserts NativeAccelerated == false. The managed path mirrors
    // native/change_detection_kernel/src/change_detection_kernel.cpp line for line, so
    // these also pin the arithmetic the native kernel must reproduce.
    public class ChangeDetectionServiceTests
    {
        private static ChangeDetectionService NewService() =>
            new(NullLogger<ChangeDetectionService>.Instance);

        // A single-band 8x8 stack where epoch A is uniformly zero. Callers paint epoch B.
        private static DetectRequestDto SingleBand(
            int width,
            int height,
            Action<double[]> paintEpochB,
            int openIterations = 0,
            int minBlobArea = 0)
        {
            var epochB = new double[width * height];
            paintEpochB(epochB);

            return new DetectRequestDto
            {
                Width = width,
                Height = height,
                BandCount = 1,
                EpochA = [.. new double[width * height]],
                EpochB = [.. epochB],
                ThresholdMode = "manual",
                ManualThreshold = 0.5,
                OpenIterations = openIterations,
                MinBlobArea = minBlobArea
            };
        }

        private static void FillSquare(double[] raster, int width, int x0, int y0, int size, double value)
        {
            for (var y = y0; y < y0 + size; y++)
            {
                for (var x = x0; x < x0 + size; x++)
                    raster[y * width + x] = value;
            }
        }

        // ── Core detection ──────────────────────────────────────────────────────

        [Fact]
        public async Task Detect_IdenticalEpochs_ProducesNoChange()
        {
            var service = NewService();

            var result = await service.DetectAsync(SingleBand(8, 8, _ => { }));

            Assert.False(result.NativeAccelerated);
            Assert.Empty(result.Blobs);
            Assert.Equal(0, result.ChangedPixels);
            Assert.Equal(0, result.BlobsBeforeFiltering);
        }

        [Fact]
        public async Task Detect_SingleSquare_FindsOneBlob()
        {
            var service = NewService();

            var result = await service.DetectAsync(
                SingleBand(8, 8, b => FillSquare(b, 8, 2, 2, 2, 1.0)));

            Assert.False(result.NativeAccelerated);
            var blob = Assert.Single(result.Blobs);
            Assert.Equal(4, blob.Area);
            Assert.Equal(2.5, blob.CentroidX, 6);
            Assert.Equal(2.5, blob.CentroidY, 6);
        }

        [Fact]
        public async Task Detect_TwoSeparateSquares_FindsTwoBlobs()
        {
            var service = NewService();

            var result = await service.DetectAsync(SingleBand(8, 8, b =>
            {
                FillSquare(b, 8, 0, 0, 2, 1.0);
                FillSquare(b, 8, 5, 5, 2, 1.0);
            }));

            Assert.Equal(2, result.Blobs.Count);
            Assert.All(result.Blobs, blob => Assert.Equal(4, blob.Area));
        }

        [Fact]
        public async Task Detect_DiagonallyAdjacentPixels_AreOneBlob()
        {
            var service = NewService();

            // (2,2) and (3,3) touch only at a corner: 8-connectivity joins them,
            // 4-connectivity would not.
            var result = await service.DetectAsync(SingleBand(8, 8, b =>
            {
                b[2 * 8 + 2] = 1.0;
                b[3 * 8 + 3] = 1.0;
            }));

            var blob = Assert.Single(result.Blobs);
            Assert.Equal(2, blob.Area);
        }

        [Fact]
        public async Task Detect_BlobAreaMatchesPixelCount()
        {
            var service = NewService();

            var result = await service.DetectAsync(
                SingleBand(8, 8, b => FillSquare(b, 8, 1, 1, 3, 1.0)));

            var blob = Assert.Single(result.Blobs);
            Assert.Equal(9, blob.Area);
            Assert.Equal(9, result.ChangedPixels);
        }

        [Fact]
        public async Task Detect_CentroidIsPixelMean()
        {
            var service = NewService();

            // Three pixels at (1,1), (2,1), (6,1): mean x = 3, mean y = 1.
            var result = await service.DetectAsync(SingleBand(8, 8, b =>
            {
                b[1 * 8 + 1] = 1.0;
                b[1 * 8 + 2] = 1.0;
                b[1 * 8 + 6] = 1.0;
            }));

            Assert.Equal(2, result.Blobs.Count);
            var totalX = result.Blobs.Sum(blob => blob.CentroidX * blob.Area);
            var totalArea = result.Blobs.Sum(blob => blob.Area);
            Assert.Equal(3.0, totalX / totalArea, 6);
            Assert.All(result.Blobs, blob => Assert.Equal(1.0, blob.CentroidY, 6));
        }

        [Fact]
        public async Task Detect_MinBlobArea_FiltersSmallBlobs()
        {
            var service = NewService();

            var result = await service.DetectAsync(SingleBand(8, 8, b =>
            {
                FillSquare(b, 8, 1, 1, 3, 1.0);
                b[6 * 8 + 6] = 1.0;
            }, minBlobArea: 4));

            Assert.Equal(2, result.BlobsBeforeFiltering);
            var blob = Assert.Single(result.Blobs);
            Assert.Equal(9, blob.Area);
            Assert.True(result.BlobsBeforeFiltering > result.Blobs.Count);
        }

        [Fact]
        public async Task Detect_BlobsAreSortedByAreaDescending()
        {
            var service = NewService();

            var result = await service.DetectAsync(SingleBand(12, 12, b =>
            {
                FillSquare(b, 12, 0, 0, 2, 1.0);
                FillSquare(b, 12, 5, 5, 4, 1.0);
                FillSquare(b, 12, 0, 9, 3, 1.0);
            }));

            Assert.Equal(3, result.Blobs.Count);
            Assert.Equal([16, 9, 4], result.Blobs.Select(blob => blob.Area).ToArray());
            Assert.Equal([1, 2, 3], result.Blobs.Select(blob => blob.Id).ToArray());
        }

        // ── Morphology ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Detect_MorphologicalOpen_RemovesSinglePixelSpeckle()
        {
            var service = NewService();

            var result = await service.DetectAsync(SingleBand(12, 12, b =>
            {
                FillSquare(b, 12, 3, 3, 5, 1.0);
                b[10 * 12 + 10] = 1.0;
            }, openIterations: 1));

            var blob = Assert.Single(result.Blobs);
            // The 5x5 square keeps its 3x3 core through erosion and dilation restores it.
            Assert.Equal(25, blob.Area);
        }

        [Fact]
        public async Task Detect_MorphologicalOpen_ZeroIterations_IsNoOp()
        {
            var service = NewService();

            var withOpen = await service.DetectAsync(SingleBand(12, 12, b =>
            {
                FillSquare(b, 12, 3, 3, 5, 1.0);
                b[10 * 12 + 10] = 1.0;
            }, openIterations: 0));

            Assert.Equal(2, withOpen.Blobs.Count);
            Assert.Equal(26, withOpen.ChangedPixels);
        }

        // ── Otsu ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Otsu_BimodalHistogram_ThresholdFallsBetweenModes()
        {
            var service = NewService();

            // A 16x16 single-band raster: 20% of pixels change by 1.0, the rest by 0.05.
            // Otsu must land strictly between the two modes.
            const int size = 16;
            var epochB = new double[size * size];
            for (var i = 0; i < epochB.Length; i++)
                epochB[i] = i % 5 == 0 ? 1.0 : 0.05;

            var result = await service.DetectAsync(new DetectRequestDto
            {
                Width = size,
                Height = size,
                BandCount = 1,
                EpochA = [.. new double[size * size]],
                EpochB = [.. epochB],
                ThresholdMode = "otsu",
                OpenIterations = 0,
                MinBlobArea = 0
            });

            Assert.False(result.NativeAccelerated);
            Assert.Equal("otsu", result.ThresholdMode);
            Assert.True(result.Threshold > 0.05, $"Threshold {result.Threshold} did not clear the low mode.");
            Assert.True(result.Threshold < 1.0, $"Threshold {result.Threshold} did not fall below the high mode.");
            Assert.Equal(52, result.ChangedPixels);
        }

        [Fact]
        public async Task Otsu_UniformMagnitude_ReturnsStableThreshold()
        {
            var service = NewService();

            var epochB = new double[64];
            for (var i = 0; i < epochB.Length; i++)
                epochB[i] = 0.4;

            var result = await service.DetectAsync(new DetectRequestDto
            {
                Width = 8,
                Height = 8,
                BandCount = 1,
                EpochA = [.. new double[64]],
                EpochB = [.. epochB],
                ThresholdMode = "otsu",
                OpenIterations = 0,
                MinBlobArea = 0
            });

            // Degenerate histogram: the threshold is the single magnitude present, which
            // classifies nothing as changed rather than everything.
            Assert.Equal(0.4, result.Threshold, 6);
            Assert.Equal(0, result.ChangedPixels);
            Assert.Empty(result.Blobs);
        }

        [Fact]
        public async Task Detect_ManualThreshold_OverridesOtsu()
        {
            var service = NewService();

            var request = SingleBand(8, 8, b => FillSquare(b, 8, 2, 2, 2, 1.0));
            request.ManualThreshold = 0.75;

            var result = await service.DetectAsync(request);

            Assert.Equal("manual", result.ThresholdMode);
            Assert.Equal(0.75, result.Threshold, 6);
            Assert.Equal(4, result.ChangedPixels);
        }

        [Fact]
        public async Task Detect_HigherThreshold_ProducesFewerChangedPixels()
        {
            var service = NewService();

            void Paint(double[] b)
            {
                FillSquare(b, 8, 0, 0, 4, 0.6);
                FillSquare(b, 8, 4, 4, 4, 1.0);
            }

            var low = SingleBand(8, 8, Paint);
            low.ManualThreshold = 0.3;
            var high = SingleBand(8, 8, Paint);
            high.ManualThreshold = 0.8;

            var lowResult = await service.DetectAsync(low);
            var highResult = await service.DetectAsync(high);

            Assert.Equal(32, lowResult.ChangedPixels);
            Assert.Equal(16, highResult.ChangedPixels);
            Assert.True(highResult.ChangedPixels < lowResult.ChangedPixels);
        }

        [Fact]
        public async Task Detect_CvaMagnitude_IsEuclideanAcrossBands()
        {
            var service = NewService();

            // 2x2, two bands. Pixel 0 changes by (3, 4) so its magnitude is exactly 5.
            var epochA = new double[8];
            var epochB = new double[8];
            epochB[0] = 3.0;   // band 0, pixel 0
            epochB[4] = 4.0;   // band 1, pixel 0

            var result = await service.DetectAsync(new DetectRequestDto
            {
                Width = 2,
                Height = 2,
                BandCount = 2,
                EpochA = [.. epochA],
                EpochB = [.. epochB],
                ThresholdMode = "manual",
                ManualThreshold = 1.0,
                OpenIterations = 0,
                MinBlobArea = 0
            });

            Assert.Equal(5.0, result.Magnitude[0], 9);
            Assert.Equal(0.0, result.Magnitude[1], 9);
            Assert.Equal(5.0, result.HistogramMax, 9);
            Assert.Equal(1, result.ChangedPixels);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Detect_MismatchedEpochLengths_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.DetectAsync(new DetectRequestDto
            {
                Width = 8,
                Height = 8,
                BandCount = 1,
                EpochA = [.. new double[64]],
                EpochB = [.. new double[32]],
                ThresholdMode = "otsu"
            }));
        }

        [Fact]
        public async Task Detect_ZeroWidth_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.DetectAsync(new DetectRequestDto
            {
                Width = 0,
                Height = 8,
                BandCount = 1,
                ThresholdMode = "otsu"
            }));
        }

        [Fact]
        public async Task Detect_TooManyPixels_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.DetectAsync(new DetectRequestDto
            {
                Width = 1024,
                Height = 1024,
                BandCount = 1,
                ThresholdMode = "otsu"
            }));
        }

        [Fact]
        public async Task Detect_NonFiniteValue_ThrowsArgumentException()
        {
            var service = NewService();

            var epochB = new double[64];
            epochB[10] = double.NaN;

            await Assert.ThrowsAsync<ArgumentException>(() => service.DetectAsync(new DetectRequestDto
            {
                Width = 8,
                Height = 8,
                BandCount = 1,
                EpochA = [.. new double[64]],
                EpochB = [.. epochB],
                ThresholdMode = "otsu"
            }));
        }

        [Fact]
        public async Task Detect_UnknownThresholdMode_ThrowsArgumentException()
        {
            var service = NewService();

            var request = SingleBand(8, 8, _ => { });
            request.ThresholdMode = "kittler";

            await Assert.ThrowsAsync<ArgumentException>(() => service.DetectAsync(request));
        }

        [Fact]
        public async Task Detect_OpenIterationsOutOfRange_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.DetectAsync(SingleBand(8, 8, _ => { }, openIterations: 6)));
        }

        [Fact]
        public async Task Detect_NegativeMinBlobArea_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.DetectAsync(SingleBand(8, 8, _ => { }, minBlobArea: -1)));
        }

        [Fact]
        public async Task Detect_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.DetectAsync(null!));
        }

        // ── Scene ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task Scene_IsDeterministic()
        {
            var service = NewService();

            var first = await service.GetSceneAsync(64, 64, 0.02);
            var second = await service.GetSceneAsync(64, 64, 0.02);

            Assert.Equal(first.EpochA, second.EpochA);
            Assert.Equal(first.EpochB, second.EpochB);
            Assert.Equal(4, first.BandCount);
            Assert.Equal(4, first.GroundTruth.Count);
            Assert.Equal(64 * 64 * 4, first.EpochA.Count);
        }

        [Fact]
        public async Task Scene_NegativeNoise_ThrowsArgumentException()
        {
            var service = NewService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.GetSceneAsync(64, 64, -0.1));
        }

        // The accuracy assertion, not a smoke test: on a clean noiseless scene at the demo
        // defaults, every planted change must be recovered by a detection whose centroid
        // falls inside the ground-truth box.
        [Fact]
        public async Task Scene_PlantedChangesAreDetectable()
        {
            var service = NewService();
            var scene = await service.GetSceneAsync(256, 256, 0);

            var result = await service.DetectAsync(new DetectRequestDto
            {
                Width = scene.Width,
                Height = scene.Height,
                BandCount = scene.BandCount,
                EpochA = scene.EpochA,
                EpochB = scene.EpochB,
                ThresholdMode = "otsu",
                OpenIterations = 1,
                MinBlobArea = 12
            });

            Assert.False(result.NativeAccelerated);
            Assert.Equal(4, scene.GroundTruth.Count);

            foreach (var box in scene.GroundTruth)
            {
                Assert.Contains(result.Blobs, blob =>
                    blob.CentroidX >= box.MinX && blob.CentroidX <= box.MaxX &&
                    blob.CentroidY >= box.MinY && blob.CentroidY <= box.MaxY);
            }

            // No false positives at the demo defaults.
            Assert.Equal(4, result.Blobs.Count);
        }

        // The same scene at the demo noise level must still recover everything — the
        // defaults are calibrated so the demo is an evaluation, not a coin flip.
        [Fact]
        public async Task Scene_PlantedChangesSurviveDemoNoiseLevel()
        {
            var service = NewService();
            var scene = await service.GetSceneAsync(256, 256, 0.025);

            var result = await service.DetectAsync(new DetectRequestDto
            {
                Width = scene.Width,
                Height = scene.Height,
                BandCount = scene.BandCount,
                EpochA = scene.EpochA,
                EpochB = scene.EpochB,
                ThresholdMode = "otsu",
                OpenIterations = 1,
                MinBlobArea = 12
            });

            var recovered = scene.GroundTruth.Count(box =>
                result.Blobs.Any(blob =>
                    blob.CentroidX >= box.MinX && blob.CentroidX <= box.MaxX &&
                    blob.CentroidY >= box.MinY && blob.CentroidY <= box.MaxY));

            Assert.Equal(4, recovered);
        }
    }
}
