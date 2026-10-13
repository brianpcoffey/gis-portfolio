# Portfolio — Agent Context

Orientation for an AI agent working in this repo. Read this first, then `README.md` and
`native/README.md` for depth. Where this file and `.github/copilot-instructions.md` disagree,
**this file and the source win** — `copilot-instructions.md` says itself that it lags the code.
A cleanup is in progress (Oct 2026): `docs/cleanup/PLAN.md` holds the work packages, decisions and
verification protocol; `docs/cleanup/findings-*.md` hold the evidence.

---

## 1. What it is

Brian Coffey's personal engineering portfolio, live at **brianpcoffey.com** (also the `onrender.com`
subdomain — never disable that one). It is a working .NET 10 web app, not a static site: a Razor Pages
front end plus a versioned JSON API hosting **18 interactive demo "projects"**, mostly GIS / spatial
compute, each with a live demo page and an interview-style "Details" page.

Purpose: a hiring artifact for **full-stack/backend .NET roles** (active clearance; recruiter-only
visibility). The pitch is *engineering rigor shown honestly*, e.g. published benchmarks where the C++
kernels often lose to the C#. **Do not inflate claims** (no "SIMD", "high-performance", "AVX2 speedup"
language the benchmarks don't support).

Site structure (`/`): Hero → Technical Skills (4 cards) → Featured Projects (carousel; the card markup
is duplicated in `Index.cshtml` for the looping slider — edit both copies) → About → Résumé (PDF
download band) → Contact. Other pages: `/Projects` (index), `/Projects/<Name>/Index` (demo),
`/Projects/<Name>/Details` (technical walkthrough), `/Login`, `/Logout`, `/Privacy`, `/Accessibility`
(display settings: text scale, contrast, motion, theme), `/Account/Profile`, `/Error`, `/AccessDenied`.
Dark/light theme, skip link, ARIA landmarks, OpenGraph/Twitter card meta in `_Layout.cshtml`.

### The 18 projects
| Group | Projects |
|---|---|
| ArcGIS / persistence | US State Explorer (ArcGIS JS 4.29, saved features + collections), Plant Operations Dashboard (fiber orders/materials/shipments + KPIs, authenticated, DataTables) |
| ArcGIS geocoding services | Batch Geocoding (CSV → async job, 202 + polling), Reverse Geocoding, Address Standardization & Validation |
| Home Finder | Redlands Smart Home Finder (weighted 10-dimension property scoring, saved searches; ~45 seeded homes) |
| Native-kernel spatial demos | Live Location Stream (`geostream`), Geometry Toolkit, Terrain Analyzer (`raster`), Route Planner (`network`), Hotspot Clusterer (`clustering`, DBSCAN), Viewshed Analyzer, Spatial Overlay / Zone Tagger |
| Industry verticals (newest) | Catastrophe Risk Analyzer (`catrisk`), Raster Change Detection (`change`), Outage Manager & Network Trace (`outage`), Emergency Response Coverage Optimizer (`response`, p-median / NFPA 1710), Fleet Route Optimizer (`fleet`, CVRPTW) |

---

## 2. Tech stack

