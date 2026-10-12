# Findings: baseline, complexity (Goal 2), refactor and simplify (Goal 3, backend/C#)

Stage 1 analysis was read-only on the repo. The subagent harness blocked me from writing this file, so the coordinator saves it to `docs/cleanup/findings-complexity.md`. I wrote nothing to the repo; an empty `docs/cleanup/` directory I created was removed again.

The analysis ran against commit `9d9ea02`, `main`, 2026-10-12. Every build, test and measurement ran in throwaway clones under the scratch directory (`<scratch>` below):
`C:\Users\brian\AppData\Local\Temp\claude\C--Users-brian-source-repos-Portfolio\6e74e331-4143-4f68-9411-0e96a263f57d\scratchpad\cx`

Line references (`file:line`) point at the current source at `9d9ea02`. Every number below comes from a command that was run; §12 lists the commands and the artifact paths. Out of scope: security, frontend UX/CSS/a11y, comments/XML docs and doc hygiene. Where something in those areas came up, it is passed along in §11.

## 0. Summary for the planner

- **Baseline.** Both builds have **0 warnings and 0 errors**: the CI build (`-p:ContinuousIntegrationBuild=true`, warnings as errors) and a plain local Release build. **697/697 tests pass** with native code absent, with `PORTFOLIO_DISABLE_NATIVE=1`, and with **all 13 C++ kernels built and loaded**. CI-method coverage is **83.64 % line / 67.36 % branch** (gate 80/60). The numbers are identical on SDK 10.0.400-preview and on 10.0.401 (§1.1).
- **Coverage depends on generated code.** The OSM-generated `RedlandsRoadNetwork.cs` is **5,730 of the 14,551 valid lines (39.4 %), all covered**. Without it, line coverage is **73.01 %**, below the 80 % gate. Portfolio.Web is 43 % covered: `Program.cs` is 0/451, and all three middlewares and the health check are at 0 %.
- **Complexity.** Of 841 production members, **35 exceed 15** on cyclomatic (CA1502) or cognitive complexity: **17 by CA1502** and **34 by cognitive**. The worst is `UserProfileSeedService.SeedForUserAsync`, a single 1,444-line method with CA1502 251 and cognitive 236. Next comes Program.cs `<Main>$` at CA1502 61. The rest of the top tier is managed algorithm code in Fleet, ChangeDetection and ResponseCoverage, with cognitive 34–83.
- **The measuring tool is calibrated.** It reproduces the real CA1502 analyzer exactly on **446/446** methods. Its cognitive scores equal clang-tidy's SonarSource implementation on the C++ mirrors (70/70, 30/30, 25/25, 25/25).
- **Program.cs characterization is feasible now.** The generated `Program` class is already **public** in .NET 10, and Web has `InternalsVisibleTo("Portfolio.Tests")`. A scratch probe using `WebApplicationFactory<Program>` booted the *current* Program.cs offline and locked the middleware order: 7/7 tests passed, and they lifted Program.cs from 0 % to 92.5 % line coverage. The only missing piece is the test package (owner decision **OD-1**).
- **The dead-code batch is validated.** I applied the whole proposed dead-code set to a scratch clone: Fiber client repository chain, unread fields, unused repository members, shadowed DI registrations, the InMemory package move, unused global usings, stray `<Folder>` items, the unused xunit pin, the dead `Details` page model and the `double.IsFinite` dedupe, plus the IDE0005 sweep. The result was **0 warnings, 697/697 passing both ways, and coverage 84.19 % line / 67.49 % branch**. The deletions alone are 40 files, +56/−409 lines. With the IDE0005 sweep it is 89 files, +110/−509.
- **Correction: the "deprecated" batch sync endpoint is the live path.** `POST /api/v1/geocoding/batch/sync` is the **only** call the Batch Geocoding demo makes (`wwwroot/js/BatchGeocoding/app.js:194`). It carries `[Obsolete]`, so coverlet excludes it from coverage. The async job endpoints it supposedly replaces have **no frontend caller**. Do not delete it (owner decision **OD-5**).
- **Controller catches cannot simply be deleted.** The 52 controller `catch` blocks are not redundant with `ApiExceptionMiddleware`. The 400 body shape `{ "error": ... }` is load-bearing because the JS reads only `body.error` (`api-fetch.js:59` plus 5 inline readers), while the middleware emits ProblemDetails. The 12 Fiber `catch (Exception)` blocks hide a real defect: unknown ids return **500 instead of 404**.
- **Biggest structural wins, in order:** (1) characterization infrastructure; (2) the validated dead-code batch; (3) Program.cs decomposition; (4) data-driven seed rewrite (−1,300 lines); (5) solver extraction in Fleet, ResponseCoverage and ChangeDetection; (6) a shared native-library loader across 13 bridges (≈ −340 lines).
- **Enforcement.** Use CA1502 at 15 through `CodeMetricsConfig.txt`. CI already turns warnings into errors. Today 17 production methods violate it (7 above 25). Baseline them with `SuppressMessage` entries that each refactor package deletes. Cognitive complexity has no first-party analyzer, so the recommendation is report-only (**OD-8**).

## 1. Baseline (measured in the scratch clone)

### 1.1 Environment, the SDK change during the run, and other notes
- Toolchain: Windows 11 26300, Node 24.17.0, Python 3.13.7, VS 2022 Community. MSVC 19.44.35224 via the VS-bundled CMake 3.31.6-msvc6. LLVM clang-tidy 19.1.5 from VS.
- **The .NET SDK changed during my run.** At about 11:29–11:33 a VS Installer run (`setup.exe`/`msiexec`) removed SDK **10.0.400-preview.0.26322.102** and its .NET 10 runtime. It then installed **SDK 10.0.401 / runtime 10.0.12**. For a few minutes no .NET 10 runtime existed, and `dotnet --info` fell back to SDK 9.0.312. Other agents building at 11:29–11:33 may have seen spurious failures.

| Measurement | When | SDK / runtime |
|---|---|---|
| First baseline: restore, local Release build, CI Release build, Release tests (normal and `PORTFOLIO_DISABLE_NATIVE=1`), coverage gate | 11:25–11:26 | **10.0.400-preview.0.26322.102**. Runtime patch not captured before the installer removed it. |
| Debug build for the native job; first CMake attempt (failed on MAX_PATH); Debug tests without kernels (697/697) | 11:27–11:28 | 10.0.400-preview |
| Second native attempt: 13 kernels built OK, but the tests **aborted** with "You must install or update .NET" | 11:29–11:30 | No .NET 10 runtime existed at that moment (installer). An environment problem, not code. |
| `csmetrics` complexity tool (Roslyn 5.0.0, syntax-only) | 11:30–11:41 | Built and run on SDK 9.0.312 / net9.0. It does not compile the repo, so its results are SDK-independent. |
| Native present (13 kernels staged in the 11:27 Debug output) and native disabled | 11:41 | **10.0.401 / 10.0.12** |
| Baseline re-run: restore, both builds, both test runs, gate | 11:41–11:43 | **10.0.401 / 10.0.12**. **All numbers identical to the first run.** |
| CA1502 + IDE0005/0051/0052 analyzer build | 11:43–11:44 | 10.0.401 |
| Semantic dead-code tool (MSBuildWorkspace), DI lifetime probe, `Program` accessibility probe, dead-code validation clone, `dotnet format`, WebApplicationFactory probe | after 11:44 | 10.0.401 / 10.0.12 |
| JS (Node), C++ (clang-tidy), LOC (Python) | — | No .NET involved |

- **CMake and MAX_PATH.** With `-B build/native/<name>`, the scratch path exceeded MAX_PATH (MSB3491, 260 characters), so all 13 configures failed. I re-ran with short binary directories (`scratchpad\b\kNN`). The repo's CMakeLists are fine; this only affects deep checkout paths.

### 1.2 Build
| Build | Command (in clone) | Warnings | Errors |
|---|---|---|---|
| CI-equivalent | `dotnet build --no-restore -c Release --no-incremental -p:ContinuousIntegrationBuild=true` | **0** | **0** |
| Plain local Release | `dotnet build --no-restore -c Release --no-incremental` | **0** | **0** |
| 13 C++ kernels | `cmake -S native/<k> -B <short> -DCMAKE_BUILD_TYPE=Release && cmake --build <short> --config Release` | **0** compiler warnings (MSVC default level) | 0 (13/13 built, about 3 s each) |

### 1.3 Tests
The test step mirrors `.github/workflows/ci.yml:69-76`: `dotnet test Portfolio.Tests --no-build -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings ...`. The gate is the inline Python from `ci.yml:82-113`, extracted verbatim to `<scratch>\tools\coverage_gate.py` and run with `py -I`.

| Run | Total | Passed | Failed | Skipped | Line | Branch |
|---|---|---|---|---|---|---|
| Release, no native libraries (the CI gate) | 697 | 697 | 0 | 0 | **83.64 %** (12170/14551) | **67.36 %** (2134/3168) |
| Release, `PORTFOLIO_DISABLE_NATIVE=1` | 697 | 697 | 0 | 0 | 83.31 % (12123/14551) | 67.42 % (2136/3168) |
| Debug, **13 kernels staged** (mirrors `native-parity`) | 697 | 697 | 0 | 0 | 77.99 % (Debug; not comparable) | 51.54 % (Debug) |
| Debug, kernels staged, `PORTFOLIO_DISABLE_NATIVE=1` | 697 | 697 | 0 | 0 | n/a | n/a |

To confirm native code actually ran, I compared bridge coverage with the kernels present against the no-native run. For example, `VrpSolverNativeBridge.cs` went from 20.5 % to 72.8 % and `NetworkTraceNativeBridge.cs` from 15.7 % to 74.3 %.

Cobertura XML files (kept, as requested):
- `<scratch>\coverage\normal\coverage.cobertura.xml` (the CI-gate run; `.trx` alongside)
- `<scratch>\coverage\nonative\…`
- `<scratch>\coverage\native\…` (Debug, kernels present)
- `<scratch>\coverage\normal-401\…` and `nonative-401\…` (re-run on SDK 10.0.401)

### 1.4 Coverage structure the planner must know
| Item | Lines covered/valid | Note |
|---|---|---|
| `Portfolio.Services/Data/RedlandsRoadNetwork.cs` (generated) | 5730/5730 | **39.4 % of all valid lines.** Excluding it: **6440/8821 = 73.01 %** line. Branch is unaffected (0 branches). |
| Portfolio.Common | 14/40 (35.0 %) | DTOs are auto-properties (SkipAutoProps). |
| Portfolio.Repositories | 326/682 (47.8 %) | No DB tests. Migrations are excluded. |
| Portfolio.Services | 11178/12321 (90.7 %) | Includes the generated file. |
| Portfolio.Web | 652/1508 (43.2 %) | `Program.cs` 0/451. `AnonymousUserMiddleware` 0/69, `ApiExceptionMiddleware` 0/33, `SecurityHeadersMiddleware` 0/9, `DatabaseReadyHealthCheck` 0/9. |
| `[Obsolete]` members | excluded | `coverlet.runsettings:22` excludes the `Obsolete` attribute. That hides `BatchGeocodingController.GeocodeAddresses`, which is the **live** endpoint (§6.7). |

**Guard for every package:** never mark `RedlandsRoadNetwork.cs` as generated *for coverlet* (`[GeneratedCode]`, `[ExcludeFromCodeCoverage]`, `ExcludeByFile`). Doing so drops line coverage to about 73 % and fails CI. An `.editorconfig` `generated_code = true` entry is safe, because coverlet does not read `.editorconfig`. See OD-7.

### 1.5 LOC by project and language
Counted with `py -I <scratch>\tools\loc.py <clone>` over git-tracked files, excluding `bin/`, `obj/`, `wwwroot/lib/` and `*/Migrations/`. "Code*" means non-blank lines minus comment-only lines; that is a line heuristic.

| Project | Lang | Files | Physical | Non-blank | Code* |
|---|---|---|---|---|---|
| Portfolio.Benchmarks | C# | 4 | 1,578 | 1,395 | 1,242 |
| Portfolio.Common | C# | 60 | 3,209 | 2,561 | 1,489 |
| Portfolio.Repositories | C# | 34 | 1,479 | 1,302 | 1,240 |
| Portfolio.Services | C# | 98 | 13,240 | 11,630 | 10,753 |
| Portfolio.Tests | C# | 50 | 12,194 | 10,303 | 9,047 |
| Portfolio.Web | C# | 72 | 4,424 | 4,052 | 3,371 |
| Portfolio.Web | cshtml | 52 | 6,514 | 6,047 | 5,827 |
| Portfolio.Web | JS | 33 | 9,808 | 8,417 | 7,739 |
| Portfolio.Web | CSS | 16 | 4,889 | 4,195 | 3,824 |
| native | C++ (.cpp/.h) | 26 | 4,122 | 3,632 | 3,132 |
| **Total** | | **445** | **61,457** | **53,534** | **47,664** |

