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

Each kernel is an independent CMake project (there is no top-level `native/CMakeLists.txt`),
so it is configured and built from its own directory.

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
a loop over the five directories):

```powershell
# From the repo root — example: the scoring kernel
cmake -B build/native/portfolio_scoring -S native/portfolio_scoring -G "Visual Studio 17 2022" -A x64
cmake --build build/native/portfolio_scoring --config Release
```

The post-build step automatically copies the built library (e.g. `portfolio_scoring.dll`)
to `Portfolio.Services/bin/Debug/net10.0/`. To target a different output directory
(e.g. a publish folder):

```powershell
cmake -B build/native/portfolio_scoring -S native/portfolio_scoring `
      -G "Visual Studio 17 2022" -A x64 `
      -DDOTNET_OUTPUT_DIR="Portfolio.Web\bin\Release\net10.0\publish"
cmake --build build/native/portfolio_scoring --config Release
```

Swap `portfolio_scoring` for `geostream_processor`, `spatial_geometry_kernel`,
`raster_terrain_kernel`, or `spatial_graph_engine` to build the other kernels.

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
```

---

## Verifying parity with the managed implementation

The scoring kernel has a true native-vs-managed **parity** test (`NativeScoringBridgeTests`) that
asserts the native result matches the managed result, and passes whether or not the native library
is present (absent → both sides run the managed path). Run it:

```powershell
dotnet test Portfolio.Tests --filter "FullyQualifiedName~NativeScoringBridgeTests"
```

The other four kernels (geostream, geometry, raster terrain, spatial graph) are currently covered by
managed-path correctness tests in `SpatialComputeServiceTests` (asserting results with
`NativeAccelerated == false`) rather than dedicated native-parity tests. Run that suite:

```powershell
dotnet test Portfolio.Tests --filter "FullyQualifiedName~SpatialComputeServiceTests"
```

Numeric kernels assert equality within a small floating-point tolerance (rounding only).

---

## File layout

```
native/
├── portfolio_scoring/         ← HomeFinder composite property scoring
├── geostream_processor/       ← GPS telemetry parsing, filtering, grid aggregation, anomaly detection
├── spatial_geometry_kernel/   ← fan triangulation and bounding-box clipping
├── raster_terrain_kernel/     ← hillshade, slope/aspect, Gaussian heatmap
└── spatial_graph_engine/      ← Dijkstra / A* shortest path and service-area computation
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
