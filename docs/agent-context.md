# Portfolio — Agent Context

Orientation for an AI agent working in this repo. Read this first, then `README.md` and
`native/README.md` for depth. `.github/copilot-instructions.md` is only a short list of invariants that
points back here. Where any document and the source disagree, **the source wins**. This file avoids
numbers that drift (test counts, line counts, endpoint counts); where a number matters it names the
command that prints it.

A cleanup is in progress (Oct 2026). `docs/cleanup/PLAN.md` holds the work packages (WP-nn), the
decisions (D-nn) and the verification protocol; `docs/cleanup/findings-*.md` hold the evidence. They are
a historical record: read them, don't edit them.

## Invariants (do not break)

- `Portfolio.Services/Data/RedlandsRoadNetwork.cs` is **generated from OpenStreetMap — never hand-edit**.
  EF migrations and the model snapshot are never hand-edited either: schema changes go through
  `dotnet ef migrations add` only.
- Every native kernel keeps its **managed fallback** (it *is* the production behavior), and native and
  managed stay result-identical. **Production ships no native code.** A C ABI change touches the C header,
  the C# struct, the native source and the parity tests together.
- All models, DTOs, enums and ArcGIS wire types live in `Portfolio.Common`, nowhere else.
- Two separate identity systems (anonymous GUID cookie, Google sign-in); `"Authenticated"` is the only
  authorization policy; no new auth schemes or policies (§6).
- The middleware order in `Startup/Pipeline.cs` is a contract (§4), and `UseSession` stays.
- **Honest claims.** Measured native speedups are **0.37×–2.26×: 9 wins / 5 losses / 7 ties at ±15%**
  (`native/README.md` is the authority). No "SIMD", "AVX", "high-performance" or other speed language the
  benchmarks don't support — in site copy, README or résumé material.
- Public routes and JSON shapes don't change without updating `wwwroot/js/api-config.js`, the callers and
  the tests together (§9). A push to `main` is a production deploy (§8).

---

## 1. What it is

Brian Coffey's personal engineering portfolio, live at **brianpcoffey.com** (also the `onrender.com`
subdomain — never disable that one). It is a working .NET 10 web app, not a static site: a Razor Pages
front end plus a versioned JSON API hosting **18 interactive demo "projects"**, mostly GIS / spatial
compute, each with a live demo page and an interview-style "Details" page.

Purpose: a hiring artifact for **full-stack/backend .NET roles** (active clearance; recruiter-only
visibility). The pitch is *engineering rigor shown honestly*, e.g. published benchmarks where the C++
kernels often lose to the C#. **Do not inflate claims.**

Site structure (`/`): Hero → Technical Skills (4 cards) → Featured Projects (carousel) → About → Résumé
(PDF download band) → Contact. The carousel cards come from a list at the top of `Pages/Index.cshtml` and
are rendered twice through `Pages/Shared/_ProjectCard.cshtml` (the second pass is an inert copy that makes
the CSS loop seamless). `/Projects` has its own hand-written card markup, so adding a project means editing
both pages. Other pages: `/Projects/<Name>/Index` (demo), `/Projects/<Name>/Details` (technical
walkthrough), `/Login`, `/Logout` (POST + antiforgery), `/Privacy`, `/Accessibility` (display settings:
text scale, contrast, motion, theme), `/Account/Profile`, `/Error`, `/AccessDenied`. Dark/light theme, skip
link, ARIA landmarks and OpenGraph/Twitter card meta live in `_Layout.cshtml`.

### The 18 projects
| Group | Projects |
|---|---|
| ArcGIS / persistence | US State Explorer (ArcGIS JS 4.29, saved features + collections), Plant Operations Dashboard (fiber orders/materials/shipments + KPIs, authenticated, DataTables) |
| ArcGIS geocoding services | Batch Geocoding (the job API returns 202 + a polling URL, with a bounded queue and workers, §4; the demo page itself posts to the synchronous `…/geocoding/batch/sync`), Reverse Geocoding, Address Standardization & Validation |
| Home Finder | Redlands Smart Home Finder (weighted 10-dimension property scoring, saved searches; a deterministic seed of Redlands homes) |
| Native-kernel spatial demos | Live Location Stream (`geostream`), Geometry Toolkit, Terrain Analyzer (`raster`), Route Planner (`network`), Hotspot Clusterer (`clustering`, DBSCAN), Viewshed Analyzer, Spatial Overlay / Zone Tagger |
| Industry verticals (newest) | Catastrophe Risk Analyzer (`catrisk`), Raster Change Detection (`change`), Outage Manager & Network Trace (`outage`), Emergency Response Coverage Optimizer (`response`, p-median / NFPA 1710), Fleet Route Optimizer (`fleet`, CVRPTW) |

---

## 2. Tech stack