Reported separately (excluded above): **`Portfolio.Services/Data/RedlandsRoadNetwork.cs`, 5,765 physical lines** (generated; never hand-edit). Production C# (Common + Repositories + Services + Web) totals 22,352 physical lines.

## 2. Complexity measurement

### 2.1 Tools, definitions and calibration
- **C#: `<scratch>\tools\csmetrics`.** A Roslyn 5.0.0 console app, syntax only, that walks every `.cs` file. It emits one row per method-like member. Lambdas and local functions are folded into their enclosing member. Top-level statements form one member, and top-level local functions are reported separately. Columns:
  - `cyclomatic_ca1502`: what the **real CA1502 analyzer** reports. I calibrated it by running CA1502 at threshold 1 over the whole solution (453 diagnostics). It is **identical on all 446 methods that join on file:line**. The 7 unjoined rows are `<Main>$`, which also matches (61/61), and six one-line `CurrentUserId` getters that CA1502 reports one line lower. Calibration showed that CA1502 counts `if`, `?:`, loops, non-default `case` labels, `&&`, `||`, `??` and `?.`. It does **not** count `catch`, `and`/`or` patterns, switch-expression arms or `??=`.
  - `cyclomatic`: textbook McCabe. Same as above plus `catch`, pattern combinators, switch-expression arms and `??=`.
  - `cognitive`: SonarSource rules, as implemented by sonar-dotnet:
    - +1 plus nesting for `if`, `?:`, `switch`, loops and `catch`.
    - +1 for `else if`, `else`, `goto`, and each new run of like `&&`/`||`/pattern operators.
    - Recursion uses a name-and-arity heuristic.
    - Lambdas and local functions add nesting.

    It is cross-checked against clang-tidy's `readability-function-cognitive-complexity` on the C++ mirrors: `OrOptMove`/`orOptMove` 70/70, `DilateOnce`/`dilate_once` 30/30, `ErodeOnce`/`erode_once` 25/25, `TwoOptPass`/`twoOptPass` 25/25.
- **JS: `<scratch>\tools\jsmetrics.js`.** Uses a real parser, the acorn 8.16 bundled inside Node (`node --expose-internals`, so no npm install). It follows the ESLint convention: every function is its own unit, nested functions are measured separately, and nesting starts at 0. Cyclomatic counts `if`, `?:`, loops, `case`, `catch`, `&&`, `||`, `??`, logical assignment and `?.`. Cognitive follows the SonarSource rules above.
- **C++.** VS-bundled clang-tidy 19.1.5 with `readability-function-cognitive-complexity` and `readability-function-size`. Branch counts serve as a cyclomatic proxy. `-D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH` was needed because clang picks up the VS 18 Insiders STL.

### 2.2 C# distribution (count of members **above** the threshold)
| Scope | Members | CA1502 >10 | >15 | >25 | McCabe >10 | >15 | >25 | Cognitive >10 | >15 | >25 |
|---|---|---|---|---|---|---|---|---|---|---|
| **Production** (Common+Repos+Services+Web) | 841 | **37** | **17** | **7** | 39 | 18 | 7 | **46** | **34** | **16** |
| Portfolio.Common | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Portfolio.Repositories | 153 | 1 | 0 | 0 | 1 | 1 | 0 | 1 | 1 | 1 |
| Portfolio.Services | 519 | 33 | 15 | 6 | 35 | 15 | 6 | 42 | 31 | 14 |
| Portfolio.Web | 167 | 3 | 2 | 1 | 3 | 2 | 1 | 3 | 2 | 1 |
| Tests | 723 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 1 | 0 |
| Benchmarks | 39 | 1 | 1 | 0 | 1 | 1 | 0 | 1 | 1 | 1 |
| Migrations + generated network | 14 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

Production members above 15 on **either** metric: **35** (above 20: 24; above 25: 16). Since `RedlandsRoadNetwork.Build()` is a single initializer with CA1502 1, the generated file needs no exclusion for CA1502.

### 2.3 Worst 25 production members (sorted by cognitive)
| # | Member | CA1502 | McCabe | Cognitive | LOC | file:line |
|---|---|---|---|---|---|---|
| 1 | `UserProfileSeedService.SeedForUserAsync` | 251 | 252 | 236 | 1444 | `Portfolio.Services/Services/UserProfileSeedService.cs:21` |
| 2 | `ChangeDetectionService.LabelComponentsManaged` | 29 | 29 | 83 | 146 | `Portfolio.Services/Services/ChangeDetectionService.cs:409` |
| 3 | `FleetRoutingService.OrOptMove` | 16 | 16 | 70 | 69 | `Portfolio.Services/Services/FleetRoutingService.cs:506` |
| 4 | `Program <top-level statements>` (CA1502 measures `<Main>$`) | 61 | 42 | 50 | 640 | `Portfolio.Web/Program.cs:31` |
| 5 | `ResponseCoverageService.SolveManaged` | 20 | 20 | 47 | 126 | `Portfolio.Services/Services/ResponseCoverageService.cs:277` |
| 6 | `FleetRoutingService.ConstructSavings` | 26 | 26 | 43 | 87 | `Portfolio.Services/Services/FleetRoutingService.cs:354` |
| 7 | `RedlandsPropertySeedData.Build` | 15 | 18 | 35 | 102 | `Portfolio.Repositories/Seed/RedlandsPropertySeedData.cs:90` |
| 8 | `ChangeDetectionService.DetectAsync` | 28 | 28 | 34 | 122 | `Portfolio.Services/Services/ChangeDetectionService.cs:50` |
| 9 | `AddressStandardizationService.ParseAddress` | 31 | 31 | 32 | 160 | `Portfolio.Services/Services/AddressStandardizationService.cs:183` |
| 10 | `RedlandsDeliveryScenario.SelectSpreadNodes` | 18 | 18 | 32 | 63 | `Portfolio.Services/Data/RedlandsDeliveryScenario.cs:155` |
| 11 | `ChangeDetectionService.BuildResult` | 19 | 19 | 31 | 76 | `Portfolio.Services/Services/ChangeDetectionService.cs:586` |
| 12 | `FleetRoutingService.Validate` | 28 | 28 | 30 | 34 | `Portfolio.Services/Services/FleetRoutingService.cs:143` |
| 13 | `ChangeDetectionService.DilateOnce` | 12 | 12 | 30 | 37 | `Portfolio.Services/Services/ChangeDetectionService.cs:370` |
| 14 | `SyntheticChangeScene.PlantBlob` | 14 | 14 | 29 | 64 | `Portfolio.Services/Data/SyntheticChangeScene.cs:195` |
| 15 | `SyntheticChangeScene.PlantDrawdown` | 13 | 13 | 29 | 56 | `Portfolio.Services/Data/SyntheticChangeScene.cs:262` |
| 16 | `FleetRoutingService.BuildResult` | 17 | 17 | 26 | 101 | `Portfolio.Services/Services/FleetRoutingService.cs:634` |
| 17 | `ChangeDetectionService.ErodeOnce` | 12 | 12 | 25 | 38 | `Portfolio.Services/Services/ChangeDetectionService.cs:331` |
| 18 | `FleetRoutingService.TwoOptPass` | 10 | 10 | 25 | 36 | `Portfolio.Services/Services/FleetRoutingService.cs:466` |
| 19 | `ViewshedService.ComputeManaged` | 10 | 10 | 25 | 71 | `Portfolio.Services/Services/ViewshedService.cs:44` |
| 20 | `SpatialOverlayService.SpatialJoinAsync` | 18 | 18 | 23 | 52 | `Portfolio.Services/Services/SpatialOverlayService.cs:23` |
| 21 | `AnonymousUserMiddleware.InvokeAsync` | 18 | 18 | 21 | 109 | `Portfolio.Web/Middleware/AnonymousUserMiddleware.cs:35` |
| 22 | `OutageTraceService.EnergizedManaged` | 13 | 13 | 21 | 53 | `Portfolio.Services/Services/OutageTraceService.cs:404` |
| 23 | `SpatialGraphService.FindShortestPathAStar` | 12 | 12 | 21 | 80 | `Portfolio.Services/Services/SpatialGraphService.cs:283` |
| 24 | `OutageTraceService.IsolationManaged` | 11 | 11 | 21 | 55 | `Portfolio.Services/Services/OutageTraceService.cs:348` |
| 25 | `FleetRoutingService.ExpandRoutePathsAsync` | 10 | 10 | 20 | 51 | `Portfolio.Services/Services/FleetRoutingService.cs:580` |

Members in the CA1502 top 25 that are not listed above:

| Member | CA1502 / cognitive | file:line |
|---|---|---|
| `CatRiskService.ValidateLocations` | 18/19 | `Portfolio.Services/Services/CatRiskService.cs:134` |
| `OutageTraceService.ProposeRestorationAsync` | 16/17 | `Portfolio.Services/Services/OutageTraceService.cs:88` |
| `SpatialGraphService.ValidateGraph` | 16/14 | `Portfolio.Services/Services/SpatialGraphService.cs:527` |
| `SpatialGraphService.ComputeDistanceMatrixAsync` | 15/19 | `Portfolio.Services/Services/SpatialGraphService.cs:136` |
| `ChangeDetectionService.OtsuManaged` | 13/18 | `Portfolio.Services/Services/ChangeDetectionService.cs:230` |
| `VrpSolverNativeBridge.TrySolveCvrptw` | 12/16 | `Portfolio.Services/Native/VrpSolverNativeBridge.cs:45` |

### 2.4 Full production violation list at ceiling 15 (CA1502 > 15 or cognitive > 15)
There are 35 members: the 25 rows of §2.3 plus these 10.

| Member | CA1502 / cognitive | file:line |
|---|---|---|
| `CatRiskService.ValidateLocations` | 18/19 | `CatRiskService.cs:134` |
| `ChangeDetectionService.OtsuManaged` | 13/18 | `ChangeDetectionService.cs:230` |
| `OutageTraceService.ProposeRestorationAsync` | 16/17 | `OutageTraceService.cs:88` |
| `SpatialClusterService.RunDbscanManaged` | 8/16 | `SpatialClusterService.cs:60` |
| `SpatialGraphService.ComputeDistanceMatrixAsync` | 15/19 | `SpatialGraphService.cs:136` |
| `SpatialGraphService.FindShortestPathDijkstra` | 10/17 | `SpatialGraphService.cs:210` |
| `SpatialGraphService.ComputeDistances` | 9/17 | `SpatialGraphService.cs:415` |
| `SpatialGraphService.ValidateGraph` | 16/14 | `SpatialGraphService.cs:527` |
| `UserProfileService.CreateOrUpdateFromGoogleAsync` | 9/18 | `UserProfileService.cs:147` |
| `VrpSolverNativeBridge.TrySolveCvrptw` | 12/16 | `VrpSolverNativeBridge.cs:45` |

Outside production: Benchmarks `Program.Render` is 16/26 (`Portfolio.Benchmarks/Program.cs:232`). In tests, the maximum is `NativeParityTests.ManagedViewshed` at 10/25.

### 2.5 JavaScript (`Portfolio.Web/wwwroot/js/**`, `lib/` excluded; 33 files, 916 functions)
| Functions | cyclo >10 | >15 | >25 | cognitive >10 | >15 | >25 |
|---|---|---|---|---|---|---|
| 916 | 17 | 4 | 1 | 18 | 5 | 2 |

