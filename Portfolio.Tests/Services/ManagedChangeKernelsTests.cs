using Portfolio.Services.Services.ChangeDetection;

namespace Portfolio.Tests.Services
{
    // Pins the managed change-detection kernels directly (the golden tests pin them through the service).
    public class ManagedChangeKernelsTests
    {
        private static byte[] RandomMask(int width, int height, int seed)
        {
            var random = new Random(seed);
            var mask = new byte[width * height];
            for (var i = 0; i < mask.Length; i++)
                mask[i] = random.NextDouble() < 0.7 ? (byte)1 : (byte)0;
            return mask;
        }

        private static bool Set(byte[] mask, int x, int y, int width, int height) =>
            x >= 0 && x < width && y >= 0 && y < height && mask[y * width + x] != 0;

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 7)]
        [InlineData(7, 1)]
        [InlineData(9, 6)]
        [InlineData(16, 16)]
        public void ErodeOnce_And_DilateOnce_MatchBruteForceWindow(int width, int height)
        {
            var source = RandomMask(width, height, 17 + width * 31 + height);
            var eroded = new byte[source.Length];
            var dilated = new byte[source.Length];

            ManagedChangeKernels.ErodeOnce(source, eroded, width, height);
            ManagedChangeKernels.DilateOnce(source, dilated, width, height);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var all = true;
                    var any = false;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var on = Set(source, x + dx, y + dy, width, height);
                            all &= on;
                            any |= on;
                        }
                    }

                    Assert.Equal(all ? 1 : 0, eroded[y * width + x]);
                    Assert.Equal(any ? 1 : 0, dilated[y * width + x]);
                }
            }
        }

        [Fact]
        public void LabelComponentsManaged_NumbersComponentsInRasterOrderWithBoundsAndCentroids()
        {
            // 6x4. Component 1: the U (merges two provisional labels). Component 2: the vertical
            // pair at x=4. Component 3: a lone pixel.
            string[] rows =
            [
                "#...#.",
                "#.#.#.",
                "###...",
                ".....#"
            ];
            const int width = 6;
            const int height = 4;
            var mask = new byte[width * height];
            var magnitude = new double[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    mask[y * width + x] = rows[y][x] == '#' ? (byte)1 : (byte)0;
                    magnitude[y * width + x] = y * width + x;
                }
            }

            var result = ManagedChangeKernels.LabelComponentsManaged(mask, width, height, magnitude, CancellationToken.None);

            Assert.Equal(3, result.ComponentCount);
            Assert.Equal([6, 2, 1], result.Areas);
            Assert.Equal([0, 4, 5], result.MinX);
            Assert.Equal([0, 0, 3], result.MinY);
            Assert.Equal([2, 4, 5], result.MaxX);
            Assert.Equal([2, 1, 3], result.MaxY);
            Assert.Equal(5.0 / 6.0, result.CentroidX[0], 12);
            Assert.Equal(8.0 / 6.0, result.CentroidY[0], 12);
            Assert.Equal(53.0 / 6.0, result.MeanMagnitude[0], 12);
            Assert.Equal(4.0, result.CentroidX[1], 12);
        }

        [Fact]
        public void LabelComponentsManaged_TooManyComponents_ThrowsWithPinnedMessage()
        {
            const int width = 200;
            const int height = 200;
            var mask = new byte[width * height];
            for (var y = 0; y < height; y += 2)
                for (var x = 0; x < width; x += 2)
                    mask[y * width + x] = 1;

            var ex = Assert.Throws<ArgumentException>(() =>
                ManagedChangeKernels.LabelComponentsManaged(mask, width, height, new double[mask.Length], CancellationToken.None));

            Assert.Equal("request", ex.ParamName);
            Assert.StartsWith(
                "The change mask contains more than 5000 connected components; raise the threshold or the open iterations.",
                ex.Message);
        }

        [Fact]
        public void LabelComponentsManaged_ExactlyMaxBlobs_Succeeds()
        {
            // 5000 isolated pixels: 100 columns x 50 rows at spacing 2 -> width 200, height 100.
            const int width = 200;
            const int height = 100;
            var mask = new byte[width * height];
            for (var y = 0; y < height; y += 2)
                for (var x = 0; x < width; x += 2)
                    mask[y * width + x] = 1;

            var result = ManagedChangeKernels.LabelComponentsManaged(mask, width, height, new double[mask.Length], CancellationToken.None);

            Assert.Equal(ManagedChangeKernels.MaxBlobs, result.ComponentCount);
        }
    }
}