- **Backend:** ASP.NET Core **10**, C# (all projects `net10.0`, nullable enabled, implicit usings). Razor Pages + API controllers.
- **Data:** PostgreSQL via Npgsql + **EF Core 10** (code-first, fluent `IEntityTypeConfiguration` maps, 4 migrations). Hosted on **Neon.tech**.
- **Cache / state:** `IDistributedCache` — Redis (Upstash in prod) when `Redis:ConnectionString` is set, else `MemoryDistributedCache`. `IBatchJobStore` = `RedisBatchJobStore` | `InMemoryBatchJobStore` (singletons). Data Protection keys → Redis, else filesystem.
- **Auth:** Cookie auth + Google OAuth 2.0 (`/signin-google`); plus an anonymous GUID cookie identity (see §6).
- **Resilience:** `Microsoft.Extensions.Http.Resilience` (Polly v8) — timeout/retry/jitter/circuit-breaker per ArcGIS typed `HttpClient` (`arcgis-features`, `arcgis-batch`, `arcgis-reverse`, `arcgis-address`).
- **API:** `Asp.Versioning` (URL segment, `v1`, no assumed default), OpenAPI + **Scalar** at `/scalar` and `/openapi/v1.json` (public in all envs), XML doc comments feed the schema.
- **Rate limiting:** global 300/min/IP; policy `"expensive"` 15/min/IP via `[EnableRateLimiting("expensive")]`. Partitioned by IP (runs before auth).
- **Health:** `/health/live` (no checks), `/health/ready` (DB check) — both opt out of rate limiting.
- **Native:** 13 C++20 / CMake shared libraries, stable C ABI, P/Invoke (`DllImport`, not `LibraryImport`), each with a verified-identical managed C# fallback.
- **Front end:** Bootstrap 5.3, Font Awesome, jQuery 3.7 + DataTables, D3 (vendored in `wwwroot/lib`), vanilla JS (one `app.js` per project), **ArcGIS JS API 4.29** (CDN `js.arcgis.com`), **Leaflet 1.9.4** (CDN unpkg) on OpenStreetMap tiles, hand-rolled SVG and `<canvas>` for the rest. No bundler, no npm, no TypeScript.
- **Testing:** xUnit v3 + Moq, coverlet (cobertura). 697 tests run (609 `[Fact]`/`[Theory]` attributes; theories expand). No DB/integration tests; two test classes already use EF InMemory (transitively, via Portfolio.Repositories).
- **Packages:** Central Package Management (`Directory.Packages.props`) with transitive pinning; `Microsoft.OpenApi` pinned to 2.7.5 for an advisory.
- **Delivery:** multi-stage `Dockerfile`, `docker/` compose (+Redis), `k8s/` manifests (Deployment/Service/HPA/Redis/ConfigMap/Secret), **Render** (Docker runtime) auto-deploys `main`.

---

## 3. Repository layout

```
Portfolio.sln
Directory.Build.props      warnings-as-errors ONLY in CI (ContinuousIntegrationBuild); NuGet audit NU1902/3/4 = errors
Directory.Packages.props   all package versions live here
Dockerfile                 restores Portfolio.Web.csproj only (not the .sln) — keeps Tests/Benchmarks out of the image
coverlet.runsettings       excludes *.Migrations.*, Portfolio.Benchmarks and [Obsolete] members (hides 3 live endpoints) from coverage
.editorconfig              utf-8, CRLF, 4-space; file-scoped namespaces = suggestion
.env.example               DATABASE_URL, Redis__ConnectionString, Authentication__Google__*
.github/workflows/ci.yml   see §8
.github/copilot-instructions.md   long conventions doc (partly stale — see §10)
.claude/launch.json        dev server config: `dotnet run --project Portfolio.Web --launch-profile http`, port 5000
docs/                      render-deployment.md, resume-update.md, plans/ (00 = graph-engine prerequisite; 01–05 = the 5 vertical projects),
                           cleanup/ (Oct 2026 cleanup plan + findings)
k8s/  docker/              deployment manifests + compose files
native/                    13 CMake projects + README.md (benchmarks, build instructions)

Portfolio.Common/          No behavior (exception: HomeSearchPreferencesDto implements IValidatableObject). All models live here:
  Models/ (EF entities)  DTOs/ (one *Dtos.cs per feature)  Enums/ConfidenceTier  ArcGis/ (wire models, [JsonPropertyName])
  Configuration/ (Options)  Constants/  Serialization/PortfolioJsonOptions
Portfolio.Repositories/    PortfolioDbContext, Interfaces/, Repositories/, Mappings/*Map(ping).cs, Migrations/, Seed/RedlandsPropertySeedData
Portfolio.Services/
  Interfaces/ + Services/  one pair per feature: 25 interfaces, 26 service classes (UserProfileSeedService has no interface)
  Native/                  {Domain}NativeStructs/Interop/Bridge.cs (+ NativeToggle). Exceptions: ChangeDetection and Viewshed
                           have no Structs file; scoring is NativeStructs / NativeScoringInterop / NativeScoringBridge
  Data/                    deterministic in-repo datasets: RedlandsRoadNetwork (OSM-generated, 5.7k lines),
                           RedlandsDistributionNetwork, RedlandsResponseScenario, RedlandsDeliveryScenario,
                           SoCalPolicyBook, SyntheticChangeScene
  Abstractions/IBatchJobStore.cs; InMemoryBatchJobStore, RedisBatchJobStore, AssemblyInfo (InternalsVisibleTo Tests) sit at the project root
Portfolio.Web/
  Program.cs               DI root + pipeline (single file; helpers at bottom parse postgres:// and redis:// URIs)
  Controllers/Api/         25 controllers, all [ApiVersion("1.0")]
  Middleware/              SecurityHeaders, ApiException (RFC 7807), AnonymousUser
  HealthChecks/            DatabaseReadyHealthCheck
  Pages/                   Index, Login/Logout, Privacy, Accessibility, Error, AccessDenied, Shared/_Layout, Projects/<Name>/{Index,Details}
                           (Projects/Details.cshtml.cs is an unrouted stub with no .cshtml)
  Areas/Account/Pages/     Profile
  wwwroot/                 css/ (see css/README.md), js/<Project>/app.js, js/{api-config,api-fetch,site,preferences,...}.js,
                           lib/ (vendored), files/Brian-Coffey-Resume.pdf, img/og-preview.jpg, samples/batch-geocoding/*.csv
Portfolio.Tests/           Controllers/ Services/ Pages/ Helpers/ (FakeTimeProvider)
Portfolio.Benchmarks/      console harness comparing native vs managed for all 13 kernels (see §7)
```

