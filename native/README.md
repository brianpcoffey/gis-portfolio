# Native Scoring Kernel

C++ shared library that implements the HomeFinder composite property-scoring
kernel. Called from `Portfolio.Services` via P/Invoke through
`NativeScoringBridge`. The managed `HomeScoringService` retains a full
fallback path so the application runs correctly if the native library is
absent.

---

## Prerequisites

| Tool | Minimum version |
|---|---|
| CMake | 3.25 |
| MSVC (Windows) | VS 2022 17.x (`cl.exe` with C++20 support) |
| GCC / Clang (Linux/macOS) | GCC 12 or Clang 15 |

---

## Building on Windows (Visual Studio)

```powershell
# From the repo root
cmake -B build/native -S native/portfolio_scoring -G "Visual Studio 17 2022" -A x64
cmake --build build/native --config Release
```

The post-build step automatically copies `portfolio_scoring.dll` to
`Portfolio.Services/bin/Debug/net10.0/`.  To target a different output
directory (e.g. a publish folder):

```powershell
cmake -B build/native -S native/portfolio_scoring `
      -G "Visual Studio 17 2022" -A x64 `
      -DDOTNET_OUTPUT_DIR="Portfolio.Web\bin\Release\net10.0\publish"
cmake --build build/native --config Release
```

---

## Building on Linux / macOS

```bash
# From the repo root
cmake -B build/native -S native/portfolio_scoring \
      -DCMAKE_BUILD_TYPE=Release
cmake --build build/native
```

Output: `build/native/portfolio_scoring.so`

Copy to the .NET publish directory before running:

```bash
cp build/native/portfolio_scoring.so \
   Portfolio.Services/bin/Debug/net10.0/
```

---

## Running with the .NET application

`NativeScoringBridge` calls `NativeLibrary.TryLoad("portfolio_scoring")`
at startup.  The runtime resolves the library in this order:

1. The directory containing `Portfolio.Services.dll` (set via
   `[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]`).
2. Standard OS library paths (`PATH` on Windows, `LD_LIBRARY_PATH` on Linux).

If the library is not found the bridge logs a warning and the service falls
back transparently to the managed C# implementation.

---

## Docker

Add this to the `Dockerfile` after the `dotnet publish` stage to include the
native library in the image:

```dockerfile
COPY --from=build /src/build/native/portfolio_scoring.so /app/publish/
```

---

## Verifying parity with the managed implementation

Run the `NativeScoringBridgeTests` test class:

```powershell
dotnet test Portfolio.Tests --filter "FullyQualifiedName~NativeScoringBridgeTests"
```

Each test asserts that the native composite score matches the managed score
within a tolerance of ±0.01 points (floating-point rounding only).

---

## File layout

```
native/
└── portfolio_scoring/
    ├── CMakeLists.txt
    ├── include/
    │   └── portfolio_scoring.h    ← exported C ABI; keep in sync with NativeStructs.cs
    └── src/
        └── scoring_kernel.cpp
```