| # | Function | Cyclo | Cognitive | LOC | file:line |
|---|---|---|---|---|---|
| 1 | `extractMetadataPairs` | 28 | 45 | 117 | `wwwroot/js/StateExplorer/uiManager.js:26` |
| 2 | collectionsList click handler | 14 | 26 | 53 | `wwwroot/js/StateExplorer/uiManager.js:173` |
| 3 | `openViewModal` | 24 | 21 | 67 | `wwwroot/js/StateExplorer/uiManager.js:452` |
| 4 | `drawHistogram` | 16 | 18 | 100 | `wwwroot/js/Response/app.js:375` |
| 5 | `updateWeightStatus` | 11 | 16 | 45 | `wwwroot/js/HomeFinder/app.js:155` |
| 6 | `scenario.candidates.forEach` callback | 12 | 15 | 21 | `wwwroot/js/Response/app.js:290` |
| 7 | `init` | 14 | 13 | 142 | `wwwroot/js/StateExplorer/uiManager.js:145` |
| 8 | `drawGlyph` | 9 | 13 | 51 | `wwwroot/js/Outage/app.js:360` |
| 9 | `saveFeature` | 17 | 12 | 28 | `wwwroot/js/StateExplorer/featureService.js:38` |
| 10 | `applySavedSearch` | 10 | 12 | 28 | `wwwroot/js/HomeFinder/app.js:635` |
| 11 | `regenerate` | 7 | 12 | 16 | `wwwroot/js/Overlay/app.js:38` |
| 12 | `lookup` | 6 | 12 | 13 | `wwwroot/js/StateExplorer/uiManager.js:43` |
| 13 | `renderFeatureDetails` | 15 | 11 | 74 | `wwwroot/js/StateExplorer/uiManager.js:596` |
| 14 | `shipments.forEach` callback | 14 | 11 | 24 | `wwwroot/js/PlantOperationsDashboard/dashboard.js:141` |
| 15 | `styleFor` | 13 | 11 | 26 | `wwwroot/js/Outage/app.js:279` |
| 16 | `describe` | 12 | 11 | 12 | `wwwroot/js/preferences.js:225` |
| 17 | `drawGrid` | 8 | 11 | 30 | `wwwroot/js/Viewshed/app.js:111` |
| 18 | `parseCSVLine` | 7 | 11 | 23 | `wwwroot/js/BatchGeocoding/app.js:36` |
| 19 | `entries.forEach` callback | 14 | 10 | 62 | `wwwroot/js/StateExplorer/uiManager.js:303` |
| 20 | `MapManager.onMapClick` callback | 12 | 10 | 67 | `wwwroot/js/StateExplorer/app.js:138` |
| 21 | `drawHistogram` | 10 | 10 | 54 | `wwwroot/js/Change/app.js:372` |
| 22 | `drawMask` | 8 | 10 | 76 | `wwwroot/js/Change/app.js:285` |
| 23 | `restorePreferences` | 8 | 10 | 24 | `wwwroot/js/HomeFinder/app.js:250` |
| 24 | `performSearch` | 8 | 10 | 63 | `wwwroot/js/HomeFinder/app.js:473` |
| 25 | `data.forEach` callback | 13 | 9 | 11 | `wwwroot/js/BatchGeocoding/app.js:347` |

The real JS problem is not per-function branching. It is **giant enclosing closures**: `StateExplorer/app.js:27` `initialize` is **597 lines** and `Response/app.js:3` is a **592-line IIFE**. Both have low cognitive scores but are hard to navigate (§3.8).

### 2.6 C++ (67 functions with cognitive > 0)
| cognitive >10 | >15 | >25 | branches+1 >10 | >15 | >25 |
|---|---|---|---|---|---|
| 28 | 19 | 9 | 11 | 6 | 2 |

Top 5:

| Function | Cognitive | Branches | file:line |
|---|---|---|---|
| `Change_LabelComponents` | 91 | 30 | `native/change_detection_kernel/src/change_detection_kernel.cpp:315` |
| `Facility_SolvePMedian` | 74 | 26 | `native/facility_location_kernel/src/facility_location_kernel.cpp:142` |
| `orOptMove` | 70 | 12 | `native/vrp_solver_kernel/src/vrp_solver_kernel.cpp:284` |
| `constructSavings` | 37 | 17 | `native/vrp_solver_kernel/src/vrp_solver_kernel.cpp:127` |
| `Trace_ComputeEnergizedSet` | 34 | 15 | `native/network_trace_kernel/src/network_trace_kernel.cpp:364` |

The full top 15 is in `<scratch>\metrics\cpp-summary.md`. The C++ ships nowhere (no native code in production), and these functions are the twins of the managed fallbacks. See OD-9 on whether to refactor both sides.

## 3. God classes: responsibilities and proposed splits
Every split keeps the public type, its interface, its constructor signature and its `Program.cs` registration **unchanged**. New types are `internal` (tests reach them through the existing `InternalsVisibleTo`). Exception messages are public, because controllers return `new { error = ex.Message }`, so they must stay **byte-identical**.

### 3.1 `UserProfileSeedService` (1,464 lines; one 1,444-line method; CA1502 251 / cognitive 236)
- **What it does** (`Portfolio.Services/Services/UserProfileSeedService.cs`): a transaction wrapper (`:23-31`, `:1442-1463`) around six "insert if missing" blocks, each followed by `SaveChangesAsync`:

  | Block | Lines | Inserts |
  |---|---|---|
  | Fiber clients | `:33-241` | 12 |
  | Materials | `:243-474` | 12 |
  | Orders | `:476-760` | 17, each guarded on its client existing |
  | Shipments | `:762-955` | 11 |
  | Inventory transactions | `:957-1317` | 23, each guarded on its material existing and using `material.Id` |
  | Properties | `:1320-1440` | **40** legacy listings (1 standalone plus 39 in a list), keyed by Street+City, not per user |

  92 `DateTime.Parse(...).ToUniversalTime()` calls. 22 `FirstOrDefaultAsync` calls without the `cancellationToken`.
- **Split:**
  - Move the data into `Portfolio.Services/Data/FiberDemoSeedData.cs`, an internal static class beside the existing deterministic datasets. It exposes `Clients(Guid userId)`, `Materials(...)`, `Orders(...)` (each paired with its client name), `Shipments(...)` (paired with the guard client name) and `InventoryTransactions(...)` (each paired with its material SKU). Build entity instances directly from `Portfolio.Common.Models`, so no new model types are needed.
  - `SeedForUserAsync` keeps the transaction wrapper and calls five or six private loops (`SeedClientsAsync`, `SeedMaterialsAsync`, …). Each loop must keep **the same existence predicate, guard, insertion order and `SaveChangesAsync` boundaries**.
  - Expect about **−1,300 lines**, with no member above 10.
- **Before changing anything:** add a **golden snapshot test**. Seed an EF InMemory database, project every seeded row of every table (excluding generated ids) into a canonical ordered string, and compare it to a checked-in fixture. The existing test only asserts `AnyAsync` on clients and materials (`Portfolio.Tests/Services/UserProfileSeedServiceTests.cs:29-30`). Also add an idempotency test: seeding twice inserts nothing new.
- **Owner decision OD-3 (behaviour).** Migration `20260719060844_ExpandRedlandsProperties.cs:63-66` says it *replaces* "legacy demo listings ... seeded outside migrations" (`DELETE FROM "Properties"`, then inserts the 45 curated homes). But this seeder still re-inserts the 40 legacy listings for the first new visitor after the migration. None of their streets exist in `RedlandsPropertySeedData` (e.g. "120 Franklin Ave"; the curated streets come from fixed per-neighbourhood lists, `RedlandsPropertySeedData.cs:41-86`). In production the Home Finder very likely serves about 85 homes, not 45. I could not check production data.

### 3.2 `Program.cs` (813 lines; CA1502 61). See §4.

### 3.3 `FleetRoutingService` (738 lines; summed cognitive 248)
- **Responsibilities:**
  1. Scenario lookup (`:44-60`).
  2. Orchestration (`OptimizeAsync` `:62-139`): snap, distance matrix, time matrix, native-or-managed dispatch, path expansion, result.
  3. Validation (`Validate` `:143-176`, CA1502 28).
  4. The **managed CVRPTW solver**, which mirrors `native/vrp_solver_kernel` move for move: `SolverContext` `:182-204`, `BuildContext`, `MapStops`, `SolveManaged` `:260-286`, `RouteDistance`, `RouteLoad`, `RouteFeasible` `:310-335`, `Objective`, `ConstructSavings` `:354-440` (cognitive 43), `TruncateToFleet`, `TwoOptPass` `:466-501` (25), `OrOptMove` `:506-574` (70).
  5. Road-path expansion (`:580-630`).
  6. Result shaping (`BuildResult` `:634-734`, cognitive 26).
- **Split:**
  - `Services/Fleet/ManagedVrpSolver.cs` (internal static) takes `SolverContext` and item 4's methods verbatim.
  - `Services/Fleet/FleetRequestValidator.cs` (internal static) holds `Validate`, split into `ValidateFleet` and `ValidateStop`. The messages stay verbatim.
  - `Services/Fleet/FleetResultBuilder.cs` holds `BuildResult`, with a `SimulateRoute(context, route) → (arrivals, returnTime, feasible)` helper.
  - The service keeps the constructor, `GetScenarioAsync`, `OptimizeAsync` and `ExpandRoutePathsAsync`, about 200 lines.
  - Inside the solver, use extract-method only:
    - `OrOptMove` → `TryRelocate(context, routes, a, pos, len)` plus `RelocationDelta(...)`.
    - `ConstructSavings` → `SingletonRoutes`, `RankedSavings` and `TryMergeEnds`.
  - **Iteration order and first-improvement return points must not change.**
- **Before changing anything:** today's 24 tests assert invariants (each stop once, capacity, non-increasing costs; `FleetRoutingServiceTests.cs:82-406`), not exact outputs. Add **golden tests**: for each `RedlandsDeliveryScenario` preset, pin the exact stop-id sequence per route, `IterationCosts` and `FinalObjective`. They must pass both with native disabled and with native present, so they double as parity checks.

### 3.4 `ResponseCoverageService` (709 lines)
- **Responsibilities:**
  1. Scenario.
  2. Isochrone (`:49-106`).
  3. Optimize orchestration (`:111-200`).
  4. Five validators (`:204-254`).
  5. **Managed p-median**, mirroring `Facility_SolvePMedian`: `SolveManaged` `:277-402` (cognitive 47), `RebuildNearest`, `EvaluateObjective`, `BuildResponseOrder`, `WeightedPercentile`.
  6. Coverage evaluation (`Evaluate` `:536-558` with native dispatch, `EvaluateManaged` `:561-612`).
  7. Result shaping (`:619-705`).
- **Split:** `Services/Response/ManagedPMedianSolver.cs` (internal static) takes item 5 plus `EvaluateManaged` and the `CoverageEvaluation` record. Split `SolveManaged` into `GreedySeed(...)` and `FindBestSwap(...)`, keeping the tie-break comparator (`:321`, `:379`) exact. The service keeps orchestration, validation and shaping.
- **Before changing anything:** add golden tests on `RedlandsResponseScenario` for each `ObjectiveMode` (0, 1, 2). Pin `ChosenCandidateIds`, `IterationObjectives` and the optimized and baseline stats.

### 3.5 `ChangeDetectionService` (665 lines; summed cognitive 246)
- **Responsibilities:** request validation inlined in `DetectAsync` (`:50-87`), then the five-stage pipeline with per-stage native dispatch (`:89-170`), the managed kernels (CVA `:200`, Otsu `:230`, morphology `:302-406`, union-find labelling `:409-582`) and result shaping (`:586-661`).
- **Split:**
  - `Services/ChangeDetection/ManagedChangeKernels.cs` (internal static) takes the managed kernels verbatim. Then:
    - `ErodeOnce` and `DilateOnce` become `AllNeighboursSet(...)` and `AnyNeighbourSet(...)` predicates.
    - `LabelComponentsManaged` (cognitive 83) becomes `CollectNeighbourLabels(...)` for the four-branch block at `:431-452`, a first-pass labeller, and a `ComponentAccumulator` replacing the eight parallel lists at `:477-484`. Slot order and the `MaxBlobs` exception text (`:503-505`) must not change.
  - `DetectAsync` becomes `ValidateDetectRequest(request) → (mode, epochA, epochB)` plus four `Compute…(ref nativeAccelerated)` stage helpers.
- **Before changing anything:** add a golden test over `SyntheticChangeScene.Build(256, 256, 0.025)` and a noisy variant. Pin the threshold, a mask checksum, the blob list and the histogram.

### 3.6 `SpatialGraphService` (553 lines)
- **Responsibilities:** cached graph, shortest path (Dijkstra `:210-281` and A* `:283-362`, about 85 % the same code), service area, distance vector, distance matrix, snapping, haversine (`:365`, plus a second inlined copy at `:394-399`), adjacency and lookup helpers, and `ValidateGraph` (`:527-551`).
- **Simplify; no new file is needed:**
  - Merge the two searches into `SearchShortestPath(request, heuristic: Func<int,double>?, algorithmName, ct)`, with Dijkstra passing a heuristic of 0.
    - Preserve the indexer semantics: `distances[edge.To]` (`:245`) and `gScore[edge.To]` (`:323`) throw `KeyNotFoundException` for undeclared edge targets. My probe shows that becomes a **404 ProblemDetails** (§4.3), whereas `ComputeDistances` skips such edges (`:448-449`).
  - Extract `HaversineKm(lat1, lon1, lat2, lon2)` with **the identical operation order**, so it is bit-identical, and use it from both sites.
    - **Do not** unify it with `CatRiskService.HaversineKm` (`CatRiskService.cs:481-489`). That one uses the `asin` form and mirrors the cat-risk kernel, so results would differ in the last bits.
  - Add a `NodeIdSet(nodes)` helper for the four copies (`:44-45`, `:77-78`, `:115-116`, `:152-153`).
  - Split `ValidateGraph` into node and edge checks.
- **Before changing anything:** add golden routes (node ids, cost, explored count and order) for Dijkstra and A* on the Redlands graph, for 3 or 4 origin–destination pairs. Add a test that pins the `KeyNotFoundException` for an undeclared edge target.

