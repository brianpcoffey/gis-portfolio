using Portfolio.Common.DTOs;
using Portfolio.Services.Native;

namespace Portfolio.Tests.Services
{
    // Parity tests that drive the NATIVE kernels directly, rather than through the services
    // (which prefer native when it is available and so cannot be used to compare the two).
    //
    // Every test here is a no-op when the shared library is absent, which is the normal CI
    // and clean-clone state — so the suite stays green either way. When a kernel HAS been
    // built they become real assertions, and they exist because that gap let two genuine
    // defects ship: the whole managed contract was tested and nothing checked that native
    // honoured it.
    //
    // Run them meaningfully by building the kernels first (see native/README.md), or reach
    // for `dotnet run --project Portfolio.Benchmarks`, which parity-checks every kernel.
    public class NativeParityTests
    {
        // ── spatial_geometry_kernel ─────────────────────────────────────────────
        //
        // Regression guard: Geometry_ClipToBoundingBox used to clamp each vertex
        // independently instead of clipping the polygon. That returned a vertex count equal
        // to the input with every outside vertex squashed onto the box edge, never emitted a
        // true edge/box intersection, and returned a degenerate full-size polygon for a
        // subject lying entirely outside the box.

        [Fact]
        public void NativeClip_PolygonStraddlingCorner_EmitsBoxCorner()
        {
            if (!SpatialGeometryNativeBridge.IsAvailable)
                return;

            // Same fixture as the managed contract test: correct clipping of this triangle
            // against [0,1]x[0,1] yields four vertices INCLUDING the (1,1) box corner.
            var result = SpatialGeometryNativeBridge.ClipToBoundingBox(new PolygonClipRequestDto
            {
                MinX = 0,
                MinY = 0,
                MaxX = 1,
                MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = 0.5, Y = 0.5 },
                    new CoordinateDto { X = 2.0, Y = 0.5 },
                    new CoordinateDto { X = 0.5, Y = 2.0 }
                ]
            });

