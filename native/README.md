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

`cat_risk_kernel` measures **~1.1×** against its managed fallback, with bit-identical outputs:

| Workload | Native | Managed | Speedup |
|---|---:|---:|---:|
| Ring accumulation, 900 locations | 5.4 ms | 5.9 ms | 1.09× |
| Ring accumulation, 5,000 locations | 166 ms | 186 ms | 1.12× |
| Simulation, 900 × 5,000 events | 51.6 ms | 56.3 ms | 1.09× |
| Simulation, 5,000 × 12,000 events | 568 ms | 645 ms | 1.13× |

`change_detection_kernel` measures **1.24×** over its four compute stages, also with
bit-identical outputs (512×512×4 stack, warm, best of 50 per stage):

| Stage | Native | Managed | Speedup |
|---|---:|---:|---:|
| CVA magnitude | 1.18 ms | 1.72 ms | 1.46× |
| Otsu threshold + histogram | 0.24 ms | 0.50 ms | 2.11× |
| Morphological open, 2 iterations | 2.90 ms | 3.16 ms | 1.09× |
| Connected components | 0.62 ms | 0.75 ms | 1.20× |
| Compute total | 4.93 ms | 6.13 ms | 1.24× |
| End-to-end `DetectAsync` | 7.74 ms | 8.45 ms | 1.09× |

The gradient inside that table is the useful result: the flatter and more branch-free the
inner loop, the more native buys. Otsu's single histogram pass wins most; morphological
open, which short-circuits on nearly every pixel, barely beats the JIT. End-to-end is lower
than compute because roughly a third of `DetectAsync` is `List<double>` ⇄ `double[]`
conversion at the DTO edge, which is identical on both paths.

`spatial_graph_engine`, over the real 2,530-node / 3,187-edge Redlands network:

| Workload | Native | Managed | Speedup |
|---|---:|---:|---:|
| One-to-all Dijkstra from Esri HQ | 6.2 ms | 10.3 ms | 1.66× |
| 40×40 cost matrix | 32.6 ms | 38.8 ms | 1.19× |

`network_trace_kernel`, integer graph traversal with no floating point, so checksums are
exactly equal rather than merely close:

| Workload | Native | Managed | Speedup |
|---|---:|---:|---:|
| Trace + restore, 267-element circuit, 1,290 faults | 149 ms | 192 ms | 1.29× |
| Energization sweeps only, 4,603 elements, 720 sweeps | 413 ms | 423 ms | 1.02× |
| Trace + restore, 4,603-element circuit, 600 faults | 882 ms | 768 ms | **0.87×** |

That last row is native **losing**. A trace is three P/Invoke calls, each re-marshalling the
whole element array; at 267 elements that is noise and the CSR traversal wins, at 4,603 it
dominates. Worth keeping in the table rather than quietly dropping.

`facility_location_kernel`:

| Workload | Native | Managed | Speedup |
|---|---:|---:|---:|
| Shipped scenario 450×24, p=4, p90 | 8.5 ms | 10.6 ms | 1.25× |
| Synthetic 1,600×120, p=10, p90 | 1,164 ms | 1,685 ms | 1.45× |
| Synthetic 1,600×120, p=10, weighted mean | 12.0 ms | 26.8 ms | 2.23× |

The mean objective shows the bigger gap because it is a flat arithmetic loop; the p90
objective spends most of its time sorting, and `Array.Sort` through a comparer delegate
versus an inlined `std::sort` compresses the ratio.

`vrp_solver_kernel`, solve time only (matrix build and polyline expansion excluded):

| Workload | Native | Managed | Speedup |
|---|---:|---:|---:|
| 60 stops, 5 improvement passes | 0.64 ms | 1.66 ms | 2.59× |
| 90 stops, 31 passes | 4.89 ms | 11.41 ms | 2.33× |
| 120 stops, 26 passes | 7.33 ms | 16.78 ms | 2.29× |

### The lesson: it is not about the language

**Two kernels were slower than the C# they were written to replace on first implementation**,
and both for the same reason — allocation and hashing inside the hot loop:

- `network_trace_kernel` used `std::unordered_map<int, std::vector<int>>` for adjacency and
  measured **2.5× slower** than managed: a heap allocation per node plus a hash lookup per
  edge visit. Densified node ids, a CSR incidence array and cached per-element endpoint
  indices — no hashing in the inner loop — turned it into a 1.29× win.
- `vrp_solver_kernel` allocated a fresh `std::vector` per candidate move and measured
  **1.7× slower** (27 ms vs 16 ms at n=120). Hoisting three scratch buffers and reusing
  capacity via `assign` produced the 2.29× win.

The .NET nursery allocator beats naive `malloc`/`new` in a tight loop. If a kernel here is
losing to its fallback, look for allocation or hashing in the inner loop before blaming the
runtime. **`spatial_graph_engine` still uses `std::map<int, std::vector<Edge>>`** — the same
losing shape — and is a prime candidate for a CSR rewrite.

RyuJIT generates good scalar code for tight `double` loops, branch-heavy inner loops defeat
auto-vectorisation on both sides, and P/Invoke array pinning costs part of the rest. **Seven
kernels remain unmeasured and should be assumed to sit in the 1–2× range until they are.**
The value demonstrated is the ABI boundary and the verified-identical fallback, not
throughput. Making native genuinely win needs explicit SIMD intrinsics, thread-level
parallelism, or batching to amortise the crossing — none of which any kernel does today.

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