Ignored/untracked local noise: `bin/ obj/ .vs/ TestResults/ build/ DataProtection-Keys/ .env appsettings.Development.json`.
Stale `net8.0` folders may exist under `bin/`/`obj/` — ignore them; everything targets `net10.0`.
Git: ~198 commits, branch `main`, remote `github.com/brianpcoffey/gis-portfolio`.

---

## 4. Architecture

```
Razor Page / API Controller  (Portfolio.Web)
   → Service interface        (Portfolio.Services)
   → Repository interface     (Portfolio.Repositories)
   → PortfolioDbContext (EF Core) / typed HttpClient (ArcGIS)
```
Reference: `Portfolio.Services → Repositories → Common`; `Web → all three`; `Tests → all four`.
Spatial-compute services are **stateless and DB-free**: controller → service → (native bridge | managed fallback) → DTO.

### API surface (`/api/v1/...`)
`addresses` · `catrisk` · `change` · `clustering` · `collections` · `features` · `features/saved` ·
`fiber/{dashboard,materials,orders,shipments}` · `fleet` · `geocoding/{batch,reverse}` · `geometry` ·
`geostream` · `homefinder` · `network` · `outage` · `overlay` · `profile` · `raster` · `response` ·
`users` · `viewshed`.
`wwwroot/js/api-config.js` (`window.PortfolioApi.routes`) is the **single source of truth for client route paths** — update it when a route changes.

### Persistence
Entities: `SavedFeature, Collection, UserProfile, UserClaim, Property, SavedSearch, FiberClient, FiberOrder, FiberShipment, FiberMaterial, FiberInventoryTransaction`. Migrations: `InitialCreate`, `AddFiberUserIdIndexes`, `RemoveUnusedFiberNavsAndNameIndex`, `ExpandRedlandsProperties`. Migrations run at startup (`db.Database.Migrate()`); `dotnet Portfolio.Web.dll --migrate` runs them and exits (Render pre-deploy), and `RunMigrationsOnStartup=false` skips the startup pass — **set both or neither**.

### Pipeline order (preserve)
`UseForwardedHeaders` → `SecurityHeadersMiddleware` → exception handler/HSTS (non-dev) → HTTPS redirect (**dev only**; Render terminates TLS) → static files → routing → `UseRateLimiter` → `UseSession` → `ApiExceptionMiddleware` → `AnonymousUserMiddleware` → auth → authz → OpenAPI/Scalar → health → controllers/pages.

---

## 5. Native-kernel pattern (the project's signature)

13 kernels: `portfolio_scoring, geostream_processor, spatial_geometry_kernel, raster_terrain_kernel, spatial_graph_engine, spatial_cluster_kernel, viewshed_kernel, spatial_overlay_kernel, cat_risk_kernel, change_detection_kernel, network_trace_kernel, facility_location_kernel, vrp_solver_kernel`.