            Assert.True(result.NativeAccelerated);
            Assert.Equal(4, result.Vertices.Count);
            Assert.Contains(result.Vertices, v => Math.Abs(v.X - 1.0) < 1e-9 && Math.Abs(v.Y - 1.0) < 1e-9);
            Assert.All(result.Vertices, v =>
            {
                Assert.InRange(v.X, 0.0, 1.0);
                Assert.InRange(v.Y, 0.0, 1.0);
            });
        }

        [Fact]
        public void NativeClip_PolygonFullyOutsideBox_ReturnsEmpty()
        {
            if (!SpatialGeometryNativeBridge.IsAvailable)
                return;

            var result = SpatialGeometryNativeBridge.ClipToBoundingBox(new PolygonClipRequestDto
            {
                MinX = 0,
                MinY = 0,
                MaxX = 1,
                MaxY = 1,
                Subject =
                [
                    new CoordinateDto { X = 5.0, Y = 5.0 },
                    new CoordinateDto { X = 6.0, Y = 5.0 },
                    new CoordinateDto { X = 6.0, Y = 6.0 }
                ]
            });

            // Clamping would have returned three vertices collapsed onto the corner (1,1).
            Assert.Empty(result.Vertices);
        }

        [Fact]
        public void NativeClip_PolygonFullyInsideBox_IsUnchanged()
        {
            if (!SpatialGeometryNativeBridge.IsAvailable)
                return;

            var subject = new List<CoordinateDto>
            {
                new() { X = 0.2, Y = 0.2 },
                new() { X = 0.8, Y = 0.2 },
                new() { X = 0.8, Y = 0.8 },
                new() { X = 0.2, Y = 0.8 }
            };

            var result = SpatialGeometryNativeBridge.ClipToBoundingBox(new PolygonClipRequestDto
            {
                MinX = 0,
                MinY = 0,
                MaxX = 1,
                MaxY = 1,
                Subject = subject
            });

            Assert.Equal(subject.Count, result.Vertices.Count);
            for (var i = 0; i < subject.Count; i++)
            {
                Assert.Equal(subject[i].X, result.Vertices[i].X, 9);
                Assert.Equal(subject[i].Y, result.Vertices[i].Y, 9);
            }
        }

        [Fact]
        public void NativeClip_MatchesManagedOnARichPolygon()
        {
            if (!SpatialGeometryNativeBridge.IsAvailable)
                return;

            // A star that crosses all four box edges — the shape whose native and managed
            // results diverged by 2,451 vertices before the fix.
            var subject = new List<CoordinateDto>();
            for (var i = 0; i < 64; i++)
            {
                var angle = 2.0 * Math.PI * i / 64;
                var radius = i % 2 == 0 ? 1.6 : 0.55;
                subject.Add(new CoordinateDto { X = 0.5 + radius * Math.Cos(angle), Y = 0.5 + radius * Math.Sin(angle) });
            }

            var request = new PolygonClipRequestDto { MinX = 0, MinY = 0, MaxX = 1, MaxY = 1, Subject = subject };
            var native = SpatialGeometryNativeBridge.ClipToBoundingBox(request);
            var managed = ManagedClip(subject, 0, 0, 1, 1);

            Assert.Equal(managed.Count, native.Vertices.Count);
            for (var i = 0; i < managed.Count; i++)
            {
                Assert.Equal(managed[i].X, native.Vertices[i].X, 9);
                Assert.Equal(managed[i].Y, native.Vertices[i].Y, 9);
            }
        }

        // ── viewshed_kernel ─────────────────────────────────────────────────────
        //
        // Regression guard: the ray walk rounded sample coordinates with std::lround in C++
        // (half away from zero) and Math.Round in C# (half to even), so any ray whose sample
        // landed exactly on .5 walked a different cell. Both now use explicit half-up.

        [Fact]
        public async Task NativeViewshed_MatchesManaged_OnAGridThatForcesMidpointSamples()
        {
            if (!ViewshedNativeBridge.IsAvailable)
                return;

            // An odd-sized grid with the observer at dead centre puts a large number of rays
            // on exact half-cell samples, which is what surfaced the rounding disagreement.
            const int size = 101;
            var elevation = new List<double>(size * size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - size / 2.0;
                    var dy = y - size / 2.0;
                    elevation.Add(120.0 * Math.Exp(-(dx * dx + dy * dy) / 420.0) + ((x * 7 + y * 13) % 11));
                }
            }

            var request = new ViewshedRequestDto
            {
                Width = size,
                Height = size,
                CellSize = 30,
                ObserverX = size / 2,
                ObserverY = size / 2,
                ObserverHeight = 2,
                Elevation = elevation
            };

            Assert.True(ViewshedNativeBridge.TryCompute(request, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var native));
            var managed = await new Portfolio.Services.Services.ViewshedService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Portfolio.Services.Services.ViewshedService>.Instance)
                .ComputeAsync(request);

            // ComputeAsync prefers native, so this only proves equality when both are native;
            // the real comparison is the cell-by-cell one below against an independent walk.
            Assert.Equal(native!.Visibility.Count, managed.Visibility.Count);

            var expected = ManagedViewshed(request);
            Assert.Equal(expected.Count(v => v == 1), native.VisibleCells);
            for (var i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i], native.Visibility[i]);
        }

        // ── Local reference implementations ─────────────────────────────────────
        // Deliberately duplicated rather than reused: these encode the contract the native
        // kernels must honour, so they must not drift if a service is refactored.

        private static List<CoordinateDto> ManagedClip(List<CoordinateDto> subject, double minX, double minY, double maxX, double maxY)
        {
            var poly = subject.Select(p => new CoordinateDto { X = p.X, Y = p.Y }).ToList();
            poly = ClipHalfPlane(poly, p => p.X >= minX, (a, b) => IntersectX(a, b, minX));
            poly = ClipHalfPlane(poly, p => p.X <= maxX, (a, b) => IntersectX(a, b, maxX));
            poly = ClipHalfPlane(poly, p => p.Y >= minY, (a, b) => IntersectY(a, b, minY));
            poly = ClipHalfPlane(poly, p => p.Y <= maxY, (a, b) => IntersectY(a, b, maxY));
            return poly;
        }

        private static List<CoordinateDto> ClipHalfPlane(
            List<CoordinateDto> polygon,
            Func<CoordinateDto, bool> inside,
            Func<CoordinateDto, CoordinateDto, CoordinateDto> intersect)
        {
            var output = new List<CoordinateDto>();
            if (polygon.Count == 0)
                return output;

            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var previous = polygon[(i - 1 + polygon.Count) % polygon.Count];
                if (inside(current))
                {
                    if (!inside(previous))
                        output.Add(intersect(previous, current));
                    output.Add(current);
                }
                else if (inside(previous))
                {
                    output.Add(intersect(previous, current));
                }
            }

            return output;
        }

        private static CoordinateDto IntersectX(CoordinateDto a, CoordinateDto b, double x)
        {
            var t = (x - a.X) / (b.X - a.X);
            return new CoordinateDto { X = x, Y = a.Y + t * (b.Y - a.Y) };
        }

        private static CoordinateDto IntersectY(CoordinateDto a, CoordinateDto b, double y)
        {
            var t = (y - a.Y) / (b.Y - a.Y);
            return new CoordinateDto { X = a.X + t * (b.X - a.X), Y = y };
        }

        private static List<byte> ManagedViewshed(ViewshedRequestDto request)
        {
            var width = request.Width;
            var height = request.Height;
            var elevation = request.Elevation;
            var visibility = new byte[width * height];
            var observerElevation = elevation[request.ObserverY * width + request.ObserverX] + request.ObserverHeight;

            for (var ty = 0; ty < height; ty++)
            {
                for (var tx = 0; tx < width; tx++)
                {
                    if (tx == request.ObserverX && ty == request.ObserverY)
                    {
                        visibility[ty * width + tx] = 1;
                        continue;
                    }

                    var dx = (double)(tx - request.ObserverX);
                    var dy = (double)(ty - request.ObserverY);
                    var steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dy));
                    var targetDistance = Math.Sqrt(dx * dx + dy * dy) * request.CellSize;
                    var targetAngle = (elevation[ty * width + tx] - observerElevation) / targetDistance;

                    var maxAngle = double.NegativeInfinity;
                    var blocked = false;
                    for (var i = 1; i < steps; i++)
                    {
                        var fraction = (double)i / steps;
                        // Explicit half-up, matching both implementations.
                        var cx = (int)Math.Floor(request.ObserverX + dx * fraction + 0.5);
                        var cy = (int)Math.Floor(request.ObserverY + dy * fraction + 0.5);
                        var distance = fraction * targetDistance;
                        if (distance <= 0)
                            continue;

                        var angle = (elevation[cy * width + cx] - observerElevation) / distance;
                        if (angle > maxAngle)
                            maxAngle = angle;
                        if (targetAngle < maxAngle)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    visibility[ty * width + tx] = blocked ? (byte)0 : (byte)1;
                }
            }

            return [.. visibility];
        }
    }
}
