# Industry Vertical Projects — Implementation Plans

> **Historical design document — these plans shipped in July 2026.** All five verticals and the
> graph-engine extensions are in the repository. The conventions below were written before they
> shipped and have been corrected only where an agent following them would go wrong; for the
> current facts and conventions read [`docs/agent-context.md`](../agent-context.md). Where this
> file disagrees with agent-context.md or the source, they win. Corrections are marked
> **Update:**.

Five new projects that carry the existing spatial + native-compute stack into industries
that hire heavily in .NET. Each is a full vertical slice following the established
native-kernel MVP pattern.

The conventions shared by all five plans are collected below. The per-project documents
assume them and only describe what is project-specific.

| # | Plan | Industry | New kernel | Depends on |
|---|---|---|---|---|
| 0 | [Graph engine extensions](00-graph-engine-extensions.md) | — | *(extends `spatial_graph_engine`)* | — |
| 1 | [CAT Risk Analyzer](01-cat-risk.md) | Insurance / reinsurance | `cat_risk_kernel` | — |
| 2 | [Fleet Route Optimizer](02-fleet-vrp.md) | Logistics / last-mile | `vrp_solver_kernel` | Plan 0 |
| 3 | [Outage Manager](03-outage-trace.md) | Utilities / energy | `network_trace_kernel` | — |
| 4 | [Response Coverage Optimizer](04-response-coverage.md) | Public safety | `facility_location_kernel` | Plan 0 |
| 5 | [Change Detection](05-change-detection.md) | Defense / GEOINT | `change_detection_kernel` | — |

### Recommended build order

1. **Plan 1 (CAT Risk)** — highest differentiation, zero dependencies, pure stateless kernel.
2. **Plan 5 (Change Detection)** — self-contained; reuses the Terrain raster-grid UI patterns.
3. **Plan 3 (Outage Manager)** — self-contained; new dataset + new kernel, no shared prerequisites.
4. **Plan 0 (Graph extensions)** — small, but blocks the next two.
5. **Plan 2 (Fleet VRP)** and **Plan 4 (Response Coverage)** — do together, they share Plan 0.

Each plan is independently shippable. Do not start two at once.

---

## Ground truth about the existing codebase

Verified against the repo. Do not re-derive; do not assume otherwise.

### The reference implementation is **Spatial Overlay**

When in doubt, copy Overlay, not Viewshed. Overlay's bridge returns a raw primitive array
and the *service* sets `NativeAccelerated` at the decision site, so DTO assembly exists in
exactly one place. Viewshed's bridge builds the whole result DTO and hardcodes
`NativeAccelerated = true` inside the bridge, with `false` hardcoded separately in the
service — two literals in two files that can drift. **Use the Overlay shape.**

Files to read before writing anything:

```
native/spatial_overlay_kernel/include/spatial_overlay_kernel.h
native/spatial_overlay_kernel/src/spatial_overlay_kernel.cpp
native/spatial_overlay_kernel/CMakeLists.txt
Portfolio.Common/DTOs/SpatialOverlayDtos.cs
Portfolio.Services/Interfaces/ISpatialOverlayService.cs
Portfolio.Services/Services/SpatialOverlayService.cs
Portfolio.Services/Native/SpatialOverlayNativeBridge.cs
Portfolio.Services/Native/SpatialOverlayNativeInterop.cs
Portfolio.Services/Native/SpatialOverlayNativeStructs.cs
Portfolio.Web/Controllers/Api/SpatialOverlayController.cs
Portfolio.Web/Pages/Projects/Overlay/Index.cshtml
Portfolio.Web/wwwroot/js/Overlay/app.js
```

### Road network facts

- Lives at `Portfolio.Services/Data/RedlandsRoadNetwork.cs` — `internal static class`,
  namespace `Portfolio.Services.Data`, single `public static RoadGraphDto Build()`.
  587 KB, 5,765 lines, generated from OSM. **Never hand-edit.**
- **2,530 nodes, 3,187 edges.** Node IDs are **contiguous 1..2530 — one-based, not zero-based.**
  Array index is `id - 1`. `EsriHqNodeId = 2530` (which equals `nodes.Count`).
- **Every edge is `Bidirectional = true`.** Cost is along-road kilometres.
- Edges have no ID, no street name, no speed, no capacity, no direction in practice.
  Street names live only on `GraphNodeDto.Label`.
- `SpatialGraphService` holds `private static readonly RoadGraphDto _cachedGraph = RedlandsRoadNetwork.Build();`
  and hands out **the same mutable instance** to every caller. Never mutate it in place.