- Layout: `native/<name>/{CMakeLists.txt, include/<name>.h, src/*.cpp}`. Each is an **independent** CMake project (no top-level CMakeLists). Build: `cmake -S native/<name> -B build/native/<name> -DCMAKE_BUILD_TYPE=Release && cmake --build build/native/<name> --config Release` (post-build copies the lib into `bin/Debug/net10.0`).
- C ABI: `extern "C"`, `#pragma pack(push,8)` structs, every pointer followed by an `int` length, caller-allocated buffers, status codes `0 / -1 invalid / -2 not found / -3 buffer too small / -99 exception`. Never let C++ exceptions or types cross.
- Managed side: `{Domain}NativeStructs.cs` (blittable, public fields, `Pack = 8`), `{Domain}NativeInterop.cs` (raw `DllImport`), `{Domain}NativeBridge.cs` (static, `IsAvailable` via one-time `NativeLibrary.TryLoad`, status-code translation). All `internal`; tests see them via `InternalsVisibleTo`.
- Service dispatches native only if `IsAvailable`; **the managed fallback is production behavior and must never be removed.** Every result DTO carries `NativeAccelerated`.
- `PORTFOLIO_DISABLE_NATIVE=1` forces the managed path (`NativeToggle`).
- **Production ships NO native code** — the Docker image compiles none and the UI shows "Native: No". Deliberate: speedups are marginal and a second binary can silently diverge.
- **Reference implementation to copy for a new kernel: Spatial Overlay** (see `docs/plans/README.md` — it has the full conventions, the 19-item checklist and known warts).
- **Measured speedups: 0.37×–2.26× across 21 workloads; native loses on 5, ties on 7, wins on 9 (±15%).** Best: VRP 2.26×, overlay 1.90×. Worst: geometry fan-triangulation 0.37×. (`native/README.md:314` still says 8/8/5; its own table says 9/5/7.)
- **Parity:** checksum equality across all 21 workloads is checked by `Portfolio.Benchmarks` only (outside CI). `NativeParityTests` covers geometry clip and viewshed (5 tests) plus 7 native-gated scoring tests. CI's native-parity job runs the whole suite with kernels present and again with `PORTFOLIO_DISABLE_NATIVE=1`; with native present, coverage measures ~78%/52% (below the gate, by design — the gate runs only in the test job).
- `RedlandsRoadNetwork.cs` is **generated from OpenStreetMap — never hand-edit.** 2,530 nodes / 3,187 edges, 1-based contiguous node IDs (`index = id − 1`), `EsriHqNodeId = 2530`, all edges bidirectional, cost in km. `SpatialGraphService` shares one cached mutable graph instance — never mutate it.

---

## 6. Identity & security model

Two **parallel** identities — do not mix them:
1. **Anonymous GUID** — `AnonymousUserMiddleware` sets a `AnonUserId` cookie (HttpOnly, Lax, 1y; Secure outside Development), stores the identity in `HttpContext.Items["PortfolioIdentity"]`; read via `IUserProfileService.GetCurrentUserId()`. Used for saved features, profile claims, fiber data scoping. On Google sign-in the anonymous profile is merged and the cookie deleted.
2. **Google OAuth/cookie** — `ClaimTypes.NameIdentifier`; used by collections. Policy `"Authenticated"` is the only policy.

