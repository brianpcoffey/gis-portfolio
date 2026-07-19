using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    /// <summary>
    /// A deterministic synthetic two-epoch, four-band multitemporal stack over a single
    /// AOI, with four labelled changes planted in epoch B so detections can be scored
    /// against ground truth.
    ///
    /// Bands are modelled loosely on Landsat surface reflectance — Red, Green, NIR, SWIR —
    /// so NDVI is computable and the false-colour composite the UI renders (NIR/Red/Green)
    /// is the real vegetation-forward composite an analyst would reach for.
    ///
    /// Generated from a fixed-seed LCG, so the same arguments always produce a
    /// byte-identical scene. No database, no persistence: stateless in the same shape as
    /// <c>RedlandsRoadNetwork</c> and <c>SoCalPolicyBook</c>.
    /// </summary>
    internal static class SyntheticChangeScene
    {
        public const string SceneName = "Cajon Foothills AOI — 4-band, 2-epoch stack";

        /// <summary>Band order of the stack.</summary>
        public static readonly string[] BandNames = ["Red", "Green", "NIR", "SWIR"];

        public const int BandCount = 4;

        // Calibrated against the measured CVA magnitude distribution of this scene rather
        // than picked as a round number. At sigma = 0.025 the unchanged population's CVA
        // magnitude sits around 0.07 while every planted change exceeds 0.30, which leaves
        // Otsu a genuinely bimodal histogram and morphological open real speckle to remove.
        public const double DefaultNoiseLevel = 0.025;

        // Real acquisitions differ in illumination. A uniform brightness offset on epoch B
        // is what makes CVA the right tool rather than naive single-band differencing.
        private const double EpochBBrightnessOffset = 0.015;

        private const int Red = 0;
        private const int Green = 1;
        private const int Nir = 2;
        private const int Swir = 3;

        // Reflectance signatures, in band order.
        private static readonly double[] Water = [0.04, 0.06, 0.02, 0.01];
        private static readonly double[] Vegetation = [0.05, 0.09, 0.45, 0.20];
        private static readonly double[] BareSoil = [0.28, 0.26, 0.32, 0.38];
        private static readonly double[] Asphalt = [0.12, 0.12, 0.13, 0.14];
        private static readonly double[] BurnedGround = [0.16, 0.13, 0.12, 0.42];
        private static readonly double[] Sediment = [0.22, 0.20, 0.24, 0.26];
        private static readonly double[] SolarPanel = [0.06, 0.06, 0.08, 0.10];
        private static readonly double[] FreshConcrete = [0.34, 0.33, 0.36, 0.34];

        public static ChangeSceneDto Build(int width, int height, double noiseLevel)
        {
            var pixels = width * height;
            var epochA = new double[pixels * BandCount];
            var epochB = new double[pixels * BandCount];

            var seed = 20260719;

            // ── Base land cover, identical in both epochs ────────────────────
            var terrain = ValueNoise(width, height, ref seed);
            var riverHalfWidth = Math.Max(2, width / 64);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var cover = BaseCover(x, y, width, height, terrain[index], riverHalfWidth);

                    // Terrain shading modulates every band together, so it does not create
                    // spurious spectral change between epochs.
                    var shade = 1.0 + (terrain[index] - 0.5) * 0.18;
                    for (var b = 0; b < BandCount; b++)
                    {
                        var value = cover[b] * shade;
                        epochA[b * pixels + index] = value;
                        epochB[b * pixels + index] = value;
                    }
                }
            }

            // ── Planted changes: paint the "before" state into epoch A and the ──
            // ── "after" state into epoch B, so each delta is exact by construction ──
            var groundTruth = new List<GroundTruthBoxDto>();

            groundTruth.Add(PlantRectangle(
                epochA, epochB, width, height, pixels,
                "New subdivision",
                (int)(0.130 * width), (int)(0.200 * height),
                Math.Max(6, (int)(0.075 * width)), Math.Max(5, (int)(0.058 * height)),
                Vegetation, FreshConcrete));

            groundTruth.Add(PlantBlob(
                epochA, epochB, width, height, pixels,
                "Burn scar",
                0.700, 0.280, 0.060,
                Vegetation, BurnedGround, ref seed));

            groundTruth.Add(PlantDrawdown(
                epochA, epochB, width, height, pixels, riverHalfWidth));

            groundTruth.Add(PlantRectangle(
                epochA, epochB, width, height, pixels,
                "New solar array",
                (int)(0.560 * width), (int)(0.700 * height),
                Math.Max(6, (int)(0.086 * width)), Math.Max(6, (int)(0.086 * height)),
                BareSoil, SolarPanel));

            // ── Acquisition differences: illumination offset then sensor noise ──
            for (var i = 0; i < epochB.Length; i++)
                epochB[i] += EpochBBrightnessOffset;

            if (noiseLevel > 0)
            {
                AddGaussianNoise(epochA, noiseLevel, ref seed);
                AddGaussianNoise(epochB, noiseLevel, ref seed);
            }

            for (var i = 0; i < epochA.Length; i++)
            {
                epochA[i] = Clamp01(epochA[i]);
                epochB[i] = Clamp01(epochB[i]);
            }

            return new ChangeSceneDto
            {
                Width = width,
                Height = height,
                BandCount = BandCount,
                SceneName = SceneName,
                BandNames = [.. BandNames],
                EpochA = [.. epochA],
                EpochB = [.. epochB],
                GroundTruth = groundTruth,
                NoiseLevel = noiseLevel
            };
        }

        // ── Base scene ──────────────────────────────────────────────────────

        // Land cover from the terrain field plus a river and a road grid. Roads are drawn
        // last so they cut across whatever they pass over, which is what makes the
        // false-colour composite read as a place rather than as noise.
        private static double[] BaseCover(int x, int y, int width, int height, double terrain, int riverHalfWidth)
        {
            var roadSpacingX = Math.Max(8, width / 6);
            var roadSpacingY = Math.Max(8, height / 5);
            if (x % roadSpacingX == 0 || y % roadSpacingY == 0)
                return Asphalt;

            if (Math.Abs(x - RiverCenterX(y, width, height)) <= riverHalfWidth)
                return Water;

            return terrain > 0.52 ? Vegetation : BareSoil;
        }

        // A sinuous north-south drainage across the AOI.
        private static double RiverCenterX(int y, int width, int height)
        {
            var t = height <= 1 ? 0 : (double)y / (height - 1);
            return 0.30 * width + 0.10 * width * Math.Sin(t * 2.0 * Math.PI * 1.4);
        }

        // ── Planted changes ─────────────────────────────────────────────────

        private static GroundTruthBoxDto PlantRectangle(
            double[] epochA, double[] epochB,
            int width, int height, int pixels,
            string label,
            int originX, int originY, int boxWidth, int boxHeight,
            double[] before, double[] after)
        {
            var maxX = Math.Min(width - 1, originX + boxWidth - 1);
            var maxY = Math.Min(height - 1, originY + boxHeight - 1);

            for (var y = originY; y <= maxY; y++)
            {
                for (var x = originX; x <= maxX; x++)
                {
                    var index = y * width + x;
                    for (var b = 0; b < BandCount; b++)
                    {
                        epochA[b * pixels + index] = before[b];
                        epochB[b * pixels + index] = after[b];
                    }
                }
            }

            return new GroundTruthBoxDto { Label = label, MinX = originX, MinY = originY, MaxX = maxX, MaxY = maxY };
        }

        // An irregular blob: a circle whose radius is perturbed by angle so the detection
        // is not a suspiciously perfect shape.
        private static GroundTruthBoxDto PlantBlob(
            double[] epochA, double[] epochB,
            int width, int height, int pixels,
            string label,
            double centerFractionX, double centerFractionY, double radiusFraction,
            double[] before, double[] after,
            ref int seed)
        {
            var centerX = centerFractionX * width;
            var centerY = centerFractionY * height;
            var baseRadius = radiusFraction * Math.Min(width, height);

            // Four fixed harmonics with LCG-drawn phases keep the outline organic and
            // still perfectly reproducible.
            var phase1 = NextDouble(ref seed) * Math.PI * 2;
            var phase2 = NextDouble(ref seed) * Math.PI * 2;

            var minX = width - 1;
            var minY = height - 1;
            var maxX = 0;
            var maxY = 0;
            var painted = false;

            var scanRadius = (int)Math.Ceiling(baseRadius * 1.5);
            for (var y = (int)(centerY - scanRadius); y <= (int)(centerY + scanRadius); y++)
            {
                if (y < 0 || y >= height)
                    continue;

                for (var x = (int)(centerX - scanRadius); x <= (int)(centerX + scanRadius); x++)
                {
                    if (x < 0 || x >= width)
                        continue;

                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    var angle = Math.Atan2(dy, dx);
                    var radius = baseRadius * (1.0 + 0.22 * Math.Sin(3 * angle + phase1) + 0.13 * Math.Sin(5 * angle + phase2));
                    if (distance > radius)
                        continue;

                    var index = y * width + x;
                    for (var b = 0; b < BandCount; b++)
                    {
                        epochA[b * pixels + index] = before[b];
                        epochB[b * pixels + index] = after[b];
                    }

                    painted = true;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (!painted)
            {
                minX = minY = maxX = maxY = 0;
            }

            return new GroundTruthBoxDto { Label = label, MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }

        // Reservoir drawdown: a widened reach of the river in epoch A whose east shoreline
        // is exposed sediment in epoch B. The result is a crescent that hugs the channel.
        private static GroundTruthBoxDto PlantDrawdown(
            double[] epochA, double[] epochB,
            int width, int height, int pixels,
            int riverHalfWidth)
        {
            var startY = (int)(0.550 * height);
            var endY = (int)(0.720 * height);
            var reservoirHalfWidth = riverHalfWidth + Math.Max(4, width / 48);

            var minX = width - 1;
            var maxX = 0;

            for (var y = startY; y <= endY && y < height; y++)
            {
                var center = RiverCenterX(y, width, height);
                for (var x = 0; x < width; x++)
                {
                    var offset = x - center;
                    if (offset < -reservoirHalfWidth || offset > reservoirHalfWidth)
                        continue;

                    var index = y * width + x;

                    // The whole reach is impounded water at the first acquisition.
                    for (var b = 0; b < BandCount; b++)
                        epochA[b * pixels + index] = Water[b];

                    // The east bank above the drawn-down level is exposed sediment at the second.
                    var exposed = offset > riverHalfWidth;
                    var after = exposed ? Sediment : Water;
                    for (var b = 0; b < BandCount; b++)
                        epochB[b * pixels + index] = after[b];

                    if (exposed)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }
            }

            if (minX > maxX)
            {
                minX = 0;
                maxX = 0;
            }

            return new GroundTruthBoxDto
            {
                Label = "Reservoir drawdown",
                MinX = minX,
                MinY = startY,
                MaxX = maxX,
                MaxY = Math.Min(height - 1, endY)
            };
        }

        // ── Noise ───────────────────────────────────────────────────────────

        // Box-Muller over the shared LCG. Both normals from each pair are consumed so the
        // sequence never wastes a draw.
        private static void AddGaussianNoise(double[] raster, double sigma, ref int seed)
        {
            for (var i = 0; i < raster.Length; i += 2)
            {
                var u1 = Math.Max(NextDouble(ref seed), 1e-12);
                var u2 = NextDouble(ref seed);
                var magnitude = sigma * Math.Sqrt(-2.0 * Math.Log(u1));
                raster[i] += magnitude * Math.Cos(2.0 * Math.PI * u2);
                if (i + 1 < raster.Length)
                    raster[i + 1] += magnitude * Math.Sin(2.0 * Math.PI * u2);
            }
        }

        // ── Value noise ─────────────────────────────────────────────────────

        // Three octaves of bilinearly interpolated lattice noise, normalised to 0..1.
        private static double[] ValueNoise(int width, int height, ref int seed)
        {
            var field = new double[width * height];
            double[] amplitudes = [0.55, 0.30, 0.15];
            int[] cells = [4, 9, 19];

            for (var octave = 0; octave < amplitudes.Length; octave++)
            {
                var cellCount = cells[octave];
                var lattice = new double[(cellCount + 1) * (cellCount + 1)];
                for (var i = 0; i < lattice.Length; i++)
                    lattice[i] = NextDouble(ref seed);

                for (var y = 0; y < height; y++)
                {
                    var fy = (double)y / height * cellCount;
                    var y0 = (int)fy;
                    var ty = Smooth(fy - y0);
                    var y1 = Math.Min(y0 + 1, cellCount);

                    for (var x = 0; x < width; x++)
                    {
                        var fx = (double)x / width * cellCount;
                        var x0 = (int)fx;
                        var tx = Smooth(fx - x0);
                        var x1 = Math.Min(x0 + 1, cellCount);

                        var v00 = lattice[y0 * (cellCount + 1) + x0];
                        var v10 = lattice[y0 * (cellCount + 1) + x1];
                        var v01 = lattice[y1 * (cellCount + 1) + x0];
                        var v11 = lattice[y1 * (cellCount + 1) + x1];

                        var top = v00 + (v10 - v00) * tx;
                        var bottom = v01 + (v11 - v01) * tx;
                        field[y * width + x] += amplitudes[octave] * (top + (bottom - top) * ty);
                    }
                }
            }

            return field;
        }

        private static double Smooth(double t) => t * t * (3.0 - 2.0 * t);

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        // Park-Miller LCG, the same generator every other synthetic dataset in the
        // portfolio uses.
        private static double NextDouble(ref int seed)
        {
            seed = (int)((long)seed * 48271 % 2147483647);
            if (seed <= 0)
                seed += 2147483646;
            return seed / 2147483647.0;
        }
    }
}