### 3.7 Other production members above 15: one-line recipes (§8 CX-12)
| Member | Recipe |
|---|---|
| `AddressStandardizationService.ParseAddress` `:183-342` (31/32) | One private helper per token step: `ConsumeTail` (ZIP, state), `ConsumeHouseNumber`, `ConsumeStreet`, `ConsumeDirectional`, `ConsumeUnit`, `ComputeConfidence`, `BuildStandardized`, sharing a small `TokenCursor`. **Golden first:** a table of about 30 raw addresses pinned to every `AddressParsedDto` field (current tests: 11 facts). |
| `AnonymousUserMiddleware.InvokeAsync` `:35-143` (18/21) | §8 CX-05. |
| `CatRiskService.ValidateLocations` `:134-154` (18/19) | Extract `ValidateLocation(...)` plus `IsUnitInterval(x)` (used 3×). Use `double.IsFinite`. |
| `SpatialOverlayService.SpatialJoinAsync` `:23-74` (18/23) | Extract `Validate(request)` into `ValidateZones` and `ValidatePoints`. The body becomes dispatch plus result. |
| `OutageTraceService.ProposeRestorationAsync` `:88-164`, `IsolationManaged` `:348-402`, `EnergizedManaged` `:404-456` | `ValidateRestore(...)` and `TryTie(...)`; `FindUpstreamProtective(...)` and `FindDownstreamSwitchFrontier(...)`; `ApplyOverrides`, `BfsFromSource` and `CollectEnergized`. These are managed mirrors, so keep the loop order. |
| `ViewshedService.ComputeManaged` `:44-114` (10/25) | Extract `IsVisible(request, elevation, observerElevation, tx, ty)`. Keep the documented half-up rounding (`:76-82`, `Math.Floor(v + 0.5)`) verbatim; it is parity-critical. |
| `SpatialClusterService.RunDbscanManaged` `:60-106` (8/16) | Extract the seed-expansion loop (`:87-100`) into `ExpandCluster(points, labels, seeds, neighbors, clusterId, epsilonSquared, minPoints)`. Keep the noise→border reclaim order exactly. |
| `UserProfileService.CreateOrUpdateFromGoogleAsync` `:147-211` (9/18) | Extract `TryGetAnonymousProfileIdAsync(ctx, ct)` and `CreateOrMergeProfileAsync(...)`. |
| `VrpSolverNativeBridge.TrySolveCvrptw` `:45-131` (12/16) | Extract `MapRoutes(routeCount, routeLengths, routeStops, stopCount)` for the validation and mapping loop. Do this **after** CX-11, which touches the same file. |
| Data generators: `RedlandsPropertySeedData.Build` (15/35), `RedlandsDeliveryScenario.SelectSpreadNodes` (18/32), `SyntheticChangeScene.PlantBlob` (14/29) and `PlantDrawdown` (13/29) | **Baseline-exempt (CX-13).** Their output *is* the demo data and the public JSON. `RedlandsPropertySeedData.All` is consumed by a migration (`20260719060844_ExpandRedlandsProperties.cs:69`), so any output change effectively edits a migration. Refactor only under a byte-level golden snapshot, if at all. |

### 3.8 JS god files (split described only; the frontend agent owns shared-module design)
| File | Lines / functions | Responsibilities → proposed files |
|---|---|---|
| `wwwroot/js/HomeFinder/app.js` | 731 / 51 | Weights and sliders (`getPreferences:56` … `restorePreferences:250`) → `HomeFinder/preferences.js`. ArcGIS map (`initMap:279`, `plotResults:314`) → `HomeFinder/map.js`. Result cards (`renderResults:384`) → `HomeFinder/results.js`. Search (`performSearch:473`) stays in `app.js`. Saved searches (`saveSearch:541`, `loadSavedSearches:567`, `applySavedSearch:635`) → `HomeFinder/saved-searches.js`. A **local toast copy** (`showToast:668`, `createToastContainer:704`) should be deleted in favour of the global `toasts.js`. |
| `wwwroot/js/StateExplorer/uiManager.js` | 669 / 47 | `escapeHtml:13` (another private escaper; cross-ref the security innerHTML audit). `extractMetadataPairs:26` (117 lines, cognitive 45) plus `lookup:43` → `StateExplorer/metadata.js` (pure, testable). Event wiring `init:145` (142 lines) stays. Lists (`renderSavedFeaturesList:289`, `renderCollections:369`, `renderStats:402`) → `lists.js`. Modals (`openSaveModal:435`, `openViewModal:452`, cognitive 21) → `modals.js`. Comparison (`renderComparison:526`, `renderCompareIndicator:580`) → `comparison.js`. `renderFeatureDetails:596` stays. A local `showToast:415` duplicates `toasts.js`. |
| `wwwroot/js/StateExplorer/app.js` | 632 / 28 | One 597-line `initialize` (`:27`) holds every subscription. Lift each handler into a named function: map click `:138` (67 lines), hover `:210`, and seven `document.addEventListener` handlers `:290-:579` (one is 102 lines, `:388`). Put them in `StateExplorer/handlers.js`; `initialize` becomes a registration list. |
| `wwwroot/js/Response/app.js` | 594 / 60 | A 592-line IIFE (`:3`). `drawHistogram:375` (100 lines, cognitive 18), `markMinute:476` and `binWeights:486` → `Response/histogram.js`. Map layers (`drawDemand:226`, `drawAssignments:260`, `drawStations:282`, `drawIsochrone:313`, `fitToScenario:523`) → `Response/layers.js`. Generic SVG helpers (`mixHex:514`, `appendLine:537`, `appendText:549`) are candidates for the frontend agent's shared SVG and chart helper. |

## 4. Program.cs decomposition

### 4.1 Current map (`Portfolio.Web/Program.cs`)
| Lines | Concern |
|---|---|
| 31 | Builder |
| 36-37 | Startup console logger, used by Redis and Data Protection logging and by the migration block |
| 44-46 | Razor Pages, HttpContextAccessor, `TimeProvider` singleton |
| 54-63 | 10 repositories (Scoped) |
| 67-88 | 22 services (Scoped) |
| 89-120 | ArcGIS feature typed client (30 s client timeout, `SocketsHttpHandler` 5-minute pooling, infinite handler lifetime) plus resilience pipeline `arcgis-features` |
| 122-127 | Geocoding options with `ValidateDataAnnotations` |
| 129-203 | Three geocoding typed clients and pipelines `arcgis-batch`, `arcgis-reverse`, `arcgis-address`. Each is preceded by a **shadowed** `AddScoped` (§6.5). |
| 208-225 | Redis connection-string normalisation, masked log, `IBatchJobStore` choice |
| 234-241 | Forwarded headers (lists cleared) |
| 246-262 | Distributed cache (Redis or memory) plus `AddMemoryCache` |
| 264-272 | Session |
| 277-420 | Cookie authentication with three inline event lambdas (`:315-378`), Google (`:381-414`), the `"Authenticated"` policy |
| 422-428 | Controllers and JSON options |
| 443-465 | Rate limiter (global 300/min; `"expensive"` 15/min; IP partition) |
| 473-474 | Health checks |
| 479-498 | API versioning, ApiExplorer, OpenAPI |
| 503-511 | DbContext (Npgsql) |
| 516-547 | Data Protection (Redis with filesystem fallback, or filesystem) |
| 549 | `Build()` |
| 558-619 | **Pipeline**, in documented order |
| 638-667 | `--migrate` and `RunMigrationsOnStartup` |
| 669-670 | Run |
| 675-743 | Local function `ParsePostgresConnectionString` |
| 748-792 | Local function `NormalizeRedisConnectionString` |
| 797-812 | Local function `MaskRedisPassword` |

Test infrastructure facts:
- There is **no** `WebApplicationFactory`, `Microsoft.AspNetCore.Mvc.Testing` or `TestServer` reference anywhere.
- The generated **`Program` class is already `public`** (reflection on the built `Portfolio.Web.dll`: `IsPublic=True`). `Portfolio.Web/AssemblyInfo.cs:6` already grants `InternalsVisibleTo("Portfolio.Tests")`.
- So **no `public partial class Program` is needed**; only the test package is missing.

### 4.2 Proposed extension methods
These go in `Portfolio.Web/Startup/`, one static class per file, all `internal static`.

| Method (file) | Moves (current lines) | Notes |
|---|---|---|
| `AddPortfolioCore()` (`CoreRegistration.cs`) | 44-46 | Razor Pages, HttpContextAccessor, TimeProvider |
| `AddPortfolioRepositories()` (`DataRegistration.cs`) | 54-63 | Unchanged list, minus `IFiberClientRepository` if CX-03 lands |
| `AddPortfolioServices()` (`DataRegistration.cs`) | 67-88 | Unchanged list |
| `AddPortfolioArcGisClients(IConfiguration)` (`ArcGisClientRegistration.cs`) | 89-203 | Uses `AddArcGisResilience(name, ArcGisResilienceProfile)` (§5.1). **Pipeline names unchanged.** The options binding (122-127) moves here. |
| `AddPortfolioRedis(IConfiguration, ILogger) → RedisSettings(bool Enabled, string ConnectionString)` (`InfrastructureRegistration.cs`) | 208-225 | Keeps both config-key lookups and the masked log |
| `AddPortfolioForwardedHeaders()` | 234-241 | The security agent may change this; coordinate (§9) |
| `AddPortfolioCaching(RedisSettings)` and `AddPortfolioSession(IWebHostEnvironment)` | 246-272 | Keeps `AddMemoryCache` unless OD-10 decides otherwise |
| `AddPortfolioAuthentication(IConfiguration, IWebHostEnvironment)` (`AuthenticationRegistration.cs`) | 277-420 | Move the cookie events into `PortfolioCookieEvents : CookieAuthenticationEvents` (overrides `RedirectToLogin`, `RedirectToAccessDenied`, `SignedIn`), assigned with `options.Events = new PortfolioCookieEvents()`. That is identical to today and makes `SignedIn` unit-testable. One helper `ApiAwareRedirect(ctx, status)` replaces the three copies of "if `/api` then set the status, else redirect" (`:317-324`, `:328-335`, `:405-412`). |
| `AddPortfolioApi()` (`ApiRegistration.cs`) | 422-428, 479-498 | Controllers, JSON, versioning, OpenAPI |
| `AddPortfolioRateLimiting()` (`RateLimitingRegistration.cs`) | 443-465 | The `ClientKey` helper becomes a private static method |
| `AddPortfolioHealthChecks()` | 473-474 | |
| `AddPortfolioDatabase(IConfiguration)` (`DataRegistration.cs`) | 503-511 | Calls `ConnectionStrings.ParsePostgres` |
| `AddPortfolioDataProtection(RedisSettings, IWebHostEnvironment, ILogger)` | 516-547 | |
| `UsePortfolioPipeline(this WebApplication app)` (`Pipeline.cs`) | 558-590 | **The exact call sequence below.** Keep the comments that explain order. |
| `MapPortfolioEndpoints(this WebApplication app)` (`Pipeline.cs`) | 597-619 | OpenAPI, Scalar, the two health endpoints with `DisableRateLimiting()`, controllers, pages, in this order |
| `StartupMigrations.Run(WebApplication, string[] args, IConfiguration, ILogger) → int?` (`StartupMigrations.cs`) | 638-667 | `null` means continue to `app.Run()` |
| `ConnectionStrings` (internal static: `ParsePostgres`, `NormalizeRedis`, `MaskRedisPassword`) (`Infrastructure/ConnectionStrings.cs`) | 675-812 | **Moved verbatim** by CX-02 so it can be unit-tested |

`UsePortfolioPipeline` must contain exactly this sequence, which matches `agent-context.md` §4:
```
app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
if (app.Environment.IsDevelopment()) { app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<AnonymousUserMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
```
The new `Program.cs` calls the `Add…` methods **in the same relative order as today's blocks**. That keeps the startup log order and any order-sensitive registrations identical. It ends at about 40 lines, with `<Main>$` CA1502 around 3.

### 4.3 Characterization strategy (before moving anything)
The recommended option is the least invasive one. Add **`Microsoft.AspNetCore.Mvc.Testing` 10.0.10** to `Portfolio.Tests` only, using the same 10.0.10 band as the other ASP.NET packages (**OD-1**). The image never sees it, because the Dockerfile restores only `Portfolio.Web.csproj`.