Rules: never accept a user/owner id from request bodies or query strings; filter per-user rows by owner in the repository; controllers carry `[Authorize(Policy="Authenticated")]` or `[AllowAnonymous]`, never neither. Default challenge scheme is **Cookie** (so unauthenticated pages go to the app's `/Login`); `/api` paths return 401/403 instead of redirecting. Security headers: nosniff, X-Frame-Options DENY, referrer policy, minimal CSP (`frame-ancestors/base-uri/object-src` only — a full script CSP is a documented follow-up). Secrets come from env vars / untracked `appsettings.Development.json`; never log the connection strings (Redis is masked).

---

## 7. Commands

```bash
dotnet restore
dotnet build -c Release                     # CI treats warnings as errors; locally they're warnings
dotnet test                                 # whole suite; passes with or without native libs
PORTFOLIO_DISABLE_NATIVE=1 dotnet test      # PowerShell: $env:PORTFOLIO_DISABLE_NATIVE="1"
dotnet run --project Portfolio.Web --launch-profile http     # http://localhost:5000 (https profile: :5001)
dotnet run --project Portfolio.Benchmarks -c Release         # native-vs-managed table; build kernels first
dotnet ef migrations add <Name> --project Portfolio.Repositories --startup-project Portfolio.Web
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up
```
Local run needs a Postgres connection string (`ConnectionStrings:DefaultConnection` or `DATABASE_URL`, both Npgsql and `postgres://` URI formats) and `Authentication:Google:ClientId/ClientSecret` (the committed `appsettings.json` has them empty; the Google handler rejects empty values, so sign-in won't work without real ones). Put them in `Portfolio.Web/appsettings.Development.json` (git-ignored). Redis optional.
Static-file edits (CSS/JS/cshtml) are not covered by the test suite — verify them in a browser.

---

## 8. CI/CD (`.github/workflows/ci.yml`)

Jobs on push/PR to `main`: **test** (Release build with warnings-as-errors, coverage gate **≥80% line / ≥60% branch** via a stdlib Python script — ratchets, never lower them), **native-parity** (builds all 13 kernels on Ubuntu, stages `.so` files, runs tests with native present, then again with `PORTFOLIO_DISABLE_NATIVE=1`), **docker** (image builds), **deploy** (calls `RENDER_DEPLOY_HOOK` if the secret exists). Render also auto-deploys `main`. Treat a push to `main` as a production deploy.

---

## 9. Conventions to follow when editing

**C#**
- Interfaces `I{Name}Service/Repository`; async methods end `Async`, take a trailing `CancellationToken cancellationToken = default` and forward it; private fields `_camelCase`.
- Services own business rules and throw `ArgumentException`→400, `InvalidOperationException`→409, `KeyNotFoundException`→404; controllers translate them. Repositories return `null`/`false`, never throw business exceptions, and never map to DTOs.
- Controllers return DTOs only (never entities); error body shape is `new { error = "..." }`; add `[ProducesResponseType]` for every status and an XML `<summary>` on public actions.
- **All models/DTOs/enums/ArcGIS wire types live only in `Portfolio.Common`.** DTOs: plain classes, `{ get; set; }`, `= []` / `= string.Empty` defaults, no validation attributes (validation is imperative in services). Legacy exceptions: DataAnnotations in 4 DTO files (Collection, FiberMaterial, FiberOrder, HomeSearchPreferences) — don't copy them.
- `DateTime.UtcNow` only. Hand-written `private static MapToDto` (no AutoMapper). Don't add MediatR/FluentValidation/Dapper or new auth policies/schemes.
- Register new services/repositories `Scoped` in `Program.cs` beside the existing block (the only singletons are the job stores and `TimeProvider`).
- **New spatial controllers must add `[EnableRateLimiting("expensive")]`** — 9 of 25 controllers have it. The 7 spatial ones (`geostream, geometry, raster, network, clustering, viewshed, overlay`) lack it (cleanup WP-14 adds a "compute" tier for them); the CRUD controllers (Collections, Features, Fiber×4, Profile, SavedFeature, UserProfile) also lack it.
- Namespace style is **mixed** (block-scoped in older files, file-scoped in newer); match the file/folder you're in. `.editorconfig` sets CRLF line endings, UTF-8, 4-space indent (2 for YAML).
- `Task`-returning-but-sync compute services use `Task.FromResult`; limits are `private const int`; validate with `ArgumentNullException.ThrowIfNull` then `ArgumentException(message, nameof(request))`.

**Frontend**
- Page = `Pages/Projects/<Name>/Index.cshtml(.cs)` + `Details.cshtml(.cs)` + `wwwroot/js/<Name>/app.js` + optional `wwwroot/css/<name>.css`. PageModels are bare (`OnGet`). `Details` pages follow one of two section orders: 10 pages use 8 sections (High-Level System Overview → Frontend Architecture → API Layer → Services and Business Logic → Data Access Layer → Infrastructure and Deployment → Engineering Decisions and Tradeoffs → Interview Discussion Points); the 8 newer spatial/vertical pages (CatRisk, Change, Clustering, Fleet, Outage, Overlay, Response, Viewshed) use 6 (Overview → Algorithm → API Layer → Services and Native Boundary → Tradeoffs → Interview); technical, no marketing copy; describe hosting as **Render + Postgres**, and Azure/AKS/etc. only as *future*.
- CSS: palette via CSS variables in `base.css`; dark mode is `body.dark-mode`; never hard-code colors; scope page rules under a page class; motion is auto-disabled by `preferences.css`. Load order is in `wwwroot/css/README.md`.
- Use the global `PortfolioApi` routes and `api-fetch.js`; render big rasters to `<canvas>`, not DOM nodes.

**Tests**
- One class per service/controller (3 legacy classes cover several SUTs; pure compute services use inline SUTs, as `docs/plans` prescribes); `Mock<IXxx>` fields, AAA comments, `[Fact]` + `[Theory]/[InlineData]` for validation. HTTP services tested with a fake `DelegatingHandler`; caches with a real `MemoryDistributedCache`. Tests must assert the computed result, **not** whether native ran. Every new service method/controller action needs a happy path plus validation/not-found cases. No DB fixtures — ask first.

---

## 10. Gotchas & known inconsistencies

- `.github/copilot-instructions.md` is **stale** in places: it lists a `FiberClientsController`/`IFiberClientService` that no longer exist (clients are seeded reference data, no CRUD); describes only 5 kernels; gives old route shapes (`api/reversegeocoding`, `api/addressstandardization`, `fiberorders`) — real routes are the `api/v1/...` ones above; says "355 tests"; says Swagger (it is OpenAPI + Scalar); says cookie `SameSite=None` (code uses `Lax`).
- `README.md` and `docs/render-deployment.md` reference repo `portfolio_v2`; the actual remote is `gis-portfolio`. The Home Finder README mentions deprecated `score` / `properties/{id}` aliases and a deprecated `POST …/geocoding/batch/sync`.
- `Portfolio.Web/Pages/Projects/Details.cshtml.cs` is an `[Authorize]` stub with a TODO and is not the per-project Details route.
- Native bridges: `IsNativeInvocationException` includes `InvalidOperationException`, so a native negative-status error logs once and silently falls back to managed — intentionally documented, don't "fix" silently.
- General caution for native code: `std::map::operator[]` on a missing key inserts a default (`0.0` = "reachable free"); use `.find()`. (The shortest-path instance was fixed by the CSR rewrite.)
- **OpenAPI defect (until cleanup WP-23 lands):** all 24 documented request bodies in `/openapi/v1.json` read "Cancellation token." — caused by `<param>` tags for the token parameter.
- **Coverage depends on the generated `RedlandsRoadNetwork.cs`** (39% of valid lines, all covered). Without it line coverage is ~73%, below the 80% gate. Never mark it generated/excluded for coverlet.
- `Program.cs` and the three middlewares have no test coverage at HEAD `9d9ea02`; cleanup WP-01 adds a `WebApplicationFactory` host suite.
- Docker `ASPNETCORE_HTTP_PORTS=${PORT:-10000}` resolves at build time; Render routes to 10000.
- Render free tier cold-starts; that's why `--migrate` pre-deploy exists.
- `xUnit1051` is the only suppressed analyzer in tests; everything else is a CI error.
- A `.vs/` dir, `TestResults/`, and `DataProtection-Keys/` may be present locally — never commit them.
- `docs/resume-update.md`: the résumé PDF in `wwwroot/files/` may lag the Drive source; the site describes the PDF using metrics read from the file.

## 11. Things the owner has asked agents not to do
- Don't hand-edit generated `RedlandsRoadNetwork.cs` (regenerate from OSM).
- Don't submit job applications autonomously; draft and show screening answers for approval.
- Don't make claims (site copy, résumé, README) the benchmarks or code don't back.
- Don't flip LinkedIn to public "Open to Work"; visibility is recruiters-only.
- Browser-pane screenshots hang on OSM tiles — verify UI with DOM/geometry assertions via `javascript_tool`.
- If another instance holds `Portfolio.Web/bin`, build to a scratch `-o` dir and run on an alternate port.
- After a "keep both" merge resolution in markup, verify tag **balance**, not tag counts.