- New server-side features should call `RedlandsRoadNetwork.Build()` (or the cached graph)
  directly rather than making the client round-trip the graph. The existing Route Planner
  page uploads ~425 KB per request; **do not copy that.**

### Native ABI conventions

- `#pragma pack(push, 8)` / `#pragma pack(pop)` around every struct.
- `extern "C"`, `__declspec(dllexport)` on `_WIN32` / `__attribute__((visibility("default")))` elsewhere.
- Every pointer parameter is followed by its own `int` length parameter.
- **Flat-buffer idiom for ragged data:** one contiguous element buffer plus an `int[]`
  of per-group sizes (see `Overlay_AssignPointsToZones`'s `ringSizes`). Reuse it.
- Status codes: `0` success, `-1` null/invalid argument, `-2` id not found,
  `-3` output buffer too small, `-99` unhandled exception (`catch (...)`).
  A function that returns a *count* returns `>= 0` on success and negative on error.
- Preallocate everything managed-side. Native code never allocates across the boundary.
- **Never use `std::map::operator[]` on a possibly-missing key.** It default-constructs to
  `0.0`, which in a shortest-path context means "reachable at zero cost". **Update:** this
  was a real latent bug in the first `spatial_graph_engine.cpp`; the CSR rewrite fixed it
  (edges to undeclared nodes are dropped when the graph is built). The rule still stands for
  new native code: use `.find()` and check.

### Managed interop conventions

`[StructLayout(LayoutKind.Sequential, Pack = 8)]` on an `internal struct` with **public
fields** (not properties — required for blittability).

```csharp
[DllImport(LibName, EntryPoint = "Kernel_DoThing", CallingConvention = CallingConvention.Cdecl)]
[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
internal static extern int DoThing([In] FooNative[] input, int inputCount, [Out] double[] output, int outputLength);
```

Classic `DllImport` — **not** `LibraryImport`/source-gen. Blittable managed arrays with
explicit `[In]`/`[Out]`. No `MarshalAs`, no `IntPtr`, no `unsafe`.

Bridge: static class. **Update:** availability is probed once per process by a
`private static readonly bool _available = NativeKernelLoader.TryLoad("<library>");` field
initializer (there is no static constructor); the shared `NativeKernelLoader` wraps
`NativeLibrary.TryLoad(...)` in a bare `catch` and honours `PORTFOLIO_DISABLE_NATIVE`. The
bridge exposes `IsAvailable` and `LogAvailability(ILogger)`, and a
`TryXxx(request, logger, out T? result)` that sets the out param to `null` first, returns
`false` when unavailable, throws `InvalidOperationException` on a negative status, and
catches via `when (NativeKernelLoader.IsNativeInvocationException(ex))`.

> **Known wart, do not fix silently:** `IsNativeInvocationException` includes
> `InvalidOperationException`, which is exactly what the negative-status check throws.
> A genuine native bug therefore logs one warning and silently falls back to managed.
> Keep the pattern for consistency, but mention it in the Details page's tradeoffs section —
> it is good interview material, not something to quietly diverge on.

### Service conventions

- `Task<T>`-returning but **not `async`** — return `Task.FromResult(...)`. The compute is
  synchronous CPU work; the `Task` only satisfies the interface.
- Constructor takes `ILogger<T>` and calls `<X>NativeBridge.LogAvailability(_logger);`.
- Limits are `private const int` at the top of the class.
- Validation order: `ArgumentNullException.ThrowIfNull(request)` first, then every other
  failure is `throw new ArgumentException(message, nameof(request))`. **Always pass
  `nameof(request)`** (ViewshedService's raster-limit checks omit it — that is a bug, not a pattern).
  **Update:** user-facing messages must be explicit `ArgumentException`s: the BCL throw helpers
  (`ThrowIfNull`, ...) are thrown from framework code, so the client only sees a generic
  "The request was invalid."
- `cancellationToken.ThrowIfCancellationRequested()` immediately before dispatch, and once
  per outer-loop iteration in managed fallbacks.
- Both paths converge on a shared `private static BuildResult(...)` that takes
  `bool nativeAccelerated` as a parameter.
- `private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);`
- Bounds-check anything coming back from native before indexing with it.

### DTO conventions

`Portfolio.Common/DTOs/<Name>Dtos.cs`, namespace `Portfolio.Common.DTOs`, **block-scoped
braces**. Plain `class` (not `record`), `{ get; set; }` (no `init`, no `required`),
collections default to `= []`, strings to `= string.Empty`. Every member gets a one-line
`/// <summary>`. Every result DTO carries `public bool NativeAccelerated { get; set; }`.

### Controller conventions

```csharp
[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[EnableRateLimiting("compute")]   // "expensive" for heavier endpoints; see the Update below
[CpuBound]
[Route("api/v{version:apiVersion}/<segment>")]
public class XController : ControllerBase
```

```csharp
[HttpPost("<verb>")]
[BadRequestOnArgumentException]
[ProducesResponseType(typeof(TResult), 200)]
[ProducesResponseType(400)]
[ProducesResponseType(413)]
[RequestSizeLimit(4_000_000)]
public async Task<ActionResult<TResult>> Verb([FromBody] TRequest request, CancellationToken cancellationToken)
```

Bare `int` status codes, not `StatusCodes.Status200OK`. **Update:** the controller no longer
catches `ArgumentException` itself — the `[BadRequestOnArgumentException]` action filter turns
it into `BadRequest(new { error = ex.Message })`. Add it to every action that validates input
(the set is pinned by a test). Do not catch `OperationCanceledException`.

The service's exact `ArgumentException` messages are the public API contract — write them
as user-facing text.

> **Update (rate limiting):** this paragraph originally said the spatial controllers lacked a
> rate-limit policy. They now all have one. A native-compute controller carries
> `[EnableRateLimiting("compute")]` (60/min/IP, sliding) **and** `[CpuBound]` (the shared
> process-wide concurrency limiter); a heavier or paid-API endpoint uses `"expensive"`
> (15/min/IP; add `[CpuBound]` only when the work is CPU-bound). The vertical controllers
> from these plans use `"expensive"` + `[CpuBound]`. See agent-context section 6.

### Frontend conventions

Page: `Portfolio.Web/Pages/Projects/<Name>/Index.cshtml` + `.cshtml.cs` + `Details.cshtml` + `.cshtml.cs`.
PageModel is bare — `OnGet() => Page()`, no DI, no attributes.

```cshtml
@page
@model Portfolio.Web.Pages.Projects.<Name>.IndexModel
@{
	Layout = "/Pages/Shared/_Layout.cshtml";
	ViewData["Title"] = "<Title>";
}

@section Styles {
	<link rel="stylesheet" href="~/css/gis.css" asp-append-version="true" />
	<link rel="stylesheet" href="~/css/spatialcompute.css" asp-append-version="true" />
}
```

**Update:** `_Layout` now has a `Styles` section (rendered in `<head>` after the global
stylesheets) — put page `<link>`s there, not in the body. Script goes in the `Scripts` section,
which renders last:

```cshtml
@section Scripts {
	<script defer src="~/js/<Name>/app.js" asp-append-version="true"></script>
}
```

Shell: `<div class="spatial-compute-page theme-root">` (mandatory — every rule in
`spatialcompute.css` is scoped under it) → `.project-hero` → `.container.pb-5` → a
`<div id="<feature>Alert" class="alert alert-danger d-none" role="alert">` → `row g-4`
with a `col-lg-4` controls `.card.spatial-card` and a `col-lg-8` results `.card.spatial-card`.

Available classes: `.spatial-card`, `.spatial-map-panel` (the results canvas, min-height 420px,
graph-paper background), `.spatial-stage`, `.kpi-card`/`.kpi-value`/`.kpi-label`,
`.sample-json` (for the `<pre>` JSON echo), `.result-list`, `.geo-cell-item`,
`.geo-cell-meta`, `.geo-cell-anomaly`, `.raster-grid`/`.raster-cell`, `.badge-gis`,
`.btn-accent`, `.btn-outline-accent`, `.theme-surface`, `.theme-text-muted`.

Element IDs are `<feature><Role>` camelCase: `catRunBtn`, `catSvg`, `catKpiAal`.

JavaScript — `wwwroot/js/<Name>/app.js`, a classic deferred script (no ES-module syntax),
written in **ES5 style** on the spatial pages (older pages use `const`/arrow functions —
match the neighbouring page). **Update:** a page is not always a single `app.js` any more;
the larger pages are split into several scripts sharing one namespace (agent-context section 9).

- `(function () { "use strict"; ... }());` — closing parens inside.
- `var` only. No arrow functions, no `let`/`const`, no template literals, no classes.
- **No `DOMContentLoaded` guard** — the script is `defer`red, so it runs after the document is
  parsed. Cache DOM nodes at module scope, wire listeners, then call the initial render directly.
- Deterministic LCG for demo data: `PortfolioCharts.rng(seed)` (the same recurrence,
  `seed = (seed * 48271) % 2147483647; return seed / 2147483647;`).
- **Update:** the helpers every page used to repeat (`setBusy`, `showAlert`, `hideAlert`,
  `escapeHtml`) are shared now. Use `PortfolioUi.inlineAlert(el)` (`show`/`hide`),
  `PortfolioUi.busy` / `runAction`, `PortfolioDom.escapeHtml` / `el`, `PortfolioFormat` for
  numbers and `PortfolioCharts` for SVG helpers, scales and legends.
- API calls use the bare global `apiPost` (not `window.apiPost`) and always read the route
  from `window.PortfolioApi.routes.spatialCompute.<group>.<endpoint>` — never a literal URL:

```js
function run() {
    ui.runAction({
        trigger: runBtn,                 // ui = window.PortfolioUi
        alert: alertBox,                 // ui.inlineAlert(document.getElementById("catAlert"))
        region: resultsPanel,
        request: function () { return apiPost(window.PortfolioApi.routes.spatialCompute.catRisk.simulate, payload); },
        render: render,
        summary: function () { return "Simulation complete."; }
    });
}
```

- SVG is built with `document.createElementNS("http://www.w3.org/2000/svg", ...)` into an
  empty `<svg viewBox="0 0 100 100">` authored in the cshtml. Normalize coordinates to 0–1
  in JS and flip Y on write (`100 - y * 100`).
- **Use CSS custom properties for SVG colors** (`fill="var(--surface)"`,
  `fill="var(--text-muted)"`) so graphics follow light/dark theme automatically.
- **There is no antiforgery on this path.** `apiFetch` sends `credentials: "same-origin"`.
  Do not add token plumbing.

Prefer **self-contained SVG over map tiles** where the data allows it. OSM tiles cannot be
verified in the Browser pane (screenshots hang). Plans 2 and 4 legitimately need Leaflet —
verify those via `read_network_requests` + DOM assertions instead of screenshots.
**Update:** build Leaflet maps with `PortfolioMap.create(id, { center, zoom, label })`
(`leaflet-setup.js`, loaded after `leaflet.js`) rather than hand-rolling the tile layer.

### Test conventions

`Portfolio.Tests/Services/<Name>ServiceTests.cs`, block-scoped namespace
`Portfolio.Tests.Services`, **no `using Xunit;`** (it is a global using from the csproj).
**Update:** one test class per service and one per controller; the old multi-SUT classes were split.

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Services;
```

- `NullLogger<T>.Instance` for services, never Moq. Moq only for service *interfaces* in
  controller tests, with `NullLogger<TController>.Instance` for the logger.
- Construct the SUT inline in each test body of a pure compute service (no collaborators).
  **Update:** controller tests, and services that have collaborators, use `Mock<IXxx>` fields
  built in the test class constructor.
- Method names are `Feature_Scenario_ExpectedOutcome`.
- Null request → `Assert.ThrowsAsync<ArgumentNullException>`; every other validation
  failure → `Assert.ThrowsAsync<ArgumentException>`. Do not assert messages.
- **Update:** the happy-path test asserts the computed result and does **not** assert
  `NativeAccelerated`. The original advice here — `Assert.False(result.NativeAccelerated)` first — is
  withdrawn: it makes the test fail the moment the kernels are built, and the suite runs both with and
  without them. Only the native-gated parity tests look at the flag.
- Collection expressions for DTO literals: `Points = [new() { X = 0, Y = 0 }, ...]`.

Every plan lists its required tests explicitly. Do not copy test counts into docs or badges:
`dotnet test` prints the current number.

---

## Wire-up checklist (easy to forget — verify every item)

| # | File | Change |
|---|---|---|
| 1 | `native/<kernel>/CMakeLists.txt` | copy `spatial_overlay_kernel`'s verbatim, swap names |
| 2 | `native/<kernel>/include/<kernel>.h` + `src/<kernel>.cpp` | the kernel |
| 3 | `Portfolio.Common/DTOs/<Name>Dtos.cs` | request/result DTOs |
| 4 | `Portfolio.Services/Native/<X>NativeStructs.cs` | blittable structs |
| 5 | `Portfolio.Services/Native/<X>NativeInterop.cs` | `DllImport` declarations |
| 6 | `Portfolio.Services/Native/<X>NativeBridge.cs` | probe + marshal + fallback decision |
| 7 | `Portfolio.Services/Interfaces/I<X>Service.cs` | interface |
| 8 | `Portfolio.Services/Services/<X>Service.cs` | validation + dispatch + managed fallback |
| 9 | `Portfolio.Web/Controllers/Api/<X>Controller.cs` | endpoints |
| 10 | **`Portfolio.Web/Startup/DataRegistration.cs`** (`AddPortfolioServices`) | `services.AddScoped<I<X>Service, <X>Service>();` — **Update:** this used to be a block in `Program.cs`; also update `ServiceRegistrationSnapshotTests` |
| 11 | **`Portfolio.Web/wwwroot/js/api-config.js`** | new `spatialCompute.<group>` route object; also the golden route list in `Portfolio.Tests/Contracts/RouteTableTests.cs` |
| 12 | `Portfolio.Web/Pages/Projects/<Name>/` | `Index.cshtml` + `.cs`, `Details.cshtml` + `.cs` |
| 13 | `Portfolio.Web/wwwroot/js/<Name>/app.js` | frontend |
| 14 | **`Portfolio.Web/Pages/Projects/Index.cshtml`** | grid card (hand-written markup) |
| 15 | **`Portfolio.Web/Pages/Index.cshtml`** | **Update:** one entry in the `projects` list at the top of the file; `_ProjectCard` renders it twice (real card plus the inert copy that makes the seamless loop) |
| 16 | `Portfolio.Tests/Services/<Name>ServiceTests.cs` | service tests |
| 17 | `Portfolio.Tests/Controllers/<X>ControllerTests.cs` | controller Ok + BadRequest cases (**Update:** one class per controller); add the action to the filter-wiring set in `Portfolio.Tests/Filters/ControllerFilterWiringTests.cs` and check `OpenApiContractTests` |
| 18 | `native/README.md` | kernel table row, benchmark table row, the Docker copy-block example, the file-layout tree and the "thirteen" kernel counts |
| 19 | `README.md` | native-integrations table + count, project section, Native Performance stack row — no test-count badge or figure |

Items 10, 11, 15, 18 and 19 are the ones historically missed.

### Line numbers drift

Every line number in this document was accurate at planning time. **Grep for the anchor
text, do not trust the number.** Useful anchors:

- `Startup/DataRegistration.cs` → `AddScoped<ISpatialOverlayService` (**Update:** formerly `Program.cs`)
- `api-config.js` → `overlay: {`
- `Pages/Index.cshtml` → the `projects` list at the top (**Update:** the cards used to be two
  hand-written halves; they are now rendered from this list by `_ProjectCard`, twice for the seamless loop)
- `Pages/Projects/Index.cshtml` → the closing of `<div class="row g-4 projects-grid">`

---

## Verification

`native/README.md` documents per-kernel CMake builds. There is **no** build script for all
kernels and **no** top-level `native/CMakeLists.txt`.

```bash
cmake -S native/<kernel> -B build/native/<kernel> -DCMAKE_BUILD_TYPE=Release
cmake --build build/native/<kernel> --config Release
```

The `POST_BUILD` step copies the shared library into **two** directories,
`Portfolio.Services/bin/Debug/net10.0` and `Portfolio.Web/bin/Debug/net10.0` (the web host
loads from its own output directory). It does not copy into `Portfolio.Tests`; CI stages the
libraries there itself.

```bash
dotnet test
```

Then run the app and drive the page. If another instance holds the build-output lock,
build to a scratch directory and run on an alternate port — see the
`verify-web-without-build-lock` note.

> **The Dockerfile does not build any native kernel.** Production on Render runs the managed
> fallback for all of them, and the UI correctly reports "Native: No". **Update:** this is a
> deliberate decision taken on the measured speedups, not an omission to fix — see the "Docker"
> section of `native/README.md`. Do not claim native acceleration in production copy.

---

## Writing the Details page

Every one of these vertical projects gets a `Details.cshtml` with exactly **six**
`<section class="panel shadow-sm rounded p-4 mb-4">` blocks (**Update:** the class was
`theme-surface` when this was written), each an `<h2 class="h4 mb-3">` plus a `<ul>` of
`<li><strong>Label:</strong> text</li>`. This 6-section template is used by the eight newer
spatial/vertical pages only (CatRisk, Change, Clustering, Fleet, Outage, Overlay, Response,
Viewshed); the ten older pages use an 8-section template — see agent-context section 9.

1. High-Level System Overview
2. Algorithm
3. API Layer
4. Services and Native Boundary
5. Engineering Decisions and Tradeoffs
6. Interview Discussion Points *(plain questions, no `<strong>` lead-in)*

Section 6 is the one that earns interviews. Each plan supplies its own questions — use them.

**Use the real industry vocabulary.** Each plan has a glossary section. Getting the words
right is most of what signals domain credibility to a hiring manager in that vertical;
getting them wrong is worse than not trying. Do not invent terminology.
