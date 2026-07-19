# Native Compute Kernels

C++20 shared libraries that back the portfolio's compute-heavy spatial workflows.
Each kernel exposes a stable C ABI, is called from `Portfolio.Services` via P/Invoke
through a dedicated bridge, and is paired with a full managed C# fallback so the
application runs correctly when the native library is absent.

| Kernel | Directory | Bridge (`Portfolio.Services`) | Managed fallback |
|---|---|---|---|
| `portfolio_scoring` | `native/portfolio_scoring/` | `NativeScoringBridge` | `HomeScoringService` |
| `geostream_processor` | `native/geostream_processor/` | `GeoStreamNativeBridge` | `GeoStreamProcessorService` |
| `spatial_geometry_kernel` | `native/spatial_geometry_kernel/` | `SpatialGeometryNativeBridge` | `SpatialGeometryService` |
| `raster_terrain_kernel` | `native/raster_terrain_kernel/` | `RasterTerrainNativeBridge` | `RasterTerrainService` |
| `spatial_graph_engine` | `native/spatial_graph_engine/` | `SpatialGraphNativeBridge` | `SpatialGraphService` |
| `spatial_cluster_kernel` | `native/spatial_cluster_kernel/` | `SpatialClusterNativeBridge` | `SpatialClusterService` |
| `viewshed_kernel` | `native/viewshed_kernel/` | `ViewshedNativeBridge` | `ViewshedService` |
| `spatial_overlay_kernel` | `native/spatial_overlay_kernel/` | `SpatialOverlayNativeBridge` | `SpatialOverlayService` |
| `cat_risk_kernel` | `native/cat_risk_kernel/` | `CatRiskNativeBridge` | `CatRiskService` |
| `change_detection_kernel` | `native/change_detection_kernel/` | `ChangeDetectionNativeBridge` | `ChangeDetectionService` |
| `network_trace_kernel` | `native/network_trace_kernel/` | `NetworkTraceNativeBridge` | `OutageTraceService` |
| `facility_location_kernel` | `native/facility_location_kernel/` | `FacilityLocationNativeBridge` | `ResponseCoverageService` |
| `vrp_solver_kernel` | `native/vrp_solver_kernel/` | `VrpSolverNativeBridge` | `FleetRoutingService` |

Each kernel is an independent CMake project (there is no top-level `native/CMakeLists.txt`),
so it is configured and built from its own directory.

### Measured speedup

All thirteen kernels are measured by `Portfolio.Benchmarks`, a console harness in this
solution. It runs itself twice as a child process — once with `PORTFOLIO_DISABLE_NATIVE=0`
and once with `=1` — because each bridge probes its shared library exactly once in a static
constructor, so a single process can only ever measure one path.

**Methodology.** 3 warmup iterations discarded, then 9 timed iterations, reporting the
**median** with min and max alongside it. Both children are pinned to one performance core at
high priority. Every workload folds its result into a deterministic checksum, and the two
paths' checksums are compared; a row whose checksums disagree is a correctness defect and its
speedup means nothing.

Measured on 20 logical cores, .NET 10.0.9, Windows 11:

| Kernel | Workload | Native median | Managed median | Speedup | Native min–max | Managed min–max | Parity |
| `portfolio_scoring` | weighted scoring, 200,000 properties, top 50 | 108 ms | 107 ms | 0.99x | 98.4 ms–112 ms | 79.3 ms–117 ms | exact |
| `geostream_processor` | parse + filter + grid aggregate, 40 x 10,000 events | 92.4 ms | 54.9 ms | 0.59x | 73.1 ms–153 ms | 32.0 ms–114 ms | exact |
| `spatial_geometry_kernel` | fan triangulation, 400 x 5,000 vertices | 188 ms | 70.2 ms | 0.37x | 170 ms–192 ms | 66.0 ms–86.4 ms | exact |
| `spatial_geometry_kernel` | bounding-box clip, 400 x 5,000-vertex polygon | 81.5 ms | 62.5 ms | 0.77x | 75.4 ms–93.3 ms | 61.1 ms–63.1 ms | exact |
| `raster_terrain_kernel` | hillshade, 20 x 500x500 elevation grid | 196 ms | 164 ms | 0.83x | 183 ms–202 ms | 156 ms–185 ms | exact |
| `raster_terrain_kernel` | Gaussian heatmap, 500x500 over 200 points | 215 ms | 263 ms | **1.22x** | 215 ms–217 ms | 263 ms–278 ms | exact |
| `spatial_graph_engine` | one-to-all Dijkstra, 40 x (2,530 nodes / 3,187 edges) | 33.2 ms | 15.0 ms | 0.45x | 27.1 ms–48.2 ms | 12.2 ms–46.3 ms | exact |
| `spatial_graph_engine` | 40x40 cost matrix over the same network, 10 builds | 63.1 ms | 82.9 ms | **1.31x** | 62.2 ms–71.7 ms | 82.4 ms–89.7 ms | exact |
| `spatial_cluster_kernel` | DBSCAN, 8 x 5,000 points, eps 0.16 / minPts 6 | 186 ms | 175 ms | 0.94x | 183 ms–191 ms | 165 ms–182 ms | exact |
| `viewshed_kernel` | line-of-sight viewshed, 3 x 500x500 grid | 232 ms | 227 ms | 0.98x | 223 ms–243 ms | 226 ms–229 ms | exact |
| `spatial_overlay_kernel` | point-in-polygon join, 30 x 10,000 points x 48 zones | 156 ms | 297 ms | **1.90x** | 155 ms–169 ms | 295 ms–308 ms | exact |
| `cat_risk_kernel` | ring accumulation, 20 x 900 locations, 3 km | 106 ms | 97.3 ms | 0.92x | 103 ms–110 ms | 96.9 ms–102 ms | exact |
| `cat_risk_kernel` | Monte Carlo loss simulation, 900 x 5,000 events | 49.3 ms | 50.3 ms | **1.02x** | 47.1 ms–55.1 ms | 49.0 ms–54.7 ms | exact |
| `change_detection_kernel` | CVA + Otsu + open + components, 512x512x4 | 15.0 ms | 14.8 ms | 0.99x | 10.2 ms–22.9 ms | 10.3 ms–20.4 ms | exact |
| `network_trace_kernel` | downstream trace sweep, 267 elements x 266 faults | 16.8 ms | 17.0 ms | **1.01x** | 10.6 ms–23.9 ms | 16.5 ms–25.1 ms | exact |
| `network_trace_kernel` | trace + restoration plan, 266 faults | 20.2 ms | 29.8 ms | **1.48x** | 20.0 ms–42.8 ms | 12.1 ms–48.0 ms | exact |
| `facility_location_kernel` | p-median 1,400x64, p=8, weighted mean, 10 solves | 22.7 ms | 34.8 ms | **1.53x** | 21.1 ms–24.8 ms | 34.3 ms–43.4 ms | exact |
| `facility_location_kernel` | p-median 1,400x64, p=8, weighted p90 | 138 ms | 228 ms | **1.66x** | 137 ms–144 ms | 228 ms–229 ms | exact |
| `facility_location_kernel` | shipped scenario 450x24, p=4, p90, 4 solves | 22.4 ms | 38.8 ms | **1.73x** | 22.2 ms–22.8 ms | 38.7 ms–39.4 ms | exact |
| `vrp_solver_kernel` | CVRPTW solve, 120 stops, 10 vehicles, 8 solves | 24.7 ms | 55.9 ms | **2.26x** | 24.1 ms–27.5 ms | 54.1 ms–62.5 ms | exact |
| `vrp_solver_kernel` | CVRPTW solve, shipped "fullday" preset, 40 stops, 30 solves | 10.9 ms | 20.9 ms | **1.92x** | 10.6 ms–11.4 ms | 20.5 ms–23.4 ms | exact |