**Feasibility proven in scratch** (`<scratch>\repo-wf`; test source at `<scratch>\probe\PipelineCharacterizationTests.cs`):
- The cache only held Mvc.Testing **8.0.11**. With restore pinned to the local cache (offline), the `PortfolioAppFactory` below booted the *current* `Program.cs`. **All 7 probe tests passed, the full suite reached 704/704, the CI build had 0 warnings**, and coverage moved as follows:
  - **Program.cs: 0 → 417/451 (92.5 %)**
  - `AnonymousUserMiddleware`: 0 → 63.8 %
  - `ApiExceptionMiddleware`: 0 → 81.8 %
  - `SecurityHeadersMiddleware`: 0 → 100 %
  - Total: **87.16 % line / 68.40 % branch**, up from 83.64/67.36
- **TestHost must match the ASP.NET major version.** With TestHost 8.0.11, MVC's JSON output failed. I observed it on an `[ApiController]` 404 ProblemDetails body: `"The PipeWriter 'ResponseBodyPipeWriter' does not implement PipeWriter.UnflushedBytes"`. Use 10.0.x.

Factory recipe (verified):
- `UseEnvironment("Development")`. In Production, `Program.cs:540` would create `/app/DataProtection-Keys` on the host disk.
- `UseSetting("ConnectionStrings:DefaultConnection", "<dummy Npgsql string>")`. Settings applied this way are seen by the pre-`Build()` reads.
- `UseSetting("RunMigrationsOnStartup", "false")`.
- Dummy `Authentication:Google:ClientId` and `Authentication:Google:ClientSecret`.
- In `ConfigureTestServices`, remove the `DbContextOptions<PortfolioDbContext>`, `DbContextOptions` and `IDbContextOptionsConfiguration<PortfolioDbContext>` descriptors, then `AddDbContext` with `UseInMemoryDatabase`.
- Capture `IServiceCollection` in the same callback for the DI snapshot.
- Create one factory per test whenever rate-limit state matters.

Characterization tests that lock order and registrations (✔ = passed in the probe):
1. ✔ `/health/live` returns 200 with `nosniff`, `X-Frame-Options: DENY` and CSP. SecurityHeaders runs before endpoints.
2. ✔ A static file (`/js/api-config.js`) returns 200 with the security headers. SecurityHeaders and StaticFiles run before Routing.
3. ✔ `/api/v1/fiber/orders` without authentication returns **401, not 302**. This locks the cookie API-aware handler.
4. ✔ 300 requests are fine and the 301st returns **429**. The 429 has security headers, **no `AnonUserId` cookie, and creates 0 profiles**, so the rate limiter runs before `AnonymousUserMiddleware`. A different `X-Forwarded-For` gets a fresh partition, so ForwardedHeaders runs before the limiter. `/health/live` is still 200 after the limit is hit.
5. ✔ An unhandled `KeyNotFoundException` becomes **404 `application/problem+json`**, so `ApiExceptionMiddleware` is in the pipeline. The trigger is a route request with an edge to an undeclared node.
6. ✔ An HTML GET without a cookie creates a profile and sets `AnonUserId` with HttpOnly and SameSite=Lax.
7. ✔ DI snapshot: the captured descriptors are available. The probe already sees **2** `IBatchGeocodingService` descriptors (§6.5).

Still to add (needs 10.0.x):
- The `"expensive"` policy: 15 then 429. Use `GET /api/v1/homefinder/property/{id}`.
- A golden **DI registration snapshot**: sorted `ServiceType | Lifetime | ImplementationType` for `Portfolio.*`, `IBatchJobStore`, `IDistributedCache`, `TimeProvider`.
- Resilience attempt counts with a fake primary handler returning 503: `arcgis-features` = 3 attempts, the other three = 4. Each test takes about 1–2.5 s because of backoff.
- Production-environment behaviour (HSTS, `/Error`) needs a configurable keys path, or must be accepted as untested.
- `/openapi/v1.json` returns 200.

**Unit tests for the URI helpers (CX-02).** Moved verbatim, they need direct tests:
- `ParsePostgres`:
  - `Host=` passthrough
  - `postgres://` and `postgresql://`
  - percent-encoded user and password
  - default port 5432
  - `[::1]` → `127.0.0.1`
  - `?sslmode=disable` override; default `Require`
  - missing database, missing user/password, wrong scheme and an invalid URI each throw `ArgumentException`
- `NormalizeRedis`:
  - blank → `""`
  - StackExchange format passthrough
  - `redis://` → port 6379, no `ssl`
  - `rediss://` → port 6380, `ssl=True`
  - password-only userinfo
  - `abortConnect=False` always appended
- `MaskRedisPassword`: masks `password=` case-insensitively and leaves everything else.

### 4.4 Risks and gotchas
- **Order drift.** Behavioural tests 1–6 lock it; review the `UsePortfolioPipeline` diff line by line.
- **Startup logging order.** Keep the Redis log before the Data Protection log.
- **Shadowed DI registrations.** The snapshot captures today's duplicates, so update it intentionally when CX-03 removes them.
- **Stale comment.** `Program.cs:397-402` says "Google is the default challenge scheme", which contradicts `:277-289`. Hand it to the comments agent; do not act on it.
- **Overlap with security.** The security agent's Program.cs edits (forwarded headers, rate limits, CSP, AllowedHosts) should land **before** CX-04 as small diffs, or **after** CX-04 on the new files. Never run both in parallel (§9).

## 5. Duplication

### 5.1 The four ArcGIS resilience pipelines (`Program.cs:98-120`, `:131-153`, `:156-178`, `:181-203`; 92 lines)
There is **one distinct profile and three identical ones**:

| Pipeline | Timeout | Retries | Delay | Circuit-breaker sampling | Min throughput | Break | Also |
|---|---|---|---|---|---|---|---|
| `features` | 25 s | 2 | 500 ms | 60 s | 5 | 30 s | 30 s client timeout, pooled `SocketsHttpHandler` |
| `batch`, `reverse`, `address` | 8 s | 3 | 300 ms | 30 s | 8 | 15 s | |

All four share an identical `ShouldHandle` predicate. It is narrower than the library default (it excludes 408, 429 and timeouts), so **keep it**.

Fix: add `static IHttpClientBuilder AddArcGisResilience(this IHttpClientBuilder b, string pipelineName, ArcGisResilienceProfile p)` and a `sealed record ArcGisResilienceProfile(...)` with two static presets, `Features` and `Geocoding`. Each registration becomes one or two lines. That is about −55 lines.

Related: the 503 bodies hard-code `retryAfterSeconds` 15 and 30 (§5.2), which equal the break durations. Reference the profiles' `BreakDuration` so the two cannot drift. Behaviour is identical.

Verify with the resilience attempt-count tests (§4.3).

### 5.2 Controller try/catch (52 `catch` clauses in 23 of 25 controllers)
| Kind | Count | Body | Where |
|---|---|---|---|
| `ArgumentException` | 27 | 400 `{ error = ex.Message }`; 21 also log a warning (20 pass the exception) | 12 compute controllers, plus Address, Batch, Collections, Reverse, SavedFeature |
| `Exception` | 12 | 500 ProblemDetails "See server logs for details." | `FiberMaterialsController` (6), `FiberOrdersController` (5), `FiberDashboardController` (1) |
| `BrokenCircuitException` | 6 | 503 ProblemDetails with `retryAfterSeconds` | 5 identical geocoding bodies ("Geocoding Unavailable", 15 s), e.g. `BatchGeocodingController.cs:79-89`; 1 Features variant, 30 s (`FeaturesController.cs:50-59`) |
| `InvalidOperationException` | 4 | 409 `{error}`, or **404** `{ error = "Profile not found" }` in `UserProfileController.cs:40-43` | |
| `KeyNotFoundException` | 3 | Empty 404 | |

**Interaction with `ApiExceptionMiddleware`** (`Middleware/ApiExceptionMiddleware.cs:50-59`): the middleware maps the same exception types but emits **RFC 7807** (`title`, `detail`, `instance`). The JS reads **only `body.error`**:
- `wwwroot/js/api-fetch.js:59`
- `AddressStandardization/app.js:348`
- `Network/app.js:408`
- `ReverseGeocoding/app.js:237`
- `StateExplorer/featureService.js:17,29,56,58,72`

So **deleting the 27 `ArgumentException` catches would change the public 400 body**, and the UI would show "An unexpected error occurred." **This is not behaviour-preserving.**

- **Recommended (CX-14, optional, OD-11):** use two `ExceptionFilterAttribute`s in `Portfolio.Web/Filters/`:
  - `[BadRequestOnArgumentException]`: 400 `{ error }`, logging a warning under the controller's category through `ILoggerFactory`.
  - `[ArcGisUnavailable(...)]`: 503 ProblemDetails with the same title, detail and extension.

  Apply them **at action level, exactly where catches exist today**. A class-level attribute would also convert currently-unhandled `ArgumentException`s in other actions from ProblemDetails to `{error}`. Expect about −230 lines and every affected action shrinking to 2–3 lines. Cost: about 23 controller tests call actions directly and assert a 400 (9 in `SpatialComputeControllerTests`, 2 each in Address/Batch/Change/Fleet/Outage/Response, 1 each in Collections/Reverse), and about 12 references assert 503. Filters do not run in direct calls, so those tests become "action lets the exception propagate", plus filter unit tests, plus one integration test per filter (needs CX-01).
- **Fiber `catch (Exception)` ×12 (CX-15, OD-2).** Delete them and let the middleware handle errors. **This changes behaviour, deliberately: it fixes a defect.**
  - `FiberMaterialService.cs:48` throws `KeyNotFoundException` for an unknown id in `ReceiveStockAsync`, and the catch at `FiberMaterialsController.cs:186` turns it into **500**. The middleware would return 404.
  - Same for `FiberOrderService.cs:66` and `FiberOrdersController.cs:120`.
  - "User not identified" (`FiberMaterialService.cs:33`) becomes 409 instead of 500.
  - 500 bodies change from "See server logs for details." to "An unexpected error occurred." and gain `instance`.
  - Client-abort `OperationCanceledException` becomes 499 at Debug level instead of 500 at Error level.
  - Update 12 tests that assert 500.
- **Owner option (not recommended inside this cleanup):** make `getApiError` accept `body.error || body.detail`, then migrate the 400s to ProblemDetails. That is a public JSON change and needs OD plus frontend work.

### 5.3 Native-bridge boilerplate (13 bridges, 2,886 lines in `Portfolio.Services/Native/`)
After normalising names, **all 12 newer bridges have byte-identical 21-line static constructors** (e.g. `SpatialOverlayNativeBridge.cs:11-31`) and byte-identical 8-line `IsNativeInvocationException` predicates (e.g. `:91-98`). `NativeScoringBridge` has the same probe logic in 24 lines (`NativeScoringBridge.cs:23-46`) and no predicate.

Fix (CX-11): add `Native/NativeKernelLoader.cs` (internal static) with:
- `TryLoad(string libraryName)`, holding the same `NativeToggle.Disabled` check, `NativeLibrary.TryLoad(..., typeof(NativeKernelLoader).Assembly, AssemblyDirectory | ApplicationDirectory, out _)` and catch-all → false. All bridges live in the same assembly, so the assembly argument is unchanged.
- `IsNativeInvocationException(Exception)`, with the **same five types, including `InvalidOperationException`** (the documented wart; keep it).

Each bridge becomes `private static readonly bool _available = NativeKernelLoader.TryLoad("spatial_overlay_kernel");`. Its `catch ... when (IsNativeInvocationException(ex))` clauses call the shared predicate.

Size: before, 12×21 + 12×9 (with blank line) + 24 = **384 lines**; after, 13 one-liners plus about 30 shared lines. That is **≈ −340 lines**. Removing the explicit static constructors makes the types `beforefieldinit`, so the library probe may run marginally earlier, at JIT time of the first caller. There is no functional effect. If exact timing matters, keep a one-line static constructor.

**Do not** introduce a generic "TryInvoke(lambda)" wrapper. `out` parameters can't be captured in lambdas, so every Try method would need restructuring, which is not clearly simpler. Keep the 9 unused `IsAvailable` properties: they are part of the documented bridge pattern and cost one line each.

Tests: a `NativeKernelLoader` predicate truth table, and `TryLoad("no_such_kernel") == false`. Re-run the native-present suite (CMake) per protocol.

### 5.4 Validation patterns across compute services
There are **143** `throw new ArgumentException` sites in 18 services:
- 17 "required collection" guards (`is null || .Count == 0`)
- 19 "too many" guards (`.Count > Max…`)
- **6 identical private `IsFinite` helpers**: `CatRiskService.cs:509`, `ChangeDetectionService.cs:663`, `FleetRoutingService.cs:736`, `ResponseCoverageService.cs:707`, `SpatialClusterService.cs:160`, `SpatialOverlayService.cs:158`
- **14 more inline `double.IsNaN(x) || double.IsInfinity(x)`** checks in 7 files: `SpatialGraphService` 5, and 2 each in GeoStream, RasterTerrain and Viewshed, 1 each in Outage, SpatialCluster and SpatialGeometry