- **Backend:** ASP.NET Core **10**, C# (all projects `net10.0`, nullable enabled, implicit usings). Razor Pages + API controllers. `global.json` pins the SDK feature band (`rollForward: latestFeature`).
- **Data:** PostgreSQL via Npgsql + **EF Core 10** (code-first, fluent `IEntityTypeConfiguration` maps, migrations in `Portfolio.Repositories/Migrations`). Hosted on **Neon.tech**.
- **Cache / state:** `IDistributedCache` — Redis (Upstash in prod) when `Redis:ConnectionString` is set, else `MemoryDistributedCache`. `IBatchJobStore` = `RedisBatchJobStore` | `InMemoryBatchJobStore` (singletons, 24 h job TTL). Data Protection keys → Redis, else filesystem. The app registers no `IMemoryCache` of its own.
- **Auth:** Cookie auth + Google OAuth 2.0 (`/signin-google`); plus an anonymous GUID cookie identity (§6).
- **Resilience:** `Microsoft.Extensions.Http.Resilience` (Polly v8) — timeout / retry / jitter / circuit-breaker per ArcGIS typed `HttpClient` (`arcgis-features`, `arcgis-batch`, `arcgis-reverse`, `arcgis-address`). Two profiles, `Features` and `Geocoding`, in `Startup/ArcGisClientRegistration.cs`.
- **API:** `Asp.Versioning` (URL segment, `v1`, no assumed default version), OpenAPI + **Scalar** at `/scalar` and `/openapi/v1.json` (public in all environments), XML doc comments feed the schema. JSON is camelCase, nulls omitted, enums as strings.
- **Rate limiting:** a global per-IP limit plus the named policies `"compute"`, `"expensive"` and `"csp"`, and one process-wide CPU concurrency limiter for `[CpuBound]` controllers — details in §6.
- **Health:** `/health/live` (no checks) and `/health/ready` (database check, result cached for a few seconds) — both opt out of rate limiting.
- **Native:** 13 C++20 / CMake shared libraries, stable C ABI, P/Invoke (`DllImport`, not `LibraryImport`), each with a verified-identical managed C# fallback (§5).
- **Front end:** Bootstrap 5.3, Font Awesome, jQuery 3.7 + DataTables, D3 (all vendored in `wwwroot/lib`), vanilla JS, **ArcGIS JS API 4.29** (CDN `js.arcgis.com`, SRI-pinned), **Leaflet 1.9.4** (CDN unpkg, SRI-pinned) on OpenStreetMap tiles, hand-rolled SVG and `<canvas>` for the rest. No bundler, no npm, no TypeScript. Shared modules and conventions: §9.
- **Testing:** xUnit v3 + Moq, `Microsoft.AspNetCore.Mvc.Testing` (test-only), coverlet (cobertura). `dotnet test` prints the current test count. Test kinds and conventions: §9.
- **Packages:** Central Package Management (`Directory.Packages.props`) with transitive pinning; `Microsoft.OpenApi` is pinned on 2.x (see the comment there for why 3.x is held back).
- **Delivery:** multi-stage `Dockerfile`, `docker/` compose (+Redis), `k8s/` manifests, and **Render** (Docker runtime). In production CI's `deploy` job ships `main` (§8).

---

## 3. Repository layout

```
Portfolio.sln              Common, Repositories, Services, Web, Tests, Benchmarks
Directory.Build.props      warnings-as-errors ONLY in CI (ContinuousIntegrationBuild); NuGet audit NU1902/3/4 = errors
Directory.Packages.props   all package versions live here
global.json                SDK pin (rollForward: latestFeature)
Dockerfile                 restores Portfolio.Web.csproj only (not the .sln) — keeps Tests/Benchmarks out of the image;
                           base images pinned by digest; ASPNETCORE_HTTP_PORTS=10000; runs as the non-root image user
coverlet.runsettings       excludes *.Migrations.*, Portfolio.Benchmarks and Obsolete / GeneratedCode / CompilerGenerated /
                           ExcludeFromCodeCoverage members (the two [Obsolete] HomeFinder aliases are therefore not in coverage)
.editorconfig              utf-8, 4-space; CRLF in the working tree (git stores LF: .gitattributes `text=auto`);
                           file-scoped namespaces = suggestion
.env.example               DATABASE_URL, Redis__ConnectionString, Authentication__Google__*
.github/workflows/         ci.yml (§8), security-scan.yml (weekly vulnerable-package sweep)
.github/dependabot.yml     weekly NuGet / GitHub Actions / Docker update PRs (never auto-merged)
.github/copilot-instructions.md   short invariants, points here
.claude/launch.json        dev server config: `dotnet run --project Portfolio.Web --launch-profile http`, port 5000
docs/                      render-deployment.md, resume-update.md, plans/ (historical design docs: 00 = graph-engine
                           prerequisite; 01–05 = the five vertical projects), cleanup/ (Oct 2026 plan + findings)
k8s/  docker/              deployment manifests + compose files
native/                    13 independent CMake projects + README.md (benchmarks, build instructions)
tools/                     measurement and audit scripts, not in the .sln and never served (§7)

Portfolio.Common/          No behavior (exception: HomeSearchPreferencesDto implements IValidatableObject). All models live here:
  Models/ (EF entities + BatchJob, ProjectHero)  DTOs/ (one *Dtos.cs per feature)  Enums/ (ConfidenceTier, ArcGisUpstream)
  ArcGis/ (wire models, [JsonPropertyName])  Configuration/ (Options)  Constants/  Serialization/PortfolioJsonOptions
Portfolio.Repositories/    PortfolioDbContext, Interfaces/, Repositories/, Mappings/*Map(ping).cs, Migrations/,
                           Seed/RedlandsPropertySeedData, Diagnostics/LogRedaction
Portfolio.Services/
  Interfaces/ + Services/  one pair per feature (UserProfileSeedService has no interface); pure compute helpers sit in
                           Services/ChangeDetection, Services/Fleet, Services/Response
  Native/                  {Domain}NativeStructs/Interop/Bridge.cs + NativeKernelLoader (shared probe) + NativeToggle.
                           Exceptions: ChangeDetection and Viewshed have no Structs file; scoring is
                           NativeStructs / NativeScoringInterop / NativeScoringBridge
  Data/                    deterministic in-repo datasets: RedlandsRoadNetwork (OSM-generated, very large),
                           RedlandsDistributionNetwork, RedlandsResponseScenario, RedlandsDeliveryScenario,
                           SoCalPolicyBook, SyntheticChangeScene, FiberDemoSeedData
  Abstractions/            IBatchJobStore, IBatchJobQueue (+ BatchWorkItem, BatchQueueFullException)
  (project root)           InMemoryBatchJobStore, RedisBatchJobStore, BoundedBatchJobQueue, AssemblyInfo (InternalsVisibleTo Tests)
Portfolio.Web/
  Program.cs               composition root: only calls the Startup/ extension methods, in order
  Startup/                 one file per concern — CoreRegistration (Kestrel limits, Razor Pages, TimeProvider),
                           DataRegistration (repositories/services Scoped, Npgsql DbContext), ArcGisClientRegistration
                           (typed clients, resilience profiles, batch queue + worker), InfrastructureRegistration (Redis,
                           HSTS, caching, session, health checks, Data Protection), ForwardedHeadersSetup,
                           AuthenticationRegistration + PortfolioCookieEvents, ApiRegistration (JSON, versioning, OpenAPI),
                           RateLimitingRegistration, Pipeline (middleware order + endpoint mapping), StartupMigrations
  Controllers/Api/         one controller per feature, all [ApiVersion("1.0")]; Controllers/CspReportController (/csp-report)
  Filters/                 BadRequestOnArgumentException, ArcGisUnavailable (exception filters, §9)
  Middleware/              SecurityHeaders, ApiException (RFC 7807), AnonymousUser
  RateLimiting/            policies, shared CPU limiter, [CpuBound]
  BackgroundServices/      BatchGeocodingWorker
  TagHelpers/              CspNonceTagHelper
  Infrastructure/          ConnectionStrings (postgres:// and redis:// URI parsing, Redis masking)
  HealthChecks/            DatabaseReadyHealthCheck
  Pages/                   Index, Login/Logout, Privacy, Accessibility, Error, AccessDenied, Shared/ (_Layout, _LoginPartial,
                           _PreferencesControls, _ProjectCard, _ProjectHero), Projects/Index, Projects/<Name>/{Index,Details}
  Areas/Account/Pages/     Profile
  wwwroot/                 css/ (see css/README.md), js/<Project>/…, js/ shared modules (§9), lib/ (vendored),
                           files/Brian-Coffey-Resume.pdf, img/og-preview.jpg, samples/batch-geocoding/*.csv
Portfolio.Tests/           Controllers/ Services/ Middleware/ Filters/ Startup/ Infrastructure/ HealthChecks/ DTOs/ Pages/
                           Repositories/ Contracts/ Golden/ Integration/ Helpers/ (§9)
Portfolio.Benchmarks/      console harness comparing native vs managed for all 13 kernels (§5, §7)
```