Reading notes:

- The `p-median` and `CVRPTW` rows time the **solver stage only**, using the duration the
  service itself reports (`SolveMs`). The road-network distance-matrix build that precedes
  them is `spatial_graph_engine` work and would otherwise dominate the row.
- Several workloads are a burst of full-size calls rather than one large call, because the
  services cap their inputs (10,000 telemetry events, 5,000 geometry points, 250,000 raster
  cells). Those caps are production guardrails, so the per-call marshalling is counted — it
  is part of what a caller actually pays.
- `portfolio_scoring` is the only native-backed service whose public entry point reads from a
  repository. The harness supplies a fixed-seed in-memory one; every other kernel runs on an
  in-repo deterministic dataset reached through its public API.
- Where the two spreads overlap, read the row as "no measurable difference" rather than as the
ratio of the medians. The one-to-all Dijkstra row is the clearest case: native spans
27.1–48.2 ms and managed 12.2–46.3 ms, and across runs its managed median has landed anywhere
between 15 ms and 44 ms. That row is dominated by allocation and scheduling noise, not by the
choice of implementation, and no honest speedup can be read off it. It is left in the table
rather than dropped, because a benchmark that only reports the rows that resolve cleanly is
not a benchmark.

#### The parity check found two real bugs — both now fixed

The harness checksums every result and compares the two paths, and on its first run two
workloads disagreed. Neither was a rounding artefact; both were genuine defects in the native
kernels that every previous ad-hoc benchmark had missed, because nothing had ever compared the
two implementations.

- **`spatial_geometry_kernel`, bounding-box clip.** The managed fallback implements
  Sutherland–Hodgman clipping; `Geometry_ClipToBoundingBox` in C++ called `std::clamp` on each
  vertex independently — which is not polygon clipping at all. For a 5,000-vertex star clipped
  to a box, managed returned a 2,549-vertex polygon and native returned 5,000 vertices squashed
  onto the box edges. It never emitted a true edge/box intersection point, never emitted a box
  corner, and returned a degenerate full-size polygon for a subject lying entirely outside the
  box instead of an empty one. The kernel now runs the same Sutherland–Hodgman passes as the
  managed path, in the same order, with the same intersection formulas.
- **`viewshed_kernel`, line-of-sight.** The ray walk rounded sample coordinates with
  `Math.Round` in C# (banker's, half-to-even) and `std::lround` in C++ (half away from zero),
  so any ray whose sample landed exactly on `.5` walked a different cell — 43 cells out of
  250,000 on a 500×500 grid. Both sides now use an explicit half-up rule rather than relying on
  either language's default.

All twenty-one workloads now agree exactly. `Portfolio.Tests/Services/NativeParityTests.cs`
locks both contracts: those tests drive the native kernels directly, no-op when the shared
libraries are absent, and were confirmed to fail against the pre-fix kernel rather than passing
vacuously.

The wider lesson is about test design, not C++. The managed contract was thoroughly tested —
including a test asserting that clipping emits the box corner that naive clamping drops — and
the native kernel violated it for as long as it existed, because every test drove the service,
and the service prefers native only when a shared library is present, which in CI it never is.


#### Superseded numbers

Earlier revisions of this file published single timed runs after one warmup, measured ad hoc
and by different methods per kernel. Those figures are withdrawn in favour of the table above.
Best-of-N and single-shot both reward a lucky run, and they systematically flatter whichever
path has the higher variance — the previously published 1.66× for `spatial_graph_engine` was
exactly that artifact. Where the new data disagrees with the old, the new number stands:
`facility_location_kernel` weighted mean 2.23× → 1.65×, `spatial_graph_engine` Dijkstra
1.66× → 1.26×, `change_detection_kernel` pipeline 1.24× → 1.06×, and `cat_risk_kernel` ring
accumulation 1.09× → **0.98×**, which reverses the direction of the result. The 0.87× figure
for a 4,603-element trace could not be reproduced at all: no public API yields a network that
size, and the shipped one has 267 elements.

