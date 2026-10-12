# Portfolio cleanup: master plan (Stage 1)

| | |
|---|---|
| Repo state | `main` @ `9d9ea02`, analysed 2026-10-12 |
| Inputs | `docs/cleanup-prompt.md` (brief), `docs/agent-context.md`, and in this folder: `findings-security.md`, `findings-complexity.md`, `findings-frontend.md`, `findings-docs-tests.md` |
| Status | **Approved 2026-10-12.** The owner accepted every §4 default (D-15's download included), set about 15 PRs (§3.3a supersedes the PR column of §3.3), and asked for a check-in after wave 1. D-12 waits for the owner's §4.5 production check. D-20 is the owner's own repository toggle. |
| How to use this file | Every Stage-2 implementer gets these four things: §C (constraints), the WP spec from §3.5, `docs/agent-context.md`, and the findings sections that WP cites. §5 is the verification protocol. |

---

## 0. Executive summary

### What's wrong, most important first
1. **Two unauthenticated DoS paths (CAT I).**
   - Seven compute endpoints have no per-endpoint rate limit, and their validators cap cell *count* but not *work*. One viewshed request with width 250,000 and height 1 burned **237 s** of CPU.
   - No endpoint without an explicit limit has a body-size cap. One 25 MB body to `/api/v1/addresses/parse` grew the working set by **1.2 GB**. The production instance has 512 MB.
2. **Identity mixing (CAT II).**
   - The anonymous `AnonUserId` cookie keeps working for a profile after it has been linked to Google.
   - A Google sign-in made while carrying someone else's GUID overwrites that account's `google_id`.
   - Every cookieless page view inserts about 76 rows.
3. **XSS and no script CSP (CAT II).**
   - A CSV upload can execute script in the Batch Geocoding results table (verified exploit).
   - PlantOps has a stored self-XSS through an inline handler.
   - There are 10 unsafe HTML sinks, 10 ad-hoc escape helpers (8 don't escape quotes), and the CSP has no `script-src`/`style-src`.
4. **Leakage and misconfiguration.**
   - Framework exception text reaches clients; oversized bodies return 500.
   - Connection strings appear inside exception messages.
   - Forwarded-header trust is wide open, and the rate-limit key is the right-most `X-Forwarded-For` entry. Render's hop layout is unverified.
   - The container runs as root, and the CI actions are pinned to tags, not SHAs.
5. **The published contract and claims are wrong.**
   - All 24 documented request bodies in `/openapi/v1.json` say "Cancellation token".
   - Site copy implies SIMD/AVX speedups the benchmarks don't show, and 5 Details pages publish withdrawn figures.
   - `native/README.md` contradicts its own table: it says 8 wins / 8 losses / 5 ties, but the table gives **9 / 5 / 7**.
6. **Tests don't guard what the cleanup will touch.**
   - `Program.cs` and all three middlewares have 0% coverage.
   - 39% of covered lines are one generated file. Real line coverage is **73%**.
   - The 12 native-gated tests report Passed when nothing ran.
7. **Complexity is concentrated.**
   - One 1,444-line seed method has CA1502 251.
   - `Program.cs` `<Main>$` has CA1502 61.
   - The managed solver mirrors have cognitive complexity 34–83.
   - 35 production members exceed 15.
8. **Frontend accessibility and consistency.**
   - Focus rings are broken site-wide and the skip link does nothing.
   - Focus drops to `<body>` after "Run" on 11 demos.
   - 19 text pairs fail contrast, and the carousel puts focus on off-screen cards.
   - There are 387 hard-coded colours.
   - jQuery and DataTables load on 42 pages that don't use them, and static assets have no `Cache-Control`.
9. **Docs drift.** `.github/copilot-instructions.md` has 50 stale statements. One of them would reintroduce a fixed native bug.

### Plan shape
- **54 work packages, in 9 waves, mapped to 55 PRs.**
  - WP-00…WP-50 are scheduled in waves W0–W8.
  - Three are owner-gated: WP-51 (CSP enforce), WP-52 (hosts/XFF) and WP-53 (native ABI, deferred).
  - Three are optional: WP-43, WP-44 and WP-47.
  - The PR map is in §3.3.
- Characterization tests and security fixes come first (W1–W2). `Program.cs` is decomposed in W3, after the security edits to it. Service splits and the frontend page batches run in W4. Comment/XML trimming, docs, IDE0005, `dotnet format` and the CA1502 gate come last (W7–W8).
- Every wave is file-disjoint, so its WPs can run in parallel worktrees. §3.4 has the matrix.

### Decisions that block the most work (full list in §4)
| ID | Decision | Blocks | Default |
|---|---|---|---|
| D-01 | Add `Microsoft.AspNetCore.Mvc.Testing` and host-level tests (EF InMemory, no real DB) | ~20 WPs | Yes |
| D-02 | Security limit values: a "compute" tier, input caps, body limits, persistence caps | WP-06, 08, 14 | As listed |
| D-09 | Visible UI changes within the current look: focus ring, input borders, carousel, State Explorer behaviour, number formats | 8 FE WPs | Yes |
| D-04 | Bug fixes that change public behaviour: Fiber 404s, graph edge skip, generic framework errors and 413, protected claims, error-detail display | WP-04, 06, 14, 16, 34 | Yes |
| D-14 | Commit the measurement tools under `tools/` | WP-13 and all complexity reporting | Yes |
| D-03 | CSP rollout plus the new public `/csp-report` route | WP-20, WP-51 | Yes |
| D-05 | Identity hardening behaviour | WP-15 | Yes |
| D-08 | Honesty edits to site copy (owner reviews wording) | WP-11 | Yes |

### What I verified and what changed versus the findings
- **Verified against the code at HEAD:** every CAT I/II security finding, every P1 item, and every latent bug an agent flagged. Each is recorded in Appendix A. No CAT I/II finding fell.
- **Corrected:**
  - TST-06 says `public partial class Program` is needed. It isn't.
  - SEC-WP4 plans a characterization test that would fail.
  - CX-10 says to preserve the graph `KeyNotFoundException`. I superseded that: native already drops undeclared edges, so the managed path will match it.
  - SEC-WP2 removes the `/health/ready` rate-limit opt-out. I kept the opt-out and added a cache.
  - I dropped one proposed cap (`maxIterations`).
- **Added:** NEW-01…NEW-08. The main ones are a protected-claim deletion hole, and a check that saved-state geometry size must be measured before it is capped.
- **Time-sensitive:** the Stage-1 tools live in the session scratchpad. WP-00 step 1 must copy them out before the session ends.

---

## C. Constraints for implementers (give this section to every Stage-2 agent)

### C.1 Hard constraints (from the owner's brief; do not violate)
1. **Behaviour-preserving.**
   - Public routes, JSON shapes and `NativeAccelerated` semantics stay unchanged. The only exceptions are the behaviour changes a WP spec lists, all of which trace to a decision in §4.
   - If a route or shape changes, update `wwwroot/js/api-config.js`, the callers and the tests together, and call it out in the PR.
2. **Never hand-edit** `Portfolio.Services/Data/RedlandsRoadNetwork.cs` (generated from OSM), EF migrations or the model snapshot. Schema and data changes go through `dotnet ef` only.
3. **Keep every managed fallback** and the native/managed parity guarantees.
   - A C ABI change must be made in four places together: the C header, the C# struct, the native sources and the parity tests.
   - Production ships no native code. Do not change that.
4. **Identity.** Keep the two identity systems separate (anonymous GUID vs Google). `"Authenticated"` stays the only policy. Add no new auth schemes or policies.
5. **Keep the `Program.cs` pipeline order** (agent-context §4). `UseSession` stays unless D-11 says to remove it.
6. **No new frameworks** (AutoMapper, MediatR, FluentValidation, Dapper, npm, a bundler, TypeScript) without the owner's approval. The only approved new dependency is `Microsoft.AspNetCore.Mvc.Testing`, and only for tests (D-01). The tools under `tools/` are covered by D-14.
7. **All models, DTOs and enums stay in `Portfolio.Common`.**
8. **Honest claims.**
   - Measured speedups are 0.37×–2.26×: 9 wins / 5 losses / 7 ties at ±15%.
   - Make no SIMD, AVX or "high-performance" claims, and don't rewrite marketing or résumé content beyond consistency fixes.
9. **Git.**
   - Work only on `cleanup/<area>` branches, in small commits prefixed with the WP ID.
   - Never push to `main` and never force-push a branch that has been pushed. Open PRs; don't merge them.
   - A push to `main` is a production deploy.
10. **No secrets** in commits, logs or output.
    - Never print `appsettings.Development.json`.
    - Don't submit anything to external services.
    - Downloads need the owner's OK. The ArcGIS SRI hashes are covered by D-15.
11. **No wholesale visual redesign.** The only visible changes allowed are those listed in D-09.

### C.2 Plan-specific guards
- **Edit only the files in your WP's list**, plus new test files the spec names. If you need another file, stop and report.
- **Locate code with the grep anchor**, not the line number. Line numbers drift.
- **Characterization first.** Commit 1 adds tests that pass on the old code. Later commits may change only the assertions the spec names as deliberate.
- **Exception text is public.** Messages thrown by services become 400 `{ "error": "…" }` bodies, so keep them byte-identical. The `{ error }` 400 shape stays; the JS reads `body.error`.
- **Coverage.**
  - Never mark `RedlandsRoadNetwork.cs` as generated for coverlet (`[GeneratedCode]`, `[ExcludeFromCodeCoverage]`, `ExcludeByFile`). Doing so drops line coverage to about 73% and fails CI. An `.editorconfig` `generated_code = true` entry is fine.
  - Never lower `MIN_LINE_COVERAGE` 80 or `MIN_BRANCH_COVERAGE` 60. Never add a coverage gate to the native-present job: it measures 78%/52% by design.
- **Native-gated tests** assert the computed result, not whether native ran.
- **Comment-only WPs (WP-48)** produce comment, XML-doc and whitespace diffs only. Check with `git diff -w --word-diff`.
- **Formatting.**
  - Don't reformat lines you don't change. The formatting pass is WP-50.
  - Keep each file's existing namespace style and line endings.
  - Write UTF-8 without a BOM.
- **Don't create new `.md` files.** Subagents can't. Usage notes go in script header comments or in `agent-context.md` (WP-49).
- **Test and verification limits.**
  - Never sign in with Google or create accounts. Auth-only pages are covered by D-16.
  - Don't trigger ArcGIS geocoding (Batch upload, Reverse, Address validate) from a browser without the owner's OK. Use fakes or static probes.
  - Browser-pane screenshots hang on OSM tiles, so use DOM and geometry assertions only.

### C.3 Environment facts (re-derivation not needed)
- **.NET:** SDK 10.0.401 and runtime 10.0.12. A Visual Studio Installer upgrade happened mid-session, and the baselines reproduce on 10.0.401.
- **CMake:** not on PATH. Use `"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"` with short `-B` directories (e.g. `%TEMP%\b\<wp>\k01`); deep paths hit MAX_PATH.
- **Python:** `py`. Run analysis scripts with `py -I`.
- **Postgres:** on `localhost:5432`.
- **Worktrees:** a worktree does not contain the git-ignored `Portfolio.Web/appsettings.Development.json`. Copy it in only when the app must run, and never print it.
- **Browser:** the pane is one shared resource, so serialize frontend verification. `tools/fe-audit/fe-audit.js` (after WP-13; before that, findings-frontend Appendix A) is the harness.

---

## 1. Baseline measurements

### 1.1 Build (source: findings-complexity §1.2; findings-docs-tests §B1)
| Build | Warnings | Errors |
|---|---|---|
| CI-equivalent: `dotnet build -c Release --no-incremental -p:ContinuousIntegrationBuild=true` | **0** | 0 |
| Plain local Release | 0 | 0 |
| 13 C++ kernels (MSVC default level, VS-bundled CMake 3.31.6) | 0 | 0 |
| `dotnet format --verify-no-changes`, migrations excluded | **fails: 118 files** | whitespace 42, final newline 63, import order 35, BOM 4. On an LF checkout, `end_of_line = crlf` adds 41,219 more edits |

### 1.2 Tests (cx §1.3; dt §0, §B3)
| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| Release, no native libraries (the CI gate) | 697 | 697 | 0 | 0 |
| Release, `PORTFOLIO_DISABLE_NATIVE=1` | 697 | 697 | 0 | 0 |
| Debug, all 13 kernels built and staged | 697 | 697 | 0 | 0 |
| Debug, kernels staged plus `PORTFOLIO_DISABLE_NATIVE=1` | 697 | 697 | 0 | 0 |

The suite has 609 `[Fact]`/`[Theory]` attributes (580 + 29) and 117 `[InlineData]` rows. Twelve native-gated tests use `if (!IsAvailable) return;`, so they pass vacuously without the libraries.

### 1.3 Coverage (cx §1.3–1.4; dt §B2–B3)
| Scope | Line | Branch |
|---|---|---|
| **CI gate** (Release, managed) | **83.64%** (12,170/14,551). dt's run measured 12,172, a 2-line difference between runs. | **67.36%** (2,134/3,168) |
| Release, `PORTFOLIO_DISABLE_NATIVE=1` | 83.31% | 67.42% |
| **Excluding the generated `RedlandsRoadNetwork.cs`**, which contributes 5,730 of 5,730 covered lines (39.4% of all valid lines) | **73.01%** (6,440/8,821) | unchanged (the file has no branches) |
| Native present (Debug) | **77.99%** (below the gate) | 51.54% |
| Managed-only, Debug | 82.35% | 67.77% |
| Union of the native and managed Debug runs | 87.72% | 69.49% |
| Portfolio.Common | 35.0% (14/40) | 0% (0/8) |
| Portfolio.Repositories (no DB tests) | 47.8% (326/682) | 47.29% |
| Portfolio.Services | 90.7% (11,178–11,180/12,321) | 75.91% |
| **Portfolio.Web** | **43.2%** (652/1,508) | **23.60%** |

Classes at 0%:
- `Program.cs` (0/451, 0/26 branches)
- `AnonymousUserMiddleware` (0/69)
- `ApiExceptionMiddleware` (0/33)
- `SecurityHeadersMiddleware` (0/9)
- `DatabaseReadyHealthCheck` (0/9)
- The controllers SpatialCluster, SpatialOverlay and Viewshed
- All 9 repositories

`coverlet.runsettings` excludes `[Obsolete]` members. That hides the **live** `POST /api/v1/geocoding/batch/sync` endpoint.

### 1.4 Complexity (cx §2; tool calibrated against the real CA1502 analyzer on 446/446 methods)
**C# distribution, production code (Common + Repositories + Services + Web):**

| Members | CA1502 >10 / >15 / >25 | McCabe >10 / >15 / >25 | Cognitive >10 / >15 / >25 |
|---|---|---|---|
| 841 | 37 / **17** / 7 | 39 / 18 / 7 | 46 / **34** / 16 |

35 members exceed 15 on either metric. Outside production, Tests peak at 10/25 (`NativeParityTests.ManagedViewshed`) and Benchmarks `Program.Render` is 16/26.

**Top 25 production members, sorted by cognitive complexity:**
| # | Member | CA1502 | Cognitive | file:line |
|---|---|---|---|---|
| 1 | `UserProfileSeedService.SeedForUserAsync` | 251 | 236 | Services/Services/UserProfileSeedService.cs:21 |
| 2 | `ChangeDetectionService.LabelComponentsManaged` | 29 | 83 | ChangeDetectionService.cs:409 |
| 3 | `FleetRoutingService.OrOptMove` | 16 | 70 | FleetRoutingService.cs:506 |
| 4 | `Program <Main>$` | 61 | 50 | Web/Program.cs:31 |
| 5 | `ResponseCoverageService.SolveManaged` | 20 | 47 | ResponseCoverageService.cs:277 |
| 6 | `FleetRoutingService.ConstructSavings` | 26 | 43 | FleetRoutingService.cs:354 |
| 7 | `RedlandsPropertySeedData.Build` (generator) | 15 | 35 | Repositories/Seed/RedlandsPropertySeedData.cs:90 |
| 8 | `ChangeDetectionService.DetectAsync` | 28 | 34 | ChangeDetectionService.cs:50 |
| 9 | `AddressStandardizationService.ParseAddress` | 31 | 32 | AddressStandardizationService.cs:183 |
| 10 | `RedlandsDeliveryScenario.SelectSpreadNodes` (generator) | 18 | 32 | Services/Data/RedlandsDeliveryScenario.cs:155 |
| 11 | `ChangeDetectionService.BuildResult` | 19 | 31 | ChangeDetectionService.cs:586 |
| 12 | `FleetRoutingService.Validate` | 28 | 30 | FleetRoutingService.cs:143 |
| 13 | `ChangeDetectionService.DilateOnce` | 12 | 30 | ChangeDetectionService.cs:370 |
| 14 | `SyntheticChangeScene.PlantBlob` (generator) | 14 | 29 | Services/Data/SyntheticChangeScene.cs:195 |
| 15 | `SyntheticChangeScene.PlantDrawdown` (generator) | 13 | 29 | SyntheticChangeScene.cs:262 |
| 16 | `FleetRoutingService.BuildResult` | 17 | 26 | FleetRoutingService.cs:634 |
| 17 | `ChangeDetectionService.ErodeOnce` | 12 | 25 | ChangeDetectionService.cs:331 |
| 18 | `FleetRoutingService.TwoOptPass` | 10 | 25 | FleetRoutingService.cs:466 |
| 19 | `ViewshedService.ComputeManaged` | 10 | 25 | ViewshedService.cs:44 |
| 20 | `SpatialOverlayService.SpatialJoinAsync` | 18 | 23 | SpatialOverlayService.cs:23 |
| 21 | `AnonymousUserMiddleware.InvokeAsync` | 18 | 21 | Web/Middleware/AnonymousUserMiddleware.cs:35 |
| 22 | `OutageTraceService.EnergizedManaged` | 13 | 21 | OutageTraceService.cs:404 |
| 23 | `SpatialGraphService.FindShortestPathAStar` | 12 | 21 | SpatialGraphService.cs:283 |
| 24 | `OutageTraceService.IsolationManaged` | 11 | 21 | OutageTraceService.cs:348 |
| 25 | `FleetRoutingService.ExpandRoutePathsAsync` | 10 | 20 | FleetRoutingService.cs:580 |

Also above 15, from cx §2.3–2.4:
- `CatRiskService.ValidateLocations` (18/19)
- `OutageTraceService.ProposeRestorationAsync` (16/17)
- `SpatialGraphService.ValidateGraph` (16/14)
- `SpatialGraphService.ComputeDistanceMatrixAsync` (15/19)
- `ChangeDetectionService.OtsuManaged` (13/18)
- `VrpSolverNativeBridge.TrySolveCvrptw` (12/16)
- `SpatialClusterService.RunDbscanManaged` (8/16)
- `SpatialGraphService.FindShortestPathDijkstra` (10/17) and `ComputeDistances` (9/17)
- `UserProfileService.CreateOrUpdateFromGoogleAsync` (9/18)

**JavaScript** (cx §2.5; 33 files, 916 functions):
- Cyclomatic >15: 4. Cognitive >15: 5.
- Worst function: `StateExplorer/uiManager.js:26 extractMetadataPairs` (28/45).
- The real problem is huge enclosing closures: `StateExplorer/app.js:27 initialize` is 597 lines, and `Response/app.js:3` is a 592-line IIFE.

**C++** (cx §2.6): 19 functions have cognitive >15 and 9 have >25. The worst are `Change_LabelComponents` (91), `Facility_SolvePMedian` (74) and `orOptMove` (70). They mirror the managed fallbacks and don't ship.

### 1.5 LOC (cx §1.5; git-tracked; excludes bin/obj, `wwwroot/lib`, Migrations)
| Project | Lang | Files | Physical | Code* |
|---|---|---|---|---|
| Portfolio.Benchmarks | C# | 4 | 1,578 | 1,242 |
| Portfolio.Common | C# | 60 | 3,209 | 1,489 |
| Portfolio.Repositories | C# | 34 | 1,479 | 1,240 |
| Portfolio.Services | C# | 98 | 13,240 | 10,753 |
| Portfolio.Tests | C# | 50 | 12,194 | 9,047 |
| Portfolio.Web | C# | 72 | 4,424 | 3,371 |
| Portfolio.Web | cshtml | 52 | 6,514 | 5,827 |
| Portfolio.Web | JS | 33 | 9,808 | 7,739 |
| Portfolio.Web | CSS | 16 | 4,889 | 3,824 |
| native | C++ | 26 | 4,122 | 3,132 |
| **Total** | | **445** | **61,457** | **47,664** |

`RedlandsRoadNetwork.cs` is not in the totals; it has 5,765 lines and is generated. The largest JS files are HomeFinder/app.js (731 lines), StateExplorer/uiManager.js (669), StateExplorer/app.js (632) and Response/app.js (594).

### 1.6 HTML sinks (sec §0, §3; spot-recounted: 66 `innerHTML =` lines in 17 files)
- **Pattern matches:** 149 in 21 files of `wwwroot/js` (excluding `lib/`). Of these, **66 are `innerHTML` writes** in 16 files.
- **10 are UNSAFE:**
  - 2 exploitable: `BatchGeocoding/app.js` DataTables cells, and `PlantOperationsDashboard/shipments.js:49` inline `onclick`.
  - 2 mitigated by the ArcGIS sanitizer: the PlantOps and ReverseGeocoding popups.
  - 6 latent: Network tooltips ×2, the HomeFinder popup, aria-label and `showToast`, and the PlantOps `showToast`.
- **Escape helpers:** 10 local `escapeHtml` copies, 8 of them quote-blind.
- **Inline scripts and handlers:**
  - 9 JS-emitted inline event handlers.
  - 5 inline `<script>` blocks, 1 of them dead (`copyEmailContact`).
  - `style=` attributes: 44 in cshtml and 18 in JS strings.
- **Libraries:** Leaflet 1.9.4 is affected by CVE-2025-69993 (`bindTooltip` uses the same HTML path). There are 0 vulnerable NuGet packages.

### 1.7 Duplication hot spots (fe §2.6, cx §5, spot-recounted)
**JavaScript:**
| Duplicated code | Copies |
|---|---|
| `showAlert` (14 files; 12 share the same shape) | 14 |
| `setBusy` (each one disables the focused button) | 11 |
| `escapeHtml` | 10 |
| `announce` | 4 |
| SVG `appendText`/`appendLine` (byte-identical) | 4 |
| Toast implementations (2 override `window.showToast` using `innerHTML`) | 3 |
| Leaflet plus OSM setup | 3 |
| Inline Esri theme loader | 2 |

**CSS:**
- Twice: `.fade-in`, `.skill-tag`, the hero responsive rules, the html/body resets, the nav underline, the focus outline.
- Three times: modal theming (×3 with `!important`), `.project-hero`, `.btn-accent`/`.btn-outline-accent`, the dark DataTables rules.

**C#:**
| Duplicated code | Copies |
|---|---|
| Native-bridge static constructor plus the `IsNativeInvocationException` predicate | 12 identical (≈ −340 lines possible) |
| ArcGIS resilience pipelines | 4 (3 identical) |
| Controller `catch` clauses (27 `ArgumentException`, 12 `Exception`, 6 `BrokenCircuit`, 4 `InvalidOperation`, 3 `KeyNotFound`) | 52 |
| Private `IsFinite` helpers | 6, plus 14 inline checks |
| Protected-claim set | 3 (one unused) |
| Dijkstra and A* | about 85% shared code |

**Markup:** the 18 carousel cards are copied in full (28 KB), plus a divergent third copy on `/Projects`.

### 1.8 Hard-coded colours and tokens (fe §2.3–2.5)
- **Literals:** 387 colour literals in 29 files (277 in CSS, 110 in JS, 0 in cshtml); 35 of them are `var()` fallbacks.
- **Three competing status palettes:** Bootstrap ×28, Chakra/Open Color ×53, and accent-alpha `rgba(56,161,105,x)` ×29.
- **Missing tokens:** status colours, a chart palette, a focus ring, an input border, on-gradient whites, and the accent RGB triplet.
- **`!important` counts:** gis.css has 71 and batchgeocoding.css has 50.

### 1.9 Accessibility, live audit (fe §0, §3; 42 URLs × 360/768/1280 × light/dark)
| Check | Result |
|---|---|
| Horizontal page overflow | **0** failures |
| Focus visibility (Home) | 9 of 75 tab stops show a real ring (65 weak, 1 none); broken site-wide on `.btn` and `.btn-outline-accent` |
| Skip link | Broken: `navigation.js` swallows it |
| Focus after "Run" | Dropped to `<body>` on 11 demos |
| Status messages | 10 of 12 compute demos never announce results |
| Target size | Outage: 258–267 failing (5–11.5 px); toast close buttons 18×18 |
| Keyboard | Viewshed observer and Response isochrones are mouse-only; Outage has 267 tab stops in one SVG |
| Names | 3 unnamed ArcGIS surfaces; 7 label-in-name failures; 4 `aria-label`s on generic `div`s |
| Text contrast | **19 pairs fail AA** (14 measured live); 7 non-text groups below 3:1 |
| Headings | 15 of 17 demo pages have only an `h1` |
| Reduced motion | The carousel strands 15 of 18 cards; smooth scroll ignores the setting |

### 1.10 Performance (fe §2.1, §8)
- **Payload on every page:**
  - Vendor CSS 341,586 B raw, site CSS 51,050, vendor JS 257,847, site JS 35,606, fonts 276,904.
  - jQuery and DataTables (212,000 B raw) load on all 45 pages, but only 3 use them.
- **Loading:** no script is `defer`. 53 page stylesheet links have no `asp-append-version`.
- **Caching:** no `Cache-Control` on static files, locally or in production.
- **Dead weight:** 10.5 MB of unreferenced vendor files ship in the image. Typeahead (47 KB) loads but is never used.

### 1.11 Other baselines
- **Comments:** 2,618 comment units (404 banners, 1 commented-out block, 1 stale TODO, 758 AAA test markers). There are 1,046 XML doc blocks, of which 54 have multi-sentence summaries, 49 trivial `<param>` and 7 trivial `<returns>` (dt §A1).
- **OpenAPI** (dt §A5.3; re-checked from the fetched document `openapi-v1.json`, 325,283 B, 56 paths): **all 24 request bodies that have a description say "Cancellation token…"**.
- **Stale docs** (dt §C): copilot-instructions 50, agent-context 16, README 11, native/README 8, plans/README 9, other docs 10.
- **Security** (sec §0): 34 findings (2 CAT I, 7 CAT II, 25 CAT III, 2 of the CAT III accepted by default) plus 9 accepted or not-applicable rows.

---

## 2. Consolidated findings

Severity labels are the source agent's own:
- **SEC:** DISA CAT I/II/III.
- **FE:** P0–P3.
- **CX, DOC, TST, CI, DH:** P1–P4.

The findings files hold the evidence. **NEW-** IDs are planner findings verified at HEAD (Appendix A).

### 2.1 Goal 1: security (findings-security §1)
| ID | Sev | Summary | Evidence (file:line) | Fix | Risk | WP |
|---|---|---|---|---|---|---|
| SEC-01 | CAT I | Unbounded compute on un-rate-limited endpoints | ViewshedService.cs:116-124 (cells-only cap), :53-101 (per-row cancel); RasterTerrainService.cs:41-62; SpatialClusterService.cs:86-100; 7 compute controllers lack `[EnableRateLimiting]` | Work caps, DBSCAN dedup, inner-loop cancellation; "compute" tier plus global compute concurrency | Narrows inputs; the UI's maxima are far below the caps | 06, 14 |
| SEC-02 | CAT I | No body limit; 25 MB → +1.2 GB | AddressStandardizationController.cs:37,76; no Kestrel limits in Program.cs; no length cap in AddressStandardizationService.cs:99-112 | Kestrel 1 MB default, per-endpoint limits, field caps | Large saved geometry (NEW-03) | 14, 08 |
| SEC-03 | CAT II | Each cookieless HTML GET creates a profile plus 75 seeded rows | AnonymousUserMiddleware.cs:120 | Seed at sign-in; throttle new identities | — | 15 |
| SEC-04 | CAT II | Anonymous cookie assumes a Google-linked profile; sign-in overwrites `google_id` | AnonymousUserMiddleware.cs:75-99; UserProfileService.cs:169-201; UserProfileRepository.cs:118-134; Profile.cshtml:72 | Check for linked profiles in both places; hash logged IDs | Merge paths | 15 |
| SEC-05 | CAT II | CSV-borne XSS in Batch Geocoding results | BatchGeocoding/app.js:260-291 | `render.text()` on columns 1 and 3 | none | 07 |
| SEC-08 | CAT II | No script/style CSP | SecurityHeadersMiddleware.cs:35-36 | Nonce CSP, Report-Only first, then enforce | ArcGIS if enforced too early | 20, 51 |
| SEC-10 | CAT II | XFF trust open; rate-limit key is the right-most entry | Program.cs:234-241, 447-448 | Config-driven trust; concurrency limiter; owner check | A wrong CIDR merges all clients | 14, 52 |
| SEC-12 | CAT II | Framework messages leak; wrong status mapping | ApiExceptionMiddleware.cs:50-59, 69-72; SpatialGraphService.cs:245, 323 | Own-assembly check; map `BadHttpRequestException`; `HasStarted` guard; graph `TryGetValue` | Message text for framework errors | 04, 06 |
| SEC-25 | CAT II | Container runs as root; images unpinned | Dockerfile:4, 29, 37 | `USER $APP_UID`, chown, digests | Render file permissions | 09 |
| SEC-06 | CAT III | Stored self-XSS via shipment status; no server allowlist | shipments.js:49; FiberShipmentDto.cs; FiberShipmentService.cs:71-75 | Delegated listeners; status allowlist and lengths | 400 for unknown statuses | 07, 08 |
| SEC-07 | CAT III | Latent sinks; 10 escape helpers | Network/app.js:169,180; HomeFinder/app.js:340-356,402,668; dashboard.js:7-57,159-161; ReverseGeocoding/app.js:210-213 | `dom-safe.js` plus DOM building | Same files as FE work | 07, 38–41 |
| SEC-09 | CAT III | ArcGIS loaded without SRI | 4 ArcGIS `Index.cshtml` | `integrity` on `init.js` and the themes | Fails closed if Esri edits the files | 46 |
| SEC-11 | CAT III | `AllowedHosts: "*"`; Host header reflected | appsettings*.json:8-9; _Layout.cshtml:52 | Explicit host list | A typo takes that host down | 52 |
| SEC-13 | CAT III | Non-finite numbers bind | GeoStreamProcessorService.cs:24-63; RasterTerrainService; ViewshedService; ReverseGeocodingService.cs:42-46; CatRiskService | Finite and magnitude guards | — | 06 |
| SEC-14 | CAT III | Headers incomplete: HSTS 30 d, no Permissions-Policy/COOP/CORP, `Server` header, no API `no-store` | Program.cs:563-567; SecurityHeadersMiddleware | Add them | Low | 14, 20 |
| SEC-15 | CAT III | Oversized body → 500 plus Error log | ApiExceptionMiddleware.cs:58 | Map to 413 | none | 04 |
| SEC-16 | CAT III | Secrets, IDs and addresses in logs and messages | Program.cs:686, 765; Warning logs in 7 places; AddressStandardizationService.cs:123-125,175; BatchGeocodingService.cs:237 | Drop or hash | none | 14, 08, 15, 36 |
| SEC-17 | CAT III | Data Protection keys unencrypted at rest | Program.cs:516-547 | Accept (default) | — | — |
| SEC-18 | CAT III | No absolute session lifetime; 1-year anonymous cookie | Program.cs:300-301 | 24 h absolute (D-11) | Daily re-auth | 35 |
| SEC-19 | CAT III | Session registered but unused | Program.cs:264-272, 582; Logout.cshtml.cs:14 | Keep (pipeline constraint) unless D-11 | — | 35 (option) |
| SEC-20 | CAT III, accepted | OpenAPI/Scalar public in production | Program.cs:596-598 | Keep | — | — |
| SEC-21 | CAT III, accepted | API CSRF relies on SameSite plus JSON | Program.cs:299 | Keep | — | — |
| SEC-22 | CAT III | Missing length and format validation on persistence | UserClaimMap.cs:19-20; SavedFeatureMap.cs:15-18; CollectionService.cs:70,101; FiberShipmentDto.cs; SaveSearchRequest | Service caps | 400s on abusive input | 08, 15 |
| SEC-23 | CAT III | `layerId` dot-segment traversal; no "expensive" limit | ArcGisService.cs:39-40; FeaturesController.cs:40-47 | Regex `^\d{1,3}$`, bbox check, limit | Non-numeric → 400 | 14 |
| SEC-24 | CAT III | Batch jobs: no global cap, no TTL | BatchGeocodingService.cs:153-202, 293; InMemoryBatchJobStore.cs | Bounded queue, TTL, summary log | 503 when full | 36 |
| SEC-26 | CAT III | Kubernetes manifests lack hardening and NetworkPolicy | k8s/*.yaml | securityContext, policies, PSA | none (not used in production) | 09 |
| SEC-27 | CAT III | Compose publishes Redis on all interfaces | docker-compose.yml:21-23 | Drop or bind to localhost | none | 09 |
| SEC-28 | CAT III | CI actions on mutable tags; no Dependabot | ci.yml:44… | SHA pins, Dependabot, scheduled scan | none | 10 |
| SEC-29 | CAT III | Microsoft.* 10.0.10; dead libraries | Directory.Packages.props; typeahead; jquery-validation | Bump; delete | none | 19, 18, 45 |
| SEC-30 | CAT III | Native ABI hardening | viewshed_kernel.cpp:25-41 and others | Defer; managed validation is the control | ABI change | 53 (deferred) |
| SEC-31 | CAT III | Committed dev password; `DATABASE_URL` fallback is dead | appsettings.json:11; appsettings.Production.json:11; Program.cs:503-505 | `IsNullOrWhiteSpace` chain; empty default | Fresh-clone convenience | 14 |
| SEC-32 | CAT III | `/health/ready` is a DB round trip, unthrottled | Program.cs:610-613 | 10 s cached result, opt-out kept | — | 14 |
| SEC-33 | CAT III | Public-cached GETs can carry `Set-Cookie` | 4 controllers `[ResponseCache]` | No identity creation on `/api` | — | 15 |
| SEC-34 | CAT III | No exhaustive auth/limit guard | convention only | Reflection tests | none | 01, 14 |
| NEW-01 | CAT III | `DELETE /api/v1/users/me/claims/{type}` can delete protected claims (`google_id`, `email`) | UserProfileController.cs, grep `[HttpDelete("me/claims/{type}")]` | 400 for `ProfileClaimTypes.Protected` | New 400 | 14 |
| NEW-06 | CAT III | A cookie carrying an unknown GUID creates a profile for any `Accept` header | AnonymousUserMiddleware.cs:75-88 | Same per-IP throttle | — | 15 |

Accepted or not-applicable rows (open redirect, OAuth/PKCE, cookie flags, CORS, antiforgery, IDOR, SQL injection, HTTP→HTTPS, secrets in history) are in findings-security §1.

### 2.2 Goal 2: complexity (findings-complexity §2–§3, §7)
| ID | Pri | Summary | Evidence | Fix | Risk | WP |
|---|---|---|---|---|---|---|
| CX-06 | P2 | 1,444-line seed method (251/236); re-inserts 40 legacy listings (OD-3) | UserProfileSeedService.cs:21, :1320-1440 | Data file plus loops; drop legacy block (D-07) | Transcription errors (golden guard) | 25 |
| CX-04 | P2 | `Program.cs` `<Main>$` 61/50 | Program.cs:31-813 | `Startup/*` extensions | Order drift | 21 |
| CX-09 | P2 | ChangeDetection kernels (83/34/31/30/25/18) | ChangeDetectionService.cs:50-661 | `ManagedChangeKernels` extract | Parity (golden + native) | 28 |
| CX-07 | P2 | Fleet solver mirror (70/43/30/26/25/20) | FleetRoutingService.cs:143-734 | `Fleet/*` extract-method | Iteration order | 26 |
| CX-08 | P2 | Response p-median mirror (47) | ResponseCoverageService.cs:277-402 | `ManagedPMedianSolver` | Tie-breaks | 27 |
| CX-10 | P2 | Dijkstra and A* about 85% the same; haversine ×2; node-ID set ×4 | SpatialGraphService.cs:210-362 | One search with a heuristic | Bit-identity | 29 |
| CX-12 | P3 | ParseAddress 31/32 and 8 other members above 15 | cx §3.7 | Extract-method | Message text | 30, 31, 32 |
| CX-05 | P2 | `AnonymousUserMiddleware.InvokeAsync` 18/21 | :35-143 | Extract methods | — | 33 |
| CX-13 | P3 | 4 data generators above 15; their output is shipped data | cx §3.7 | Permanent suppressions | — | 50 |
| CX-16 | P2 | No complexity gate | — | CA1502 at 15 | — | 50 |
| JS-CX | P3 | Huge closures (597- and 592-line) | StateExplorer/app.js:27; Response/app.js:3 | Split files (optional) | Script order | 44 |
| CPP-CX | — | C++ mirrors (91/74/70) | native/*.cpp | Unchanged (OD-9) | — | — |

### 2.3 Goal 3: refactor and simplify (findings-complexity §5–§6)
| ID | Pri | Summary | Evidence | Fix | Risk | WP |
|---|---|---|---|---|---|---|
| CX-03 | P1 | Validated dead-code batch (40 files, −409 lines) | cx §6.2–6.6 | Delete; `double.IsFinite`; one protected set | Low (compile-checked) | 01, 14, 19, 21, 22, 23 |
| CX-11 | P2 | 12 identical bridge constructors and predicates | Native/*Bridge.cs | `NativeKernelLoader` | `beforefieldinit` timing | 30 |
| CX-15 | P2 | 12 Fiber `catch (Exception)` turn 404/409 into **500** (latent bug) | FiberMaterialsController ReceiveStock; FiberOrdersController.Update (declares 404, returns 500) | Delete; the middleware maps (D-04) | Status and body changes | 34 |
| CX-14 | P3 | 52 controller catches | cx §5.2 | Action-level filters (D-17) | ~35 tests rewritten | 43 |
| §5.1 | P2 | 4 resilience pipelines (1 distinct) | Program.cs:98-203 | Helper plus 2 profiles | Attempt counts (tests) | 21 |
| §6.5 | P2 | 3 shadowed `AddScoped`; `AddMemoryCache` unused | Program.cs:129, 154, 179, 262 | Delete (with snapshot and boot proof) | — | 21 |
| CX-18 | P3 | Batch sync endpoint is **live** but marked `[Obsolete]` "for tests"; legacy aliases | BatchGeocodingController.cs:116-120; HomeFinderController.cs:49-83 | D-06: keep; drop `[Obsolete]` from the live endpoint | Public routes | 36 |
| CX-19 | P3 | `DefaultNoiseLevel` unused while a literal duplicates it; `retryAfter` duplicates the break duration | ChangeDetectionController.cs:48; cx §5.1 | Use the constants | — | 28, 43 |
| CX-21 | P3 | `Portfolio.Web.csproj.user` is tracked | git | Untrack; ignore `*.user` | — | 09 |
| CX-20 | — | JS god-file splits | cx §3.8 | After the FE batches | — | 44 |

### 2.4 Goal 4: comments and XML docs (findings-docs-tests §A)
| ID | Pri | Summary | Evidence | Fix | Risk | WP |
|---|---|---|---|---|---|---|
| DOC-01 | P1 | 24/24 OpenAPI request bodies read "Cancellation token" | 42 controller `<param name="cancellationToken\|ct">` lines | Delete them; NoWarn 1573 | Docs only | 23 |
| DOC-02 | P1 | 9 comments that are now wrong | Program.cs:261, 305-314, 397-402, 494, 500; AnonymousUserMiddleware.cs:13; UserProfileService.cs:14; SpatialGraphService.cs:443-446; site.css:7 | Rewrite | — | 06, 12, 15, 21 |
| DOC-03 | P2 | Encoding defects (4 files, 54 mojibake lines) | Property.cs:41; Profile.cshtml.cs:72; mapManager.js:32; navigation.js:21; 3 CSS files | Re-save as UTF-8 | — | 17, 40, 48 |
| DOC-04 | P2 | The `IsNativeInvocationException` wart has no source comment; seed InMemory "why" missing | 12 bridges; UserProfileSeedService.cs:27 | Add the comments | — | 30, 25 |
| DOC-05 | P3 | 1 commented-out block; 1 stale TODO | inventory.js:15-19; Projects/Details.cshtml.cs:27 | Delete | — | 41, 22 |
| DOC-06 | P3 | 54 multi-sentence summaries; 14 with embedded CR/LF; trivial params and returns | dt §A5.4-5 | Rules R5-R7 | OpenAPI text | 48 |
| DOC-07 | P3 | 404 banners, about 460 restating comments, 39 `//` member descriptions, 150 shipped HTML comments | dt §A1 | Rules R1-R4, R8-R10 | High conflict, so it runs last | 48 |
| DOC-08 | P3 | `#region` | ProfileController.cs:36 | Remove | — | 22 |

The keep-list (52 entries, owner can veto) is findings-docs-tests §A7. The trimming rules for implementers are §A8.

### 2.5 Goal 5: frontend (findings-frontend §4–§8)
| ID | Pri | Summary | Evidence | WP |
|---|---|---|---|---|
| FE-A01 | P0 | Focus rings removed by Bootstrap; `.btn-outline-accent` gets *worse* on focus | preferences.css:67-71 vs bootstrap `.btn:focus-visible{outline:0}`; site.css:20-48 | 12, 17, 18 |
| FE-A02 | P0 | Skip link and in-page anchors don't move focus; smooth scroll ignores reduced motion | navigation.js:16-38 | 17 |
| FE-A03 | P0 | Carousel: focus off-screen, no pause, reduced motion strands cards, 12 px seam | layout.css:317-368 | 17 |
| FE-C01 | — | 18 carousel cards duplicated in source (28 KB); `/Projects` copy diverges | Index.cshtml:105-865 | 17, 42 |
| FE-A04 | P1 | "Run" drops focus to `<body>`; results not announced | 11 × `setBusy` | 16, 38–41 |
| FE-A05 | P1 | Viewshed observer is mouse-only | Viewshed/app.js:118-146 | 38 |
| FE-A06 | P1 | Outage: 267 tab stops; 5–11 px targets | Outage/app.js:331-357 | 39 |
| FE-A07 | P1 | State Explorer redirects anonymous visitors to /Login (D-09f) | api-fetch.js:36-43 | 16, 40 |
| FE-A08 | P1 | Esri theme CSS missing on 2 pages; nested, unnamed `role=application` | StateExplorer and ReverseGeocoding `Index.cshtml` | 16, 40, 41 |
| FE-A09 | P1 | 19 text-contrast failures (T1–T19) | fe §4 FE-A09 table | 12, 17, 18, 40, 41, 42 |
| FE-A10 | P1 | 7 non-text contrast groups | fe §4 FE-A10 | 12, 16, 18, 38, 39, 40 |
| FE-A11 | P1 | HomeFinder slider names replaced with internal keys; assertive spam | HomeFinder/app.js:222-226; Index.cshtml:66 | 40 |
| FE-D01 | P1 | Token gaps | base.css:4-50 | 12 |
| FE-D03 | P1 | Button system redefined per page | site.css; reversegeocoding.css; addressstandardization.css | 12, 40 |
| FE-P01 | P1 | No static `Cache-Control` | Program.cs:575 | 37 |
| FE-P02 | P1 | 53 page CSS links unversioned and placed in `<body>` | page `.cshtml` files | 24, 38–42 |
| FE-P03 | P1 | jQuery and DataTables on every page | _Layout.cshtml:85-86, 172-174 | 24 |
| FE-J01 | P1 | One shared `ui`/`format`/`charts`/`leaflet`/`esri` module set | fe §6 | 16 |
| FE-A12…A16 | P2/P3 | Naming, headings, toasts, reduced motion, scroll regions | fe §4 | 07, 12, 16, 24, 38–42 |
| FE-D02, D04–D10 | P2 | Hard-coded literals; panel, KPI, legend, header and state patterns; formatting; dark-mode overrides | fe §5 | 12, 16, 18, 24, 38–42 |
| FE-D11 | P3 | Dark map tiles (option) | — | not done (default) |
| FE-D12 | P3 | Encoding and dead code (PlantOps unclosed `<div>`: 63 open / 62 close, verified) | PlantOps Index.cshtml:50 | 17, 40, 41, 45 |
| FE-D13 | P3 | SIMD copy; Details section count | Index.cshtml:283, 660 | 11, 49 |
| FE-P04…P08 | P2/P3 | Typeahead; `defer`; FA weight; 10.5 MB dead vendor files; home HTML weight | fe §8 | 18, 24, 45 |

### 2.6 Goal 6: tests and CI (findings-docs-tests §B; findings-complexity §4.3, §7)
| ID | Pri | Summary | WP |
|---|---|---|---|
| CX-01/TST-06 | P1 | No host-level tests; `Program.cs` 0% (WebApplicationFactory proven in scratch) | 01 |
| TST-01 | P1 | 3 middlewares at 0%; `HomeSearchPreferencesDto.Validate` 0/19 | 02, 04 |
| TST-02 | P1 | 3 controllers untested; no test asserts the `{ error }` body | 02 |
| TST-03 | P1 | No route-table guard | 01 |
| TST-04/07/09 | P1/P2 | No golden outputs for the services being split | 03 |
| TST-05/CX-02 | P1 | `Program.cs` helpers can't be tested in place | 05 |
| TST-08/CI-05 | P2 | Native-gated tests pass vacuously; the CI native job never asserts the libraries loaded | 10 |
| TST-10 | P3 | 3 test classes mix SUTs | 47 |
| TST-11 | P3 | Repository tests (D2) | §6 |
| CI-01 | P2 | No format gate; format fails today | 50 |
| CI-02/03 | P2 | No Dependabot; no dependency review | 10 |
| CI-04 | P2 | No CodeQL | D-20 (owner setting) |
| CI-06 | P3 | Gate reads `reports[0]` from a multi-match glob; no per-assembly floor; ratchets never raised | 10, 50 |
| CI-07/08 | P3 | Native coverage union; C++ warnings | §6 |
| CX-16/17 | P2/P4 | CA1502 gate; IDE0005 sweep (42 hits) | 50 |
| NEW-05 | — | `pull_request: branches: [main]` means stacked PRs get no CI | 10 |

### 2.7 Goal 7: documentation hygiene (findings-docs-tests §C)
| ID | Pri | Summary | WP |
|---|---|---|---|
| DH-01 | P1 | copilot-instructions.md has 50 stale statements, incl. "bounding-box vertex clamp" (L783-784, verified) | 49 |
| DH-02 | P1 | Withdrawn figures on 5 Details pages; SIMD/AVX wording | 11 |
| DH-03 | P1 | native/README.md contradicts its own table ("wins 8, loses 8, ties 5" → 9/5/7) | 11 |
| NEW-07 | P1 | Terrain/Details.cshtml:76 "AVX2-friendly compiler flags" is false: raster_terrain_kernel/CMakeLists.txt sets `/O2 /fp:fast` only | 11 |
| NEW-08 | P1 | HomeFinder/Details.cshtml:98 "amortizes … across AVX2 vector lanes" (measured 0.99×) | 11 |
| DH-09 | P1 | agent-context.md has 16 inaccuracies | 00, 49 |
| DH-04/05/06/07 | P2 | README (680 tests, 5 projects, `portfolio_v2` ×3, port); render-deployment; plans README; css README | 49 |
| DH-08 | P2 | resume-update.md: "high-performance", "role-based", counts (résumé wording is the owner's) | 49 |
| ADD-* | — | ADD-01→19, 02→01, 03→36, 04→10, 05/06→21, 07→14, 08→48, 09→49, 10→§6, 11→00, 12→23, 13→03 | as listed |

---

## 3. Work packages

### 3.1 How Stage 2 runs a WP
1. **Branching.**
   - Each WP is developed in its own worktree (`isolation: "worktree"`), cut from its wave base.
   - **W1 bases on `main`.** The base for wave N+1 is `cleanup/wave-N`: the previous base plus every wave-N WP branch merged with `--no-ff`. The orchestrator creates and pushes it. It is never `main`.
2. **PRs.**
   - Each WP branch opens a PR against the base it was cut from (W1 → `main`), so each PR shows only its own diff.
   - WP-10 adds `cleanup/**` to the `pull_request` filter so these PRs get CI.
   - Review fixes are **merged** down the stack. Never rebase or force-push a pushed branch.
   - The owner merges bottom-up, with merge commits rather than squash. After merging, retarget the next PRs to `main`.
3. **Per WP:** implement, run the §5 gates, self-review, independent review, then open the PR. The PR description lists:
   - the WP ID;
   - the findings it closes;
   - the decisions it applied;
   - any behaviour changes;
   - verification output;
   - complexity before and after, for C# WPs.

### 3.2 How the cross-agent conflicts are resolved
| Conflict | Resolution | Why |
|---|---|---|
| **`Program.cs`**: security (limits, forwarded headers, HSTS, health, connection strings), complexity (extract helpers, decompose, dead registrations), FE-P01 (static caching), docs (wrong comments) | One `Program.cs` owner per wave, in this order: **WP-05** helpers (W1) → **WP-14** security edits (W2) → **WP-21** decomposition plus dead registrations plus comment fixes (W3). After that, **WP-35** (cookie events), **WP-36** (worker registration) and **WP-37** (static caching) edit different `Startup/*` files in W4. | Security first, as both agents agree. The decomposition carries the hardened code. Extracting the helpers first gives WP-14 a testable place to fix the leak. |
| **Session removal** (SEC-19 vs OD-10) | Keep `UseSession` by default (D-11); SEC-19 is accepted | It is a hard constraint, and SEC-19 is low value |
| **JS: `escapeHtml`/innerHTML (WP-07)** vs shared `ui.js` (FE) vs god-file splits (CX-20) | `dom-safe.js` (`PortfolioDom`) is the **only** escaper; `PortfolioUi` delegates to it. WP-07 (W1) fixes only the 10 unsafe sinks and the 3 escapers in those files. The 7 text-only escapers are replaced in the FE page batches (W4). Splits come last and are optional (WP-44). | A small security surface, reviewed early. No file is touched twice in the same wave. |
| **Inline Esri loaders**: nonce them (security) vs move them to `esri-theme.js` (FE) | Move them (WP-16 adds the file; WP-40 and WP-41 adopt it). Until then, the nonce tag helper (WP-20) covers them. SRI goes into `esri-theme.js` (WP-46). | This also fixes FE-A08 |
| **Controller catches vs `ApiExceptionMiddleware`** | Keep the `{ error }` 400 shape (the JS reads only `body.error`). Delete only the Fiber catch-alls, as a bug fix (D-04a). Filters are optional and come last (WP-43, D-17). `getApiError` also reads `detail` once WP-04 sanitizes it (WP-16). | Behaviour-preserving |
| **Graph indexers**: skip (SEC-WP1) vs preserve the 404 (CX-10) | **Skip** undeclared edges (WP-06); CX-10 preserves the new semantics | Verified: native CSR drops them (`spatial_graph_engine.cpp` `if (from < 0 \|\| to < 0) continue;`) and `ComputeDistances` skips them. Today managed throws: a parity gap. |
| **Rate-limit attribute**: "expensive" (agent-context §9, dt B4.6) vs a new "compute" tier (sec) | "compute", 60/min (D-02a); agent-context is updated in WP-49 | Viewshed runs on every cell click (verified `onCellClick → run()`). 15/min would break the UX. |
| **Two factory designs** (sec `PortfolioWebFactory` with an unreachable DB; cx `PortfolioAppFactory` with an InMemory swap) | One `PortfolioAppFactory` with the InMemory swap (WP-01) | Superset: HTML/profile paths testable |
| **TST-06 `public partial class Program`** | Not added | `Program` is already public in .NET 10. The probe clone changed only package files (verified). |
| **SEC-WP4 Properties characterization** | Replaced (WP-15 asserts no seed rows); Properties go to D-07/WP-25 | Verified false: the legacy streets are absent from `RedlandsPropertySeedData` |
| **`/health/ready`**: remove the opt-out (sec) vs documented opt-out (agent-context) | Keep the opt-out and cache the result for 10 s | Same protection; the Render probe path is unknown |
| **Vulnerability check**: redundant (dt) vs scheduled (sec) | Scheduled weekly workflow only | NuGetAudit already gates PRs; a schedule catches new advisories |
| **CodeQL**: workflow (dt) vs default setup (sec) | Default setup (owner toggle, D-20); workflow is the fallback | No file to maintain |
| **Coverage depends on the generated file** | Never exclude it in this cleanup (OD-7). Raise the ratchets at the end to floor(measured)−1, never below 80/60. Add a Web floor (WP-50). | Excluding it now would fail the gate at 73% |
| **CA1502 baseline suppressions vs parallel refactors** | Measure per WP with `tools/metrics`; add the gate **last** (WP-50) with only the residual suppressions | Avoids shared-file churn in a `GlobalSuppressions.cs` |
| **`copyEmailContact`, typeahead, toast overrides** claimed by 2–3 agents | Single owners: WP-17 (`copyEmailContact`), WP-18 (typeahead), WP-07 (toast overrides) | No duplicate edits |
| **Comment trimming vs everything** | Comments only in W7 (WP-48 C1–C12, after the code). Wrong comments (DOC-02) are fixed inside the code WP that touches the file. | Lowest conflict |
| **Response `maxIterations ≤ 100`** (sec D2) | Dropped | The service already clamps (`Math.Min(request.MaxIterations, MaxIterations)`); the UI sends 50; the concurrency limiter bounds CPU |

### 3.3 Wave schedule and PR map
| Wave | WP (size) | PR branch | Depends on |
|---|---|---|---|
| W0 | WP-00 Plan, orientation, artifacts (S) | `cleanup/plan` | D-19 |
| W1 | WP-01 Test host and pipeline characterization (L) | `cleanup/test-host` | D-01 |
| | WP-02 Unit characterization (M) | `cleanup/test-units` | — |
| | WP-03 Golden outputs (M) | `cleanup/test-golden` | — |
| | WP-04 Exception middleware hygiene (S) | `cleanup/sec-errors` | D-04 |
| | WP-05 Connection-string helpers (S) | `cleanup/web-connection-strings` | — |
| | WP-06 Compute caps and finite validation (M) | `cleanup/sec-compute-caps` | D-02, D-04 |
| | WP-07 Client XSS and `dom-safe.js` (M) | `cleanup/sec-xss` | D-16 (PlantOps verification) |
| | WP-08 Persistence validation and log redaction (M) | `cleanup/sec-persistence` | D-02 |
| | WP-09 Containers and k8s (M) | `cleanup/sec-containers` | — |
| | WP-10 CI supply chain and native-run honesty (S–M) | `cleanup/ci-supply-chain` | — |
| | WP-11 Honest claims (S) | `cleanup/honest-claims` | D-08 |
| | WP-12 Core CSS and tokens (M) | `cleanup/fe-core-css` | D-09 |
| | WP-13 Durable tools (S) | `cleanup/tools` | D-14, WP-00 step 1 |
| W2 | WP-14 Request limits and config hardening (L) | `cleanup/sec-pipeline` | 01, 04, 05, 08 |
| | WP-15 Identity separation (M) | `cleanup/sec-identity` | 01, 02, 08; D-05 |
| | WP-16 Shared JS modules (M) | `cleanup/fe-shared-js` | 04, 07 |
| | WP-17 Home page (M) | `cleanup/fe-home` | 11, 12 |
| | WP-18 Family stylesheets and typeahead (M) | `cleanup/fe-family-css` | 12 |
| | WP-19 NuGet patch updates (S) | `cleanup/deps` | 01 |
| W3 | WP-20 Headers and CSP Report-Only (M) | `cleanup/sec-csp-report-only` | 02, 14; D-03 |
| | WP-21 `Program.cs` decomposition (L) | `cleanup/web-startup` | 01, 05, 13, 14 |
| | WP-22 Dead-code batch (M) | `cleanup/dead-code` | 01, 06, 08, 14 |
| | WP-23 OpenAPI request-body fix (S) | `cleanup/openapi-docs` | 01, 14 |
| | WP-24 Asset loading and layout (S–M) | `cleanup/fe-loading` | 07, 16 |
| W4 | WP-25 Data-driven seed (L) | `cleanup/refactor-seed` | 03, 15; D-07 |
| | WP-26 Fleet split (M) | `cleanup/refactor-fleet` | 03, 22 |
| | WP-27 Response split (M) | `cleanup/refactor-response` | 03, 22 |
| | WP-28 ChangeDetection split (M) | `cleanup/refactor-change` | 03, 22, 23 |
| | WP-29 SpatialGraph simplification (M) | `cleanup/refactor-graph` | 03, 06, 22 |
| | WP-30 `NativeKernelLoader` (M) | `cleanup/refactor-native-loader` | 10 |
| | WP-31 ParseAddress (M) | `cleanup/refactor-address` | 03, 08, 22 |
| | WP-32 Remaining members above 15 (M) | `cleanup/refactor-members` | 06, 15, 22 |
| | WP-33 Middleware extraction (S) | `cleanup/refactor-members` (same PR as 32) | 15 |
| | WP-34 Fiber error handling (S) | `cleanup/fiber-errors` | 01, 04, 14; D-04 |
| | WP-35 Session lifetime (S) | `cleanup/sec-session` | 21; D-11 |
| | WP-36 Batch-geocoding bounds (M) | `cleanup/sec-batch-jobs` | 21, 23; D-06, D-10 |
| | WP-37 Static-asset caching (S) | `cleanup/static-caching` | 21 |
| | WP-38 FE spatial-1 (M) | `cleanup/fe-spatial-1` | 12, 16, 18, 24 |
| | WP-39 FE spatial-2 (L) | `cleanup/fe-spatial-2` | 12, 16, 18, 24 |
| | WP-40 FE map/form pages (L) | `cleanup/fe-map-form` | 07, 12, 16, 18, 24 |
| | WP-41 FE Network and PlantOps (M) | `cleanup/fe-network-plantops` | 07, 12, 16, 18, 24; D-16 |
| | WP-42 FE site and Details (M) | `cleanup/fe-site-details` | 11, 12, 18, 24 |
| W5 | WP-43 Controller filters, optional (M) | `cleanup/controller-filters` | 02, 21, 23, 34, 36; D-17 |
| | WP-44 JS god-file splits, optional (M) | `cleanup/js-splits` | 38–41 |
| | WP-45 Dead vendor assets and encoding sweep (S) | `cleanup/fe-vendor-assets` | 38–42 |
| | WP-46 ArcGIS SRI (S) | `cleanup/sec-sri` | 20, 40, 41; D-15 |
| W6 | WP-47 Test-class structure, optional (M) | `cleanup/test-structure` | all W4–W5 test edits |
| W7 | WP-48 Comment/XML trim C1–C12 (L, 5 PRs) | `cleanup/comments-{backend,api,tests,frontend,native}` | every code WP for that folder |
| | WP-49 Docs hygiene (M) | `cleanup/docs` | all code WPs |
| W8 | WP-50 Quality gates: CA1502, IDE0005, format, ratchets (M) | `cleanup/quality-gates` | everything; D-13 |
| Gated | WP-51 Enforce CSP (S) | `cleanup/sec-csp-enforce` | 20, 07, 40, 41, 1–2 week production soak, owner sign-off |
| | WP-52 Host filtering and XFF trust (S) | `cleanup/sec-hosts` | 14, owner check §4.5, D-12 |
| | WP-53 Native ABI hardening (L) | `cleanup/native-abi` | deferred (default) |

PR count: 55 with this mapping.
- W0–W6: 44 PRs (WP-32 and WP-33 share one).
- W7: 6 (5 comment PRs plus docs).
- W8: 1.
- Gated: 3 (WP-51, WP-52, WP-53; WP-53 only if its decision is reversed).

The critical path is WP-01 → WP-14 → WP-21 → W4 → WP-43 → WP-47 → WP-48 → WP-50. W4 may be sub-batched. Frontend verification is serialized because there is one browser pane.

### 3.3a Approved PR map (owner, 2026-10-12: about 15 PRs; supersedes the PR column above)
Each WP still runs as its own worktree, on its own local branch `cleanup/wp-NN-<slug>`, and lands as its own commit(s) prefixed `WP-NN`. The orchestrator merges the WP branches of one group into the group's PR branch with `--no-ff`. It then runs the §5 gates on the combined branch, has the group reviewed, and pushes. The wave base for wave N+1 is `cleanup/wave-N`: the previous base plus that wave's PR branches.

| PR | Branch | Wave | WPs | Base |
|---|---|---|---|---|
| 1 | `cleanup/plan` | W0 | 00 | `main` |
| 2 | `cleanup/w1-tests` | W1 | 01, 02, 03 | `main` |
| 3 | `cleanup/w1-security` | W1 | 04, 05, 06, 07, 08 | `main` |
| 4 | `cleanup/w1-delivery` | W1 | 09, 10, 13 | `main` |
| 5 | `cleanup/w1-site` | W1 | 11, 12 | `main` |
| 6 | `cleanup/w2-security` | W2 | 14, 15, 19 | `cleanup/wave-1` |
| 7 | `cleanup/w2-frontend` | W2 | 16, 17, 18 | `cleanup/wave-1` |
| 8 | `cleanup/w3-startup` | W3 | 20, 21, 23 | `cleanup/wave-2` |
| 9 | `cleanup/w3-cleanup` | W3 | 22, 24 | `cleanup/wave-2` |
| 10 | `cleanup/w4-refactor-data` | W4 | 25, 29, 30, 31 | `cleanup/wave-3` |
| 11 | `cleanup/w4-refactor-solvers` | W4 | 26, 27, 28, 32, 33 | `cleanup/wave-3` |
| 12 | `cleanup/w4-backend-hardening` | W4 | 34, 35, 36, 37 | `cleanup/wave-3` |
| 13 | `cleanup/w4-frontend-pages` | W4 | 38, 39, 40, 41, 42 | `cleanup/wave-3` |
| 14 | `cleanup/w5-finish` | W5–W6 | 43, 44, 45, 46, 47 | `cleanup/wave-4` |
| 15 | `cleanup/w7-comments-docs` | W7 | 48, 49 | `cleanup/wave-6` |
| 16 | `cleanup/w8-quality-gates` | W8 | 50 | `cleanup/wave-7` |
| — | separate, when unblocked | gated | 51, 52, 53 | latest wave |

### 3.4 File-conflict matrix (at most one WP per file per wave)
| File / area | W1 | W2 | W3 | W4 | W5 | W6 | W7 | W8 |
|---|---|---|---|---|---|---|---|---|
| `Portfolio.Web/Program.cs` | 05 | 14 | 21 | — | — | — | 48 (C3) | 50 |
| `Portfolio.Web/Startup/*` (new in WP-21) | | | 21 | 35 (auth/cookie events), 36 (service registration), 37 (pipeline) | | | 48 | 50 |
| `Infrastructure/ConnectionStrings.cs` | 05 | 14 | | | | | 48 | |
| `appsettings*.json` | | 14 | | | | | | (52 gated) |
| `Middleware/ApiExceptionMiddleware.cs` | 04 | | | | | | 48 | |
| `Middleware/SecurityHeadersMiddleware.cs` | | | 20 | | | | 48 | (51 gated) |
| `Middleware/AnonymousUserMiddleware.cs` | | 15 | | 33 | | | 48 | |
| `Controllers/Api/*` | | 14 (17 files) | 22 (Profile, FiberShipments), 23 (19 files, XML only) | 28 (ChangeDetection), 34 (3 Fiber), 36 (BatchGeocoding) | 43 | | 48 (C2) | 50 |
| Services: Viewshed, RasterTerrain, SpatialCluster, GeoStream, CatRisk, ReverseGeocoding | 06 | | 22 (`IsFinite`) | 32 (CatRisk, Viewshed, Cluster) | | | 48 | 50 |
| `SpatialGraphService.cs` | 06 | | 22 | 29 | | | 48 | |
| Fleet, Response, ChangeDetection services | | | 22 | 26, 27, 28 | | | 48 | |
| `AddressStandardizationService.cs` | 08 | | 22 | 31 | | | 48 | |
| FiberShipment, SavedFeature, Collection, SavedSearch services | 08 | | 22 (FiberShipment) | | | | 48 | |
| `UserProfileService.cs`, `UserProfileRepository.cs` | | 15 | | 32 (service) | | | 48 | |
| `UserProfileSeedService.cs` | | | | 25 | | | 48 | |
| `FiberOrderService.cs`; FiberClient repository/DTO | | | 21 | | | | 48 | |
| BatchGeocoding service and job store | | | | 36 | | | 48 | |
| `Services/Native/*Bridge.cs` | | | | 30 | | | 48 (C5) | (53) |
| Repository interfaces and implementations | 08 (Collection, SavedFeature logs) | 15 (UserProfile) | 21 (FiberClient), 22 (unused members) | | | | 48 (C6) | |
| `Directory.Packages.props` | 01 | 19 | | | | | | |
| `Portfolio.Tests.csproj` | 01 | | 22 (global usings) | | | | | 50? |
| `Portfolio.Repositories.csproj` | 01 | | | | | | | |
| `Portfolio.Web.csproj` | | | 23 | | | | | |
| `Tests/Controllers/*` | 02 | 14 (Profile, UserProfile, Features) | 22 (FiberShipments, Profile) | 34 | 43 | 47 | 48 (C7) | |
| `Tests/Integration/*`, `Tests/Contracts/*` | 01 | 14 (extend), 15 (new file) | 20 (new), 21 (snapshot), 23 (snapshot) | 34, 36, 37 (new files) | 43 | 47 | 48 | |
| `Tests/Middleware/*` | 02, 04 (different files) | 15 | 20 | | | | 48 | |
| `Tests/Services/*` | 06, 08, 10 (different files) | 15 | 21, 22 | 25–32 (new unit files; golden unchanged) | | 47 | 48 | |
| `Tests/Golden/*` | 03 | | | 25 (Properties assertion, per D-07) | | | 48 | |
| `Pages/Shared/_Layout.cshtml` | 07 | | 24 | | | | 48 (C9) | |
| `Pages/Index.cshtml` | 11 (2 lines) | 17 | | | | | 48 | |
| `Projects/*/Details.cshtml` | 11 | | | 42 | | | 48 | |
| `Projects/StateExplorer/Index.cshtml` | | 18 (typeahead tags) | | 40 | 46 | | 48 | |
| `Projects/{AddressStd, Batch, PlantOps}/Index.cshtml` | | | 24 | 40, 41 | 46 (PlantOps) | | 48 | |
| `_ViewImports` ×2, `TagHelpers/`, `CspReportController` | | | 20 | | | | 48 | |
| `js/api-fetch.js`, `js/toasts.js` | | 16 | | | | | 48 (C10) | |
| `js/<page>/*.js` | 07 (6 page sets) | | | 38–41 | 44 | | 48 | |
| `js/navigation.js` | | 17 | | | | | 48 | |
| `css/{base, preferences, site, components, utilities}.css` | 12 | | | | | | 48 (C11) | |
| `css/layout.css` | | 17 | | | | | 48 | |
| `css/gis.css`, `css/spatialcompute.css` | | 18 | | | | | 48 | |
| `css/<page>.css` | | | | 38–42 | | | 48 | |
| `.github/*`, `global.json` | 10 | | | | | | | 50 |
| `.editorconfig`, `Directory.Build.props`, `CodeMetricsConfig.txt` | | | | | | | | 50 |
| `Dockerfile`, `docker/`, `k8s/`, `.gitignore` | 09 | | | | | | | |
| `.dockerignore`, `tools/` | 13 | | | | | | | |
| `native/README.md` | 11 | | | | | | 49 (if needed) | |
| README, `docs/*`, copilot-instructions, `css/README` | 00 (agent-context) | | | | | | 49 | |
| `wwwroot/lib/*` | | 18 (typeahead) | | | 45 | | | |

### 3.5 Work-package specs
Unless a spec says otherwise, every WP runs the §5.1 gates and can run in parallel with its wave peers.

---

#### WP-00 · Plan, orientation fixes, artifact preservation · W0 · S · orchestrator
- **Goal.** Make the plan durable, give implementers an accurate `agent-context.md`, and save the Stage-1 tools before the scratchpad disappears.
- **Closes.** ADD-11 and DH-09 (current-state items).
- **Steps.**
  1. **Now, before this session ends,** copy the following from `C:\Users\brian\AppData\Local\Temp\claude\…\6e74e331-…\scratchpad\` to a durable folder outside the repo, e.g. `%USERPROFILE%\portfolio-cleanup-artifacts\`. Copy no `bin/`, no `obj/` and no `appsettings.Development.json` copies.
     - `cx\tools`, `cx\probe`, `cx\metrics`
     - `dt\scripts`, `dt\run`
     - `fe\*.py`
     - `fe\repo\Portfolio.Web\wwwroot\__audit\*.js`
     - `sec\harness\{Program.cs,Harness.csproj}`
     - `sec\cspprobe\{tools,site}`
  2. Save this file as `docs/cleanup/PLAN.md`.
  3. Correct `docs/agent-context.md` per findings-docs-tests §C6 rows 1–16:
     - The identity key is `Items["PortfolioIdentity"]`.
     - 697 tests run (609 attributes).
     - coverlet also excludes `[Obsolete]`.
     - Plan doc 00 is the graph-engine prerequisite.
     - Four Common DTO files use DataAnnotations or `IValidatableObject`.
     - There are 26 service classes and 25 interfaces.
     - Native file-naming exceptions; the Abstractions layout.
     - Add the AccessDenied page; `Projects/Details.cshtml.cs` is unrouted.
     - The checklist has 19 items.
     - Parity scope: NativeParityTests covers clip and viewshed; checksums for all 21 workloads come from Benchmarks only.
     - `AnonUserId` is Secure only outside Development.
     - 9 of 25 controllers use "expensive".
     - Details pages use 8 sections (10 pages) or 6 sections (8 pages).
     - The `std::map` note is historical.
     - Add notes: the cancellation-token OpenAPI defect, native-present coverage below the gate, the EF InMemory precedent.
  4. Commit to `cleanup/plan`: `docs/cleanup/*`, `docs/agent-context.md` and `docs/cleanup-prompt.md` (D-19). Open a PR to `main`.
- **Acceptance.**
  - The artifacts folder exists and contains no secrets.
  - The PR contains documentation only.
- **Rollback.** Revert.

#### WP-01 · Test host and pipeline characterization · W1 · L (tests only)
- **Goal.** Lock pipeline order, headers, auth redirects, rate limits, DI registrations, resilience attempts, routes and the OpenAPI contract before anything changes.
- **Closes.** CX-01, TST-06, TST-03, SEC-34 (auth half), SEC-WP0 (host parts), ADD-02, CX-03 (InMemory move and the Repositories `<Folder>` item).
- **Files.**
  - `Directory.Packages.props` (+`<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />`).
  - `Portfolio.Tests/Portfolio.Tests.csproj` (+`PackageReference` for Mvc.Testing and for `Microsoft.EntityFrameworkCore.InMemory`).
  - `Portfolio.Repositories/Portfolio.Repositories.csproj` (−InMemory reference; −`<Folder Include="Migrations\" />`).
  - New: `Portfolio.Tests/Integration/{PortfolioAppFactory,PipelineOrderTests,RateLimitingTests,ServiceRegistrationSnapshotTests,ArcGisResilienceTests,OpenApiContractTests,HostHeaderTests}.cs`.
  - New: `Portfolio.Tests/Contracts/{RouteTableTests,AuthorizationMetadataTests}.cs`.
  - Embed golden lists as C# raw string literals, so no csproj content items are needed.
- **Steps.**
  1. **Factory.** Follow findings-complexity §4.3 (source: artifacts `cx/probe/PipelineCharacterizationTests.cs`).
     - Development environment.
     - `UseSetting` for a dummy `ConnectionStrings:DefaultConnection`, `RunMigrationsOnStartup=false`, and dummy Google ClientId/ClientSecret.
     - In `ConfigureTestServices`, remove the three DbContext-options descriptors and add `UseInMemoryDatabase(<unique name>)`. Capture the registrations.
     - Add no `public partial class Program` and no `NuGet.config`.
  2. **PipelineOrderTests.** Cases 1–4 and 6 from cx §4.3:
     - Headers on `/health/live` and on `/js/api-config.js`.
     - `/api/v1/fiber/orders` without auth → 401 with no `Location`.
     - `/Account/Profile` → 302 to `/Login`.
     - The 301st request → 429 with headers, no `AnonUserId` cookie, and 0 profiles. A new XFF value gets a fresh partition. `/health/live` still returns 200 after the limit is hit.
     - An HTML GET without a cookie creates a profile and sets the cookie (HttpOnly, Lax). **Don't assert seed rows.**
     - Pipeline-presence trigger: `GET /api/v1/features/saved` with a JSON `Accept` and no cookie → 409 `application/problem+json`, title "Conflict".
     - **Don't pin** "oversized body → 500" (WP-04 fixes it) or "undeclared graph edge → 404" (WP-06 fixes it).
  3. **RateLimitingTests.** "expensive" allows 15, then 429 on `GET /api/v1/catrisk/book`. Pin the XFF cases (a)–(d) from findings-security §4.2 as today's behaviour.
  4. **ServiceRegistrationSnapshotTests.** Sorted `ServiceType | Lifetime | ImplementationType` for `Portfolio.*`, `IBatchJobStore`, `IDistributedCache`, `TimeProvider` and `IMemoryCache`. This includes today's duplicate geocoding registrations.
  5. **ArcGisResilienceTests.** A fake primary handler returns 503. Expected attempts: `arcgis-features` 3; `arcgis-batch`, `-reverse` and `-address` 4 each.
  6. **OpenApiContractTests.** `/openapi/v1.json` returns 200. Snapshot the sorted path+verb list, the schema names, and the request-body descriptions (today's buggy text; WP-23 updates it).
  7. **RouteTableTests** (dt §B4.8). Snapshot all 77 action routes, including the 3 hidden aliases. Every `/api/v1/…` string in `wwwroot/js/api-config.js` must match a template.
  8. **AuthorizationMetadataTests.** Every public action carries `Authorize(Policy="Authenticated")` or `AllowAnonymous`. No mixed controller has a class-level `AllowAnonymous`.
  9. **HostHeaderTests.** `Host: evil.example` is reflected in the canonical link (today's behaviour).
- **Tests.** This WP is the characterization. Every test must pass on unmodified production code.
- **Acceptance.**
  - Gates green both ways; 0 warnings.
  - `Program.cs` coverage rises from 0% (the probe reached 92.5%).
  - No production `.cs` file changed.
- **Risk.**
  - TestHost must be 10.0.x: 8.x breaks MVC JSON.
  - Restore needs network.
  - Use one factory per rate-limit test.
- **Rollback.** Revert.
- **Deps.** D-01.

#### WP-02 · Unit characterization: middleware, DTO, controller gaps · W1 · M (tests only)
- **Closes.** TST-01 (SecurityHeaders, AnonymousUser, DTO), TST-02, and the CX-05 tests-first step.
- **Files.**
  - New: `Portfolio.Tests/Middleware/{SecurityHeadersMiddlewareTests,AnonymousUserMiddlewareTests}.cs`.
  - New: `Portfolio.Tests/DTOs/HomeSearchPreferencesDtoTests.cs`.
  - New: `Portfolio.Tests/Controllers/{SpatialClusterControllerTests,SpatialOverlayControllerTests,ViewshedControllerTests}.cs`.
  - Existing `Portfolio.Tests/Controllers/*Tests.cs`: add assertions only.
- **Steps.**
  1. **SecurityHeadersMiddleware.** `DefaultHttpContext` with a no-op `next`. Assert the 4 headers and the exact CSP string.
  2. **AnonymousUserMiddleware.** EF InMemory, `Tests/Helpers` FakeTimeProvider, and a fake `IAuthenticationService` in `RequestServices`, with the real `UserProfileSeedService`. Cover the cases in cx CX-05 and dt §B4.6:
     - `Items` preset → passthrough.
     - Authenticated with a claim → `Items` set; authenticated without one → `Items` not set.
     - Valid cookie with a stale profile → touched; with a fresh profile → no write.
     - Valid cookie with no profile → created, with no seed, for any `Accept`.
     - No cookie with HTML `Accept` → profile created, **seed rows > 0**, cookie (HttpOnly; Secure only outside Development; Lax; +1 year).
     - No cookie with JSON `Accept` → passthrough.
     - An invalid GUID cookie behaves like no cookie.
  3. **HomeSearchPreferencesDto.** `Validator.TryValidateObject` with out-of-range weights, and with weights whose sum is not about 1.
  4. **New controller tests.** Ok, and 400 with the exact body, for `RunDbscan`, `SpatialJoin` and `Compute`.
  5. **Every catch branch of all 77 actions.** Assert the status **and** the body:
     - `{ error: "<exact>" }` for 400 and 409. A `nameof(request)` argument appends `" (Parameter 'request')"`; Viewshed omits `nameof`.
     - ProblemDetails `title`, `detail` and `retryAfterSeconds` for 500 and 503.
     - Add `ProfileController.SetAuthenticatedClaim`, and the FiberShipments Create/Update/UpdateStatus error paths.
- **Acceptance.** Green both ways. No production change.
- **Deps.** None. EF InMemory is available transitively today and directly after WP-01.

#### WP-03 · Golden outputs for refactor targets · W1 · M (tests only)
- **Closes.** TST-04, TST-07, TST-09, ADD-13, and the tests-first step for WP-25–29 and WP-31.
- **Files.** New `Portfolio.Tests/Golden/{FleetRoutingGoldenTests,ResponseCoverageGoldenTests,ChangeDetectionGoldenTests,SpatialGraphGoldenTests,UserProfileSeedGoldenTests,AddressParseGoldenTests}.cs`. Expected values are embedded in C#.
- **Steps.**
  1. **Fleet** (dt §B4.2). For each preset, pin `VehiclesUsed`, `Feasible`, `UnservedStopIds`, `TotalDistanceKm` (±1e-2), `FinalObjective` (±1e-3), per-vehicle `StopIds` and `IterationCosts`. Measured values:
     - morning: 2/3 vehicles, 35.04 km, 85.043
     - fullday: 4/5, 61.38 km, 161.381
     - tight: 6/6, 77.00 km, 226.999
     Add validation theories and a `TruncateToFleet` case.
  2. **Response** (dt §B4.3). Use p = 4 with modes 0, 1 and 2. Expected:
     - mode 0: `[1,2,3,5]`
     - mode 1: `[1,2,3,4]` (94.06%, p90 2.42)
     - mode 2: `[8,12,13,14]`
     - baseline: 76.40% / 4.68 / 2.48
     Pin the IDs, `IterationObjectives` and stats to ±1e-2. Add validation theories.
  3. **ChangeDetection** (dt §B4.5). Use `SyntheticChangeScene.Build(256,256,0.025)` and a noisy variant, with the page defaults from `Pages/Projects/Change/Index.cshtml`. Pin the threshold, changed pixels, blob boxes and the histogram checksum. Each of the 4 ground-truth boxes must overlap a blob. Add a U-shaped `UnionLabels` mask and validation theories.
  4. **SpatialGraph.** Dijkstra and A* from 3–4 fixed nodes to node 2530:
     - Pin `TotalCost`, `NodeIds`, `DistanceKm`, `EstimatedMinutes`, explored count and explored order. A* must explore fewer nodes than Dijkstra.
     - Pin the service-area count, and a distances checksum.
     - Immutability guard: hashes of the graph match before and after route, fleet and response runs, and `ReferenceEquals` holds across calls.
     - **Don't pin undeclared-edge behaviour** (WP-06 changes it).
  5. **UserProfileSeed.** For a fresh InMemory user:
     - Pin per-entity counts.
     - Pin a canonical ordered text of the business fields (no IDs or timestamps) as a raw string.
     - Seeding twice adds nothing; seeding user B doesn't change user A.
     - Keep the **Properties assertion separate**: D-07 may delete it.
  6. **AddressParse.** About 30 raw addresses, pinned to every `AddressParsedDto` field.
- **Acceptance.**
  - Green with `PORTFOLIO_DISABLE_NATIVE=1`.
  - Green with all 13 kernels present (§5.3). These tests double as parity checks.

#### WP-04 · Exception middleware hygiene · W1 · S
- **Closes.** SEC-12 (middleware) and SEC-15. Decision D-04(c).
- **Files.** `Portfolio.Web/Middleware/ApiExceptionMiddleware.cs` (grep `var (status, title, detail) = ex switch`); new `Portfolio.Tests/Middleware/ApiExceptionMiddlewareTests.cs`.
- **Steps.**
  1. **Commit 1, characterization.** One test row per mapping in dt §B4.6. Include today's leak: a framework `KeyNotFoundException` message is returned.
  2. **Commit 2,** following findings-security SEC-WP3 steps 1–4:
     - `IsOwn(ex)` checks that `TargetSite.DeclaringType.Assembly` starts with `"Portfolio."`. A null `TargetSite` counts as not own.
     - Non-own exceptions get generic detail text, one fixed string per status.
     - Add `BadHttpRequestException b => (b.StatusCode, "Bad Request", "The request was rejected.")` before the default arm, logged at Information without a stack trace.
     - If `Response.HasStarted`, log and rethrow.
     - The ProblemDetails shape doesn't change.
- **Tests after.**
  - Framework `KeyNotFoundException` → 404 with generic text.
  - Service `ArgumentException`, including one thrown inside an async method → 400 with the original message.
  - `BadHttpRequestException(413)` → 413.
  - Response already started → no second write.
- **Acceptance.** Only the `detail` of non-own exceptions changes.
- **Risk.** The UI shows generic text for framework errors. That is intended.
- **Deps.** D-04.

#### WP-05 · Connection-string helpers out of `Program.cs` · W1 · S
- **Closes.** CX-02 and TST-05(a).
- **Files.**
  - `Portfolio.Web/Program.cs` (grep `static NpgsqlConnectionStringBuilder ParsePostgresConnectionString`, `NormalizeRedisConnectionString(`, `MaskRedisPassword(`, and their 3 call sites).
  - New `Portfolio.Web/Infrastructure/ConnectionStrings.cs` (`internal static`: `ParsePostgres`, `NormalizeRedis`, `MaskRedisPassword`).
  - New `Portfolio.Tests/Infrastructure/ConnectionStringsTests.cs`.
- **Steps.** Move the three functions verbatim and rename the call sites. Add the cases in dt §B4.1(a), including that the malformed-input message **currently contains the raw string**. WP-14 flips that assertion.
- **Acceptance.** The `Program.cs` diff is deletions plus 3 call-site edits.
- **Note.** This is the only W1 WP that touches `Program.cs`.

#### WP-06 · Compute work caps and finite validation · W1 · M
- **Closes.** SEC-01 (service half), SEC-13, SEC-12 (graph), DOC-02 (`SpatialGraphService` comment). Decisions D-02(b) and D-04(b).
- **Files.**
  - `Portfolio.Services/Services/ViewshedService.cs` (grep `ValidateRasterShape`, `for (var ty = 0; ty < height; ty++)`).
  - `RasterTerrainService.cs` (`MaxHeatmapPoints`, `GenerateHeatmapManaged`).
  - `SpatialClusterService.cs` (`seeds.AddRange(neighbors)`).
  - `GeoStreamProcessorService.cs` (`GridSizeDegrees <= 0`, `telemetryEvent.Latitude < -90`).
  - `SpatialGraphService.cs` (`candidate >= distances[edge.To]`, `tentativeG >= gScore[edge.To]`, comment `std::map lookup`).
  - `CatRiskService.cs` (`location.InsuredValue <= 0`, `catEvent.RadiusKm <= 0`).
  - `ReverseGeocodingService.cs` (`latitude < -90.0`).
  - Tests: `Portfolio.Tests/Services/{HotspotViewshedOverlayServiceTests,SpatialComputeServiceTests,CatRiskServiceTests,ReverseGeocodingServiceTests}.cs`.
- **Steps.** Follow findings-security §10 SEC-WP1 steps 1–8, with these specifics:
  - **Viewshed:** width and height each ≤ 512; check cancellation every 4,096 target cells.
  - **Hillshade and heatmap:** the same side cap, plus `(long)width*height*points ≤ 25_000_000`; check cancellation every 1,024 cells; finite checks on elevation, azimuth, altitude, extent and point x/y/weight.
  - **DBSCAN:** dedup the seeds with an `int[] queuedIn` array (−1 initially). Labels stay identical, because a duplicate seed is already labelled and so is a no-op.
  - **GeoStream:** count events with non-finite lat/lon as invalid (skip them; don't throw). Throw `ArgumentException` for a non-finite speed. Require `GridSizeDegrees ≥ 1e-6`.
  - **Graph:** use `TryGetValue` and skip at both indexers, which matches native and `ComputeDistances`. Rewrite the stale `std::map` comment: native drops undeclared edges when it builds the CSR.
  - **CatRisk:** `InsuredValue ≤ 1e12`; `RadiusKm ≤ 5,000`.
  - **ReverseGeocoding:** non-finite input → `ArgumentException`.
  - Existing messages stay byte-identical. Native code doesn't change.
- **Tests.**
  - Commit 1 pins today's undeclared-edge `KeyNotFoundException`; commit 2 flips it to "returns a route", for both Dijkstra and A*.
  - Each cap throws `ArgumentException`.
  - Boundaries pass: viewshed 512×488, the UI's 40×40, a heatmap exactly at budget.
  - DBSCAN with 5,000 identical points allocates < 32 MB (`GC.GetAllocatedBytesForCurrentThread`), and its labels equal an embedded copy of the old algorithm on 1,000 seeded random cases.
  - A NaN case for every guarded field.
- **Acceptance.**
  - The §5.2 inputs from findings-security are rejected or finish in < 1 s.
  - The native-present run and the parity tests are green.
- **Deps.** D-02, D-04.

#### WP-07 · Client-side XSS fixes and the one vetted helper · W1 · M
- **Closes.** SEC-05, SEC-06 (client), SEC-07 (unsafe and latent sinks), the 9 inline handlers that block CSP, and the two toast overrides (FE-A14).
- **Files.**
  - New `wwwroot/js/dom-safe.js`.
  - `Pages/Shared/_Layout.cshtml`: one tag after `<script src="~/js/api-fetch.js"`.
  - `wwwroot/js/BatchGeocoding/app.js` (`{ title: "Original Address"  }`).
  - `wwwroot/js/PlantOperationsDashboard/{dashboard,inventory,orders,shipments}.js` (`onclick=`, `function showToast(`, `window.plantOpsToast`, `popupTemplate: new PopupTemplate`).
  - `wwwroot/js/Network/app.js` (`bindTooltip`).
  - `wwwroot/js/HomeFinder/app.js` (`popupTemplate: new PopupTemplate`, `aria-label="View property`, `function showToast(message`, `function escapeHtml(s)`).
  - `wwwroot/js/ReverseGeocoding/app.js` (`markerGraphic.popupTemplate`, `function escapeHtml(str)`).
  - `wwwroot/js/StateExplorer/uiManager.js` (`style="background:${escapeHtml(col.color)}`, `function escapeHtml(s)`).
- **Steps.**
  1. **`dom-safe.js`.** Take it verbatim from findings-security §3.3 (`PortfolioDom.escapeHtml` escapes 5 characters; `PortfolioDom.el` builds DOM).
  2. **Batch Geocoding.** Columns 1 and 3 get `render: $.fn.dataTable.render.text()`.
  3. **PlantOps.**
     - Replace the 9 `onclick` attributes with `data-action` and `data-id`; set `data-status` through `setAttribute`. Add one delegated listener per module.
     - Replace `plantOpsToast(...)` with the global `showToast(message, type)`; check that the argument order and type names match `toasts.js`.
     - Delete the toast helpers in `dashboard.js`.
     - Escape the popup fields.
  4. **Network.** Build the tooltips with `PortfolioDom.el` nodes.
  5. **HomeFinder.** Escape the popup; use the 5-character escape in the `aria-label`; delete the local `showToast`/`createToastContainer`; use `PortfolioDom.escapeHtml`.
  6. **ReverseGeocoding.** Escape the popup and use `PortfolioDom.escapeHtml`.
  7. **`uiManager.js`.** Build the colour badge with `el()` and set `style.backgroundColor` after a `^#[0-9a-fA-F]{6}$` check; use `PortfolioDom.escapeHtml`.
  8. Leave the 7 remaining text-context escapers alone (WP-38–41). Don't edit PlantOps `Index.cshtml`.
- **Tests.**
  - Before: the static DataTables probe (findings-security A.7, vendored libraries, local only) shows the payload fires.
  - After: the same probe shows text.
  - Browser protocol (§5.4) on Network, HomeFinder, ReverseGeocoding and State Explorer. PlantOps per D-16.
- **Acceptance.**
  - `grep -rnE 'on[a-z]+="' Portfolio.Web/wwwroot/js` → 0.
  - `function showToast` exists only in `toasts.js`.
  - `PortfolioDom` is defined on every page.
  - The console is clean.
- **Risk.** PlantOps CRUD wiring: click every action.

#### WP-08 · Persistence validation and log redaction · W1 · M
- **Closes.** SEC-06 (server), SEC-22 (everything except claims), SEC-16 (service and repository logs), SEC-02 (address length). Decisions D-02(c) and D-02(d).
- **Files.**
  - `Portfolio.Services/Services/{FiberShipmentService,SavedFeatureService,CollectionService,SavedSearchService,AddressStandardizationService}.cs`.
  - `Portfolio.Repositories/Repositories/{CollectionRepository,SavedFeatureRepository}.cs`.
  - New `Portfolio.Repositories/Diagnostics/LogRedaction.cs`.
  - Tests in the matching `*ServiceTests.cs`, plus new `Portfolio.Tests/Repositories/LogRedactionTests.cs`.
- **Steps.**
  1. Apply the rules in findings-security SEC-WP6:
     - Shipment status allowlist {Pending, In Transit, Delivered, Exception}; string fields ≤ 100, state ≤ 50; `Route.Count ≤ 1,000`; finite coordinates.
     - Saved features: IDs ≤ 64, name ≤ 200, description ≤ 2,000.
     - Collection colour matches `^#[0-9a-fA-F]{6}$`.
     - Saved search: name ≤ 200; `PropertyIds` ≤ 50.
     - `rawAddress` ≤ 1,000 characters.
  2. **Measure before capping GeometryJson (NEW-03).** Saved states store the full Census polygon. Run the app once and record the largest `GeometryJson` length returned by `GET /api/v1/features?layerId=3`.
     - Set the cap to max(1,000,000, 2 × the observed maximum). If you can't measure, use 8,000,000.
     - Write the value in the PR. WP-14 uses it for the endpoint limit.
  3. Add `LogRedaction.UserId(Guid)`: the first 8 hex characters of its SHA-256.
     - Use it in the Warning logs at `SavedFeatureService.cs:101,111`, `CollectionService.cs:119`, `CollectionRepository.cs:54` and `SavedFeatureRepository.cs:62`.
     - Address logs record the score and length only (`AddressStandardizationService.cs:123-125,175`).
- **Tests.** A boundary case and an over-limit case per rule. A log-capture test that no raw GUID or address appears.
- **Risk.** Over-limit rows already stored still read; only writes are validated.

#### WP-09 · Container and Kubernetes hardening · W1 · M
- **Closes.** SEC-25, SEC-26, SEC-27, CX-21, and the compose port bug.
- **Files.**
  - `Dockerfile` (grep `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build`, `aspnet:10.0 AS runtime`, `RUN mkdir -p /app/DataProtection-Keys`, `ENV ASPNETCORE_HTTP_PORTS=${PORT:-10000}`).
  - `docker/docker-compose.yml` (`- "6379:6379"`, `- PORT=8080`).
  - `k8s/{deployment,redis,namespace}.yaml`; new `k8s/networkpolicy.yaml`; `git mv k8s/secret.yaml k8s/secret.example.yaml`; `k8s/README.md` (one line).
  - `.gitignore` (+`k8s/secret.yaml`, +`*.user`).
  - `git rm --cached Portfolio.Web/Portfolio.Web.csproj.user`.
- **Steps.**
  1. Follow findings-security SEC-WP11.
  2. **Digest pins.** Resolve them with a registry metadata request, which needs no image download: `docker buildx imagetools inspect`, or `curl -sI` against the `mcr.microsoft.com/v2/…/manifests/10.0` endpoint. Keep the tag in a comment.
  3. `chown $APP_UID /app/DataProtection-Keys`, then `USER $APP_UID`.
  4. Replace `${PORT:-10000}` with a literal `10000`. The value is identical: `PORT` is not a build ARG.
  5. **Compose.** Don't publish Redis (or bind it to 127.0.0.1). Set `ASPNETCORE_HTTP_PORTS=8080` instead of `PORT`.
  6. **k8s.** Add a `securityContext`, `automountServiceAccountToken: false`, Redis `requirepass` from the Secret, NetworkPolicies, and PSA `restricted`.
- **Tests.**
  - The CI docker job.
  - If Docker is installed locally: `docker run`, then `/health/live` returns 200 and `id -u` prints 1654.
  - If kubectl is installed: `kubectl apply --dry-run=client -f k8s/`.
- **Risk.** A Render deploy failure on a write permission. Production stores keys in Redis. The owner checks the first deploy (`/health/live` plus one page).
- **Rollback.** Revert; that is one deploy.

#### WP-10 · CI supply chain and native-run honesty · W1 · S–M
- **Closes.** SEC-28, CI-02, CI-03, CI-05, CI-06 (report pick), TST-08, NEW-05, ADD-04.
- **Files.**
  - `.github/workflows/ci.yml` (every `uses:`; `root = ET.parse(reports[0])`; the `pull_request:` filter; the "Test with native kernels present" step).
  - New `.github/dependabot.yml` and `.github/workflows/security-scan.yml`.
  - New `global.json`.
  - `Portfolio.Tests/Services/{NativeParityTests,NativeScoringBridgeTests}.cs` (grep `if (!` … `IsAvailable)`); new `Portfolio.Tests/Services/NativeAvailabilityTests.cs`.
- **Steps.**
  1. Pin each action to the SHA in findings-security §7.3, with a `# vX.Y.Z` comment. Keep the same majors.
  2. Set `pull_request.branches: [main, 'cleanup/**']`.
  3. Dependabot: nuget (groups from dt §B6), github-actions, docker; weekly.
  4. A `dependency-review` job on PRs, pinned, with `contents: read`.
  5. `security-scan.yml`: weekly plus `workflow_dispatch`. Restore, then `dotnet list package --vulnerable --include-transitive`; fail on any finding.
  6. Coverage gate: sort the glob and assert exactly one distinct report by hash.
  7. Replace the 12 early returns with `Assert.SkipUnless(…IsAvailable, "native library not loaded")`. `NativeAvailabilityTests` fails when `PORTFOLIO_REQUIRE_NATIVE=1` and any of the 13 bridges is unavailable. Set that variable on the native-present step only.
  8. `global.json`: `{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}`.
- **Tests.**
  - The PR's own run is green.
  - Locally, the managed run shows 685 passed and 12 skipped.
  - The native-present run with `REQUIRE=1` passes.
- **Risk.** Reported test counts change. CodeQL is the owner's setting (D-20).

#### WP-11 · Honest claims · W1 · S
- **Closes.** DH-02, DH-03, NEW-07, NEW-08, native/README stale parity text (dt §C5.1), the State Explorer "Typeahead.js" claim, and the Batch Geocoding async/sync description. Decision D-08.
- **Files.**
  - `Pages/Index.cshtml`: only the 2 lines matching `SIMD-friendly native terrain kernel`.
  - `Pages/Projects/Index.cshtml`: 1 line, same grep.
  - `Pages/Projects/{CatRisk,Change,Fleet,Outage,Response}/Details.cshtml`: figures per dt §C4.2.
  - `Pages/Projects/HomeFinder/Details.cshtml` (:59, :96, :98), `Terrain/Details.cshtml` (:59, :76), `Clustering/Details.cshtml` (:55), `Overlay/Details.cshtml` (:55), `Viewshed/Details.cshtml` (:35, :55).
  - `Pages/Projects/StateExplorer/Details.cshtml` (grep `Typeahead.js`).
  - `Pages/Projects/BatchGeocoding/Details.cshtml` (:46).
  - `native/README.md` (grep `Native wins 8 of the 21`; also :123-129, :166, :169, :176, :232-233, :329-352).
- **Steps.**
  - Replace each withdrawn figure with the table value, or with a link to `native/README.md#measured-speedup`.
  - Remove SIMD, AVX and vectorization implications.
  - Keep build-flag facts only where they are true: the scoring kernel does use `/arch:AVX2`; the terrain kernel does not. Interview questions and explicit "no SIMD" denials stay.
  - Batch Geocoding: say that the demo uses the synchronous endpoint and the async job API exists.
  - native/README.md: 9/5/7 at ±15%, and the current parity mechanism.
  - Keep edits minimal and change no layout.
- **Acceptance.**
  - None of these strings remain except in interview questions or denials: "SIMD-friendly", "AVX2-friendly", "vector lanes", the withdrawn figures (1.1×, 1.46×, 1.3×/0.87× at 4,603, "between 1.2× and 2.2×").
  - native/README's counts agree with its table.
  - The owner approves the wording in the PR.

#### WP-12 · Core CSS: tokens, focus rings, buttons · W1 · M
- **Closes.**
  - FE-A01 (global part), FE-D01, FE-A10 N1/N3, FE-D03 (one definition).
  - FE-A14 (toast CSS moved into components).
  - Additive `.panel`, `.kpi`, `.legend`, `.state-*` and `.project-hero` (FE-D04–D07).
  - T12, T18; DOC-02 (`site.css:7`); the duplicates in `utilities.css`.
  - Decision D-09(a,b).
- **Files.** `wwwroot/css/{base,preferences,site,components,utilities}.css`.
- **Steps.**
  - FE-WP1 (findings-frontend §10) and FE-A01 fix steps 1–3: the token table in FE-D01, focus overrides after Bootstrap, `.btn-accent` and `.btn-outline-accent` on Bootstrap's variable API.
  - Delete `utilities.css:4-16` and `:21-33`; the `components.css` copies already win.
  - Add `.project-hero` to `components.css`, copied from the rule that wins today.
  - Fix the `site.css:7` comment.
- **Acceptance.**
  - After one real Tab, `focusIndicators()` reports none = 0 and weak = 0 on Home, Response and Batch at 1280.
  - Contrast failures don't increase on any audited page.
  - At rest, `.btn-accent` and `.btn-outline-accent` have the same computed styles as before.

#### WP-13 · Durable analysis tools · W1 · S
- **Closes.** D-14, the cx §12 durability caveat, and FE D11.
- **Files.**
  - New `tools/metrics/`: `csmetrics/{csmetrics.csproj, Program.cs}`, `summarize_cs.py`, `jsmetrics.js`, `summarize_js.py`, `loc.py`, `coverage_gate.py`, `parse_analyzers.py`, `cpp-complexity.sh`, `parse_clang_tidy.py`; optionally `deadcode/`.
  - New `tools/fe-audit/`: `fe-audit.js`, `grad.js`, `contrast.py`, `colors.py`, `balance.py`, `inventory.py`.
  - `.dockerignore` (+`tools/`).
- **Steps.**
  1. Copy from the WP-00 artifacts folder.
  2. Retarget csmetrics to `net10.0` and set `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`, with inline package versions.
  3. Keep the tools out of `Portfolio.sln`.
  4. **Strip absolute paths and usernames:** the repo is public. Take the clone root as an argument.
  5. `coverage_gate.py` takes `--min-line` and `--min-branch` (defaults 80 and 60).
  6. Put usage in a header comment in each script; don't add a new `.md`.
- **Acceptance.**
  - csmetrics on HEAD reproduces §1.4 (CA1502 column), and `loc.py` reproduces §1.5.
  - `dotnet build Portfolio.sln` and the docker build are unaffected.

---

#### WP-14 · Request limits and config hardening · W2 · L
- **Closes.**
  - SEC-01 (limits), SEC-02, SEC-10 (config surface), SEC-14 (HSTS, `Server` header), SEC-16 (connection-string messages).
  - SEC-23, SEC-31, SEC-32, SEC-34 (limits half).
  - NEW-01, CX-03 (protected-claim dedupe), ADD-07.
  - Decisions D-02(a,c), D-04(c,d), and the defaults for HSTS, D15 and `/health/ready`.
- **Files.**
  - `Program.cs` (grep `builder.Services.AddRateLimiter`, `options.KnownIPNetworks.Clear()`, `GetConnectionString("DefaultConnection")`, `app.UseHsts();`, `MapHealthChecks("/health/ready"`).
  - `Infrastructure/ConnectionStrings.cs`.
  - `appsettings.json`.
  - `HealthChecks/DatabaseReadyHealthCheck.cs`.
  - `Controllers/Api/{GeoStream,SpatialGeometry,RasterTerrain,SpatialNetwork,SpatialCluster,Viewshed,SpatialOverlay,Features,AddressStandardization,Profile,UserProfile,SavedFeature,Collections,FiberMaterials,FiberOrders,FiberShipments,HomeFinder}Controller.cs`.
  - Tests: extend `Integration/RateLimitingTests.cs` and `Contracts/AuthorizationMetadataTests.cs`; new `Integration/HostingOptionsTests.cs`; `Infrastructure/ConnectionStringsTests.cs`; a new health-check test; `Controllers/{Profile,UserProfile,Features}ControllerTests.cs`.
- **Steps** (one commit per concern):
  1. **Rate limiting.**
     - Add a "compute" policy (sliding window, 60/min/IP, 6 segments) and a "csp" policy (fixed window, 30/min/IP).
     - `GlobalLimiter = PartitionedRateLimiter.CreateChained(<existing per-IP 300/min>, <compute concurrency>)`.
     - The concurrency key: if `ctx.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName` is "compute" or "expensive", use one shared `ConcurrencyLimiter` partition (`PermitLimit = Math.Max(2, Environment.ProcessorCount)`, `QueueLimit = 16`); otherwise `GetNoLimiter`.
     - Make the key selector an `internal static` method so it can be unit-tested.
  2. **Attributes.** Class-level `[EnableRateLimiting("compute")]` on the 7 compute controllers; `[EnableRateLimiting("expensive")]` on `FeaturesController`.
  3. **Kestrel.** `builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 1_000_000; o.AddServerHeader = false; });`.
  4. **`[RequestSizeLimit]`.** Address parse/validate 16_384; profile and user claims 8_192; `POST features/saved` = the WP-08 cap + 65_536; collections POST/PUT 4_096; fiber POST/PUT 65_536; HomeFinder POSTs 16_384. Existing limits stay.
  5. **HSTS.** `AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = false; o.Preload = false; })`.
  6. **ConnectionStrings.** Remove `{connectionString}` from both messages, and flip the WP-05 assertions.
  7. **Database URL.** Add `ConnectionStrings.ResolveDatabaseUrl(IConfiguration, Func<string,string?> env)`: DefaultConnection, then `DATABASE_URL`, then throw, each step skipping null or whitespace. Set `appsettings.json` `DefaultConnection` to `""` (D15).
  8. **Forwarded headers.** Read `ForwardedHeaders:ForwardLimit` (default 1) and `ForwardedHeaders:KnownNetworks` (default empty). When the list is empty, keep both `Clear()` calls (findings-security §4.4 item 1). Add an empty section to `appsettings.json`.
  9. **`/health/ready`.** Keep `DisableRateLimiting()`. Cache the DB check result in `DatabaseReadyHealthCheck` for 10 s (static, via `TimeProvider`).
  10. **Features.** `layerId` must match `^\d{1,3}$`; `bbox` must be null or 4 comma-separated finite doubles; otherwise 400 `{ error }`.
  11. **NEW-01.** `UserProfileController.RemoveClaim` returns 400 `{ error }` for `ProfileClaimTypes.Protected`. Both controllers use `ProfileClaimTypes.Protected`: delete the private sets and keep the messages.
- **Tests after.**
  - Reflection:
    - All 8 controllers carry their policy.
    - Every `[HttpPost]` or `[HttpPut]` has `[RequestSizeLimit]` or appears on an explicit exemption list.
  - Options tests:
    - `KestrelServerOptions`: 1 MB, `AddServerHeader` false.
    - `HstsOptions`.
    - `RateLimiterOptions`: 429.
  - The 61st `POST /api/v1/geometry/triangulate` (tiny body) → 429.
  - The concurrency-key unit test.
  - The health check hits the DB once per 10 s (fake clock).
  - Messages contain no secret.
  - `ResolveDatabaseUrl` falls back to `DATABASE_URL` when DefaultConnection is `""`.
  - Features returns 400; deleting a protected claim returns 400.
  - **NEW-04:** TestServer doesn't run Kestrel, so check 413 live (§5.2).
- **Acceptance.**
  - Live: a 20 KB address body → 413; no `Server` header.
  - 20 Viewshed cell clicks in one minute all succeed (§5.4).
- **Risk.** An endpoint without an explicit limit now gets 1 MB; the reflection test lists them. Queueing adds latency under load.
- **Deps.** WP-01, WP-04, WP-05, WP-08.

#### WP-15 · Identity separation and anonymous-identity throttling · W2 · M
- **Closes.** SEC-03, SEC-04, SEC-22 (claims), SEC-33, SEC-16 (identity logs), DOC-02 (2 summaries), NEW-06. Decision D-05.
- **Files.**
  - `Middleware/AnonymousUserMiddleware.cs` (grep `await seedService.SeedForUserAsync`, `context.Request.Cookies.TryGetValue(CookieName`).
  - `Services/UserProfileService.cs` (`ctx.Request.Cookies.TryGetValue("AnonUserId"`, `SetClaimAsync`).
  - `Repositories/Interfaces/IUserProfileRepository.cs`; `Repositories/Repositories/UserProfileRepository.cs` (add `HasClaimAsync` and `CountClaimsAsync`; the log at `:90`).
  - `Areas/Account/Pages/Profile.cshtml` (grep `<div class="profile-detail-label">User ID</div>`).
  - Tests: `Middleware/AnonymousUserMiddlewareTests.cs` (deliberate updates), `Services/UserProfileServiceTests.cs`, new `Integration/IdentityTests.cs`.
- **Steps.**
  1. **Cookie path.** If the profile exists, the request is unauthenticated, and `HasClaimAsync(id, "google_id")` is true: delete the cookie, don't set `Items`, and continue.
  2. **Remove `SeedForUserAsync` from the middleware** and drop the parameter.
     - Add an instance-field `PartitionedRateLimiter<string>` keyed by `RemoteIpAddress`, 10 new identities/min. Apply it to both creation paths: no cookie with HTML, and a cookie with an unknown GUID. When it denies, continue without an identity.
     - Skip identity creation for `/api` paths.
  3. **`CreateOrUpdateFromGoogleAsync`.** Adopt the anonymous profile only if `!HasClaimAsync(anonId, "google_id")`; otherwise create a new profile. Call `SeedForUserAsync` in the adoption branch too (it is idempotent).
  4. **`SetClaimAsync`.** Value ≤ 1,000 characters, type ≤ 200, at most 50 claims per user for new types. These throw `ArgumentException`, which the middleware returns as a ProblemDetails 400. That shape is accepted for these abuse-only paths.
  5. **Logs.** Use `LogRedaction.UserId` in the middleware, `UserProfileRepository:90` and `UserProfileService:88`.
  6. **Profile page.** Show the first 8 characters of the ID, then "…".
  7. Fix the `Items["AnonUserId"]` summaries (`AnonymousUserMiddleware.cs:13`, `UserProfileService.cs:14`).
- **Correction to findings-security.** Don't add the "Properties already exist after migrations" test; it is false. Instead assert that a fresh anonymous visit inserts 0 seed rows.
- **Tests.**
  - Before: WP-02's middleware tests, plus a test pinning today's adoption of a *linked* anonymous profile.
  - After:
    - A linked cookie gets no identity and the cookie is deleted.
    - Signing in with someone else's linked GUID creates a new profile and leaves the victim's `google_id` unchanged.
    - Adopting an unlinked profile seeds and links it.
    - The 11th new identity in a minute from one IP gets no cookie.
    - `/api` creates no profile.
    - The claim caps.
    - Logs contain no raw GUID.
- **Acceptance.** A fresh anonymous visit creates 1 profile row and 0 seed rows.
- **Risk.** Old anonymous profiles keep their seeded rows (harmless).
- **Deps.** WP-01, WP-02, WP-08; D-05.

#### WP-16 · Shared JS modules · W2 · M
- **Closes.** FE-J01, the FE-A07 option, FE-A14 (`toasts.js`), FE-A10 N5, FE §6.4. Decision D-04(e).
- **Files.**
  - New `wwwroot/js/{ui,format,charts,leaflet-setup,esri-theme}.js`.
  - `wwwroot/js/api-fetch.js` (grep `if (res.status === 401)`, `body && body.error`).
  - `wwwroot/js/toasts.js` (grep `okBtn.focus()`).
- **Steps.** Follow findings-frontend §6.1–6.6 and FE-WP2:
  - `PortfolioUi.escapeHtml` and `PortfolioUi.el` **delegate to `PortfolioDom`**.
  - `getApiError` returns `body.error || body.detail || body.title`, with a dedicated 429 message.
  - `apiFetch(url, { redirectOn401 = true, timeoutMs })`: strip the custom keys before calling `fetch`.
  - Toasts: danger and warning persist; success and info last 6 s and pause on hover or focus. The close button is ≥ 24×24 and `btn-close-white` only on success and danger. `confirmDialog` gets `aria-labelledby` and focuses Cancel.
  - `esri-theme.js` is the inline loader made reusable, with a hook for the WP-46 SRI hashes.
  - No page adopts the modules in this WP.
- **Acceptance.**
  - Each module loads on a blank page.
  - The console checks in FE-WP2 pass: `PortfolioFormat.money(832900000,{compact:true}) === '$832.9M'`, and the `rng(1337)` sequence matches.
  - Existing pages behave the same, except that error text can now show `detail`.
- **Deps.** WP-04, WP-07.

#### WP-17 · Home page: carousel, skip link, hero · W2 · M
- **Closes.**
  - FE-C01 (Home), FE-A03, FE-A02, FE-A15.
  - T10 and T11; the hamburger focus ring (`layout.css:78-81`); `layout.css` duplicates and the nav underline.
  - Dead `copyEmailContact`; DOC-03 (`navigation.js`).
  - Decision D-09(d,e).
- **Files.**
  - `Pages/Index.cshtml` (grep `<!-- Duplicates for seamless loop -->`, `function copyEmailContact`).
  - New `Pages/Shared/_ProjectCard.cshtml` and new `wwwroot/js/carousel.js`.
  - `wwwroot/css/layout.css` (`@keyframes scrollProjects`).
  - `wwwroot/js/navigation.js` (`function initSmoothScroll`).
- **Steps.**
  - findings-frontend §7: a value-tuple model, rendered in two passes, with `inert` on the copies.
  - FE-A03 fix steps 1–4: pause button, manual scroll-snap mode on focus or reduced motion, the seam fix.
  - FE-A02 fix: focus the target, respect reduced motion, and re-save the file as UTF-8.
  - Delete the `copyEmailContact` block. Leave `/Projects` for WP-42.
- **Acceptance.**
  - The §7 verify list: 36 cards; the copies are text-identical; no duplicate IDs; the normalized HTML of `/` is unchanged apart from attribute order and removed comments.
  - The FE-A02 and FE-A03 verify blocks pass.
  - `grad.js` gives ≥ 4.5 for the hero and contact text at 360, 768 and 1280.
- **Deps.** WP-11, WP-12.

#### WP-18 · Family stylesheets and typeahead removal · W2 · M
- **Closes.** The FE-WP5 items (FE-A01 `outline:none` deletions, N2, T6/T7/T9, N6, FE-D10 in `gis.css`, the single `.project-hero`, dead CSS) plus FE-P04 and SEC-29 for typeahead.
- **Files.**
  - `wwwroot/css/gis.css`, `wwwroot/css/spatialcompute.css`.
  - `Pages/Projects/StateExplorer/Index.cshtml`: only the `typeahead.css` link, the `typeahead.bundle.min.js` script, and the `typeahead` class on `#searchInput`.
  - Delete `wwwroot/lib/typeahead/`.
- **Steps.** The FE-WP5 row in findings-frontend §10. Also record the computed style of `#searchInput` before and after, at 3 widths × 2 themes.
- **Acceptance.**
  - `run()` passes on 2 GIS pages and 2 spatial pages.
  - In dark mode, the modal `.btn-close` filter is not `none`.
  - No 404s; State Explorer search still filters.

#### WP-19 · NuGet patch updates · W2 · S
- **Closes.** SEC-29 (packages) and ADD-01.
- **Files.** `Directory.Packages.props` only.
- **Steps.**
  - Microsoft.* 10.0.10 → 10.0.12, including Mvc.Testing.
  - `Microsoft.Extensions.Http.Resilience` → 10.10.0.
  - Delete the `xunit` 2.9.3 pin.
  - Keep `Microsoft.OpenApi` at 2.7.5. Major bumps are in §6.
- **Acceptance.** Restore is clean (NuGet audit); gates green; docker build green.
- **Deps.** WP-01.

---

#### WP-20 · Security headers and CSP Report-Only · W3 · M
- **Closes.** SEC-08 phase 0, and SEC-14 (Permissions-Policy, COOP, CORP, API `no-store`). Decision D-03.
- **Files.**
  - `Middleware/SecurityHeadersMiddleware.cs` (grep `headers["Content-Security-Policy"]`).
  - New `TagHelpers/CspNonceTagHelper.cs`.
  - `Pages/_ViewImports.cshtml` and `Areas/Account/Pages/_ViewImports.cshtml` (+`@addTagHelper *, Portfolio.Web`).
  - New `Controllers/CspReportController.cs`.
  - Tests: `Middleware/SecurityHeadersMiddlewareTests.cs` (deliberate updates), new `Integration/CspTests.cs`, and the `RouteTableTests` snapshot (+1 route).
- **Steps.**
  - Follow findings-security SEC-WP7 and the policy text in §2.4.
    - Nonce: `Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))`, stored in `Items["csp-nonce"]`.
    - Keep the existing enforced header unchanged, and add the Report-Only header and `Reporting-Endpoints`.
    - Add Permissions-Policy, COOP `same-origin` and CORP `same-origin`.
    - `/api` responses without a `Cache-Control` get `no-store` (through `OnStarting`), so `[ResponseCache]` GETs keep theirs.
  - **Tag helper:** targets `script` and `style`.
  - **Report endpoint** (§2.7): `/csp-report`, `[AllowAnonymous]`, `[EnableRateLimiting("csp")]`, `[RequestSizeLimit(8192)]`, `[IgnoreAntiforgeryToken]`, `IgnoreApi`.
    - Read the raw body for `application/csp-report` and `application/reports+json`.
    - Log one sanitized Warning line, drop the `js.arcgis.com/4.29/` eval report, and return 204.
    - If API versioning rejects the unversioned route, add `[ApiVersionNeutral]`.
- **Tests after.**
  - Header values.
  - Nonce: 16 random bytes, unique per request, present on every `<script>` and `<style>` of `/` and of one project page.
  - Both content types → 204; the eval report is dropped.
  - `no-store` on an API JSON response, and unchanged on `catrisk/book`.
- **Acceptance.**
  - Report-Only is live; the enforced header is unchanged.
  - The headless probe over all pages (findings-security §2.6) shows only the filtered ArcGIS eval.
  - Live: a body over 8 KB → 413.
- **Deps.** WP-02, WP-14; D-03.

#### WP-21 · `Program.cs` decomposition and dead registrations · W3 · L
- **Closes.**
  - CX-04 and §5.1 resilience dedupe.
  - The `Program.cs`-coupled CX-03 items: the Fiber client chain, `FiberClientDto`, 3 shadowed `AddScoped`, `GoogleProfileDto.AnonymousUserId`.
  - OD-10/ADD-05 (`AddMemoryCache`), ADD-06.
  - DOC-02 in `Program.cs` (keep-list KR #6, #10, #11; "Swagger"; "Render / Supabase"; the `AddMemoryCache` comment).
- **Files.**
  - `Program.cs`.
  - New `Portfolio.Web/Startup/{CoreRegistration,DataRegistration,ArcGisClientRegistration,InfrastructureRegistration,AuthenticationRegistration,PortfolioCookieEvents,ApiRegistration,RateLimitingRegistration,Pipeline,StartupMigrations}.cs`.
  - `Services/FiberOrderService.cs` (`_clientRepo`).
  - Delete: `Repositories/Interfaces/IFiberClientRepository.cs`, `Repositories/Repositories/FiberClientRepository.cs`, `Common/DTOs/FiberClientDto.cs`.
  - `Common/DTOs/GoogleProfileDto.cs`.
  - Tests: `Tests/Services/FiberOrderServiceTests.cs`, the DI snapshot, new `Tests/Startup/PortfolioCookieEventsTests.cs`.
- **Steps.**
  1. **Commit "dead registrations".**
     - Delete the Fiber client chain, the three shadowed `AddScoped<I…GeocodingService…>` lines, and `GoogleProfileDto.AnonymousUserId` with `AnonymousUserId = anonId` (keep the `anonId` local; the cookie delete uses it).
     - Delete `AddMemoryCache()` only if the boot and page-render tests stay green and nothing else resolves `IMemoryCache`.
     - Update the DI snapshot deliberately and list the expected diff in the PR.
  2. **One commit per extension, moved verbatim** (findings-complexity §4.2).
     - Keep all names, pipeline names, the same relative order of the `Add…` calls, and the Redis log before the Data Protection log.
     - Resilience: `AddArcGisResilience(name, ArcGisResilienceProfile)` with the `Features` and `Geocoding` presets. Keep the custom `ShouldHandle`.
     - `PortfolioCookieEvents : CookieAuthenticationEvents`, with one `ApiAwareRedirect` helper replacing 3 copies.
  3. **`UsePortfolioPipeline`** holds exactly the agent-context §4 sequence, with the WP-14 additions in place and the keep-list why-comments #1–5 and #12–15. Add `MapPortfolioEndpoints` and `StartupMigrations.Run(...) → int?`.
  4. Rewrite the wrong comments (dt §A6, `Program.cs` rows).
- **Tests.**
  - Before: all of WP-01 and WP-05.
  - After:
    - `PortfolioCookieEventsTests`: on SignedIn, a present anonymous cookie produces a merge DTO, the cookie is deleted and `Items` is set. For redirects, `/api` gets 401/403 with no `Location`, and pages get 302.
    - Options tests per extension (dt §B4.1(b)).
- **Acceptance.**
  - WP-01 tests unchanged and green.
  - The DI snapshot equals the post-step-1 snapshot.
  - `<Main>$` CA1502 < 10, measured with `tools/metrics`.
  - `Program.cs` is about 40 lines; startup log order is unchanged.
- **Risk.** Medium: order or registration drift, guarded by WP-01.
- **Deps.** WP-01, WP-05, WP-13, WP-14.

#### WP-22 · Dead-code batch (outside `Program.cs`) · W3 · M
- **Closes.** The rest of CX-03, cx §5.4 `IsFinite`, DOC-05 (the TODO), DOC-08.
- **Files.**
  - `Services/AddressStandardizationService.cs` (grep `HouseNumberPattern`).
  - `Services/Data/SyntheticChangeScene.cs` (band constants `Red`, `Green`, `Nir`, `Swir`).
  - `Services/FiberShipmentService.cs` (`_timeProvider`).
  - `Controllers/Api/FiberShipmentsController.cs` (`_logger`) and `ProfileController.cs` (`_logger`, `#region Helper Methods`).
  - The 13 unused repository members and their implementations (findings-complexity §6.3).
  - `Pages/Projects/Details.cshtml.cs` (delete).
  - Services carrying a private `IsFinite`: CatRisk, ChangeDetection, FleetRouting, ResponseCoverage, SpatialCluster, SpatialOverlay.
  - Services with inline `double.IsNaN(x) || double.IsInfinity(x)`: SpatialGraph, GeoStream, RasterTerrain, Viewshed, Outage, SpatialCluster, SpatialGeometry.
  - `Portfolio.Tests.csproj` (4 unused global usings).
  - Tests: `{FiberShipmentsController,ProfileController,FiberShipmentService,BatchGeocodingService}Tests.cs`.
- **Steps.**
  - Apply cx §6.2–6.6, minus the WP-01, WP-14, WP-21 and WP-23 items. Use the artifacts `cx/tools/apply_deadcode.py` and `cx/metrics/deadcode-validation.diff` as a checklist, not a patch: the diff has LF endings.
  - `double.IsFinite` replacements are semantic no-ops.
- **Acceptance.** 0 warnings; tests green both ways; coverage at or above the wave base; Benchmarks still compiles.

#### WP-23 · Fix the published OpenAPI request-body descriptions · W3 · S
- **Closes.** DOC-01, ADD-12, and CX-03 (the stray `<Folder>` in the Web csproj).
- **Files.**
  - The 19 controllers listed in dt §2 DOC-01; XML doc lines only.
  - `Portfolio.Web/Portfolio.Web.csproj` (NoWarn += 1573; delete `<Folder Include="obj\…">`).
  - The `OpenApiContractTests` snapshot.
- **Steps.**
  1. Add 1573 to NoWarn **first**.
  2. Delete the 42 `<param name="cancellationToken|ct">` lines.
  3. Diff `/openapi/v1.json` before and after using the dt §1 recipe.
- **Acceptance.**
  - The only OpenAPI change is the 24 request-body descriptions.
  - A test asserts that no request-body description contains "cancellation" and that every public action has a summary.

#### WP-24 · Asset loading and layout · W3 · S–M
- **Closes.** FE-P02 (layout part), FE-P03, FE-P04 (PlantOps duplicate `toasts.js` and the unused container), FE-P05, and the `_ProjectHero` partial (FE-D07 enabler).
- **Files.**
  - `Pages/Shared/_Layout.cshtml`.
  - New `Pages/Shared/_ProjectHero.cshtml`.
  - `Pages/Projects/{AddressStandardization,BatchGeocoding,PlantOperationsDashboard}/Index.cshtml`: Scripts and Styles sections only, plus the `#plantOpsToastContainer` block.
- **Steps.**
  1. Add `@await RenderSectionAsync("Styles", required: false)` after `preferences.css`.
  2. Move jQuery, the DataTables CSS and the DataTables JS to the three pages (AddressStandardization gets jQuery only).
  3. Add `defer` to external scripts, keeping their order. The two pre-paint inline scripts stay inline and first.
  4. Add global tags for `ui.js`, `format.js` and `charts.js` after `dom-safe.js`.
  5. Remove the duplicate PlantOps `toasts.js` tag and the unused container.
  6. Create `_ProjectHero.cshtml` (tuple model per FE-D07). Pages adopt it in WP-38–42.
- **Acceptance.**
  - The console is clean on all 42 URLs.
  - Batch DataTables works (static probe). PlantOps per D-16.
  - On Home, `typeof window.jQuery === 'undefined'`.

---

#### WP-25 · Data-driven seed · W4 · L
- **Closes.** CX-06, D-07 (OD-3), DOC-04 (seed), and the 22 queries missing a cancellation token.
- **Files.**
  - `Services/UserProfileSeedService.cs`.
  - New `Services/Data/FiberDemoSeedData.cs`.
  - `Golden/UserProfileSeedGoldenTests.cs`: the Properties assertion only, per D-07.
- **Steps.**
  - Follow cx §3.1. Keep the same existence predicates, guards, insertion order and `SaveChangesAsync` boundaries.
  - Forward the cancellation token in the 22 queries.
  - Per D-07, drop the legacy Properties block.
  - Add a one-line "EF InMemory has no transactions" comment.
- **Acceptance.**
  - The golden snapshot is identical, apart from the D-07 change.
  - `SeedForUserAsync` CA1502 ≤ 10; about −1,300 lines.

#### WP-26 / WP-27 / WP-28 / WP-29 · Managed solver and graph splits · W4 · M each
| WP | Scope (cx section) | Extra in this WP | Must not change |
|---|---|---|---|
| WP-26 Fleet | §3.3: `Services/Fleet/{ManagedVrpSolver, FleetRequestValidator, FleetResultBuilder}.cs`; extract-method only | — | Iteration order, first-improvement returns, messages |
| WP-27 Response | §3.4: `Services/Response/ManagedPMedianSolver.cs`; `GreedySeed` and `FindBestSwap` | — | Tie-break comparator (`:321`, `:379`) |
| WP-28 ChangeDetection | §3.5: `Services/ChangeDetection/ManagedChangeKernels.cs`; `ComponentAccumulator` | CX-19: replace the `0.025` literal in `ChangeDetectionController.cs` with `SyntheticChangeScene.DefaultNoiseLevel` (OpenAPI default must be identical; diff it) | Slot order, the `MaxBlobs` text |
| WP-29 SpatialGraph | §3.6: one `SearchShortestPath(…, heuristic)`; `HaversineKm` with identical operation order; `NodeIdSet`; split `ValidateGraph` | Keep-list #30 and #32 comments ("shared instance; never mutate") | The skip semantics from WP-06; `CatRiskService.HaversineKm` stays separate |

For all four:
- **Tests:** the WP-03 golden tests and the existing tests stay unedited. New unit tests go in new files.
- **Native:** run §5.3 with native present (`REQUIRE_NATIVE=1`) and with it disabled.
- **Acceptance:** golden results are identical both ways; every touched member is ≤ 15 on CA1502 and cognitive (`tools/metrics`), or the PR explains why not.
- **Rollback:** revert.

#### WP-30 · `NativeKernelLoader` · W4 · M
- **Closes.** CX-11, CX-12 (`VrpSolverNativeBridge.TrySolveCvrptw`), DOC-04 (the wart comment).
- **Files.** The 13 `Portfolio.Services/Native/*Bridge.cs` files; new `Native/NativeKernelLoader.cs`; new `Tests/Services/NativeKernelLoaderTests.cs`.
- **Steps.**
  - Follow cx §5.3. `TryLoad(name)` keeps the same toggle and `NativeLibrary.TryLoad` flags.
  - `IsNativeInvocationException` keeps the same five types, **including `InvalidOperationException`**, and gets a one-line why-comment.
  - Use field initializers. Keep the 9 `IsAvailable` properties.
  - Add `MapRoutes` to the VRP bridge.
  - No generic `TryInvoke` wrapper.
- **Tests.** A predicate truth table; `TryLoad("no_such_kernel") == false`.
- **Acceptance.**
  - §5.3 run with `REQUIRE_NATIVE=1` green.
  - The CI native-parity job green.
  - About −340 lines.

#### WP-31 · `ParseAddress` decomposition · W4 · M
- **Closes.** The `ParseAddress` row of CX-12.
- **Files.** `Services/AddressStandardizationService.cs`.
- **Steps.** Follow cx §3.7: `ConsumeTail`, `ConsumeHouseNumber`, `ConsumeStreet`, `ConsumeDirectional`, `ConsumeUnit`, `ComputeConfidence`, `BuildStandardized`, sharing a `TokenCursor`. Keep keep-list #52.
- **Acceptance.** The WP-03 AddressParse golden table is identical; CA1502 ≤ 15.

#### WP-32 + WP-33 · Remaining members above 15, and the middleware extraction · W4 · M + S (one PR)
- **WP-32** (cx §3.7 rows):
  - `CatRiskService.ValidateLocations`: `ValidateLocation`, `IsUnitInterval`.
  - `SpatialOverlayService.SpatialJoinAsync`: `ValidateZones`, `ValidatePoints`.
  - `OutageTraceService`, 3 members (keep loop order).
  - `ViewshedService.ComputeManaged`: `IsVisible`; keep the half-up rounding verbatim.
  - `SpatialClusterService.RunDbscanManaged`: `ExpandCluster`, applied after WP-06's dedup.
  - `UserProfileService.CreateOrUpdateFromGoogleAsync`: `TryGetAnonymousProfileIdAsync`, `CreateOrMergeProfileAsync`.
  - Run §5.3 with native present for the CatRisk, Overlay, Outage, Viewshed and Cluster paths.
- **WP-33** (CX-05): extract `TryResolveAuthenticatedAsync`, `TouchProfileAsync`, `CreateAnonymousAsync` and `BuildCookieOptions`. Keep why-comments #19–21. The WP-02 and WP-15 middleware tests stay unedited.

#### WP-34 · Fiber error handling · W4 · S
- **Closes.** CX-15 (latent bug). Decision D-04(a).
- **Files.** `Controllers/Api/{FiberMaterials,FiberOrders,FiberDashboard}Controller.cs` (grep `catch (Exception ex)`); their 3 test files; new `Integration/FiberErrorTests.cs`.
- **Steps.** Delete the 12 catch-all blocks and let the middleware map errors.
- **Tests after.**
  - The direct-call tests now assert that the exception propagates (12 asserts change).
  - Integration, with a test auth scheme registered **in test code only**:
    - An unknown ID on `receive` or `update` → 404 ProblemDetails (today 500).
    - Missing identity → 409.

#### WP-35 · Session and auth-cookie lifetime · W4 · S
- **Closes.** SEC-18, plus SEC-19 if D-11 chooses removal. SEC-17 only if D13 is reversed.
- **Files.** `Startup/PortfolioCookieEvents.cs` and `Startup/AuthenticationRegistration.cs`. Only if session removal is chosen: `Startup/Pipeline.cs`, `Startup/InfrastructureRegistration.cs`, `Pages/Logout.cshtml.cs`.
- **Steps.**
  - Add an `auth_time` claim at sign-in. In `ValidatePrincipal`, call `RejectPrincipal()` when the principal is older than 24 h. Inject `TimeProvider`.
  - Keep the 8 h sliding window.
- **Tests.** A 25-hour-old principal is rejected; a 1-hour-old one is accepted; logout still works.

#### WP-36 · Batch-geocoding job bounds · W4 · M
- **Closes.** SEC-24, SEC-16 (the per-row log), D-06 (code part), ADD-03. Decision D-10.
- **Files.**
  - `Services/BatchGeocodingService.cs` (grep `Task.Run`; the "legacy" comment at `:49`).
  - `Portfolio.Services/InMemoryBatchJobStore.cs`.
  - A new bounded queue abstraction in Portfolio.Services.
  - New `Portfolio.Web/BackgroundServices/BatchGeocodingWorker.cs` (built-in `BackgroundService` plus `Channel`).
  - Its registration in the `Startup` file that registers services.
  - `Controllers/Api/BatchGeocodingController.cs`: 503 with `Retry-After` when the queue is full; remove `[Obsolete]` and the "kept for test compatibility" comment from the **live** sync endpoint.
  - Tests.
- **Steps.**
  - Follow findings-security SEC-WP15: channel capacity 8, 2 workers, the final `UpdateAsync` inside `try/finally`, a 24 h TTL with pruning on write, one summary log per job, `MaxRows` 1,000 for both sync and async.
  - HomeFinder aliases and `/api/batchgeocoding/sync` stay (D-06).
- **Tests.** Queue full → 503 with `Retry-After`; TTL eviction; the summary log is emitted once; 1,001 rows → 400.
- **Acceptance.** coverlet now counts the sync endpoint (coverage must not fall).

#### WP-37 · Static-asset caching · W4 · S
- **Closes.** FE-P01.
- **Files.** `Startup/Pipeline.cs` (`app.UseStaticFiles()`); new `Integration/StaticCachingTests.cs`.
- **Steps.** `StaticFileOptions.OnPrepareResponse`: if the query string has `v`, `public,max-age=31536000,immutable`; otherwise `public,max-age=3600`. Keep the same position in the pipeline. Don't use `MapStaticAssets`.
- **Acceptance.** A test asserts both header values; live `curl -sI` matches.

#### WP-38 … WP-42 · Frontend page batches · W4
The scope is the matching FE-WP6a–e row in findings-frontend §10. On top of that row, each batch:
- moves its page CSS into `@section Styles` with `asp-append-version` (P02);
- adopts `_ProjectHero` (D-07 in the frontend findings), `PortfolioUi.runAction`/`busy`/`announce` (A04), `PortfolioFormat` (D-09g) and `PortfolioCharts` (palettes N4, legends, `describe`);
- turns panel titles into `<h2 class="h6">` (A13);
- replaces any remaining local `escapeHtml` with `PortfolioDom.escapeHtml`;
- fixes the contrast items listed for its pages (FE-A09);
- fixes encoding in the files it touches.

| WP | Pages and files | Additional findings | Size |
|---|---|---|---|
| WP-38 spatial-1 | GeoStream, Geometry, Terrain, Clustering, Viewshed, Overlay (Index, app.js) | FE-A05 (Viewshed observer inputs), A12 (generic `aria-label`s, Terrain output, label-in-name) | M |
| WP-39 spatial-2 | CatRisk, Change, Outage, Response, Fleet (Index, app.js) | FE-A06 (Outage select, `tabindex=-1`), keyboard-operable Response stations, `PortfolioMap`, Change inline styles → classes, A16 | L |
| WP-40 map/form | HomeFinder, StateExplorer (Index, uiManager, app, collectionService, mapManager, featureService), ReverseGeocoding, AddressStandardization, BatchGeocoding, plus their `.css` | FE-A07 (`redirectOn401:false`, sign-in hint), A08 (`esri-theme.js`, names), A11, A12 (nested buttons, emoji names, `prompt()` → modal), D03 (State Explorer recolour, D-09c), D10, T2–T4, T8, T13, T15–T17, the `.form-range` box, Batch `aria-pressed` | L |
| WP-41 Network + PlantOps | Network (Index, app.js, network.css); PlantOps (Index, 4 JS files, css) | `api-fetch` adoption (delete the local `apiPost` and raw `fetch`), `PortfolioMap`, T5/T6, "A*" and Yes/No formats, `--nav-h`; PlantOps: close the `<div>` at `:50`, invalid CSS at `:16`, dark DataTables duplicates, `esri-theme.js`, DOC-05 (`inventory.js:15-19`) | M |
| WP-42 site + Details | Login, Error, Privacy, Accessibility, AccessDenied, Account/Profile (+`profile.css`), `Projects/Index.cshtml` (adopt `_ProjectCard`, keeping both copy variants), 18 Details pages | T1, T19; Details pages drop `gis.css` (`.theme-surface` now in components), `aria-hidden` on icons, `/Projects` link names | M |

- **Acceptance (each):**
  - §5.4 passes on every touched page at 3 widths × 2 themes.
  - The verify blocks of each listed finding pass.
  - Console and network are clean.
  - Hard-coded colour count for the batch's files: the `colors.py` total drops and no file gains literals.
- **Deps.** WP-12, WP-16, WP-18, WP-24, and WP-07 for the files it touched. D-09; D-16 for PlantOps and Profile.

---

#### WP-43 · Controller exception filters (optional) · W5 · M
- **Closes.** CX-14 and CX-19 (`retryAfter` ↔ `BreakDuration`). Decision D-17.
- **Files.** About 17 controllers; new `Portfolio.Web/Filters/{BadRequestOnArgumentExceptionAttribute,ArcGisUnavailableAttribute}.cs`; about 12 controller test files.
- **Steps.**
  - Follow cx §5.2. Apply the attributes **at action level, only where catches exist today**.
  - The 400 `{ error }` body stays, logged under the controller's category.
  - Optionally apply WP-04's `IsOwn` check to the `{ error }` message (default: yes).
  - The 503 `retryAfterSeconds` reads the resilience profile's `BreakDuration`.
- **Tests.**
  - The direct-call tests change to "the exception propagates".
  - Add filter unit tests, and one integration test per filter that asserts the same body as WP-02's pins.

#### WP-44 · JS god-file splits (optional) · W5 · M
- **Closes.** CX-20.
- **Files.** Only files still over 500 lines after WP-38–41, among `HomeFinder/app.js`, `StateExplorer/{uiManager,app}.js` and `Response/app.js`, plus the `Index.cshtml` script tags for split classic scripts.
- **Steps.** Follow cx §3.8. State Explorer uses ES modules. Classic scripts split into files sharing one `window.<Page>` namespace, keeping load order.
- **Acceptance.** §5.4 on the affected pages; no behaviour change.

#### WP-45 · Dead vendor assets and encoding sweep · W5 · S
- **Closes.** FE-P07, FE-D12 (the remainder), SEC-29 (jquery-validation).
- **Files.**
  - Delete: `wwwroot/lib/bootstrap/dist/**`, `lib/jquery/dist/**`, `lib/jquery-validation*/**`, `lib/fontawesome/webfonts/*.ttf`, the README and PLACEHOLDER files.
  - Delete `Pages/Shared/_ValidationScriptsPartial.cshtml`.
  - Delete the dead CSS classes listed in FE-D12.
- **Acceptance.** A full 42-URL sweep shows no 404s and a clean console. The image shrinks by about 10 MB.

#### WP-46 · ArcGIS SRI · W5 · S
- **Closes.** SEC-09. Decision D-15.
- **Files.** `wwwroot/js/esri-theme.js` (the theme hashes); the 4 ArcGIS `Index.cshtml` files (the script tag).
- **Steps.**
  1. Change `src` to `https://js.arcgis.com/4.29/init.js` and add `integrity="sha384-…" crossorigin="anonymous"`.
  2. Set `link.integrity` and `crossOrigin` for each theme before `href`.
  3. Compute the hashes with `curl -s <url> | openssl dgst -sha384 -binary | openssl base64 -A`. This needs the D-15 download OK.
- **Acceptance.** Each page reaches `view.when()`. A deliberately wrong hash blocks loading (manual check, then revert).

#### WP-47 · Test-class structure (optional) · W6 · M
- **Closes.** TST-10 and V8.
- **Files.** `SpatialComputeControllerTests.cs` (5 SUTs), `SpatialComputeServiceTests.cs` (4 SUTs plus 1 controller test), `HotspotViewshedOverlayServiceTests.cs` (3 SUTs); new per-SUT files.
- **Steps.** Pure moves; the test count is identical.
- **Acceptance.** The same 697-plus tests pass, now one class per SUT.

#### WP-48 · Comment and XML-doc trim, batches C1–C12 · W7 · L (5 PRs)
- **Closes.** DOC-06, DOC-07, the rest of DOC-03, ADD-08. Decision D-defaults (dt D4).
- **Scope.** dt §A9 batches:
  - C1 Common, C3 Web other, C4 Services, C5 Native/Data, C6 Repositories, C8 Benchmarks → `cleanup/comments-backend`.
  - C2 Controllers → `comments-api`.
  - C7 Tests → `comments-tests`.
  - C9 cshtml, C10 JS, C11 CSS → `comments-frontend`.
  - C12 C++ → `comments-native`.
- **Steps.**
  - Apply rules R1–R10 (dt §A8) and the keep-list (dt §A7): rewrite the KR rows and add the A rows if they aren't already done.
  - Never touch `RedlandsRoadNetwork.cs`, `**/Migrations/**` or `wwwroot/lib/**`.
- **Acceptance.**
  - Diffs are comment, XML and whitespace only (`git diff -w --word-diff`).
  - The build has 0 warnings (watch CS1573 and CS1591).
  - The OpenAPI diff for C1 and C2 shows only deliberate description changes.

#### WP-49 · Documentation hygiene · W7 · M
- **Closes.** DH-01, DH-04, DH-05, DH-06, DH-07, DH-08 (the doc parts), DH-09 (final), ADD-09, FE-D13 (section-count docs).
- **Files.**
  - `.github/copilot-instructions.md`: slim it to about 80 lines of invariants plus a pointer to agent-context (D-18).
  - `README.md`: the `portfolio_v2` ×3 → `gis-portfolio`, port 5000, 6 projects, no hard-coded test counts or static badge.
  - `docs/render-deployment.md`: repo name, the pre-deploy `--migrate` step, the auto-deploy trade-off.
  - `docs/plans/README.md`: a "historical" banner plus the 9 fixes.
  - `wwwroot/css/README.md`.
  - `docs/resume-update.md`: doc facts only; résumé wording is the owner's.
  - `docs/agent-context.md`, final state:
    - the "compute" policy;
    - CSP;
    - identity rules;
    - the session decision;
    - `Startup/*`;
    - `tools/` commands in §7;
    - the test conventions decision (Mock fields for controllers, inline SUTs for compute services);
    - the 8-section and 6-section Details templates.
- **Acceptance.** Every statement is checked against the source. No stale count remains.

#### WP-50 · Quality gates · W8 (last) · M
- **Closes.** CX-16, CX-13, CX-17, CI-01, CI-06 (floors), ratchet raise. Decision D-13.
- **Files.**
  - New `CodeMetricsConfig.txt` (`CA1502: 15`).
  - `Directory.Build.props` (`AdditionalFiles`).
  - `.editorconfig`:
    - `CA1502` warning;
    - Migrations `none`;
    - `generated_code = true` for `RedlandsRoadNetwork.cs` (analyzers only);
    - Benchmarks `none`;
    - remove `end_of_line = crlf` per D-13.
  - `GlobalSuppressions.cs` per project, for residual violators only: the 4 data generators permanently, with a justification.
  - `ci.yml`: a format step; `MIN_LINE_COVERAGE` and `MIN_BRANCH_COVERAGE` raised to floor(measured)−1; a Web floor.
  - The files touched by `dotnet format`.
- **Steps.**
  1. CA1502 configuration; fix or suppress residual violators.
  2. `dotnet format style --diagnostics IDE0005 --severity info --exclude Portfolio.Repositories/Migrations Portfolio.Services/Data/RedlandsRoadNetwork.cs`.
  3. Run `dotnet format` with the same excludes.
  4. CI step: `dotnet format Portfolio.sln --verify-no-changes --no-restore --exclude …`.
  5. Measure coverage and set the new ratchets and the Web floor (never below 80/60).
- **Acceptance.**
  - CI green.
  - A temporary method with CA1502 16 fails the CI build (revert it).
  - `--verify-no-changes` exits 0 on a fresh LF checkout.

---

#### WP-51 · Enforce the CSP (owner-gated) · S
- **Prerequisites.** 1–2 weeks of production Report-Only with only the filtered ArcGIS eval reported, plus the owner's sign-off.
- **Steps.** Emit the §2.4 policy as `Content-Security-Policy`, merged with the old directives, and keep reporting. Run the headless probe on every page under **enforcement**.
- **Rollback.** Switch back to Report-Only (one line).

#### WP-52 · Host filtering and forwarded-header trust (owner-gated) · S
- **Prerequisites.** The owner's 5-minute production check (findings-security §4.5) and D-12.
- **Files.** `appsettings.Production.json` (`AllowedHosts`, `ForwardedHeaders:*`).
- **Steps.** Use the hosts the owner supplies, `KnownNetworks` set to the observed proxy CIDR, and `ForwardLimit` 1 (or 2 if a Cloudflare hop is appended). Never raise `ForwardLimit` without `KnownNetworks`.
- **Tests.** A Production-config factory test: `Host: evil.example` → 400.
- **Acceptance.** Immediately after the deploy, `curl -I` on every host returns 200.
- **Rollback.** Revert at once if a host returns 400.

#### WP-53 · Native ABI hardening (deferred by default) · L
- **Scope.** findings-security SEC-WP16: length parameters, 64-bit size arithmetic, guards on float→int conversion, status codes for scoring, DBSCAN dedup on the native side.
- **Constraint.** Header, C# struct/interop/bridge, native source and parity tests change together. Run §5.3 and the CI native job.

---

## 4. Decisions needed from the owner

The list is sorted by how much work each decision blocks. **If you say nothing, the default goes ahead.**

| ID | Question | Options | Default | Blocks | If silent |
|---|---|---|---|---|---|
| **D-01** | Add `Microsoft.AspNetCore.Mvc.Testing` (test-only) and host-level tests with an EF InMemory swap and no real DB? (sec D16, cx OD-1, dt D1) | Yes / No (unit tests only) | **Yes** | WP-01 and ~20 later WPs | Proceed |
| **D-02** | Security limit values (sec D1, D2, D3). Sub-items follow the table. | Accept / adjust per item | **Accept** | WP-06, 08, 14 | Proceed |
| **D-09** | Visible UI changes within the current look (fe D1, D2, D4, D5, D6, D8). Sub-items follow the table. | Accept all / per item | **Accept** | WP-12, 17, 38–42 | Proceed |
| **D-04** | Bug fixes that change public behaviour. Sub-items follow the table. | Per item | **Yes to all** | WP-04, 06, 14, 16, 34 | Proceed |
| **D-14** | Commit the measurement and audit tools under `tools/metrics` and `tools/fe-audit` (not in the .sln, not served, CPM opt-out, paths scrubbed)? (fe D11, cx §12) | Yes / keep outside the repo | **Yes** | WP-13, and per-WP complexity reporting | Proceed |
| **D-03** | CSP: nonce policy, Report-Only for at least 1–2 weeks, then enforce; add the public `/csp-report` route (anonymous, 8 KB, 30/min, hidden from OpenAPI)? (sec D4) | Yes / hashes / no report route | **Yes** | WP-20, WP-51 | Proceed (enforcement waits for sign-off) |
| **D-05** | Identity behaviour: the anonymous cookie is no longer honoured for linked profiles; visitors are seeded at sign-in, not first visit; 10 new identities/min/IP; no identity creation on `/api`; Profile shows 8 characters of the ID (sec D11) | Yes / variants | **Yes** | WP-15 | Proceed |
| **D-08** | Honesty edits to site copy (withdrawn figures, SIMD/AVX wording, Typeahead claim, Batch Details) and native/README 9/5/7. You review wording in the PR. (dt D5) | Yes / edit wording | **Yes** | WP-11 | Proceed |
| **D-13** | Remove `end_of_line = crlf` from `.editorconfig` before the format gate (index is already LF; Windows keeps CRLF through autocrlf) (dt D3) | Remove / `.gitattributes eol=crlf` | **Remove** | WP-50 | Proceed |
| **D-07** | The seeder re-inserts 40 legacy Home Finder listings that the 2026-07-19 migration deleted (verified). (cx OD-3) | Remove the block (production rows stay) / also add a new `dotnet ef` data migration to delete them / keep | **Remove block, no data migration** | WP-25 | Proceed |
| **D-06** | Legacy endpoints: `POST homefinder/score`, `GET homefinder/properties/{id}`, `POST /api/batchgeocoding/sync`. The *live* `POST …/geocoding/batch/sync` keeps its route but loses `[Obsolete]`. (cx OD-4/5/6, dt D6) | Keep all / remove the aliases after checking Render logs | **Keep all** (public-facing) | WP-36 | Keep |
| **D-10** | Batch geocoding: `MaxRows` 1,000 (sync and async; samples are 10–12 rows), queue of 8, 2 workers, 503 with `Retry-After` when full (sec D21) | Accept / adjust | **Accept** | WP-36 | Proceed |
| **D-11** | Sessions: keep `AddSession`/`UseSession` (pipeline constraint) or remove them (SEC-19); add a 24 h absolute auth lifetime with 8 h sliding kept (records a deviation from STIG V-222389) (sec D14, cx OD-10) | Keep + 24 h / remove session / neither | **Keep session; add 24 h** | WP-35 | Proceed |
| **D-16** | Verifying sign-in-only pages (PlantOps, Profile) | Scratch-only Development test-auth handler in a throwaway worktree, never committed / owner runs the fe-audit checks | **Scratch-only test auth** | Verification of WP-07, 24, 41, 42 | Proceed |
| **D-17** | Controller exception filters (CX-14): replace about 45 catch blocks (−230 lines; about 35 tests rewritten) | Do (last code wave) / skip | **Do** | WP-43 | Proceed |
| **D-12** | `AllowedHosts` list and forwarded-header trust: needs your §4.5 production check and the Render hostname (sec D8, D9) | Supply values / defer | — | WP-52 | WP-52 not done (`AllowedHosts` stays `*`) |
| **D-15** | ArcGIS SRI, plus permission to download `init.js` (1.48 MB) and two theme CSS files from js.arcgis.com to compute hashes (sec D5) | Yes / no | **Yes** | WP-46 | Proceed |
| **D-18** | `copilot-instructions.md`: slim it to a pointer plus invariants, or patch it in place (dt D7) | Slim / patch | **Slim** | WP-49 | Proceed |
| **D-19** | Commit `docs/agent-context.md`, `docs/cleanup/*` and `docs/cleanup-prompt.md` | Yes / not the prompt | **Yes** | WP-00 | Proceed |
| **D-20** | Turn on CodeQL default setup and secret-scanning push protection (repository settings; your action) (sec D20, dt CI-04) | Yes / workflow file / no | **Yes, you toggle it** | — | Not done |

**D-02 sub-items** (sec D1, D2, D3):
- (a) A "compute" tier of 60/min/IP on the 7 compute controllers, plus a global compute concurrency limit of max(2, CPUs) with a queue of 16. Features moves to "expensive".
- (b) Viewshed and terrain sides ≤ 512; heatmap cells × points ≤ 25M; GeoStream grid ≥ 1e-6; CatRisk insured value ≤ 1e12 and radius ≤ 5,000 km; DBSCAN seed dedup.
- (c) A 1 MB Kestrel default; address 16 KB; claims 8 KB; collections 4 KB; fiber 64 KB; HomeFinder 16 KB; saved feature = the measured geometry cap + 64 KB (NEW-03).
- (d) Persistence caps: claims ≤ 1,000 characters and ≤ 50 per user; hex colours; a shipment status allowlist, lengths, and a route of ≤ 1,000 points; saved search name ≤ 200 and ≤ 50 IDs; address ≤ 1,000 characters.

**D-09 sub-items** (fe D1, D2, D4, D5, D6, D8):
- (a) A 3 px green keyboard focus ring.
- (b) Darker form-control borders (`#7a8699`).
- (c) State Explorer recoloured to brand and status tokens.
- (d) Hero text at .95 opacity and a dark tint behind the contact links.
- (e) Carousel option A: a pause button, manual scroll-snap on focus or reduced motion, the seam fix.
- (f) State Explorer keeps anonymous visitors on the page, with collections disabled and a sign-in hint.
- (g) Number formats: 1 decimal for %, km and min (e.g. 2.42 → 2.4 min); compact money; Yes/No for the native flag; humanised enum names.

**D-04 sub-items:**
- (a) Remove the Fiber catch-alls: an unknown ID returns 404 instead of 500, a missing identity returns 409, and the 500 body text changes.
- (b) Managed Dijkstra and A* skip edges to undeclared nodes: 404 becomes 200, matching native.
- (c) Framework-originated error text becomes generic; oversized bodies return 413 instead of 500.
- (d) `DELETE users/me/claims/{protected}` returns 400.
- (e) `apiFetch` shows the ProblemDetails `detail` and a 429 message.

### 4.1 Defaults applied unless you object
**Security posture:**
- Leaflet stays on unpkg with its verified SRI (sec D6).
- OpenAPI and Scalar stay public (SEC-20, sec D7).
- HSTS: 1 year, no `includeSubDomains`, no preload (sec D10).
- The anonymous-profile retention purge is deferred (sec D12).
- The Data Protection key ring stays unencrypted; document that the Upstash credentials are equivalent to the keys (sec D13).
- The committed `Password=postgres` default is removed and the `DATABASE_URL` fallback is fixed. A fresh clone then needs `appsettings.Development.json` (sec D15).
- No CSRF custom header (SEC-21 accepted, sec D19).
- Native ABI hardening is deferred (sec D17).
- `/health/ready` keeps its rate-limit opt-out and gains a 10 s cache.
- The graph skips undeclared edges.
- The PlantOps and HomeFinder local toast overrides are deleted.

**Dependencies and dead code:**
- Delete the unused libraries: typeahead, jquery-validation and unobtrusive, `_ValidationScriptsPartial`, `bootstrap/dist`, `jquery/dist`, Font Awesome `.ttf` fallbacks, placeholders (sec D18, fe D10).
- Remove `AddMemoryCache()` if the boot test stays green (cx OD-10).
- Untrack the `.user` file (cx OD-12).
- `global.json` pins SDK `10.0.100` with `rollForward: latestFeature`.
- The Response `maxIterations` cap stays at 500 (clamped).

**Coverage, complexity and tests:**
- Don't exclude the generated network file from coverage (cx OD-7).
- CA1502 gate at 15; cognitive complexity reported per PR, not gated; no SonarAnalyzer (cx OD-8).
- Managed mirrors are refactored by extract-method only; C++ is unchanged (cx OD-9).
- The 4 data generators are permanently exempt (CX-13); the Benchmarks project is exempt from CA1502.
- Ratchets are raised at the end to floor(measured)−1, never below 80/60, with a Web floor.
- Repository tests on EF InMemory are skipped (dt D2).
- Test conventions: Mock fields for controller tests, inline SUTs for pure compute services.

**Docs and frontend:**
- OpenAPI-visible XML: keep the facts; the 13 Common summaries may keep two sentences; delete the 7 trivial `<param>`s (dt D4).
- Card copy: share `_ProjectCard` between Home and `/Projects` and keep both copy variants (fe D3).
- No dark map tiles (fe D7). Keep D3 and Font Awesome (fe D9).
- JS splits only for files still over 500 lines after the FE batches.

**Process:** one PR per WP (WP-32 and WP-33 share one, and the comment batches use 5 PRs). Merge in wave order with merge commits.

---

## 5. Verification protocol

### 5.1 Gates for every code WP (PowerShell, from the WP worktree)
```powershell
dotnet restore
dotnet build -c Release --no-restore -p:ContinuousIntegrationBuild=true        # 0 warnings, 0 errors
dotnet test Portfolio.Tests -c Release --no-build --collect:"XPlat Code Coverage" `
  --settings coverlet.runsettings --results-directory TestResults\<wp>
py -I tools\metrics\coverage_gate.py TestResults\<wp> --min-line 80 --min-branch 60   # before WP-13: the inline script in ci.yml
$env:PORTFOLIO_DISABLE_NATIVE="1"; dotnet test Portfolio.Tests -c Release --no-build; Remove-Item Env:PORTFOLIO_DISABLE_NATIVE
```
1. All tests pass in both runs.
2. Coverage is at or above 80/60 **and** no more than 0.5 pt below the wave base. Report both numbers in the PR.
3. For C# WPs, put a complexity table for the touched members (before and after, from `tools/metrics/csmetrics`) in the PR.
4. Self-review against the WP's acceptance criteria. `git diff --stat` must list only files the WP allows.
5. **Independent review.** A fresh agent receives only the diff, §C and the WP's acceptance criteria. It checks:
   - scope;
   - unlisted behaviour changes;
   - that tests-first commits are present;
   - exception-message identity;
   - secrets;
   - honest claims;
   - encoding and line endings;
   - the pipeline order.
   Fix what it finds and re-run the gates.
6. Commit with the WP ID, push the branch, and open a PR against the wave base (§3.1).

### 5.2 Backend and security specifics
- **Characterization before change.** Commit 1 (tests only) is green on the old code. Later commits may change only the assertions the WP lists.
- **Live Kestrel checks** for WP-04, 14, 20 and 37, because TestServer does not enforce Kestrel limits (NEW-04).
  - Run `dotnet run --project Portfolio.Web --no-launch-profile --urls http://localhost:51xx` with `ASPNETCORE_ENVIRONMENT=Development` and `RunMigrationsOnStartup=false`.
  - Use a scratch database: a connection string pointing at `Database=portfolio_stage2` on localhost:5432, derived from the dev file in memory and **never printed**.
  - With `curl`, check:
    - 413 for oversized bodies;
    - the 429 thresholds;
    - `curl -sI` header values: `Server` absent, CSP and Report-Only, HSTS, `Cache-Control`.
- **`Program.cs` and `Startup` WPs (05, 14, 21, 35–37):**
  - `PipelineOrderTests` and the DI snapshot pass.
  - Review `UsePortfolioPipeline` line by line against agent-context §4.
  - Keep the startup log order.
- **OpenAPI diff** for any change that can affect the document: WP-14, 20, 23, 28, 43, and WP-48 C1/C2.
  - Recipe: findings-docs-tests §1. Fetch `/openapi/v1.json` before and after.
  - Only the intended differences may appear.
- **Exception messages.** The WP-02 body pins must stay green, unless the WP lists a deliberate change.
- **Identity WPs** use unit and integration tests only; never real Google.
- **ArcGIS geocoding:** no outbound geocoding (Batch, Reverse, Address validate) without the owner's OK. Use fake `DelegatingHandler`s.

### 5.3 Native (any change to `Native/**` or `native/**`, any service that dispatches to a bridge, and WP-03)
```powershell
$cmake = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$b = "$env:TEMP\b\<wp>"; dotnet build -c Debug
$i = 0; Get-ChildItem native -Directory | ForEach-Object { $i++; $d = "$b\k{0:d2}" -f $i
  & $cmake -S $_.FullName -B $d -DCMAKE_BUILD_TYPE=Release; & $cmake --build $d --config Release }
Get-ChildItem $b -Recurse -Filter *.dll | Where-Object FullName -match '\\Release\\' |
  Copy-Item -Destination Portfolio.Tests\bin\Debug\net10.0
$env:PORTFOLIO_REQUIRE_NATIVE="1"; dotnet test Portfolio.Tests -c Debug --no-build; Remove-Item Env:PORTFOLIO_REQUIRE_NATIVE   # after WP-10
$env:PORTFOLIO_DISABLE_NATIVE="1"; dotnet test Portfolio.Tests -c Debug --no-build; Remove-Item Env:PORTFOLIO_DISABLE_NATIVE
```
- Expect 13/13 kernels to build, and every test to pass in both runs.
- Golden and parity tests must give identical results both ways.
- The CI `native-parity` job (Ubuntu/GCC) is the authority. Check it on the PR.
- Never add a coverage gate to the native run.

### 5.4 Frontend
- **Setup.**
  - Run the WP worktree as in §5.2 (scratch DB, alternate port).
  - Copy `tools/fe-audit/{fe-audit.js,grad.js}` into `Portfolio.Web/wwwroot/__audit/`. Never commit them; delete them afterwards.
  - Open the page with `preview_start {url}`. **No screenshots.**
  - The browser pane is shared, so FE WPs verify one at a time.
- **Per touched page,** at 360×800, 768×1024 and 1280×800 (via `resize_window`):
  - Run `(await import('/__audit/fe-audit.js?v=N')).run({wait:1500})`, adding click variants for result states (button IDs in findings-frontend §11). The run measures light and dark itself.
  - `assert(r)` must return `['PASS']`. Allowlist `esri-view-surface` until WP-40.
- **Focus.** After one real `Tab` key, `focusIndicators()` must show none = 0 and weak = 0 (after WP-12).
- **Console and network.** `read_console_messages {onlyErrors:true}` must be empty. `read_network_requests` must show no 4xx/5xx on localhost.
- **Per-finding checks.** Run each finding's own Verify block in findings-frontend §4–§8.
  - Keyboard checks use real keys: skip link, Run actions, carousel.
  - Reduced motion via `SitePreferences.set('motion','reduce')`.
  - Hero contrast via `grad.js`.
- **Compare against the wave base.** Run the same script on the wave base and report the deltas.
- **Auth pages** per D-16. **ArcGIS geocoding pages:** static probes only, unless the owner approves calls.
- **Reset** the viewport to `desktop`, clear storage, and stop the server.

### 5.5 Docs, comments and XML docs
- `git diff -w --word-diff` must show only comment, XML-doc or whitespace changes (rule R10). The build must have 0 warnings.
- An OpenAPI diff is required for Common or controller XML changes.
- Honour the keep-list (findings-docs-tests §A7).
- Never touch the generated file, Migrations or `wwwroot/lib`.
- Documentation facts are re-measured, or the hard-coded count is removed (ADD-09).

### 5.6 CI
- The PR's own workflow run is the test, and every job must be green.
- **Action SHAs:** check each against its tag with a read-only `git ls-remote https://github.com/<owner>/<repo> refs/tags/<tag>`.
- **New scheduled workflows:** the owner triggers `workflow_dispatch` once after the merge.
- **WP-50:**
  - `dotnet format --verify-no-changes` exits 0 locally.
  - A temporary CA1502 violation fails the CI build (then revert it).
  - The new ratchets equal floor(measured)−1.

---

## 6. Additional recommendations (out of scope unless low-risk)
| Item | Effort | Impact | Notes |
|---|---|---|---|
| Upgrade ArcGIS Maps SDK 4.29 (March 2024) to a current release | L | Medium (security posture, features) | Breaking API changes; re-run the CSP probe |
| Major bumps: actions (checkout v7 etc.), xunit.v3 4.x, Scalar 2.18, Asp.Versioning 10.2.1, DataTables 2.x | S–M each | Low–medium | Dependabot raises PRs; review individually |
| Anonymous-profile retention purge (profiles without `google_id`, inactive > 90 days) | M | Medium (data growth) | Needs a hosted service or a Render cron (sec D12) |
| Encrypt the Data Protection key ring at rest | M | Low | Certificate management; a wrong certificate logs everyone out |
| Repository tests on EF InMemory (TST-11) | M | Medium (Repositories coverage 47.8%) | InMemory semantics differ from Npgsql |
| Union coverage report for the native run (CI-07) | M | Low | Report only, no gate |
| C++ warning flags, report-only first (CI-08) | S | Low | 13 CMakeLists |
| Re-implement the D3 charts with `PortfolioCharts` (−280 KB on 2 pages) | M | Medium (performance) | fe D9 option |
| Font Awesome SVG sprite or subset (−277 KB of fonts) | M–L | Medium | fe P06 |
| Dark map tiles | S | Low (visual) | fe D11 |
| Have the Batch demo use the async job API | M | Product | Today the async endpoints have no UI caller |
| Exclude the generated network file from coverage and re-baseline honestly (OD-7) | S | Honesty of the metric | Only once real coverage is ≥ 80% without it |
| `__Host-` cookie prefix | S | Low | Logs everyone out once |
| `HomeFinderController.cs:42` unreachable `ModelState` branch: align the 400 shape (ADD-10) | S | Consistency | The visible error shape changes |
| Chiseled runtime image and a Dockerfile `HEALTHCHECK` | M / S | Low | `libgssapi` is needed |
| Self-host Leaflet | S | Low | SRI already verified |

## 7. Explicitly out of scope, or not verified
| Item | Why not verified | How to check |
|---|---|---|
| Render's X-Forwarded-For hop layout (SEC-10) | Needs spoofed requests to production | The owner's 5-minute check, findings-security §4.5; then D-12 / WP-52 |
| ArcGIS SRI hashes | Needs file downloads | D-15, WP-46 |
| PlantOps and Account/Profile (auth-only) | Agents may not sign in | D-16 (scratch test auth) or owner-run fe-audit |
| State Explorer populated state (`/api/v1/features` returned 500 in the frontend agent's run, `System.Net.Http.Json`; likely environmental) | Mid-run runtime swap | WP-40: load the page and expect `#statTotal` ≠ 0 |
| Real saved-feature `GeometryJson` sizes (NEW-03) | Needs ArcGIS calls | WP-08 step 2 measures them |
| `/scalar` and the Google profile-picture host under the target CSP; visual tile rendering under an enforced CSP | Not loaded under the policy; no screenshots | Report-Only soak (WP-20); headless probe; owner visual check before WP-51 |
| Render settings: health path, pre-deploy `--migrate`, `RunMigrationsOnStartup`, auto-deploy, deploy hook | Dashboard only | Owner, before WP-49 finalizes docs |
| Production Home Finder row count (D-07); external callers of the legacy endpoints (D-06) | No production DB or log access | Owner: a count query; Render logs |
| ArcGIS popups and the Batch results table at 360 px | External ArcGIS calls | Owner-approved run, or static probes |
| Animations in a visible window; OS-level `prefers-reduced-motion`; 320 px reflow and 150% text; screen-reader output | Hidden pane; no AT | Owner or manual (NVDA); CDP emulation if available |
| Linux/GCC native build | Reproduced on Windows/MSVC only | CI `native-parity` job |
| Change Detection "4 of 4 recovered" | Computed in the browser | WP-03 asserts the overlap server-side |
| Résumé PDF wording; Docker/kubectl availability locally; CodeQL eligibility (the repo is public, so likely eligible) | Not checked | Owner |
| Exhaustive secret scan (only a pattern scan of history was run) | gitleaks/trufflehog not run | Optional owner run |
| Production CPU and memory costs (measured on an i5-13600K) | Render instance is smaller | Production monitoring after WP-14 |
| `Microsoft.AspNetCore.Mvc.Testing` 10.0.x restore | The probe used 8.0.11 offline | WP-01 restore (needs network) |

---

## Appendix A. Spot-check record (planner, against HEAD `9d9ea02`)
**Confirmed in the source**, with the evidence I looked at:
| Item | Evidence checked |
|---|---|
| SEC-01 | `ValidateRasterShape` caps cells only; `ThrowIfCancellationRequested` runs per row; the heatmap has no cells×points budget; DBSCAN `seeds.AddRange(neighbors)` with no dedup. Exactly 9 of 25 controllers have `[EnableRateLimiting]`; the 7 compute controllers and Features don't. |
| SEC-02 | No `ConfigureKestrel`, `MaxRequestBodySize` or `RequestSizeLimit` in `Program.cs`; none on the address actions; no length cap in `ParseAsync`/`ParseAddress`. |
| SEC-03 | `AnonymousUserMiddleware.cs:120` seeds every new HTML visitor. |
| SEC-04 | The cookie path checks only that the profile exists (`:75-99`). The Google merge adopts without checking for `google_id`. `SetClaimAsync` is an upsert. `Profile.cshtml:72` shows the GUID. |
| SEC-05 | DataTables `data:` rows with no `render`, carrying `r.originalAddress` and `matchedAddr`. |
| SEC-06 | `shipments.js:49` interpolates `'${row.status}'` inside `onclick`. |
| SEC-08 | The CSP string is the minimal one. |
| SEC-10 | `KnownIPNetworks.Clear()` and `KnownProxies.Clear()`; `ClientKey` uses `RemoteIpAddress`. |
| SEC-11 | `"AllowedHosts": "*"` in both appsettings files. |
| SEC-12 | `ex.Message` is returned for Argument, KeyNotFound, InvalidOperation and UnauthorizedAccess; no `HasStarted` guard; `distances[edge.To]` and `gScore[edge.To]`. |
| SEC-13 | `ReverseGeocodingService` range checks let NaN through; GeoStream's per-event range check passes NaN. |
| SEC-15 | `BadHttpRequestException` falls through to the default arm. |
| SEC-16 | `{connectionString}` in both helper messages. |
| SEC-19 | `HttpContext.Session` is used only in `Logout.cshtml.cs:14`. |
| SEC-23 | `Uri.EscapeDataString(layerId)` leaves `..` intact. |
| SEC-25 | No `USER`; tag-only `FROM`. |
| SEC-30 | `Viewshed_Compute` has no elevation length parameter. |
| SEC-31 | `appsettings.Production.json` sets `DefaultConnection` to `""`, so `??` never falls back. |
| CX-01 | No WebApplicationFactory, TestServer or Mvc.Testing in the tests. The probe clone's diff touched only `Directory.Packages.props` and `Portfolio.Tests.csproj`. |
| CX-02 | The helpers are top-level local functions (`Program.cs:675-812`). |
| CX-03 | Confirmed dead or unused: `_clientRepo`, `_timeProvider`, both unused `_logger` fields, the 4 regexes, the write-only `GoogleProfileDto.AnonymousUserId`, `Projects/Details.cshtml.cs` with no `.cshtml`, `ProfileClaimTypes.Protected`, 6 `IsFinite` helpers, 3 shadowed `AddScoped`, `xunit` 2.9.3, the `<Folder>` items, InMemory in the Repositories csproj, `IMemoryCache` unused. |
| CX-15 | `KeyNotFoundException` inside `catch (Exception)` → 500 in `ReceiveStock` and `Orders.Update` (which declares 404). |
| OD-3 | The migration runs `DELETE FROM "Properties"` and inserts the curated set. The seeder's streets ("120 Franklin Ave" and others) appear 0 times in `RedlandsPropertySeedData`. |
| OD-5 | `batchSync` in `api-config.js:24` is the demo's call, and the endpoint carries `[Obsolete]`. coverlet excludes `Obsolete`. |
| DOC-01 | The fetched `dt/run/openapi-v1.json` (325,283 B, 56 paths): 24 of 24 request-body descriptions are token text. There are 42 token `<param>` lines. In the fix experiment there are 0 of 24; the only other schema difference is the `<remarks>` experiment, which also confirms that property remarks are dropped. |
| DOC-02 | `Program.cs:397-402`, `AnonymousUserMiddleware.cs:13` and `SpatialGraphService.cs:443-446` are wrong. |
| TST-02 | No tests reference SpatialCluster, SpatialOverlay or Viewshed controllers. |
| TST-08 | 12 `if (!…IsAvailable)` guards. |
| DH-01 | `copilot-instructions.md:783` says "bounding-box vertex clamp"; the managed code is Sutherland–Hodgman. |
| DH-02 / DH-03 | SIMD copy at `Index.cshtml:283, 660` and `Projects/Index.cshtml:178`. Re-counting native/README's table gives 9/5/7 at ±15%; `:314-315` says 8/8/5. |
| DH-09 (sample) | Wrong `Items` key; the "expensive" count. |
| `README.md` / `render-deployment.md` | `portfolio_v2` at README:444, 445, 530 and render-deployment:49; the remote is `gis-portfolio`. |
| FE-A01 | Bootstrap `.btn:focus-visible{…outline:0…}` and `.nav-link:focus-visible{outline:0…}` beat `:focus-visible`. |
| FE-A02 | `preventDefault` with no focus move; a cp1252 byte at `:21`. |
| FE-A03 / FE-C01 | `translateX(-50%)` with a 1.5 rem gap; 36 cards; no JS uses the copies; `copyEmailContact` has no caller. |
| FE-A04 | `setBusy` disables the buttons, ×11. |
| FE-A05 | `onCellClick` calls `run()`. |
| FE-A06 | `role=button` and `tabindex=0` on the hit lines. |
| FE-A07 | `apiFetch` redirects on 401 before `collectionService` checks `res.ok`. |
| FE-A08 | Only HomeFinder and PlantOps load `esri/themes`. |
| FE-A11 | Keys are written into `aria-label`. |
| FE-A12 | Nested buttons. |
| FE-A14 | `okBtn.focus()`; PlantOps loads `toasts.js` twice. |
| FE-D07 | `calc(100vh - 62px)`. |
| FE-D10 | The `.form-range` dark rule is declared twice; `var(--accent)33` is invalid. |
| FE-D12 | PlantOps has 63 opening and 62 closing `<div>`s; it is the only page with a mismatch. |
| FE-P01 | `UseStaticFiles()` with no options. |
| FE-P03 | jQuery and DataTables are used only by AddressStandardization, BatchGeocoding and PlantOps. |
| Viewshed UX | Every cell click triggers a compute (supports the "compute" tier). |
| Response UI | Sends `maxIterations: 50`; the service clamps with `Math.Min`. |

**Corrected or dropped:**
1. **TST-06** "needs `public partial class Program`": dropped.
2. **SEC-WP4** characterization ("Properties already exist after migrations"): false. Replaced (WP-15).
3. **CX-10** "preserve the `KeyNotFoundException`": superseded by the skip. Native already drops these edges (`spatial_graph_engine.cpp`).
4. **SEC-WP2 step 9** (remove the `/health/ready` opt-out): changed to a cache.
5. **sec D2** Response `maxIterations ≤ 100`: dropped (clamped).
6. **WP0 host characterization** of oversized → 500 and graph → 404: not pinned, because both change in W1.
7. **The 12 `showAlert` copies:** 14 files define it, 12 with the same shape.
8. **The "vulnerability check is redundant" claim:** scheduled-only.

**Added (planner):**
| ID | Finding |
|---|---|
| NEW-01 | Protected-claim deletion through `UserProfileController.RemoveClaim` |
| NEW-02 | Managed/native parity gap for undeclared graph edges |
| NEW-03 | `GeometryJson` is the full state polygon; measure before capping |
| NEW-04 | TestServer doesn't enforce Kestrel body limits |
| NEW-05 | The CI `pull_request` filter excludes stacked PRs |
| NEW-06 | A cookie with an unknown GUID creates a profile for any `Accept` header |
| NEW-07 | The Terrain Details AVX2 claim is false |
| NEW-08 | The HomeFinder Details "AVX2 vector lanes" claim |

**Not re-measured.** I relied on the agents for:
- timing and memory figures (sec A.4, A.6);
- coverage XML values;
- complexity scores (from the calibrated tool);
- contrast ratios and live a11y counts;
- the CSP headless-probe results;
- the Leaflet SRI hashes;
- the measured golden values.