Ignored/untracked local noise: `bin/ obj/ .vs/ TestResults/ build/ DataProtection-Keys/ .env appsettings.Development.json k8s/secret.yaml *.user .claude/worktrees/`.
`Pages/Shared/_Layout.cshtml.css` is an unreferenced scaffold stylesheet (nothing links the scoped-CSS bundle); the site's CSS is in `wwwroot/css`.
Git: branch `main`, remote `github.com/brianpcoffey/gis-portfolio`; cleanup work lands through `cleanup/*` branches and PRs.

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

### Startup (`Portfolio.Web/Startup/*`)
`Program.cs` replaced the old single-file startup. It builds the host, calls the `Add*` registrations in a
fixed order (the order the old inline blocks ran in — keep it), then `UsePortfolioPipeline()`,
`MapPortfolioEndpoints()` and `StartupMigrations.Run(...)`. Startup log lines (Redis, Data Protection,
migration) go through one console logger created in `Program.cs`. **Never log the database connection
string** (it carries the password); Redis strings are masked by `ConnectionStrings.MaskRedisPassword`.
A new service or repository is registered `Scoped` in `Startup/DataRegistration.cs`; a new ArcGIS client in
`ArcGisClientRegistration.cs`. The registration list is pinned by `ServiceRegistrationSnapshotTests`.

### API surface (`/api/v1/...`)
`addresses` · `catrisk` · `change` · `clustering` · `collections` · `features` · `features/saved` ·
`fiber/{dashboard,materials,orders,shipments}` · `fleet` · `geocoding/{batch,reverse}` · `geometry` ·
`geostream` · `homefinder` · `network` · `outage` · `overlay` · `profile` · `raster` · `response` ·
`users` · `viewshed`; plus the version-neutral `POST /csp-report` (§6).
`Portfolio.Tests/Contracts/RouteTableTests.cs` holds the golden route list and also checks every route
string in `wwwroot/js/api-config.js` (`window.PortfolioApi.routes`, the **single source of truth for client
route paths**) resolves to a controller route — update both when a route changes.

### Persistence
Entities (`PortfolioDbContext`): `SavedFeature, Collection, UserProfile, UserClaim, Property, SavedSearch,
FiberClient, FiberOrder, FiberShipment, FiberMaterial, FiberInventoryTransaction`. `ls
Portfolio.Repositories/Migrations` lists the migrations. Migrations run at startup (`db.Database.Migrate()`);
`dotnet Portfolio.Web.dll --migrate` runs them and exits (Render's pre-deploy command; a failure exits
non-zero so the deploy aborts), and `RunMigrationsOnStartup=false` skips the startup pass — **set both or
neither**.

### Pipeline order (preserve; `Startup/Pipeline.cs`, pinned by `PipelineOrderTests`)
`UseForwardedHeaders` → `SecurityHeadersMiddleware` → exception handler/HSTS (non-dev) → HTTPS redirect
(**dev only**; Render terminates TLS) → static files → routing → `UseRateLimiter` → `UseSession` →
`ApiExceptionMiddleware` → `AnonymousUserMiddleware` → auth → authz → OpenAPI/Scalar → health →
controllers/pages. Static files sit before routing, so they are never rate-limited. Request bodies are
capped at 1 MB by Kestrel; endpoints that need more raise it with `[RequestSizeLimit]` (TestServer does not
enforce Kestrel limits — only a live run does).

### Error handling
Services throw `ArgumentException` → 400, `KeyNotFoundException` → 404, `InvalidOperationException` → 409,
`UnauthorizedAccessException` → 401. They reach the client in three layers:
1. **`[BadRequestOnArgumentException]`** (an *action* attribute, never class-level) turns an
   `ArgumentException` into 400 `{ "error": "<message>" }`. The public JS reads `body.error`, so this shape stays.
2. **`[ArcGisUnavailable(ArcGisUpstream.Geocoding|Features)]`** turns the open circuit breaker
   (`BrokenCircuitException`) into a 503 ProblemDetails with `retryAfterSeconds` taken from the resilience profile.
3. Everything else reaches **`ApiExceptionMiddleware`** (RFC 7807 ProblemDetails; 499 for a cancelled request,
   500 with a fixed generic text for unmapped exceptions). `UseExceptionHandler("/Error")` is only the final fallback.