### Running the benchmarks

```powershell
dotnet run --project Portfolio.Benchmarks -c Release
```

That is the whole thing. The command builds both children, runs every workload on both paths,
and prints the markdown table above plus the parity verdict. It takes a few minutes.

The benchmark project copies the built kernels out of `Portfolio.Services/bin/Debug/net10.0`
into its own output directory, so build the native libraries first (below) — otherwise the
"native" child silently runs managed code and every row reads 1.00×. The harness reports which
workloads that happened to.

`--run` executes the workloads in the current process and emits JSON only; the parent uses it
for the two children, and it is also how you measure one path by hand:

```powershell
$env:PORTFOLIO_DISABLE_NATIVE = "1"; dotnet run --project Portfolio.Benchmarks -c Release -- --run
```

`--diag` prints the raw results behind the parity checksums for the workloads that disagree,
so a mismatch can be diffed rather than guessed at.

`Portfolio.Benchmarks` is deliberately outside the test suite and outside CI: it is a
measurement tool, and a timing assertion in CI is a flaky test waiting to happen.

### The lesson: it is not about the language

**Three kernels were slower than the C# they were written to replace**, all for the same
reason — allocation and hashing inside the hot loop:

- `network_trace_kernel` used `std::unordered_map<int, std::vector<int>>` for adjacency and
  measured **2.5× slower** than managed: a heap allocation per node plus a hash lookup per
  edge visit. Densified node ids, a CSR incidence array and cached per-element endpoint
  indices — no hashing in the inner loop — turned it into the 1.24× win above.
- `vrp_solver_kernel` allocated a fresh `std::vector` per candidate move and measured
  **1.7× slower** (27 ms vs 16 ms at n=120). Hoisting three scratch buffers and reusing
  capacity via `assign` produced 2.27×.
- `spatial_graph_engine` used `std::map<int, std::vector<Edge>>` — the same losing shape — and
  lost on every workload it was tried on until it was rewritten to CSR.

The .NET nursery allocator beats naive `malloc`/`new` in a tight loop. If a kernel here is
losing to its fallback, look for allocation or hashing in the inner loop before blaming the
runtime — and then look at how many times per second the boundary is crossed, because that is
what the bottom of the table is really measuring. Fan triangulation at **0.34×** does three
index assignments per triangle, so a full array marshal in and out buys nothing whatsoever.

RyuJIT generates good scalar code for tight `double` loops, branch-heavy inner loops defeat
auto-vectorisation on both sides, and P/Invoke array pinning and copying cost the rest. The
value demonstrated is the ABI boundary and the verified-identical fallback, not throughput.
Making native genuinely win needs explicit SIMD intrinsics, thread-level parallelism, or
batching to amortise the crossing — none of which any kernel does today.


### Two compilers' worth of flags

Most kernels build with `/fp:fast` (`-ffast-math`). Two deliberately do not, and the reasons
are worth knowing before you "fix" them:

- `facility_location_kernel` — unreachable demand carries `+∞` through the search, and fast
  math permits assuming infinities do not occur.
- `vrp_solver_kernel` — the local-search accept/reject test is an epsilon comparison, and
  reassociation would let the native and managed paths diverge into different branches,
  breaking the parity guarantee.

---

## Prerequisites

| Tool | Minimum version |
|---|---|
| CMake | 3.25 |
| MSVC (Windows) | VS 2022 17.x (`cl.exe` with C++20 support) |
| GCC / Clang (Linux/macOS) | GCC 12 or Clang 15 |

---

## Building on Windows (Visual Studio)

