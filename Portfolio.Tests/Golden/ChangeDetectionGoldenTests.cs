using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Golden
{
    // Golden output for ChangeDetectionService, captured before it was split. The shipped synthetic
    // scene (256 x 256, four bands, two epochs) is run with the page's defaults: Otsu
    // threshold, one open iteration, minimum blob area 12. The managed path and the native
    // change_detection_kernel must agree, so these hold with and without the kernels.
    public class ChangeDetectionGoldenTests
    {
        private static ChangeDetectionService NewService() =>
            new(NullLogger<ChangeDetectionService>.Instance);

        private static DetectRequestDto PageDefaults(ChangeSceneDto scene) =>
            new()
            {
                Width = scene.Width,
                Height = scene.Height,
                BandCount = scene.BandCount,
                EpochA = scene.EpochA,
                EpochB = scene.EpochB,
                ThresholdMode = "otsu",
                ManualThreshold = 0.2,
                OpenIterations = 1,
                MinBlobArea = 12
            };

        private static async Task<(ChangeSceneDto Scene, DetectResultDto Result)> RunAsync(double noise)
        {
            var service = NewService();
            var scene = await service.GetSceneAsync(256, 256, noise);
            return (scene, await service.DetectAsync(PageDefaults(scene)));
        }

        public record BlobGolden(int Area, int MinX, int MinY, int MaxX, int MaxY);

        public record SceneGolden(
            double Noise,
            double Threshold,
            int ChangedPixels,
            double HistogramWeightedSum,
            BlobGolden[] Blobs);

        private static readonly SceneGolden Shipped = new(
            0.025, 0.19640672, 1740, 1241922,
            [
                new(768, 166, 53, 196, 90),
                new(484, 143, 179, 164, 200),
                new(266, 33, 51, 51, 64),
                new(222, 56, 140, 85, 184)
            ]);

        private static readonly SceneGolden Noisy = new(
            0.06, 0.18861639, 1766, 1985819,
            [
                new(788, 166, 53, 196, 90),
                new(484, 143, 179, 164, 200),
                new(266, 33, 51, 51, 64),
                new(228, 56, 140, 85, 184)
            ]);

        public static TheoryData<double> Noises => [0.025, 0.06];

        private static SceneGolden For(double noise) => noise < 0.05 ? Shipped : Noisy;

        [Theory]
        [MemberData(nameof(Noises))]
        public async Task PageDefaults_ProducePinnedThresholdAndChangedPixels(double noise)
        {
            // Arrange
            var golden = For(noise);

            // Act
            var (_, result) = await RunAsync(noise);

            // Assert
            Assert.Equal("otsu", result.ThresholdMode);
            Assert.Equal(golden.Threshold, result.Threshold, 1e-4);
            Assert.Equal(golden.ChangedPixels, result.ChangedPixels);
            Assert.Equal(golden.ChangedPixels, result.Mask.Sum(m => (long)m));
            Assert.Equal(4, result.BlobsBeforeFiltering);
        }

        [Theory]
        [MemberData(nameof(Noises))]
        public async Task PageDefaults_ProducePinnedBlobBoxes(double noise)
        {
            // Arrange
            var golden = For(noise);

            // Act
            var (_, result) = await RunAsync(noise);

            // Assert
            Assert.Equal(golden.Blobs.Length, result.Blobs.Count);
            for (var i = 0; i < golden.Blobs.Length; i++)
            {
                var expected = golden.Blobs[i];
                var actual = result.Blobs[i];
                Assert.Equal(expected, new BlobGolden(actual.Area, actual.MinX, actual.MinY, actual.MaxX, actual.MaxY));
            }
        }

        [Theory]
        [MemberData(nameof(Noises))]
        public async Task PageDefaults_ProducePinnedHistogramChecksum(double noise)
        {
            // Arrange
            var golden = For(noise);

            // Act
            var (_, result) = await RunAsync(noise);

            // Assert
            Assert.Equal(128, result.Histogram.Count);
            Assert.Equal(256 * 256, result.Histogram.Sum(h => (long)h));
            Assert.Equal(golden.HistogramWeightedSum, result.Histogram.Select((h, i) => (long)h * (i + 1)).Sum());
        }

        [Theory]
        [MemberData(nameof(Noises))]
        public async Task EachGroundTruthBox_IsOverlappedByADetectedBlob(double noise)
        {
            // Act
            var (scene, result) = await RunAsync(noise);

            // Assert
            Assert.Equal(4, scene.GroundTruth.Count);
            Assert.All(scene.GroundTruth, truth =>
                Assert.Contains(result.Blobs, b =>
                    b.MinX <= truth.MaxX && b.MaxX >= truth.MinX &&
                    b.MinY <= truth.MaxY && b.MaxY >= truth.MinY));
        }

        // A U whose two arms get different provisional labels on the way down and are only
        // joined by the bottom bar, so the label merge (union-find) has to run.
        [Fact]
        public async Task UShapedMask_MergesBothArmsIntoOneBlob()
        {
            // Arrange
            const int size = 12;
            var epochB = new double[size * size];
            for (var y = 1; y <= 6; y++)
            {
                epochB[y * size + 1] = 1.0;
                epochB[y * size + 5] = 1.0;
            }
            for (var x = 2; x <= 4; x++)
                epochB[6 * size + x] = 1.0;

            // Act
            var result = await NewService().DetectAsync(new DetectRequestDto
            {
                Width = size,
                Height = size,
                BandCount = 1,
                EpochA = [.. new double[size * size]],
                EpochB = [.. epochB],
                ThresholdMode = "manual",
                ManualThreshold = 0.5,
                OpenIterations = 0,
                MinBlobArea = 0
            });

            // Assert
            Assert.Equal(15, result.ChangedPixels);
            var blob = Assert.Single(result.Blobs);
            Assert.Equal(15, blob.Area);
            Assert.Equal((1, 1, 5, 6), (blob.MinX, blob.MinY, blob.MaxX, blob.MaxY));
        }

        private static DetectRequestDto Valid() =>
            new()
            {
                Width = 4,
                Height = 4,
                BandCount = 1,
                EpochA = [.. new double[16]],
                EpochB = [.. new double[16]],
                ThresholdMode = "manual",
                ManualThreshold = 0.5
            };

        private static readonly (Action<DetectRequestDto> Break, string Message)[] InvalidCases =
        [
            (r => r.Width = 0, "Raster dimensions must be greater than zero."),
            (r => r.Height = -1, "Raster dimensions must be greater than zero."),
            (r => r.Width = 513, "Raster dimensions are limited to 512."),
            (r => r.BandCount = 0, "Band count must be between 1 and 8."),
            (r => r.BandCount = 9, "Band count must be between 1 and 8."),
            (r => r.EpochA = null!, "Both epochs are required."),
            (r => r.EpochB = null!, "Both epochs are required."),
            (r => r.EpochB = [.. new double[15]], "Both epochs must have identical dimensions."),
            (r => { r.EpochA = [.. new double[17]]; r.EpochB = [.. new double[17]]; }, "Epoch raster length must equal width * height * bandCount."),
            (r => r.OpenIterations = -1, "Open iterations must be between 0 and 5."),
            (r => r.OpenIterations = 6, "Open iterations must be between 0 and 5."),
            (r => r.MinBlobArea = -1, "Minimum blob area cannot be negative."),
            (r => r.ThresholdMode = "adaptive", "Threshold mode must be 'otsu' or 'manual'."),
            (r => r.ManualThreshold = -0.1, "Manual threshold must be a finite non-negative value."),
            (r => r.ManualThreshold = double.NaN, "Manual threshold must be a finite non-negative value."),
            (r => r.EpochA[3] = double.NaN, "Raster values must be finite."),
            (r => r.EpochB[7] = double.PositiveInfinity, "Raster values must be finite.")
        ];

        public static TheoryData<int> InvalidCaseIndexes => [.. Enumerable.Range(0, InvalidCases.Length)];

        [Theory]
        [MemberData(nameof(InvalidCaseIndexes))]
        public async Task Detect_InvalidRequest_ThrowsArgumentExceptionWithPinnedMessage(int index)
        {
            // Arrange
            var (breakRule, message) = InvalidCases[index];
            var request = Valid();
            breakRule(request);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().DetectAsync(request));

            // Assert
            Assert.StartsWith(message, ex.Message);
        }

        [Theory]
        [InlineData(0, 8, 0.0, "Raster dimensions must be greater than zero.")]
        [InlineData(8, 513, 0.0, "Raster dimensions are limited to 512.")]
        [InlineData(8, 8, -0.01, "Noise level must be between 0 and 0.3.")]
        [InlineData(8, 8, 0.31, "Noise level must be between 0 and 0.3.")]
        [InlineData(8, 8, double.NaN, "Noise level must be between 0 and 0.3.")]
        public async Task GetScene_InvalidArguments_ThrowsPinnedMessage(int width, int height, double noise, string message)
        {
            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => NewService().GetSceneAsync(width, height, noise));

            // Assert
            Assert.StartsWith(message, ex.Message);
        }
    }
}