Both the filter and the middleware surface `ex.Message` **only when the exception was thrown from Portfolio
code** (`ApiExceptionMiddleware.IsOwn` checks the throwing assembly); framework-thrown exceptions get the
generic "The request was invalid." / "The requested resource was not found." text so no framework detail leaks.
**Consequence for authors:** user-facing validation must be an explicit
`throw new ArgumentException(message, paramName)`. BCL throw helpers (`ArgumentNullException.ThrowIfNull`,
`ArgumentException.ThrowIfNullOrWhiteSpace`, …) are thrown from framework code, so the client sees only the
generic text — use them for conditions whose message nobody needs. Exception messages are public API: keep them
byte-identical unless a test or spec changes them (`ControllerErrorBodyTests` pins bodies;
`ControllerFilterWiringTests` pins which actions carry which filter, so a new validating action needs the
attribute *and* an entry there). A few controllers still translate a 404/409 inline; the Fiber controllers
have no catch blocks (unknown id → 404, missing identity → 409, both via the middleware).

### Batch geocoding (async job path)
`POST /api/v1/geocoding/batch` validates the CSV (≤ 2 MB upload, ≤ 1,000 data rows, `.csv` only), persists a
`BatchJob` in `IBatchJobStore`, and enqueues it on `IBatchJobQueue`, a **bounded in-process channel (capacity
8)** — `TryEnqueue` never waits, so a full queue answers **503 with `Retry-After`** (`BatchQueueFullException`).
The hosted `BatchGeocodingWorker` drains it with **2 concurrent workers** (each job fans out `MaxConcurrency`
geocode calls); on shutdown it marks still-queued jobs `Failed`. The queue is per process; job *state* is shared
through Redis when configured, so any replica can answer `GET …/{jobId}/status`. The synchronous
`POST …/geocoding/batch/sync` (hidden from OpenAPI; the demo page uses it) shares the same row cap.

### Static asset caching
`UseStaticFiles` sets `Cache-Control: public,max-age=31536000,immutable` when the URL carries `?v=…` (what
`asp-append-version` emits, so the URL changes whenever the file does) and `public,max-age=3600` otherwise;
ETag/304 handling is the default. `/api` responses that don't set their own `Cache-Control` get `no-store` from
`SecurityHeadersMiddleware`; the static scenario GETs (`catrisk/book`, `fleet/scenario`, `outage/network`, `response/scenario`) opt into `[ResponseCache(Duration = 3600)]`.

---

## 5. Native-kernel pattern (the project's signature)

13 kernels: `portfolio_scoring, geostream_processor, spatial_geometry_kernel, raster_terrain_kernel, spatial_graph_engine, spatial_cluster_kernel, viewshed_kernel, spatial_overlay_kernel, cat_risk_kernel, change_detection_kernel, network_trace_kernel, facility_location_kernel, vrp_solver_kernel`.