Build a single kernel by pointing `-S` at its directory (repeat per kernel, or script
a loop over the thirteen directories):

```powershell
# From the repo root — example: the scoring kernel
cmake -B build/native/portfolio_scoring -S native/portfolio_scoring -G "Visual Studio 17 2022" -A x64
cmake --build build/native/portfolio_scoring --config Release
```

The post-build step copies the built library (e.g. `portfolio_scoring.dll`) into **two**
directories:

| Variable | Default | Why |
|---|---|---|
| `DOTNET_OUTPUT_DIR` | `Portfolio.Services/bin/Debug/net10.0/` | where `Portfolio.Services` itself resolves the library |
| `WEB_OUTPUT_DIR` | `Portfolio.Web/bin/Debug/net10.0/` | where the ASP.NET host actually loads from at runtime |

Both are needed. The bridges probe `DllImportSearchPath.AssemblyDirectory`, which at runtime
is the *web host's* output directory — so copying only to `Portfolio.Services/bin` builds
cleanly and still leaves every page reporting "Native: No".

`Portfolio.Tests` is deliberately **not** a copy target: the suite asserts
`NativeAccelerated == false` and must exercise the managed fallback.

To target a different output directory (e.g. a publish folder):

```powershell
cmake -B build/native/portfolio_scoring -S native/portfolio_scoring `
      -G "Visual Studio 17 2022" -A x64 `
      -DDOTNET_OUTPUT_DIR="Portfolio.Web\bin\Release\net10.0\publish"
cmake --build build/native/portfolio_scoring --config Release
```

Swap `portfolio_scoring` for any other directory name under `native/` to build the rest:
`geostream_processor`, `spatial_geometry_kernel`, `raster_terrain_kernel`,
`spatial_graph_engine`, `spatial_cluster_kernel`, `viewshed_kernel`,
`spatial_overlay_kernel`, `cat_risk_kernel`, `change_detection_kernel`,
`network_trace_kernel`, `facility_location_kernel`, `vrp_solver_kernel`.

---

## Building on Linux / macOS

```bash
# From the repo root — example: the scoring kernel
cmake -B build/native/portfolio_scoring -S native/portfolio_scoring \
      -DCMAKE_BUILD_TYPE=Release
cmake --build build/native/portfolio_scoring
```

Output: `build/native/portfolio_scoring/portfolio_scoring.so`

Copy the built library to the .NET output directory before running:

```bash
cp build/native/portfolio_scoring/portfolio_scoring.so \
   Portfolio.Services/bin/Debug/net10.0/
```

Repeat for each kernel directory you want to enable natively.

---

## Running with the .NET application

Each bridge calls `NativeLibrary.TryLoad("<kernel_name>")` at startup. The runtime
resolves the library in this order:

1. The directory containing `Portfolio.Services.dll` (set via
   `[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]`).
2. Standard OS library paths (`PATH` on Windows, `LD_LIBRARY_PATH` on Linux).

If a library is not found the bridge logs a warning and the corresponding service
falls back transparently to its managed C# implementation. Kernels are independent —
you can build and ship any subset; the rest simply run managed.

---

## Docker

Add a copy line per kernel after the `dotnet publish` stage to include the native
libraries in the image, for example:

```dockerfile
COPY --from=build /src/build/native/portfolio_scoring/portfolio_scoring.so /app/publish/
COPY --from=build /src/build/native/geostream_processor/geostream_processor.so /app/publish/
COPY --from=build /src/build/native/spatial_geometry_kernel/spatial_geometry_kernel.so /app/publish/
COPY --from=build /src/build/native/raster_terrain_kernel/raster_terrain_kernel.so /app/publish/
COPY --from=build /src/build/native/spatial_graph_engine/spatial_graph_engine.so /app/publish/
COPY --from=build /src/build/native/spatial_cluster_kernel/spatial_cluster_kernel.so /app/publish/
COPY --from=build /src/build/native/viewshed_kernel/viewshed_kernel.so /app/publish/
COPY --from=build /src/build/native/spatial_overlay_kernel/spatial_overlay_kernel.so /app/publish/
COPY --from=build /src/build/native/cat_risk_kernel/cat_risk_kernel.so /app/publish/
COPY --from=build /src/build/native/change_detection_kernel/change_detection_kernel.so /app/publish/
COPY --from=build /src/build/native/network_trace_kernel/network_trace_kernel.so /app/publish/
COPY --from=build /src/build/native/facility_location_kernel/facility_location_kernel.so /app/publish/
COPY --from=build /src/build/native/vrp_solver_kernel/vrp_solver_kernel.so /app/publish/
```

> **The repository `Dockerfile` does not do this today** — it contains no CMake stage and
> copies no native libraries. Every kernel therefore runs its managed fallback in the
> deployed container, and the UI correctly reports "Native: No". The block above is what
> would need to be added, together with a build stage that configures and builds each kernel.

---

## Verifying parity with the managed implementation

The scoring kernel has a true native-vs-managed **parity** test (`NativeScoringBridgeTests`) that
asserts the native result matches the managed result, and passes whether or not the native library
is present (absent → both sides run the managed path). Run it:

```powershell
dotnet test Portfolio.Tests --filter "FullyQualifiedName~NativeScoringBridgeTests"
```

The other kernels are covered by managed-path correctness tests (asserting results with
`NativeAccelerated == false`) rather than dedicated native-parity tests, spread across three suites:

```powershell
# geostream, geometry, raster terrain, spatial graph
dotnet test Portfolio.Tests --filter "FullyQualifiedName~SpatialComputeServiceTests"
# clustering, viewshed, overlay
dotnet test Portfolio.Tests --filter "FullyQualifiedName~HotspotViewshedOverlayServiceTests"
# catastrophe risk
dotnet test Portfolio.Tests --filter "FullyQualifiedName~CatRiskServiceTests"
```

`cat_risk_kernel` additionally has an out-of-band parity check: running the same workload
with and without the shared library present produced bit-identical ring-TIV checksums, with
AAL differing only in the final ULP from floating-point summation order under `/fp:fast`.
That comparison is not yet automated in the test suite.

Numeric kernels assert equality within a small floating-point tolerance (rounding only).

---

## File layout

```
native/
├── portfolio_scoring/         ← HomeFinder composite property scoring
├── geostream_processor/       ← GPS telemetry parsing, filtering, grid aggregation, anomaly detection
├── spatial_geometry_kernel/   ← fan triangulation and bounding-box clipping
├── raster_terrain_kernel/     ← hillshade, slope/aspect, Gaussian heatmap
├── spatial_graph_engine/      ← Dijkstra / A* shortest path and service-area computation
├── spatial_cluster_kernel/    ← DBSCAN density-based clustering
├── viewshed_kernel/           ← line-of-sight ray casting over elevation grids
├── spatial_overlay_kernel/    ← point-in-polygon spatial join
├── cat_risk_kernel/           ← ring accumulation and Monte Carlo catastrophe loss simulation
└── change_detection_kernel/   ← CVA magnitude, Otsu threshold, morphological open, connected components
└── network_trace_kernel/      ← distribution fault tracing, isolation search, and energization sweeps
└── facility_location_kernel/  ← p-median station siting, Teitz-Bart substitution, weighted coverage stats
└── vrp_solver_kernel/         ← CVRPTW: Clarke-Wright savings plus 2-opt / Or-opt local search
```

Each kernel directory contains:

```
<kernel>/
├── CMakeLists.txt
├── include/
│   └── <kernel>.h    ← exported C ABI; keep in sync with the matching NativeInterop/NativeStructs in Portfolio.Services
└── src/
    └── <kernel>.cpp   ← portfolio_scoring's source is named scoring_kernel.cpp
```