Recommendation, preferring deletion:
- Replace the helpers and inline checks with BCL **`double.IsFinite`**. The semantics are identical, it removes one `||` per site, and I validated it in scratch (the 6 helpers).
- Split each long `Validate` by concern (extract method).
- **Do not** add a Guard library. Every message is part of the public 400 body, so a helper would just take two message strings per call and add indirection.

### 5.5 Other duplicates
- **The protected-claim set exists 3×.** `ProfileClaimTypes.Protected` (`Portfolio.Common/Constants/ProfileClaimTypes.cs:21`) is **unused**. `ProfileController` keeps a static copy (`:21-27`). `UserProfileController.SetClaim` rebuilds it **on every request** (`:132-141`). All three have the same four members and OrdinalIgnoreCase. Use the Common one; I validated this in scratch.
- **`SyntheticChangeScene.DefaultNoiseLevel`** (`Data/SyntheticChangeScene.cs:31`, unused) and the literal `0.025` in `ChangeDetectionController.cs:48` should use the constant. A `const` is a legal default value, and the behaviour and OpenAPI default are identical.
- The "`/api` → status else redirect" logic exists 3× in `Program.cs` (§4.2). The Dijkstra/A* pair, the haversine pair and the node-id-set ×4 are covered in §3.6.

## 6. Dead code
All findings below were confirmed across C#, cshtml, JS and tests. Analyzer counts come from a scratch build with `IDE0005`, `IDE0051` and `IDE0052` set to warning, `EnforceCodeStyleInBuild`, and `GenerateDocumentationFile` enabled for all projects. Raw counts are deduplicated, because MSBuild prints each warning twice.

### 6.1 Unnecessary usings (IDE0005): 42
| Project | Count | Notes |
|---|---|---|
| Portfolio.Tests | 26 | Mostly redundant `using Xunit;` (there is a global `<Using Include="Xunit" />`) |
| Portfolio.Web | 8 | `Program.cs:4,7,11,26`, `AnonymousUserMiddleware.cs:4`, `ApiExceptionMiddleware.cs:2`, `DatabaseReadyHealthCheck.cs:1`, `Privacy.cshtml.cs:1` |
| Portfolio.Services | 4 | `FiberMaterialService.cs:1`, `SavedFeatureService.cs:1,5`, `UserProfileService.cs:8` |
| Portfolio.Repositories | 2 | `ICollectionRepository.cs:2`; **`Migrations/20260316020003_InitialCreate.cs:1` must be left alone (migration)** |
| Portfolio.Common | 1 | `HomeSearchPreferencesDto.cs:2` |
| Portfolio.Benchmarks | 1 | `Workloads.cs:3` |

Separately, 4 of the 5 **global usings in `Portfolio.Tests.csproj` are unused**: `Metadata.Builders` (`:54`), `Migrations`, `Npgsql…Metadata` and `Storage.ValueConversion` (`:58-60`). No `EntityTypeBuilder`, `MigrationBuilder`, `Migration`, `NpgsqlValueGenerationStrategy`, `ValueConverter` or `ModelBuilder` appears in the tests. IDE0005 cannot see csproj global usings, so I validated this by deleting them and building.

`dotnet format style --diagnostics IDE0005 --exclude Portfolio.Repositories/Migrations` fixes all of them. It also **sorts every touched using block**, because `.editorconfig:14` sets `dotnet_sort_system_directives_first`, so it touches about 49 files. Schedule it **last** (CX-17).

### 6.2 Unused private members (IDE0051 / IDE0052): 13
| Location | Member | Fix |
|---|---|---|
| `AddressStandardizationService.cs:75-78` | `HouseNumberPattern`, `UnitPattern`, `StateZipPattern`, `ZipPattern` (compiled regexes, never read) | Delete |
| `SyntheticChangeScene.cs:37-40` | `Red`, `Green`, `Nir`, `Swir` band consts | Delete. Optional: one-line band-order comment, for the comments agent. |
| `FiberOrderService.cs:11,17,22` | `_clientRepo` (`IFiberClientRepository`, never read) | Delete the field and constructor parameter. That makes the whole client repository dead (§6.3). |
| `FiberShipmentService.cs:12,17,21` | `_timeProvider` | Delete the field and constructor parameter |
| `FiberShipmentsController.cs:20`, `ProfileController.cs:19` | `_logger` | Delete the field and constructor parameter. Update `FiberShipmentsControllerTests.cs:19-21` and `ProfileControllerTests.cs:20-22`. If CX-14 lands later and needs logging, it uses `ILoggerFactory`. |
| `Portfolio.Tests/Services/BatchGeocodingServiceTests.cs:47` | `BuildGeocodeResponse` | Delete |

### 6.3 Unused public and internal symbols
Method: Roslyn `MSBuildWorkspace` over the solution (all 4 production projects compiled with 0 errors in the workspace) and `SymbolFinder.FindReferencesAsync` across all projects, tests included. Framework entry points are excluded: actions, handlers, overrides, interface implementations of referenced members, and DTO properties. Remaining names were then text-searched in cshtml and JS. Full list: `<scratch>\metrics\deadcode.tsv`.

| Symbol | Evidence | Verdict |
|---|---|---|
| `IFiberClientRepository` (5 members) and `FiberClientRepository` | Only consumer is the unread `_clientRepo`; registration `Program.cs:54`; one test mock `FiberOrderServiceTests.cs:14,23` | **Delete the chain** |
| `FiberClientDto`, `CreateFiberClientDto` (`Portfolio.Common/DTOs/FiberClientDto.cs:6,42`) | No references; Fiber client CRUD was removed | **Delete the file** (not in OpenAPI: no action uses them) |
| Repository members never called anywhere (interface plus implementation): `IFiberInventoryTransactionRepository.GetByMaterialAsync`; `IFiberMaterialRepository.GetLowStockAsync`; `IFiberOrderRepository.GetByStatusAsync`, `GetByDateRangeAsync`; `IPropertyRepository.GetAllAsync`, `AddAsync`, `DeleteAsync`, `ExistsAsync`, `CountAsync`; `ISavedFeatureRepository.GetByIdAsync`, `ExistsAsync`; `ISavedSearchRepository.GetByIdAsync`, `UpdateAsync` | Not in tests either | **Delete.** 13 members plus 13 implementations. `Portfolio.Benchmarks/DeterministicPropertyRepository.cs` keeps compiling: its extra methods simply stop implementing the interface. |
| `GoogleProfileDto.AnonymousUserId` (`Portfolio.Common/DTOs/GoogleProfileDto.cs:21`) | Written once (`Program.cs:367`), never read; the service re-reads the cookie (`UserProfileService.cs:170-179`) | **Delete the property and the assignment.** Keep `anonId` in Program.cs: it drives the cookie delete at `:371-374`. |
| `ProfileClaimTypes.Protected` | Unused, but two private copies exist | **Use it** (§5.5) |
| `SyntheticChangeScene.DefaultNoiseLevel` | Unused while a literal duplicates it | **Use it** (§5.5) |
| `DesignTimeDbContextFactory`, `ErrorModel.ShowRequestId`, 9× bridge `IsAvailable` | Used by EF tooling through reflection, by `Error.cshtml:11`, and by the bridge pattern respectively | **False positives / keep** |

### 6.4 Packages and project hygiene (every `PackageReference` checked against `obj/project.assets.json` and code usage)
| Item | Evidence | Recommendation |
|---|---|---|
| `Microsoft.EntityFrameworkCore.InMemory` in `Portfolio.Repositories.csproj:13` | **Not dead.** Used by tests through `UseInMemoryDatabase` (`UserProfileSeedServiceTests.cs:18`, `UserProfileServiceTests.cs:32`), reaching them only transitively. It also lands in **Portfolio.Web's** dependency graph and so in the production image. Production code references it only by provider-name string (`UserProfileSeedService.cs:13`). | **Move** the `PackageReference` to `Portfolio.Tests.csproj`. Validated: Web's `project.assets.json` no longer lists the library. |
| `<PackageVersion Include="xunit" Version="2.9.3" />` (`Directory.Packages.props:28`) | No project resolves `xunit/2.x` | Delete |
| `<Folder Include="Migrations\" />` (`Portfolio.Repositories.csproj:26-28`) and `<Folder Include="obj\Debug\net10.0\…XmlCommentGenerator\" />` (`Portfolio.Web.csproj:36-38`) | No-ops; the second points into `obj/` | Delete both |
| `Microsoft.OpenApi` (`Portfolio.Web.csproj:25`, `Portfolio.Tests.csproj:31`) | No code uses the namespace. It is the advisory pin (GHSA-v5pm-xwqc-g5wc); transitive pinning is on. | **Keep.** Explicit is clearer for a security pin, and NU1903-as-error would catch a regression. |
| `Microsoft.EntityFrameworkCore.Relational` and `.Tools` (Repositories and Web) | Relational APIs are used; Tools supports PMC and `dotnet ef` flows | Keep. Removal is unverifiable without running PMC. |
| Every other reference (Asp.Versioning ×2, Google auth, DataProtection Redis, AspNetCore.OpenApi, Caching Redis, Http.Resilience, Npgsql, Scalar; test packages) | Used | Keep |
| `Portfolio.Web/Portfolio.Web.csproj.user` | Tracked in git (`ActiveDebugProfile=https`), and `.gitignore` has no `*.user` | OD-12: `git rm --cached` and add `*.user` |

### 6.5 Dead registrations and data flow
- **Shadowed `AddScoped`** at `Program.cs:129`, `:154` and `:179`. Each is followed by `AddHttpClient<I,T>()` for the same interface, which registers a **transient** typed client that wins resolution. I verified this empirically with a DI probe: 2 descriptors (Scoped, Transient), and two resolutions in one scope return different instances. The factory probe also captured 2 descriptors. Delete the three `AddScoped` lines; single-instance resolution is unchanged and nothing enumerates these services.
- **`AddMemoryCache()`** (`Program.cs:261-262`): no `IMemoryCache` consumer exists in app code. Remove it only with the CX-01 boot test in place (OD-10).
- **Session** (`Program.cs:264-272`, `:582`): the only consumer is `HttpContext.Session.Clear()` in `Pages/Logout.cshtml.cs:14`, and nothing ever writes the session. `[TempData]` (`Areas/Account/Pages/Profile.cshtml.cs:33`) uses the cookie provider. Removing it changes the documented pipeline (OD-10; default: keep).

### 6.6 Dead page model
`Portfolio.Web/Pages/Projects/Details.cshtml.cs` (30 lines) is an `[Authorize]` `DetailsModel` with a TODO. **No `Pages/Projects/Details.cshtml` has ever existed** (`git log --follow` is empty), so Razor never routes it and nothing references the type. It is 0/8 covered. **Delete it**; there is no route or behaviour change.

### 6.7 Deprecated and obsolete endpoints (all public-facing, so owner decisions)
| Endpoint | Callers (wwwroot/js, cshtml, tests, docs) | Recommendation |
|---|---|---|
| `POST /api/v1/homefinder/score` (`HomeFinderController.cs:47-58`, `[Obsolete]`, `IgnoreApi`) | **No JS caller** (`api-config.js:51-53` has search, property and searches only). Tests reference it only as `ApiVersioningAttributeTests.cs:118` attribute data. Documented in `README.md:160` and in public page copy `Pages/Projects/HomeFinder/Details.cshtml:48`. | **OD-4.** Default: remove, together with the test `InlineData`, README, Details copy and `copilot-instructions.md`. External callers can't be ruled out from the repo; check Render logs. |
| `GET /api/v1/homefinder/properties/{id}` (`:72-83`) | Same; `ApiVersioningAttributeTests.cs:120` | Same as above (OD-4) |
| `POST /api/v1/geocoding/batch/sync` (`BatchGeocodingController.cs:116`, `[Obsolete]` `:120` "kept for test compatibility") | **LIVE.** `wwwroot/js/BatchGeocoding/app.js:194` → `api-config.js:24` `batchSync` is the demo's **only** geocoding call. 5 controller tests. `BatchGeocodingService.cs:49` repeats the false "legacy" note. | **OD-5. Default: keep. Remove `[Obsolete]`**: no runtime effect, but coverlet will count it. Fix the two misleading comments and the public copy at `BatchGeocoding/Details.cshtml:46`, which describes the async path as primary. Exposing it in OpenAPI is optional. |
| `POST /api/batchgeocoding/sync` (absolute, unversioned alias, `:117`) | No in-repo caller; only `copilot-instructions.md:400` | **OD-6.** Default: remove, in the OD-5 PR |
| `POST /api/v1/geocoding/batch` plus `GET …/{jobId}/status` (async job) | **No frontend caller.** Tested; documented as primary on the Details page. | Keep; flagged for the frontend agent and owner (the demo could adopt it) |