- Layout: `native/<name>/{CMakeLists.txt, include/<name>.h, src/*.cpp}`. Each is an **independent** CMake project (no top-level CMakeLists). Build: `cmake -S native/<name> -B build/native/<name> -DCMAKE_BUILD_TYPE=Release && cmake --build build/native/<name> --config Release`. The post-build step copies the library into `Portfolio.Services/bin/Debug/net10.0` and `Portfolio.Web/bin/Debug/net10.0`; to run the test project against it, also copy it into `Portfolio.Tests/bin/Debug/net10.0` (CI does this when it stages the `.so` files).
- C ABI: `extern "C"`, `#pragma pack(push,8)` structs, every pointer followed by an `int` length, caller-allocated buffers, status codes `0 / -1 invalid / -2 not found / -3 buffer too small / -99 exception`. Never let C++ exceptions or types cross.
- Managed side: `{Domain}NativeStructs.cs` (blittable, public fields, `Pack = 8`), `{Domain}NativeInterop.cs` (raw `DllImport`), `{Domain}NativeBridge.cs` (static; `IsAvailable` is a `static readonly` field initialised once per process by `NativeKernelLoader.TryLoad(...)`, plus status-code translation). All `internal`; tests see them via `InternalsVisibleTo`.
- `PORTFOLIO_DISABLE_NATIVE=1` (also `true`/`yes`) forces the managed path for the whole process (`NativeToggle`, read once). Service code dispatches native only if `IsAvailable`; **the managed fallback is production behavior and must never be removed.** Every result DTO carries `NativeAccelerated`.
- **Production ships NO native code** — the Docker image compiles none and the UI shows "Native: No". Deliberate: speedups are marginal and a second binary can silently diverge.
- **Reference implementation to copy for a new kernel: Spatial Overlay.** `docs/plans/README.md` (a historical design doc) has the long conventions list and a wire-up checklist; where it disagrees with this file, this file wins.
- **Measured speedups: 0.37×–2.26× across 21 workloads; native loses on 5, ties on 7, wins on 9 (±15%).** Best: VRP 2.26×, overlay 1.90×. Worst: geometry fan-triangulation 0.37×. The table in `native/README.md` is the authority.
- **Parity:** checksum equality across all 21 workloads is checked by `Portfolio.Benchmarks` only (run by hand, outside CI). In the test suite: `NativeParityTests` (geometry clip, viewshed) and `NativeScoringBridgeTests` drive native directly and report **Skipped** when the library isn't loaded; the `Golden/` tests pin shipped-scenario output that must hold with native present *and* disabled. CI's `native-parity` job runs the whole suite with all kernels present (`PORTFOLIO_REQUIRE_NATIVE=1` makes `NativeAvailabilityTests` fail if any bridge didn't load) and again with `PORTFOLIO_DISABLE_NATIVE=1`; with native present, coverage measures lower than the gate, by design — the gate runs only in the `test` job.
- `RedlandsRoadNetwork.cs` is **generated from OpenStreetMap — never hand-edit.** Node IDs are 1-based and contiguous (`index = id − 1`), all edges are bidirectional, cost is in km (the counts are in the file header / `RedlandsRoadNetworkTests`). `SpatialGraphService` shares one cached mutable graph instance — never mutate it.

---

## 6. Identity & security model

### Two separate identities — do not mix them
1. **Anonymous GUID** — `AnonymousUserMiddleware` sets an `AnonUserId` cookie (HttpOnly, Lax, 1 year; Secure outside Development) and stores the profile id in `HttpContext.Items["PortfolioIdentity"]`; read it through `IUserProfileService.GetCurrentUserId()`. Throttled to 10 new identities per minute per IP; never created for `/api` paths, and with no cookie present only for requests that accept `text/html` (probes and API clients get no identity). A cookie that names a Google-linked profile is dropped (an anonymous cookie must never act as a signed-in account). Visitor seed data is created at sign-in, not on first visit.
2. **Google OAuth + cookie authentication** — the sign-in `ClaimTypes.NameIdentifier` is linked to a profile through a `GoogleId` claim. At first sign-in the visitor's still-unlinked anonymous profile is adopted (merged) and the anonymous cookie deleted; later requests resolve `PortfolioIdentity` from the GoogleId claim.

**`"Authenticated"` is the only authorization policy** (`RequireAuthenticatedUser`); there are no roles. Every controller action is `[Authorize(Policy = "Authenticated")]` or `[AllowAnonymous]` (at action or class level, never neither; a class-level `[AllowAnonymous]` on a controller that also has authorized actions silently disables them, so mixed controllers annotate each action) — `AuthorizationMetadataTests` enforces it. Per-user rows are always scoped by `GetCurrentUserId()` in services/repositories; never accept a user or owner id from a request body or query string. Default challenge scheme is **Cookie** (unauthenticated pages go to the app's own `/Login`); `/api` paths return 401/403 instead of redirecting (`PortfolioCookieEvents.ApiAwareRedirect`). Log user ids only through `LogRedaction.UserId`.

### Session and sign-in lifetime (decision D-11, WP-35)
`AddSession`/`UseSession` **stay** (30 min idle, HttpOnly, essential cookie, Secure outside Development): nothing stores state in the session, but `Logout` calls `HttpContext.Session.Clear()` and the pipeline order is a constraint. The auth cookie is **8 h sliding** with a **24 h absolute cap**: `PortfolioCookieEvents.SigningIn` stamps an `auth_time` claim (Unix seconds); sliding renewals keep the original value; `ValidatePrincipal` rejects and signs out a ticket older than 24 h. A pre-existing cookie without the claim is stamped on first validation (the 24 h clock starts then); a present but unparseable claim is rejected. `Logout` is a POST with antiforgery, signs out, clears the session and deletes `AnonUserId`.

### Security headers and the CSP (`SecurityHeadersMiddleware`)
Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, `Cross-Origin-Opener-Policy` and `Cross-Origin-Resource-Policy: same-origin`; the `Server` header is removed. HSTS (1 year, no `includeSubDomains`, no preload) is non-Development only.
- **Enforced CSP — unchanged:** `Content-Security-Policy: frame-ancestors 'none'; base-uri 'self'; object-src 'none'` (deliberately does not restrict script/style sources yet).
- **Report-Only CSP:** the full policy (`default-src 'self'`, script/style from self + a per-request nonce + `js.arcgis.com` + the pinned `unpkg.com/leaflet@1.9.4/` path, `script-src-attr 'none'`, tile/image hosts, …) is sent as `Content-Security-Policy-Report-Only` with `report-uri /csp-report; report-to csp` and a `Reporting-Endpoints` header. The nonce is 128 random bits per request in `HttpContext.Items["csp-nonce"]`, stamped on **every `<script>` and `<style>` element** by `CspNonceTagHelper`. Consequence: no inline event-handler attributes (`onclick=` …), and inline code needs no hand-added nonce.
- **`POST /csp-report`** (`CspReportController`): anonymous, version-neutral, hidden from OpenAPI, 8 KB cap, accepts `application/csp-report` and `application/reports+json`, logs one sanitised Warning per violation (URLs reduced to origin, control characters stripped), drops the known ArcGIS 4.29 `eval` probe, answers 204; rate-limited by the `"csp"` policy.
- **Rollout:** Report-Only soak start date **2026-10-13**; enforcement (cleanup WP-51) happens only after 1–2 weeks of clean reports in production and the owner's sign-off. Rollback is switching the header back to Report-Only.

### Rate limiting (`Startup/RateLimitingRegistration.cs`, `RateLimiting/*`)
It runs after routing (so per-endpoint policies resolve) and before auth, so partitioning is by remote IP — which is the real client behind Render only because forwarded headers are applied first. 429 responses carry `Retry-After` and an empty body.
- **Global:** fixed window, 300/min/IP, on every routed request.
- **`"compute"`** — sliding window, 60/min/IP (viewshed re-runs on every cell click, so 15/min would break the UI). On the seven native-compute spatial controllers (`geostream, geometry, raster, network, clustering, viewshed, overlay`).
- **`"expensive"`** — fixed window, 15/min/IP. On controllers that call the paid ArcGIS API or run heavy compute: the four ArcGIS-calling controllers (addresses, batch geocoding, reverse geocoding, features), Home Finder, and the five vertical controllers (`catrisk, change, fleet, outage, response`).
- **`"csp"`** — fixed window, 30/min/IP, on `/csp-report`.
- **Shared CPU concurrency limiter:** controllers marked **`[CpuBound]`** (every native-compute controller; **not** the I/O-bound ArcGIS proxies) pass through one process-wide `ConcurrencyLimiter` (permits = max(2, cores), queue 16, oldest first) *after* their per-IP window, so total CPU stays bounded even if the per-IP key is spoofed. Queue overflow → 429 with `Retry-After: 5`.
- CRUD controllers (collections, fiber×4, profile, saved features, user profile) have only the global limit. Health endpoints call `DisableRateLimiting()`.
- A new compute controller gets `[EnableRateLimiting("compute")]` + `[CpuBound]`; a new paid-API caller gets `"expensive"` and not `[CpuBound]`.

### Hosting and secrets
Forwarded headers (`X-Forwarded-For/Proto`, limit 1) are trusted from any proxy until `ForwardedHeaders:KnownNetworks` is set, and `AllowedHosts` is `*` — both wait on the owner's production check (cleanup WP-52); never raise `ForwardLimit` without `KnownNetworks` (the code clamps it to 1 while the list is empty). Secrets come from environment variables / the untracked `appsettings.Development.json`; never print or commit them. Data Protection keys live in Redis or on the filesystem, unencrypted, so the Upstash credentials are as sensitive as the keys themselves.

---

## 7. Commands

```bash
dotnet restore
dotnet build -c Release                     # CI treats warnings as errors; locally they're warnings
dotnet test                                 # whole suite (prints the current test count); passes with or without native libs
PORTFOLIO_DISABLE_NATIVE=1 dotnet test      # PowerShell: $env:PORTFOLIO_DISABLE_NATIVE="1"
# coverage exactly as CI measures it, then the gate (80% line / 60% branch; never lower them)
dotnet test Portfolio.Tests -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
py -I tools/metrics/coverage_gate.py TestResults
dotnet run --project Portfolio.Web --launch-profile http     # http://localhost:5000 (the https profile serves https://localhost:5001 and :5000)
dotnet Portfolio.Web.dll --migrate                           # migrate and exit (Render pre-deploy; see §4)
dotnet run --project Portfolio.Benchmarks -c Release         # native-vs-managed table; build kernels first
dotnet ef migrations add <Name> --project Portfolio.Repositories --startup-project Portfolio.Web
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up
```
Local run needs a Postgres connection string (`ConnectionStrings:DefaultConnection` or `DATABASE_URL`, both Npgsql and `postgres://` URI formats; the committed `appsettings.json` has them empty) and `Authentication:Google:ClientId/ClientSecret` (the Google handler rejects empty values, so sign-in won't work without real ones). Put them in `Portfolio.Web/appsettings.Development.json` (git-ignored). Redis is optional.
Static files and Razor markup are largely outside the test suite (exceptions: `ArcGisSriTests` reads source, the integration tests hit the pipeline) — verify UI changes in a browser with DOM/geometry assertions, not screenshots.

### `tools/` (not in the .sln; never served)
- **`tools/metrics/`** — `coverage_gate.py` (the script CI runs, runnable locally: `py -I tools/metrics/coverage_gate.py [results_dir] [--min-line N] [--min-branch N] [--package-floor NAME LINE BRANCH]`; defaults come from `MIN_LINE_COVERAGE` / `MIN_BRANCH_COVERAGE`); `csmetrics` (Roslyn cyclomatic + cognitive complexity per C# member: `dotnet run -c Release --project tools/metrics/csmetrics -- . cs-metrics.csv`, then `py -I tools/metrics/summarize_cs.py cs-metrics.csv`); `jsmetrics.js` (`node --expose-internals tools/metrics/jsmetrics.js . js-metrics.csv`, then `summarize_js.py`); `cpp-complexity.sh` + `parse_clang_tidy.py` (needs clang-tidy); `loc.py` (LOC by project and language); `parse_analyzers.py` (calibrates csmetrics against CA1502 from a build log of a *throwaway* clone). Each script's header comment has its exact usage.
- **`tools/fe-audit/`** — `fe-audit.js` and `grad.js` run in the browser tool: copy them temporarily into `Portfolio.Web/wwwroot/__audit/` (never commit them there) and run `(await import('/__audit/fe-audit.js?v=1')).run({ wait: 1200 })` (exports `run`, `assert`, `focusIndicators`, `tabOrder`, `focusProbe`); `balance.py <repoRoot>` (Razor tag *balance* — run it after any merge that touched markup), `colors.py <repoRoot> <out.json>` (hard-coded colour literals), `inventory.py <repoRoot> <out.json>` (per-page asset inventory), `contrast.py` (WCAG ratios for token pairs; edit its tables when tokens change). Run the Python ones with `py -I`.

---

## 8. CI/CD (`.github/workflows/ci.yml`)

Runs on push to `main`, on PRs to `main` and `cleanup/**`, and on manual dispatch. Actions are pinned by commit SHA. Jobs:
- **test** — restore, `dotnet format --verify-no-changes` (Migrations and `RedlandsRoadNetwork.cs` excluded), Release build with warnings-as-errors (including the CA1502 complexity gate at 15, `CodeMetricsConfig.txt`), the suite with coverage, and `tools/metrics/coverage_gate.py` with the floors in `ci.yml`'s env block: overall line/branch plus a `Portfolio.Web` floor. The floors are ratchets — never lower them; raise them to just under the measured values when coverage grows.
- **native-parity** — builds all 13 kernels on Ubuntu, stages the `.so` files into the test output (the staged count must equal the number of `CMakeLists.txt`), runs the suite with `PORTFOLIO_REQUIRE_NATIVE=1`, then again with `PORTFOLIO_DISABLE_NATIVE=1`. No coverage gate here.
- **docker** — the production image builds.
- **dependency-review** — pull requests only.
- **deploy** — push to `main` only, after the three jobs above; POSTs the `RENDER_DEPLOY_HOOK` secret. It exits 0 with a "skipping" message when the secret is missing, so a green job is not proof a deploy fired.

Also `security-scan.yml` (weekly vulnerable-package sweep) and `dependabot.yml`. **Treat a push to `main` as a production deploy.** With Render's own Auto-Deploy switched *off* and the hook secret set — the owner's configuration since July 2026, not visible in the repo — only a green build deploys. (A `dotnet format` gate and a CA1502 complexity gate are planned by cleanup WP-50 and are not in CI at the time of writing.)

---

## 9. Conventions to follow when editing

**C#**
- Interfaces `I{Name}Service/Repository`; async methods end `Async`, take a trailing `CancellationToken cancellationToken = default` (the batch-geocoding types use `ct`) and forward it; private fields `_camelCase`. Do not write a `<param>` tag for the token (it overwrote the request-body description in `/openapi/v1.json`; `OpenApiContractTests` pins the document, and CS1573 is allowed for it).
- Errors: see **Error handling** in §4 — explicit `ArgumentException(message, paramName)` for user-facing validation; filters on actions, not controllers. Repositories return `null`/`false`, never throw business exceptions, and never map to DTOs.
- Controllers return DTOs only (never entities); error body shape is `new { error = "..." }`; add `[ProducesResponseType]` for every status and an XML `<summary>` on public actions.
- **All models/DTOs/enums/ArcGIS wire types live only in `Portfolio.Common`.** DTOs: plain classes, `{ get; set; }`, `= []` / `= string.Empty` defaults; validation is imperative in services. Legacy exceptions — DataAnnotations in `CollectionDto`, `FiberMaterialDto`, `FiberOrderDto`, `HomeSearchPreferencesDto` (which also implements `IValidatableObject`) and the Options classes — don't copy them.
- Time comes from an injected `TimeProvider` (a singleton registration); never `DateTime.Now`. Tests use `Helpers/FakeTimeProvider`.
- Hand-written `private static MapToDto` (no AutoMapper). Don't add MediatR/FluentValidation/Dapper or new auth policies/schemes.
- DI: services/repositories `Scoped` in `Startup/DataRegistration.cs`; the only singletons are the job store and queue and `TimeProvider`; the ArcGIS-calling services are typed `AddHttpClient<I,T>` registrations (transient) — never add a parallel `AddScoped` for them.
- Rate limiting for new controllers: §6.
- `Task`-returning-but-sync compute services use `Task.FromResult`; limits are `private const int`; validate with `ArgumentNullException.ThrowIfNull(request)` then `throw new ArgumentException(message, nameof(request))`.
- Namespace style is **mixed** (block-scoped in older files, file-scoped in newer); match the file/folder you're in. UTF-8, 4-space indent (2 for YAML), CRLF in the working tree; don't reformat lines you don't change.

**Frontend**
- **Pages:** `Pages/Projects/<Name>/Index.cshtml(.cs)` + `Details.cshtml(.cs)`; PageModels are bare (`OnGet`). Page stylesheets go in the page's `Styles` section, scripts in its `Scripts` section as `<script defer src="~/js/…" asp-append-version="true">`.
- **Page scripts are no longer "one `app.js` per project".** Most pages still have a single classic `wwwroot/js/<Name>/app.js` (an IIFE, no module syntax). Exceptions: **HomeFinder** is six classic scripts sharing `window.HomeFinder` (`context.js` loads first, `app.js` last); **Response** is five sharing `window.ResponsePage` (`context.js` first, `app.js` last); **State Explorer** is ES modules (`<script type="module">`: `stateStore`, `featureService`, `mapManager`, `uiManager`, `app`, …); **Plant Operations** is four scripts (dashboard, orders, shipments, inventory). Copy that mentions client files (Details pages) must match what is on disk.
- **Shared modules** (deferred, loaded by `_Layout.cshtml` in this order after the Bootstrap bundle): `api-config.js` → `PortfolioApi.routes`; `api-fetch.js` → `apiFetch/apiGet/apiPost/apiPut/apiDelete/apiPostForm/getApiError` (401 → redirect to `/Login?returnUrl=…` unless `redirectOn401: false`; 429 → a friendly message; error text from `body.error`, then ProblemDetails `detail`/`title`); `dom-safe.js` → **`PortfolioDom`** (`escapeHtml`, `el` — the only sanctioned way to put untrusted text into markup; never concatenate unescaped data into `innerHTML`); `toasts.js` → `showToast`; `ui.js` → **`PortfolioUi`** (`announce`, `inlineAlert`, `busy`, `runAction`); `format.js` → **`PortfolioFormat`** (`number/percent/money/unit/clock/coord/yesNo`, one set of number formats); `charts.js` → **`PortfolioCharts`** (`token`, `palette`, **`mapPalette`**, `status`, `svg`, `scale`, `ticks`, `mix`, `rng`, `legend`, `describe`); then `site.js`, `preferences.js` (theme and display prefs; fires `themeChanged`), `navigation.js`, `animations.js`. Loaded per page where needed: **`PortfolioMap`** (`leaflet-setup.js`: `create(id, {center, zoom, label})`, the one Leaflet factory with the OSM tile layer — after `leaflet.js`; used by Route Planner, Fleet, Response), **`PortfolioEsri`** (`esri-theme.js`: `load()` / `loadTheme()` for the ArcGIS 4.29 script and light/dark widget theme, with sha384 SRI pins for `init.js` and both theme sheets — the pins fail closed if Esri republishes 4.29 or `VERSION` changes without new digests; Plant Operations also carries the `init.js` digest in its markup and `ArcGisSriTests` keeps the copies in sync), and `carousel.js` (home page).
- **Use the shared modules** instead of per-page copies: `PortfolioApi` routes and the `api-fetch` helpers for every call, `PortfolioUi.runAction/busy/inlineAlert` for run buttons and alerts, `PortfolioFormat` for numbers, `PortfolioCharts` for SVG, scales, seeded RNG and legends. Render big rasters to `<canvas>`, not DOM nodes.
- **CSS tokens** live in `wwwroot/css/base.css`: `:root` (light) with `body.dark-mode` overrides. Families: palette (`--primary`, `--accent*`, `--surface`, `--text*`, `--border`), contrast-safe accent tokens (`--accent-on-fill`, `--accent-text`, …), `--focus-ring`, `--input-border`, status (`--status-{success,danger,warning,info,caution}-*`), theme-aware chart colours (`--chart-1…8`), and the **theme-independent `--map-series-1…8` and `--map-muted`** for marks drawn over basemap tiles (the tiles stay light in both themes, so these are declared once in `:root` and never redefined in `body.dark-mode`; read them with `PortfolioCharts.mapPalette()` / `token("map-muted")`). Never hard-code colours (`tools/fe-audit/colors.py` counts literals); scope page rules under a page class; motion is neutralised by `preferences.css`. Load order and per-page sheets: `wwwroot/css/README.md`.
- **CSP:** inline `<script>`/`<style>` get their nonce automatically; no inline event-handler attributes; a page's inline script in `Scripts` runs *before* the deferred layout scripts, so it must wait for `DOMContentLoaded` or be an external deferred file.
- **Details pages** use one of two templates, each section being `<section class="panel shadow-sm rounded p-4 mb-4">` with an `<h2 class="h4 mb-3">` and a `<ul>` of `<li><strong>Label:</strong> text</li>`; the last section ("Interview Discussion Points") is a plain list of questions. **8-section** (AddressStandardization, BatchGeocoding, GeoStream, Geometry, HomeFinder, Network, PlantOperationsDashboard, ReverseGeocoding, StateExplorer, Terrain): High-Level System Overview → Frontend Architecture → API Layer → Services and Business Logic → Data Access Layer → Infrastructure and Deployment → Engineering Decisions and Tradeoffs → Interview Discussion Points. **6-section** (CatRisk, Change, Clustering, Fleet, Outage, Overlay, Response, Viewshed): High-Level System Overview → Algorithm → API Layer → Services and Native Boundary → Engineering Decisions and Tradeoffs → Interview Discussion Points. Technical, no marketing copy; describe hosting as **Render + Postgres** and Azure/AKS only as *future*; speedup claims point at the `native/README.md` table.

**Tests** (`Portfolio.Tests`, xUnit v3 + Moq; `Assert.SkipUnless` for native-gated cases)
- **One test class per SUT**, in `Controllers/<Controller>Tests.cs` or `Services/<Service>Tests.cs`. The old multi-SUT classes (`SpatialComputeControllerTests`, `SpatialComputeServiceTests`, `HotspotViewshedOverlayServiceTests`) were split in the cleanup; keep it that way. Small helper types may share a file.
- **Controller tests** hold `Mock<IXxx>` fields and build the controller in the constructor; services with collaborators do the same. **Pure compute services** (no collaborators) are constructed inline in each test with `NullLogger<T>.Instance`. Names are `Feature_Scenario_ExpectedOutcome`; arrange/act/assert comments; `[Fact]` plus `[Theory]/[InlineData]` for validation.
- Every new service method or controller action needs a happy path plus validation / not-found cases, and **assert error bodies** (`{ error }`), not just the result type. Tests assert the computed result, **not** whether native ran (only the native-gated parity tests look at `NativeAccelerated`).
- HTTP services are tested with a fake `DelegatingHandler`; caches with a real `MemoryDistributedCache`; **no test may reach arcgis.com**. A few classes use EF InMemory (transitively through `Portfolio.Repositories`); there are no real-database tests — ask first.
- Other kinds: `Golden/` (pinned output of shipped scenarios; must hold with native present and disabled), `Contracts/` (route table, authorization metadata, ArcGIS SRI — they guard "public routes don't change", so a deliberate change edits the golden list), `Integration/` (boots the real `Program.cs` in-process through `PortfolioAppFactory`: EF InMemory swapped in, migrations skipped, every ArcGIS client faked to 503 — no database, no network; use **one factory per test** when rate-limit state matters), `Startup/` and `Middleware/`.
- `xUnit1051` is the only suppressed analyzer in the tests; everything else is a CI error. Wall-clock assertions need generous bounds (coverage instrumentation is slow).

---

## 10. Gotchas & known inconsistencies

- `README.md` mentions the deprecated Home Finder `score` / `properties/{id}` aliases: both are still `[Obsolete]`, hidden from OpenAPI, and (because of `coverlet.runsettings`) absent from coverage. `POST …/geocoding/batch/sync` is *live* (the demo uses it) but hidden from OpenAPI; `POST /api/batchgeocoding/sync` is its unversioned legacy twin.
- Native bridges: `NativeKernelLoader.IsNativeInvocationException` includes `InvalidOperationException`, so a native negative-status error logs once and silently falls back to managed — intentionally documented, don't "fix" silently. The comments in `NativeToggle.cs` and `Portfolio.Benchmarks/Program.cs` still say the probe happens "in a static constructor"; it is a `static readonly` field initialiser, with the same once-per-process behaviour.
- General caution for native code: `std::map::operator[]` on a missing key inserts a default (`0.0` = "reachable free"); use `.find()`.
- **Coverage depends on the generated `RedlandsRoadNetwork.cs`**: without its lines, line coverage fell to ~73% at the cleanup baseline, below the 80% gate. Never mark it generated/excluded for coverlet (`[GeneratedCode]`, `ExcludeByFile`); an `.editorconfig` `generated_code` entry (analyzers only) is fine.
- Docker: the `Dockerfile` sets `ASPNETCORE_HTTP_PORTS=10000` (Render's default port); `docker/docker-compose.yml` overrides it to 8080.
- Render's free tier cold-starts; that's why the `--migrate` pre-deploy step exists. Render's actual dashboard settings (pre-deploy command, `RunMigrationsOnStartup`, Auto-Deploy, the hook secret) are not in the repo.
- `Pages/Shared/_Layout.cshtml.css` is an unreferenced scaffold file (§3).
- A `.vs/` dir, `TestResults/` and `DataProtection-Keys/` may be present locally — never commit them. Stale `net8.0` folders under `bin/`/`obj/` are noise; everything targets `net10.0`.
- `docs/resume-update.md`: the résumé PDF in `wwwroot/files/` may lag the Drive source; the site describes the PDF using metrics read from the file.

## 11. Things the owner has asked agents not to do
- Don't hand-edit generated `RedlandsRoadNetwork.cs` (regenerate from OSM).
- Don't submit job applications autonomously; draft and show screening answers for approval.
- Don't make claims (site copy, résumé, README) the benchmarks or code don't back.
- Don't flip LinkedIn to public "Open to Work"; visibility is recruiters-only.
- Don't push to `main` (a production deploy) or force-push a pushed branch; don't touch other people's worktrees.
- Browser-pane screenshots hang on OSM tiles — verify UI with DOM/geometry assertions via `javascript_tool`.
- If another instance holds `Portfolio.Web/bin`, build to a scratch `-o` dir and run on an alternate port.
- After a "keep both" merge resolution in markup, verify tag **balance** (`tools/fe-audit/balance.py`), not tag counts.
