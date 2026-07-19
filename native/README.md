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

| Kernel | Workload | Native | Managed | Speedup | Native min–max | Managed min–max | Parity |
|---|---|---:|---:|---:|---:|---:|---|
| `vrp_solver_kernel` | CVRPTW solve, 120 stops, 8 solves | 24.3 ms | 55.2 ms | **2.27×** | 24.2–25.3 | 53.2–64.0 | exact |
| `vrp_solver_kernel` | CVRPTW solve, shipped "fullday" preset, 40 stops, 30 solves | 10.3 ms | 21.3 ms | **2.06×** | 10.3–10.7 | 20.9–21.8 | exact |
| `spatial_overlay_kernel` | point-in-polygon join, 30 × 10,000 points × 48 zones | 156 ms | 302 ms | **1.94×** | 154–167 | 302–304 | exact |
| `facility_location_kernel` | shipped scenario 450×24, p=4, p90, 4 solves | 21.8 ms | 41.9 ms | **1.92×** | 21.7–22.6 | 41.7–42.6 | exact |
| `facility_location_kernel` | p-median 1,400×64, p=8, weighted p90 | 136 ms | 244 ms | **1.80×** | 135–136 | 243–245 | exact |
| `facility_location_kernel` | p-median 1,400×64, p=8, weighted mean, 10 solves | 22.5 ms | 37.1 ms | **1.65×** | 20.9–44.2 | 34.8–39.9 | exact |
| `spatial_graph_engine` | 40×40 cost matrix, 10 builds | 61.1 ms | 89.3 ms | **1.46×** | 58.9–69.0 | 88.6–96.4 | exact |
| `raster_terrain_kernel` | Gaussian heatmap, 500² over 200 points | 214 ms | 276 ms | **1.29×** | 213–215 | 274–277 | exact |
| `spatial_graph_engine` | one-to-all Dijkstra, 40 × (2,530 nodes / 3,187 edges) | 35.0 ms | 44.2 ms | 1.26× | 26.7–46.7 | 13.0–67.7 | exact |
| `network_trace_kernel` | downstream trace sweep, 267 elements × 266 faults | 16.6 ms | 20.5 ms | **1.24×** | 16.4–16.8 | 20.0–39.3 | exact |
| `network_trace_kernel` | trace + restoration plan, 266 faults | 30.5 ms | 36.1 ms | 1.18× | 30.4–43.3 | 18.2–45.3 | exact |
| `cat_risk_kernel` | Monte Carlo loss simulation, 900 × 5,000 events | 48.4 ms | 52.6 ms | **1.09×** | 46.3–53.1 | 50.6–57.6 | exact |
| `change_detection_kernel` | CVA + Otsu + open + components, 512²×4 | 10.4 ms | 11.1 ms | 1.06× | 9.35–21.2 | 10.3–22.0 | exact |
| `spatial_cluster_kernel` | DBSCAN, 8 × 5,000 points, eps 0.16 / minPts 6 | 183 ms | 188 ms | 1.03× | 178–187 | 177–202 | exact |
| `cat_risk_kernel` | ring accumulation, 20 × 900 locations, 3 km | 102 ms | 100 ms | 0.98× | 101–107 | 98.9–106 | exact |
| `portfolio_scoring` | weighted scoring, 200,000 properties, top 50 | 98.8 ms | 95.6 ms | 0.97× | 92.1–104 | 76.2–113 | exact |
| `raster_terrain_kernel` | hillshade, 20 × 500² elevation grid | 190 ms | 156 ms | **0.82×** | 180–196 | 155–170 | exact |
| `spatial_geometry_kernel` | bounding-box clip, 400 × 5,000-vertex polygon | 67.3 ms | 53.6 ms | **0.80×** | 64.7–84.1 | 52.9–55.0 | **MISMATCH** |
| `viewshed_kernel` | line-of-sight viewshed, 3 × 500² grid | 296 ms | 226 ms | **0.76×** | 295–308 | 224–228 | **MISMATCH** |
| `geostream_processor` | parse + filter + grid aggregate, 40 × 10,000 events | 88.6 ms | 53.9 ms | **0.61×** | 72.0–131 | 30.4–111 | exact |
| `spatial_geometry_kernel` | fan triangulation, 400 × 5,000 vertices | 169 ms | 57.9 ms | **0.34×** | 158–174 | 57.4–74.1 | exact |

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
- Where the two min–max spreads overlap — the Dijkstra row most obviously — read the row as
  "no measurable difference", not as the ratio of the medians.

#### Two parity failures

Nineteen of twenty-one workloads produce bit-identical checksums on both paths. Two do not,
and both are genuine defects in the native kernel rather than measurement noise:

- **`spatial_geometry_kernel`, `Geometry_ClipToBoundingBox`.** The managed fallback runs
  Sutherland–Hodgman polygon clipping; the C++ implementation calls `std::clamp` on each
  vertex independently. Clipping a 5,000-vertex star to a box gives 2,549 vertices managed and
  5,000 vertices squashed onto the box edges natively. Different shapes, not different
  rounding. The two agree only when every vertex already lies inside the box.
- **`viewshed_kernel`, `Viewshed_Compute`.** The ray walk rounds sample coordinates with
  `Math.Round` in C# (banker's rounding, half-to-even) and `std::lround` in C++ (half away
  from zero). Any ray whose sample lands exactly on `.5` walks a different cell — 43 cells out
  of 250,000 on a 500×500 grid.

Both are recorded here rather than quietly fixed alongside a measurement change; the parity
check exists precisely to surface them.

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