### 6.8 Validation run of the dead-code batch (scratch only)
`<scratch>\tools\apply_deadcode.py` applied §6.2–§6.6 (excluding §6.7), plus the `double.IsFinite` and protected-set dedupes, to `<scratch>\repo-dead`. Results:
- **CI build: 0 warnings, 0 errors.**
- **Tests: 697/697**, both normally and with `PORTFOLIO_DISABLE_NATIVE=1`.
- **Coverage: 84.19 % line (12151/14432), 67.49 % branch (2126/3150).**
- Diff: 40 files, +56/−409. After the IDE0005 sweep: 89 files, +110/−509, still 0 warnings and 697/697.
- Reference diff: `<scratch>\metrics\deadcode-validation.diff`. Use it as a guide, not as a patch: the script wrote LF line endings.
- Every run in this section used SDK 10.0.401.

## 7. Enforcing a complexity ceiling

### 7.1 Recommended: CA1502 at 15 (first-party; no new packages; verified working on this repo)
- `CodeMetricsConfig.txt` at the repo root containing `CA1502: 15`. CA1502 fires when the value is **greater than** the threshold; I verified this at threshold 1.
- In `Directory.Build.props`: `<ItemGroup><AdditionalFiles Include="$(MSBuildThisFileDirectory)CodeMetricsConfig.txt" /></ItemGroup>`
- In `.editorconfig` (comments on their own lines; Roslyn does not support inline comments after a value):
  ```
  [*.cs]
  dotnet_diagnostic.CA1502.severity = warning

  [**/Migrations/*.cs]
  dotnet_diagnostic.CA1502.severity = none

  # Analyzers only; coverlet does not read .editorconfig, so coverage is unaffected.
  [Portfolio.Services/Data/RedlandsRoadNetwork.cs]
  generated_code = true

  # Or fix Program.Render (CA1502 16) instead of exempting the project.
  [Portfolio.Benchmarks/**.cs]
  dotnet_diagnostic.CA1502.severity = none
  ```
  CI already sets `TreatWarningsAsErrors` (`Directory.Build.props:15`), so a violation fails the build. Locally it stays a warning.

  The migration and network-file exclusions are belt-and-braces: migrations peak at CA1502 2 and the network `Build()` is 1. Designer and snapshot files already carry `<auto-generated>`.
- **What CA1502 measures:** it counts `?.` and `??` but not `catch`, pattern `and`/`or`, switch-expression arms or `??=` (§2.1). Implementers can't "game" it by converting `if` chains into switch expressions; the cognitive report would still show that.

### 7.2 Violations today
| Threshold (fires above) | Production | Tests | Benchmarks |
|---|---|---|---|
| 10 | 37 | 0 | 1 |
| **15** | **17** | 0 | 1 (`Program.Render` 16) |
| 20 | 7 | 0 | 0 |
| 25 | 7 | 0 | 0 |

The 17 at threshold 15:

| Member | CA1502 |
|---|---|
| `SeedForUserAsync` | 251 |
| `<Main>$` | 61 |
| `ParseAddress` | 31 |
| `LabelComponentsManaged` | 29 |
| `DetectAsync` | 28 |
| Fleet `Validate` | 28 |
| `ConstructSavings` | 26 |
| Response `SolveManaged` | 20 |
| ChangeDetection `BuildResult` | 19 |
| `SelectSpreadNodes` | 18 |
| `ValidateLocations` | 18 |
| `SpatialJoinAsync` | 18 |
| `AnonymousUserMiddleware.InvokeAsync` | 18 |
| Fleet `BuildResult` | 17 |
| `OrOptMove` | 16 |
| `ProposeRestorationAsync` | 16 |
| `ValidateGraph` | 16 |

### 7.3 Ratchet
Land CX-16 early, with a **baseline of suppressions**: one `[assembly: SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Scope = "member", Target = "~M:…", Justification = "Baseline; remove in CX-xx")]` per violator, in `GlobalSuppressions.cs` for Services, Repositories and Web. `<Main>$`'s target is `~M:Program.<Main>$(System.String[])`. Each refactor package deletes its own lines. The four data generators keep theirs permanently, with a justification (CX-13).

New code can't regress from day one. As an alternative, start at 25 (7 violators) and lower the threshold to 15 after the refactors. That is simpler but lets new 16–25 methods in meanwhile.

### 7.4 Cognitive complexity
There is no first-party analyzer for it. Options:
- (a) `SonarAnalyzer.CSharp` S3776 (default threshold 15). It is a third-party package bringing roughly 400 rules that would all need configuring, so it is an owner decision.
- (b) Commit the csmetrics tool and gate on a JSON baseline. That adds a maintained tool.
- (c) **Report-only:** re-run `<scratch>\tools\csmetrics` per package and put the before/after in the PR.

Recommended default: **(c)** (OD-8).

For JS there is no gate without Node tooling (frontend agent and owner). The acorn approach relies on Node internals, so it is suitable for reports but not for CI.

### 7.5 Coverage interplay
CX-01 and CX-02 add tests for Web, which raise real coverage; the probe alone reached 87.16 %. After that, the owner can choose to exclude the generated network file and re-baseline the line ratchet honestly (OD-7). Doing that first would fail the gate at about 73 %.

## 8. Recommended work items
Size: **S** ≤ ~100 changed lines or ≤ 5 files; **M** ≤ ~500 lines or ≤ 15 files; **L** larger. "Tests first" are the characterization tests to add **before** the change, in the same package.

| ID | Pri | Title | Size | Depends on |
|---|---|---|---|---|
| CX-01 | P1 | WebApplicationFactory characterization suite (pipeline order, DI snapshot, resilience) | M | OD-1 |
| CX-02 | P1 | Move Program.cs URI/mask helpers to `ConnectionStrings` (verbatim) and unit-test them | S | — |
| CX-03 | P1 | Validated dead-code batch A (§6.2–6.6, IsFinite, protected set) | M | — |
| CX-04 | P2 | Program.cs decomposition (+ resilience helper, cookie events class) | M | CX-01, CX-02, security edits to Program.cs |
| CX-05 | P2 | `AnonymousUserMiddleware`: tests, then extract methods | M | — (CX-01 helps) |
| CX-06 | P2 | `UserProfileSeedService` → data-driven seed | L | OD-3 |
| CX-07 | P2 | `FleetRoutingService` split | M | CX-03 |
| CX-08 | P2 | `ResponseCoverageService` split | M | CX-03 |
| CX-09 | P2 | `ChangeDetectionService` split | M | CX-03 |
| CX-10 | P2 | `SpatialGraphService` simplification | M | — |
| CX-11 | P2 | `NativeKernelLoader`: bridge consolidation | M | — |
| CX-12 | P3 | Remaining >15 members (§3.7) | S each | CX-03; CX-11 for the VRP bridge |
| CX-13 | P3 | Exempt the 4 data generators (suppressions with justification) | S | CX-16 |
| CX-14 | P3 | Controller exception filters (action-level) | M | CX-01, OD-11 |
| CX-15 | P2 | Delete 12 Fiber `catch (Exception)` blocks | S | OD-2 |
| CX-16 | P2 | CA1502 enforcement with baseline suppressions | S | — |
| CX-17 | P4 (last) | IDE0005 sweep with `dotnet format` | M (many files) | Everything else |
| CX-18 | P3 | Endpoint/obsolete cleanup per OD-4/5/6 | S | Owner |
| CX-19 | P3 | Small dedupes (DefaultNoiseLevel; 503 retryAfter ↔ breaker profile) | S | CX-04 / CX-14 |
| CX-20 | — | JS splits (§3.8): description only | — | Frontend agent |
| CX-21 | P3 | Untrack `Portfolio.Web.csproj.user` | S | OD-12 |

### CX-01: Characterization infrastructure (P1, M)
- **Evidence:** Program.cs 0/451 and middleware 0 % (§1.4). No factory or TestServer exists (§4.1).
- **Fix:**
  - Add `<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />` and a `PackageReference` in `Portfolio.Tests.csproj`.
  - Add `Portfolio.Tests/Integration/PortfolioAppFactory.cs`, using the recipe in §4.3 and the probe source at `<scratch>\probe\PipelineCharacterizationTests.cs`.
  - Add `PipelineOrderTests.cs` (tests 1–6 plus the expensive policy), `ServiceRegistrationSnapshotTests.cs` (golden sorted list) and `ArcGisResilienceTests.cs` (3 and 4 attempts).
- **Files:** `Directory.Packages.props`, `Portfolio.Tests/Portfolio.Tests.csproj`, `Portfolio.Tests/Integration/*` (new).
- **Risk:** low (tests only). Rate-limit tests need a factory per test. Retry tests take 1–2.5 s each.
- **Verify:** the full suite passes twice (with and without native disabled), and coverage rises.

### CX-02: Connection-string helpers (P1, S)
- **Evidence:** `Program.cs:675-812` are top-level local functions, so they can't be tested.
- **Fix:** move them verbatim to `internal static class ConnectionStrings` in `Portfolio.Web/Infrastructure/ConnectionStrings.cs`. Call sites are `:208`, `:218` and `:507`. Add `Portfolio.Tests/Infrastructure/ConnectionStringsTests.cs` with the cases in §4.3.
- **Risk:** very low. Note for security: the exception messages at `:686` and `:765` embed the raw connection string (§11).
- **Overlap:** touches Program.cs, so serialize with the security agent, CX-03 and CX-04.

### CX-03: Dead-code batch A (P1, M; validated in scratch)
- **Fix:** everything in §6.2–§6.6 except Session and `AddMemoryCache`, plus `double.IsFinite` (§5.4, the 6 helpers) and the protected-claim dedupe (§5.5). Use `<scratch>\tools\apply_deadcode.py` as the exact list.
- **Files (40):**
  - `Directory.Packages.props`
  - csproj ×3 (Repositories, Tests, Web)
  - `Program.cs` (5 lines)
  - `Common/DTOs/FiberClientDto.cs` (delete), `Common/DTOs/GoogleProfileDto.cs`
  - `Repositories/Interfaces/*` ×7, `Repositories/Repositories/*` ×7 (2 deleted)
  - Services: `AddressStandardization`, `CatRisk`, `ChangeDetection`, `FiberOrder`, `FiberShipment`, `FleetRouting`, `ResponseCoverage`, `SpatialCluster`, `SpatialOverlay`, `Data/SyntheticChangeScene`
  - Web: `Controllers/Api/{FiberShipments,Profile,UserProfile}Controller.cs`, `Pages/Projects/Details.cshtml.cs` (delete)
  - Tests: `{FiberShipmentsController,ProfileController,FiberOrderService,FiberShipmentService,BatchGeocodingService}Tests.cs`
- **Risk:** low. The constructor removals are compile-checked, and DI resolves by type.
- **Tests first:** none needed beyond the existing suite. If CX-01 has landed, update the DI snapshot expectations (two fewer descriptors each for 3 services, and no `IFiberClientRepository`).
- **Verify:** 0 warnings; 697 tests or more; coverage ≥ 84.19/67.49 (the measured result).

### CX-04: Program.cs decomposition (P2, M)
- **Fix:** as described in §4.2, with resilience per §5.1 and `PortfolioCookieEvents`.
- **Files:** `Program.cs` and `Portfolio.Web/Startup/*.cs` (new, about 8 files).
- **Risk:** medium. Order and registration drift are guarded by CX-01.
- **Tests first:** CX-01 and CX-02, plus a `PortfolioCookieEvents.SignedIn` unit test: anonymous cookie present → merge DTO, cookie deleted, `Items["PortfolioIdentity"]` set.
- **Verify:** CX-01 passes unchanged, and `<Main>$` CA1502 drops from 61 to under 10.

### CX-05: AnonymousUserMiddleware (P2, M)
- **Evidence:** `InvokeAsync` 18/21 at `:35-143`; 0 % covered.
- **Fix:** extract `TryResolveAuthenticatedAsync`, `TouchProfileAsync` (keeps the 5-minute throttle at `:93`), `CreateAnonymousAsync` (profile, seed, cookie) and `BuildCookieOptions`. Keep the `Secure = !IsDevelopment()` rationale comment at `:124-127`.
- **Tests first** (unit level, with `DefaultHttpContext`, EF InMemory, a real `UserProfileSeedService` and a fake `IAuthenticationService`):
  - key already in `Items` → passthrough with no database access
  - authenticated with a claim → `Items` set
  - authenticated without a claim → `Items` not set
  - valid cookie with a stale profile → `LastActiveDate` updated; fresh profile → no write
  - valid cookie without a profile → profile created, **no seed**
  - no cookie with an HTML Accept → profile, seed and cookie (HttpOnly, Secure only outside Development, Lax, +1 year)
  - no cookie and not HTML → passthrough, no profile
  - an invalid GUID cookie behaves like no cookie
- **Files:** the middleware file and new tests.

### CX-06: Data-driven seed (P2, L)
- Per §3.1. **Golden snapshot first.** If OD-3 says remove, the properties block is dropped. Any cleanup of existing production rows goes through a new `dotnet ef` migration, never by editing an existing one.
- **Files:** `UserProfileSeedService.cs`, `Services/Data/FiberDemoSeedData.cs` (new), tests.
- **Risk:** medium, from transcription errors; the golden snapshot guards against them.

### CX-07, CX-08, CX-09, CX-10: Service splits (P2, M each)
- Per §3.3–§3.6. **Golden output tests first**; they must pass with native disabled and with native present.
- Each package re-runs the native-present suite (CMake build) because results must stay identical. Delete the matching CA1502 baseline suppressions.
- The four packages touch disjoint files and can run in parallel after CX-03.

### CX-11: NativeKernelLoader (P2, M)
- Per §5.3. **Files:** 13 `*NativeBridge.cs` files and the new `NativeKernelLoader.cs`.
- **Risk:** low.
- **Verify:** the native-present run with all 13 kernels (protocol in §12) and NativeParityTests.

### CX-12: Remaining members above 15 (P3, S each)
- Per §3.7, with golden tables first for `ParseAddress`. Split by file to avoid conflicts.

### CX-13: Data-generator exemptions (P3, S)
- Suppressions with a justification ("output is shipped data / migration-coupled") for the four generators named in §3.7.

### CX-14: Controller exception filters (P3, M, optional)
- Per §5.2 (action-level only). It rewrites about 23 tests that assert 400 and about 12 that assert 503.
- **Files:** about 17 controllers, the new `Portfolio.Web/Filters/*`, and about 12 controller test files. This **conflicts with security** (rate-limit attributes on the 7 spatial controllers) and with the comments agent.

### CX-15: Fiber catch-alls (P2, S, OD-2)
- **Files:** `FiberMaterialsController.cs`, `FiberOrdersController.cs`, `FiberDashboardController.cs` and their 3 test files (12 asserts change to "throws"). Add integration tests for 404 (unknown id on receive/update) once CX-01 exists.

### CX-16: CA1502 enforcement (P2, S)
- Per §7.1 and §7.3. **Files:** `CodeMetricsConfig.txt` (new), `Directory.Build.props`, `.editorconfig`, and `GlobalSuppressions.cs` for Services, Repositories and Web (new).
- **Verify:** the CI build has 0 warnings. A temporary violation (method at 16) fails CI. Revert it.

### CX-17: IDE0005 sweep (P4, last)
- `dotnet format style --diagnostics IDE0005 --exclude Portfolio.Repositories/Migrations` (§6.1). It touches about 49 files and sorts usings. Run it together with the comments and XML-docs pass.

### CX-18: Endpoint cleanup (P3, S, owner-gated)
- Per §6.7.

### CX-19: Small dedupes (P3, S)
- Per §5.5 and §5.1.

### CX-21: Untrack `Portfolio.Web.csproj.user` (P3, S, OD-12)
- `git rm --cached Portfolio.Web/Portfolio.Web.csproj.user` and add `*.user` to `.gitignore`.

## 9. File-overlap / conflict matrix
| File or area | Packages touching it | Sequencing advice |
|---|---|---|
| `Portfolio.Web/Program.cs` | security (forwarded headers, rate limits, AllowedHosts?), CX-02, CX-03, CX-04, CX-17, comments | Serialize: security → CX-03 → CX-02 → CX-04 → comments/CX-17. CX-01 can be written in parallel because it adds no Program.cs edits. |
| `Directory.Packages.props`, `Portfolio.Tests.csproj` | CX-01 (Mvc.Testing), CX-03 (xunit pin, InMemory, global usings), security (package bumps) | Trivial merges. Prefer one owner for both files in a wave. |
| `Controllers/Api/*` | security (`[EnableRateLimiting]` on 7 spatial controllers, size limits), CX-03 (3 controllers), CX-14, CX-15, CX-19 (`ChangeDetectionController`), comments | CX-14 last among the code packages, if done at all |
| `Services/Services/{Fleet,ResponseCoverage,ChangeDetection,CatRisk,SpatialCluster,SpatialOverlay,AddressStandardization}*` | security (input caps, NaN checks), CX-03 (IsFinite, regexes), CX-07/08/09/12 | Security validation fixes and CX-03 first, then the splits |
| `Services/Native/*Bridge.cs` | CX-11, CX-12 (`VrpSolverNativeBridge`) | CX-11 first |
| `Middleware/*` | security (CSP in `SecurityHeadersMiddleware`, detail leakage in `ApiExceptionMiddleware`), CX-05 (`AnonymousUserMiddleware` only) | Disjoint files, so they can run in parallel |
| `Portfolio.Tests/Controllers/*` | CX-03, CX-14, CX-15 | Serialize CX-14 after CX-15 |
| `.editorconfig`, `Directory.Build.props` | CX-16, possibly a security or CI-analyzer package | Serialize |

## 10. Decisions needed from the owner (with recommended defaults)
| ID | Decision | Default |
|---|---|---|
| OD-1 | Add `Microsoft.AspNetCore.Mvc.Testing` 10.0.10 (test-only) | **Yes.** Unblocks CX-01, CX-04, CX-14, CX-15 and the security integration tests. |
| OD-2 | Delete the 12 Fiber `catch (Exception)` blocks. Unknown ids go from 500 to 404, missing user from 500 to 409, and 500 body text changes. | **Yes** (a bug fix) |
| OD-3 | The seeder re-inserts 40 legacy Home Finder listings that the 2026-07-19 migration meant to remove | **Remove the block** in CX-06. Leave existing production rows alone unless a count check shows clutter you want cleaned. |
| OD-4 | Remove `homefinder/score` and `properties/{id}` aliases | **Remove**, with README, Details copy and test updates (check Render logs first) |
| OD-5 | Batch sync endpoint: drop `[Obsolete]` (it is the live path) and fix the copy | **Yes, keep the endpoint.** Exposing it in OpenAPI is optional. |
| OD-6 | Remove the unversioned `/api/batchgeocoding/sync` alias | **Remove** |
| OD-7 | Exclude the generated network from coverage and re-baseline the ratchet | **Not now.** Revisit after CX-01 raises Web coverage. |
| OD-8 | Cognitive gate via SonarAnalyzer S3776 | **No.** CA1502 gate plus cognitive reported per PR. |
| OD-9 | Managed "mirror" functions diverge structurally from C++ after refactoring | **Refactor the managed side only** (extract-method, same iteration order) and keep a "mirrors `<native fn>`" note. C++ unchanged; golden and parity tests guard behaviour. |
| OD-10 | Remove `AddSession`/`UseSession` and `AddMemoryCache` | **Keep Session** (pipeline-order constraint). Remove `AddMemoryCache` only after the CX-01 boot test passes without it. |
| OD-11 | Do CX-14 (exception filters) at all | **Optional:** only after P1/P2 |
| OD-12 | Untrack `Portfolio.Web.csproj.user` | **Yes** |

## 11. Cross-references for other agents (not acted on here)
- **Security:**
  - `UserProfileController.RemoveClaim` (`DELETE /api/v1/users/me/claims/{type}`, `:153-166`) has **no protected-type check**, while `ProfileController.RemoveClaim` (`:85-104`) does. An authenticated user can delete their own `google_id` or `email` claims.
  - `Program.cs:686` and `:765` put the **raw connection string** (password) into exception messages at startup.
  - `ApiExceptionMiddleware.cs:55` returns `InvalidOperationException.Message` as 409 detail (e.g. "UserId not available").
  - `AnonymousUserMiddleware.cs:47` calls `AuthenticateAsync` itself, before `UseAuthentication`.
  - `AddressStandardizationService.cs:16-17` hard-codes the ArcGIS URL; the other services use options.
  - A malformed graph (an edge to an undeclared node) yields 404 instead of 400 (§3.6).
- **Frontend:**
  - The demo uses the sync batch endpoint; the async endpoints are unused (§6.7).
  - Local `showToast` copies: `HomeFinder/app.js:668`, `StateExplorer/uiManager.js:415`.
  - Private `escapeHtml`: `uiManager.js:13`.
  - The error reader accepts only `body.error`, so unhandled 400s show the generic message.
  - JS splits are in §3.8.
- **Comments/docs:**
  - Stale "Google is the default challenge scheme" (`Program.cs:397-402`).
  - "kept for test compatibility" (`BatchGeocodingController.cs:120`) and "legacy" (`BatchGeocodingService.cs:49`) are false.
  - `AnonymousUserMiddleware.cs:13` and `agent-context.md` §6 say `Items["AnonUserId"]`, but the key is `"PortfolioIdentity"` (`:23`).
  - "Example shipment seeds (add more as needed)" (`UserProfileSeedService.cs:766`).
  - The `agent-context.md` test count is about 680; the measured count is 697.

## 12. Could not verify, and caveats
- **Production data:** I could not check how many Home Finder rows exist (OD-3), or whether anything calls the deprecated endpoints (no Render log access).
- **TestHost version:** `WebApplicationFactory` was proven with **8.0.11** from the cache; 10.0.10 could not be downloaded offline. The 7 probe tests avoided MVC JSON bodies, because of the 8.x TestHost incompatibility.
- **Native test configuration:** the native-present run used Debug (as CI does) and Windows DLLs. CI uses Linux `.so` files. The C++ compiled with 0 warnings at MSVC's default level; no `/W4` run was made.
- **The 11:29 test abort:** this was the .NET 10 runtime being removed by the VS installer, not a code problem. The re-run on 10.0.401 / 10.0.12 at 11:41 passed 697/697 with native present and with native disabled.
- **Measurement conventions:**
  - Cognitive recursion detection is a name-and-arity heuristic.
  - JS functions are measured independently (ESLint style); SonarJS would fold callbacks into their parents.
  - LOC "code" uses a line heuristic for comments.
- **Scratch lifetime:** the scratch directory is session-specific. If Stage 2 runs in a new session, copy `<scratch>\tools\` somewhere durable first, or ask the owner to commit it under `tools/metrics/`.

### How to re-run (all from a scratch clone; never in the repo)
| What | Command |
|---|---|
| Baseline build, tests and gate | `bash <scratch>\tools\baseline.sh` (paths are hard-coded to `<scratch>\repo`). The gate alone: run `py -I <scratch>\tools\coverage_gate.py` from the clone root after `dotnet test … --results-directory TestResults`. |
| Native present | Build the kernels with short `-B` directories and stage the DLLs into `Portfolio.Tests/bin/Debug/net10.0` (`<scratch>\tools\native2.sh`), then `<scratch>\tools\native-test.sh`. CMake: `"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"`. |
| C# metrics | `dotnet build -c Release <scratch>\tools\csmetrics`, then `dotnet <scratch>\tools\csmetrics\bin\Release\net9.0\csmetrics.dll <clone> <out.csv>`, then `py -I <scratch>\tools\summarize_cs.py <out.csv> 25` |
| Exact CA1502 values | In a clone, add `CodeMetricsConfig.txt` (`CA1502: 1`), the `.editorconfig` severity and the `AdditionalFiles` item. Build with `dotnet build -c Release --no-incremental -p:ContinuousIntegrationBuild=false > log`, then `py -I <scratch>\tools\parse_analyzers.py log <cs-metrics.csv> <outdir>`. This also yields the IDE0005/0051/0052 lists if those severities are set. |
| JS | `node --expose-internals <scratch>\tools\jsmetrics.js <clone> <out.csv>`, then `py -I <scratch>\tools\summarize_js.py <out.csv>` |
| C++ | `bash <scratch>\tools\cpp-complexity.sh <clone> <log>`, then `py -I <scratch>\tools\parse_clang_tidy.py <log>` |
| Semantic dead code | `dotnet build -c Release <scratch>\tools\deadcode`, then `dotnet <scratch>\tools\deadcode\bin\Release\net10.0\deadcode.dll <clone>\Portfolio.sln <out.tsv>` |
| LOC | `py -I <scratch>\tools\loc.py <clone>` |

Artifacts:

| Artifact | Path |
|---|---|
| C# metrics | `<scratch>\metrics\cs-metrics.csv`, `cs-summary.md` |
| JS metrics | `js-metrics.csv`, `js-summary.md` |
| C++ summary | `cpp-summary.md` |
| Analyzer output | `analyzers\` (`CA1502.csv`, `IDE0005.txt`, `IDE0051.txt`, `IDE0052.txt`), `analyzers-summary.txt` |
| Coverage by file | `coverage-by-file.md` |
| Dead-code list | `deadcode.tsv` |
| Dead-code validation diff | `deadcode-validation.diff` |
| Coverage XML | `<scratch>\coverage\{normal,nonative,native,normal-401,nonative-401,deadcode-validation,wf-probe}\coverage.cobertura.xml` |
| Factory probe source | `<scratch>\probe\PipelineCharacterizationTests.cs` |
| Logs | `<scratch>\logs\` |