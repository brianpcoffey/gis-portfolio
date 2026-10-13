# Findings: comments and XML docs (Goal 4), tests and CI (Goal 6), documentation hygiene (Goal 7)

Stage 1, read-only analysis. Repo HEAD `9d9ea02`, analysed 2026-10-12. All builds and tests ran in scratch clones under the session scratchpad (`.../scratchpad/dt/{repo,fmt2,exp}`), never in the repo. Every number below comes from a command that was actually run; section F lists what could not be verified.

---

## 0. Headline numbers

| Metric | Value | How measured |
|---|---|---|
| Tests | **697 executed, 697 passed, 0 skipped**; 609 `[Fact]`/`[Theory]` attributes (580 + 29), 117 `[InlineData]` rows | `dotnet test Portfolio.Tests --no-build -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings` (CI's test step) |
| Coverage, CI gate (Release, managed only) | **83.65% line (12,172/14,551), 67.36% branch (2,134/3,168)**; gate 80/60 passes | CI's inline Python gate (ci.yml:82-114), extracted verbatim, run with `py` |
| Coverage by assembly | Common 35.00%/0%; Repositories 47.80%/47.29%; **Services 90.73%/75.91%**; **Web 43.23%/23.60%** | cobertura per-package rates |
| With all 13 kernels present (Debug) | 77.99% line / 51.54% branch, **below the 80/60 gate**; union with the managed run is 87.72%/69.49% | §B3 |
| Native-parity job reproduced (Windows, MSVC 14.44) | 13/13 kernels built and staged; 697/697 pass with native present and again with `PORTFOLIO_DISABLE_NATIVE=1` | §B3 |
| `dotnet format --verify-no-changes` | **Fails.** 118 files need changes (migrations excluded): whitespace in 42, final newline in 63, import order in 35, BOM in 4. On a Linux (LF) checkout, `end_of_line = crlf` adds **41,219 ENDOFLINE fixes in 319 files** | §B7 |
| Comment units (non-XML) | 2,618 units / 4,067 lines in 445 files: banners 404, high-confidence restating 44, labels and step labels 415, `//` member descriptions 39, commented-out code **1**, TODO **1** (stale), AAA test markers 758 | §A1 |
| XML doc blocks | 1,046; multi-sentence `<summary>` 54; trivial `<param>` 49 (42 of them `cancellationToken`/`ct`); trivial `<returns>` 7 | §A5 |
| **Published OpenAPI bug** | **24 of 24 documented request bodies are described as "Cancellation token."** (or a variant). The fix was verified | §A5.3 |
| Keep-list | **52 entries** (40 keep, 8 keep but rewrite because currently wrong or stale, 4 add-missing) | §A7 |
| Stale statements | copilot-instructions.md **50**; agent-context.md **16**; README **11**; native/README **8**; docs/plans/README **9**; other docs **10** | §C |
| Speedup verdict | **agent-context.md is right**: 0.37×–2.26×, 9 win / 5 lose / 7 tie at ±15%. "0.32×" appears in no committed file in any revision. "Loses on 8 of 21" comes from native/README.md:314-315 and matches no threshold applied to the table. Five Details pages publish withdrawn ad-hoc figures | §C4 |

---

## 1. Environment notes (affect reproducibility)

- **The .NET SDK changed mid-session.** A Visual Studio Installer run (setup.exe started 11:27:44) removed `10.0.400-preview.0.26322.102` (by 11:29:42 only its `Roslyn` folder remained, held open by a VBCSCompiler server) and installed **10.0.401** at 11:34. The first build, test and format runs used the preview; every later run used 10.0.401. The format counts were identical on both. Recommend `global.json` (ADD-04).
- MSBuild/CMake hit the **260-character path limit** in the long scratch path, so kernels had to be built with very short build dirs. Another Stage-1 agent (`cx`) also builds into `scratchpad/b/k01..k13`. My first staging step picked up four of their DLLs, so I rebuilt those four into `scratchpad/dt/nb/10..13` and re-ran using only my own binaries (§B3). Their directories were not touched.
- To fetch OpenAPI without a database, the app was run from a scratch build output with `ASPNETCORE_ENVIRONMENT=Development`, `RunMigrationsOnStartup=false` and generated dummy Google client values, on loopback ports 5317-5319. It booted; `/openapi/v1.json` and `/health/live` returned 200. **Verification recipe for any XML-doc package:**
  ```powershell
  # from <scratch clone>\Portfolio.Web\bin\Release\net10.0  (content root = working dir)
  $env:ASPNETCORE_ENVIRONMENT="Development"; $env:ASPNETCORE_URLS="http://127.0.0.1:5317"
  $env:RunMigrationsOnStartup="false"
  $env:Authentication__Google__ClientId="dummy"; $env:Authentication__Google__ClientSecret="dummy"
  dotnet Portfolio.Web.dll   # then GET /openapi/v1.json, before and after; diff
  ```

---

## 2. Recommended changes (master list for the planner)

Priority: P1 = do (it guards or fixes something real), P2 = should, P3 = mechanical or nice-to-have. Size: S < 2 h, M ≈ half a day, L > 1 day. "Overlaps" lists files other packages also touch.

### Goal 4: comments and XML docs

| ID | Pri | Evidence | Recommended fix | Files | Risk | Size | Overlaps / ordering |
|---|---|---|---|---|---|---|---|
| DOC-01 | **P1** | All 24 documented request bodies in `/openapi/v1.json` read "Cancellation token." (§A5.3). Deleting the token `<param>` alone raises **CS1573** errors under CI warnings-as-errors (seen at AddressStandardizationController.cs:41,79, BatchGeocodingController.cs:57,102,122) | Delete all 42 `<param name="cancellationToken">` / `<param name="ct">` lines in controllers. Add `1573` to `<NoWarn>` in `Portfolio.Web.csproj` (currently `$(NoWarn);1591`). **Verified in a scratch clone:** the build is clean and the OpenAPI diff is exactly 24 request-body descriptions, now taken from the `request`/`dto`/`file` param; schemas are identical | 19 files in `Portfolio.Web/Controllers/Api/` (all except FiberDashboard, FiberMaterials, FiberOrders, FiberShipments, HomeFinder, Profile) + `Portfolio.Web.csproj` | Low (docs only) | S | Same files as the try/catch consolidation. Run right after it, or first and let it rebase (different lines) |
| DOC-02 | **P1** | Comments that are now **wrong** (§A6), e.g. Program.cs:397-402 says "Google is the default challenge scheme" while :288 sets Cookie | Rewrite or delete the 9 wrong comments listed in §A6 | Program.cs, AnonymousUserMiddleware.cs, UserProfileService.cs, SpatialGraphService.cs, site.css | Low | S | Program.cs split, SpatialGraphService split, CSS work. Do inside those packages, or after them |
| DOC-03 | P2 | Invalid UTF-8 or U+FFFD in 4 files; 54 double-encoded mojibake lines in 3 CSS files (§A4.2) | Re-save as UTF-8 (no BOM, per `.editorconfig`) and replace the broken dash. The CSS mojibake lines are all banners, so DOC-07 deletes them | Property.cs, Profile.cshtml.cs, StateExplorer/mapManager.js, navigation.js, addressstandardization.css, batchgeocoding.css, reversegeocoding.css | Low | S | Frontend CSS/JS packages |
| DOC-04 | P2 | The `IsNativeInvocationException` wart has **no comment in source** in any of the 12 bridges (documented only in docs/plans/README.md:111-115). UserProfileSeedService.cs:27-31 skips transactions under EF InMemory without saying why | Add a one-line "why" at each definition, or once in the shared helper if bridge dedup lands. Add one line at UserProfileSeedService.cs:27 | 12 `*NativeBridge.cs`, UserProfileSeedService.cs | Low | S | Bridge dedup package, seed-service split |
| DOC-05 | P3 | One commented-out block (inventory.js:15-19). One stale TODO in a PageModel with no `.cshtml` (Pages/Projects/Details.cshtml.cs:27) | Delete both. The TODO goes when dead-code removal deletes `Details.cshtml.cs` | inventory.js, Details.cshtml.cs | Low | S | Dead-code package |
| DOC-06 | P3 | 54 multi-sentence summaries (§A5.4); 14 of 74 OpenAPI operation summaries contain embedded CR/LF; 7 restating `<returns>`, 7 restating non-token `<param>` | Apply rules R5-R7 (§A8). OpenAPI-visible text needs an owner decision (D4) | ~35 C# files | Low | M | Last-wave batches |
| DOC-07 | P3 | Banners 404; labels and restating ~460; `//` member descriptions 39; 150 HTML comments in cshtml shipped to browsers | Apply R1-R4 and R8-R10 per batch C1-C12 (§A9) | ~260 files | Low (comments only), but **high merge-conflict risk** | L | **Run last**, after every refactor and frontend package |
| DOC-08 | P3 | `#region Helper Methods` at ProfileController.cs:36 (the only one) | Remove the region markers | ProfileController.cs | Low | S | Controller packages |

### Goal 6: tests and CI

| ID | Pri | Evidence | Recommended fix | Files | Risk | Size | Overlaps / ordering |
|---|---|---|---|---|---|---|---|
| TST-01 | **P1** | 0% coverage on SecurityHeadersMiddleware (9 lines), ApiExceptionMiddleware (33 lines, 16 branches) and AnonymousUserMiddleware (69 lines, 32 branches). HomeSearchPreferencesDto.Validate is 0/19 lines, 0/8 branches | Characterization tests (§B4.6), all host-free: `DefaultHttpContext` for the middlewares, EF InMemory for AnonymousUserMiddleware (precedent already exists), `Validator.TryValidateObject` for the DTO | new `Portfolio.Tests/Middleware/*Tests.cs`, `Portfolio.Tests/DTOs/HomeSearchPreferencesDtoTests.cs` | Low | M | **Before** the CSP, forwarded-header and error-leak security packages |
| TST-02 | **P1** | 3 controllers have no tests at all (SpatialCluster, SpatialOverlay, Viewshed: 0%); ProfileController.SetAuthenticatedClaim is untested; none of 38 `BadRequestObjectResult` assertions checks the `{ error }` body | Add the missing tests. Pin status **and body** for every catch branch of all 77 actions (§B4.7) | new controller test classes + existing controller tests | Low | M | **Before** try/catch consolidation |
| TST-03 | **P1** | Nothing guards "public routes don't change". `ApiVersioningAttributeTests` checks prefixes only | Reflection route snapshot (verb + full template, 77 actions incl. 3 hidden aliases) plus a check that every route string in `wwwroot/js/api-config.js` resolves to a controller route (§B4.8) | new `Portfolio.Tests/Controllers/RouteTableTests.cs` | Low | S | Before any controller or route refactor |
| TST-04 | **P1** | No test pins the shipped-scenario outputs that README quotes. I measured them and they currently hold (§B4.3-4) | Golden-output tests for Response, Fleet, Change and Graph on the shipped data before splitting those services (§B4.2-5) | `*ServiceTests.cs` (new test methods) | Low | M | **Before** the FleetRouting, ResponseCoverage, ChangeDetection and SpatialGraph splits |
| TST-05 | **P1** | Program.cs is 0% (451 lines). Its 3 helpers are local functions and can't be tested in place | Extract first, then test: move the helpers verbatim to an `internal static class` and pin their behaviour. Add one options test per new `AddPortfolio*` extension (§B4.1) | Program.cs → new `Portfolio.Web/Configuration/*.cs` + tests; `InternalsVisibleTo` for Web | Low | M | It **is** step 1 of the Program.cs split |
| TST-06 | **P1, needs decision D1** | Pipeline order, headers, 401-vs-302, 429 and the health rate-limit opt-out are all untested. I verified the host boots without a DB (§1) | `WebApplicationFactory<Program>` smoke tests plus an OpenAPI snapshot (§B4.1c). Needs `Microsoft.AspNetCore.Mvc.Testing` and `public partial class Program {}` | Directory.Packages.props, Portfolio.Tests.csproj, Program.cs (1 line), new tests | Low-Med (new test infra) | M | Before the Program.cs split and the security packages |
| TST-07 | P2 | UserProfileSeedService: 1,169 coverable lines in **one method**; its only test asserts `Any()` for clients and materials | Pin per-entity counts, a content fingerprint, idempotency and per-user isolation (§B4.4) | UserProfileSeedServiceTests.cs | Low | S | Before the seed-service split |
| TST-08 | P2 | 12 native-gated tests (`if (!IsAvailable) return;`, 7 in NativeScoringBridgeTests and 5 in NativeParityTests) report **Passed** when nothing ran. The CI native job counts `.so` files but never checks that they load | Use `Assert.SkipUnless(...)` (present in the referenced xunit.v3.assert 3.2.2). Add `NativeAvailabilityTests` that fails when `PORTFOLIO_REQUIRE_NATIVE=1` and any of the 13 bridges reports unavailable | NativeScoringBridgeTests.cs, NativeParityTests.cs, new test, ci.yml | Low | S | Pairs with CI-05 |
| TST-09 | P2 | Validation branches in the services to be split are uncovered (line lists in §B4.2-5) | `[Theory]` rows per branch | 4 service test files | Low | S | Before the splits |
| TST-10 | P3 | Convention §9 "one class per service/controller" is broken by 3 classes (§B5) | Pure moves into per-SUT files | test files only | Low | M | After TST-02/04/09, before comment batch C7 |
| TST-11 | P3, **decision D2** | 9 repositories at 0% (Repositories assembly 47.8%) | Optional EF InMemory repository tests (precedent exists) | new tests | Low | M | Independent |
| CI-01 | P2 | Format not gated; fails today (§B7) | One-time `dotnet format` pass plus a CI step (YAML §B6). Prerequisite: line-ending decision D3 | .editorconfig, 118 files, ci.yml | Low (whitespace) / **high conflict** | S + L | **The very last package** |
| CI-02 | P2 | No `.github/dependabot.yml` | Add NuGet, GitHub Actions and Docker updates (YAML §B6) | new file | Low | S | Independent |
| CI-03 | P2 | No dependency review on PRs | Add `actions/dependency-review-action` (YAML §B6) | ci.yml | Low | S | Security agent owns SHA pinning |
| CI-04 | P2 | No CodeQL | `codeql.yml` covering csharp, javascript-typescript and c-cpp (YAML §B6). Check repo visibility / GHAS (F) | new workflow | Low | M | Independent |
| CI-05 | P2 | Native job could pass vacuously (TST-08) | Set `PORTFOLIO_REQUIRE_NATIVE: '1'` on the "native present" step | ci.yml | Low | S | With TST-08 |
| CI-06 | P3 | The gate takes `reports[0]` from a glob that matches **2** files locally (the trx logger adds a copy under `<user>_<machine>_<ts>/In/`; both had the same md5 here). No per-assembly floor: Web sits at 43% behind an 84% total | Pick deterministically and assert one distinct report. Add a Web floor once TST-01/02/06 land. **Raise the ratchets** after the test packages (e.g. to just under the new measured values) | ci.yml | Low | S | After test packages |
| CI-07 | P3 | Coverage collected only on the managed run; bridges show 15-34% | Optional: upload the native run's cobertura and report the union (87.72%/69.49% measured). Report only, no gate | ci.yml | Low | M | After CI-05 |
| CI-08 | P3 | No warning flags in any of the 13 CMakeLists (no `/W4`, `-Wall`, `-Wextra`) | Add warnings in report-only mode first; `-DCMAKE_COMPILE_WARNING_AS_ERROR=ON` later | 13 CMakeLists.txt | Med (native) | S | Native packages |

### Goal 7: documentation hygiene

| ID | Pri | Evidence | Recommended fix | Files | Risk | Size |
|---|---|---|---|---|---|---|
| DH-01 | **P1** | copilot-instructions.md: **50 stale statements** (§C1), one of them dangerous: "managed fallback: … bounding-box vertex clamp" (L783-784) describes the exact native bug the parity check found | Slim it to about 80 lines of invariant rules and point to `docs/agent-context.md` for facts that drift (counts, routes, kernels). **Prerequisite:** commit `docs/agent-context.md` (currently untracked) | .github/copilot-instructions.md, docs/agent-context.md | Low | S |
| DH-02 | **P1, needs decision D5** | 5 Details pages publish ad-hoc benchmark figures that native/README.md:118-129 says are withdrawn; "SIMD-friendly" wording on 3 cards and 7 Details bullets (§C4.3) | Replace each figure with the current table row, or link `native/README.md#measured-speedup`. Drop the SIMD/AVX implications | 5 + 6 Details.cshtml, Index.cshtml (2 copies), Projects/Index.cshtml | Low (copy) | S |
| DH-03 | **P1** | native/README.md is internally inconsistent: 8 statements contradict its own table (§C5.1) | Fix the 8 lines (sentence "wins 8, loses 8, ties 5" → "9 / 5 / 7 at ±15%") | native/README.md | Low | S |
| DH-04 | P2 | README: test count 680 (badge + text), "5 projects", `portfolio_v2` ×3, `https://localhost:5001` for a `dotnet run` that serves `http://localhost:5000`, and more (§C3) | Fix the 11 items | README.md | Low | S |
| DH-05 | P2 | render-deployment.md: repo name, no pre-deploy `--migrate` step, auto-deploy guidance conflicts with the ci.yml deploy gate (§C5.3) | Fix the 3 items | docs/render-deployment.md | Low | S |
| DH-06 | P2 | docs/plans/README.md tells agents to "assert `Assert.False(result.NativeAccelerated)` first" (L260-261), which reverses the current rule, plus 8 more stale points (§C5.2) | Add a "historical: shipped Jul 2026" banner and fix the 9 points | docs/plans/README.md | Low | S |
| DH-07 | P2 | wwwroot/css/README.md:12 describes `@import`s that no longer exist (§C5.5) | Fix 2 lines; also the site.css:7 comment (DOC-02) | css/README.md | Low | S |
| DH-08 | P2, **owner only** | resume-update.md:19 quotes a résumé bullet "13 high-performance C++ modules", which its own L46 forbids; L21 "680 tests", "gates deployment on every commit"; L18 "role-based authorization" (there are no roles) | Owner decides on résumé wording. The doc fixes themselves are trivial | docs/resume-update.md (+ résumé PDF, owner) | — | S |
| DH-09 | **P1** | agent-context.md: 16 inaccuracies (§C6) | Correct now; re-measure counts at the end of Stage 2 | docs/agent-context.md | Low | S |

---

## A. Comments and XML docs (Goal 4)

### A1. Inventory

Scanned: 445 tracked `.cs/.cshtml/.js/.css/.cpp/.h` files. Excluded: `wwwroot/lib/**`, `**/Migrations/**`, and `Services/Data/RedlandsRoadNetwork.cs` (generated). Scripts: `scratchpad/dt/scripts/comments.py` and `refine.py` (heuristic classifiers, calibrated by sampling). A **unit** is one `/* */` block, one Razor or HTML comment, or a run of adjacent `//` lines.

| Project | units | lines | banner | restates | label / step | `//` member desc. | other prose | "why" | TODO | AAA | XML blocks | XML >1 sentence | trivial `<param>` | trivial `<returns>` |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Benchmarks | 45 | 81 | 20 | 0 | 0 | 0 | 10 | 15 | 0 | 0 | 8 | 5 | 0 | 0 |
| Common | 22 | 22 | 0 | 2 | 16 | 0 | 4 | 0 | 0 | 0 | 777 | 13 | 0 | 0 |
| Repositories | 40 | 50 | 0 | 2 | 14 | 4 | 18 | 2 | 0 | 0 | 6 | 3 | 0 | 0 |
| Services | 316 | 544 | 37 | 3 | 31 | 28 | 122 | 95 | 0 | 0 | 140 | 22 | 0 | 0 |
| Tests | 1,109 | 1,284 | 125 | 3 | 47 | 1 | 104 | 70 | 0 | 758 | 6 | 2 | 0 | 0 |
| Web (C#) | 102 | 246 | 22 | 3 | 5 | 1 | 41 | 29 | 1 | 0 | 109 | 9 | **49** | **7** |
| Web (cshtml) | 170 | 220 | 4 | 0 | 127 | 0 | 18 | 21 | 0 | 0 | — | — | — | — |
| Web (js) | 447 | 710 | 115 | 12 | 81 | 3 | 176 | 59 | 0 | 0 | — | — | — | — |
| Web (css) | 208 | 374 | 73 | 18 | 65 | 0 | 37 | 15 | 0 | 0 | — | — | — | — |
| native (C++) | 159 | 536 | 8 | 1 | 29 | 2 | 60 | 59 | 0 | 0 | — | — | — | — |
| **Total** | **2,618** | **4,067** | **404** | **44** | **415** | **39** | **590** | **365** | **1** | **758** | **1,046** | **54** | **49** | **7** |

How to read the columns:
- **banner**: exact (regex for runs of ≥4 of `- = * # ~ _ ─ ━ ═`, titled or not). In a 45-item sample, every hit was a banner.
- **restates**: high-confidence only (≥75% of the comment's content words appear in the next statement, and no reason words). Examples: Program.cs:354 "Merge anonymous profile if present", :370, :376, :773 "Build StackExchange.Redis-compatible connection string"; BatchGeocodingService.cs:302 "Skip header line."; FiberMaterialRepository.cs:58 "stamp on every edit"; PlantOperationsDashboard/orders.js:179 "Delete order"; inventory.js:111 "Show receive stock modal".
- **label / step**: ≤4-word labels and numbered steps. A 14-item sample was roughly 55% pure restatement (e.g. Property.cs:10/26/32/36/64 "Listing basics", "Address", "GIS coordinates", "Financial", "Environmental"; Program.cs:52/65 "Repositories", "Services"; toasts.js:61 "Expose globally"; HomeScoringService.cs:34/46/50 "1. … 2. … 3. …") and 45% carrying information ("price range in $thousands", RedlandsPropertySeedData.cs:28; "wire buttons (idempotent)", uiManager.js:498). **127 of them are cshtml HTML comments** (e.g. `<!-- Terrain Analyzer -->` card labels in Index.cshtml).
- **`//` member descriptions**: doc-style `//` comments above members, e.g. UserProfileService.cs:51/60/69/81 and UserProfileRepository.cs:50/83/98/116 restate the interface docs; RasterTerrainService.cs:20; SpatialGraphService.cs:69/104/364.
- **"why"**: contains a reason word (because / so that / must / never / otherwise / avoid / parity / …). Mostly keep; see §A7.
- **AAA**: `// Arrange / Act / Assert` markers. They are the documented test convention (§9). **Keep.**

### A2. TODO / FIXME / HACK / XXX: exactly one, and it is stale

`git grep -nE '\b(TODO|FIXME|HACK|XXX)\b'` over all tracked files except vendored libs:

| Location | Text | Status |
|---|---|---|
| Portfolio.Web/Pages/Projects/Details.cshtml.cs:27 | `// TODO: Load project data by projectId scoped to userId` | **Stale.** The PageModel has no `Details.cshtml` beside it, so it is never routed (0% coverage, 8 lines). Per-project Details pages exist at `Projects/<Name>/Details`. Delete it with the file (dead-code package) |

### A3. Commented-out code: exactly one block

- `Portfolio.Web/wwwroot/js/PlantOperationsDashboard/inventory.js:15-19`: a commented-out jQuery row-click handler plus its one-line explanation. **Delete.**
- I searched for `// var|let|const|if (|return|$(|…;`, CSS `/* prop: value; */`, Razor and HTML comments wrapping markup, and C++ `// #`. No other hits. (The classifier's one other hit, NativeScoringBridgeTests.cs:363, is prose.)

### A4. Banners and encoding

**A4.1 Banner styles in use** (all 404 to be deleted by R1; any prose inside them is kept):
1. `// ── Title ─────…`: tests (125), JS, Services. E.g. FiberMaterialsControllerTests.cs:25, ChangeDetectionService.cs:138, CatRisk/app.js:203.
2. `// ----` / `// Title` / `// ----` triples: Program.cs (22 units, e.g. :33-35, :430-432).
3. `// ====` / `// TITLE` / `// ====`: UserProfileSeedService.cs (e.g. :243 "FIBER MATERIALS").
4. `/* ====\n   Title\n   ==== */`: CSS (layout.css:521, components.css:141, site.css:291).
5. `// ---- Title ----` / `/* ---- Title ---- */`: preferences.js:205, preferences.css:11.
6. `// ------------- kernel_name`: Benchmarks/Workloads.cs:388, :498, :554, :611.
7. `#region Helper Methods`: ProfileController.cs:36 (the only `#region`).

**A4.2 Encoding defects** (found with a full-repo UTF-8 decode plus a mojibake regex scan):

| File:line | Problem |
|---|---|
| Portfolio.Common/Models/Property.cs:41 | U+FFFD replacement character: `// Neighborhood scores (0�100)` (a lost en dash) |
| Portfolio.Web/Areas/Account/Pages/Profile.cshtml.cs:72 | File is not valid UTF-8 (cp1252 em dash). `dotnet format` also flags its charset |
| Portfolio.Web/wwwroot/js/StateExplorer/mapManager.js:32 | cp1252 byte in a comment (served to browsers) |
| Portfolio.Web/wwwroot/js/navigation.js:21 | cp1252 byte in a comment |
| addressstandardization.css (20 lines), batchgeocoding.css (15), reversegeocoding.css (19) | **54 lines of double-encoded box-drawing mojibake** (`â”€â”€`), all inside banner comments (verified: no non-comment line affected) |

### A5. XML documentation

**A5.1 Settings (confirmed in source):**

| Project | `GenerateDocumentationFile` | NoWarn |
|---|---|---|
| Portfolio.Common | true (csproj:9, with a comment saying it feeds OpenAPI) | `1591` |
| Portfolio.Services | true (csproj:9) | `1591` |
| Portfolio.Repositories | true | `CS1591` |
| Portfolio.Web | true | `1591` |
| Portfolio.Tests | — | `xUnit1051` only (with a rationale comment, csproj:10-21) |
| Portfolio.Benchmarks | — | — |

Directory.Build.props sets `TreatWarningsAsErrors` only when `ContinuousIntegrationBuild=true` and promotes NU1902/3/4 to errors. **Consequence for comment trimming: CS1573 (a method documents some params but not all) is a CI error** (§2 DOC-01).

**A5.2 What actually reaches OpenAPI/Scalar.** I fetched `/openapi/v1.json` (74 operations, 106 schemas) and matched every XML text against it.

| XML source | Reaches OpenAPI? | Evidence |
|---|---|---|
| Controller action `<summary>` | **Yes**, as operation `summary` (74/74 operations have one; the 3 hidden actions are excluded) | 74/77 present |
| Controller action `<remarks>` | **Yes**, as operation `description` | verified by experiment (BatchGeocodingController) |
| Controller action `<param>` (route, query, body) | **Yes**, as parameter or request-body description | 58/84 present |
| Controller action `<param name="cancellationToken">` | **Yes, wrongly**: it becomes the **request-body description** | §A5.3 |
| Controller action `<returns>` | Only for 4 GET actions, as the 200 description (catrisk/book, outage/network, response/scenario, network/graph). The other 35 are unused | 4/39 present |
| Controller class `<summary>` | **No.** 0 of 25 tags have descriptions | — |
| Portfolio.Common class `<summary>` | Yes, as schema `description` (102 schemas) | — |
| Portfolio.Common property `<summary>` | Yes, as property `description` (584 properties) | 671/777 Common summaries present; the rest are on types not in the API |
| Portfolio.Common property `<remarks>` | **No, it is dropped** | verified by experiment (ChangeBlobDto.Confidence) |
| Services, Repositories, Web non-controller | No | 1/167 coincidental |

**A5.3 Bug: every documented request body is described as "Cancellation token."**

In the published document (before any change), 24 request bodies have descriptions and **all 24** are a cancellation-token phrase: 18 "Request cancellation token.", 4 "Cancellation token.", 2 "Cancellation token for the request.". Affected operations include `POST /api/v1/addresses/parse`, `/geocoding/batch`, `/catrisk/accumulation`, `/viewshed/compute` and 20 others.

**Fix verified in scratch clone `exp`:** delete the 42 controller `<param name="cancellationToken|ct">` lines (19 files) and add `1573` to Portfolio.Web NoWarn. The release build stays clean with CI flags. In the re-fetched document every one of the 24 request bodies now carries the intended text (e.g. viewshed → "Elevation grid, observer position, and observer height."), and nothing else changed (schemas identical; the only other diff was my `<remarks>` experiment).

Add a regression test that reads `Portfolio.Web.xml` (the generated doc file) and asserts no controller member has a `cancellationToken`/`ct` `<param>`. This is cheap and needs no host.

**A5.4 Multi-sentence `<summary>` (54), grouped by OpenAPI impact:**
- **OpenAPI operation (2):** BatchGeocodingController.cs:44 (2nd sentence: required CSV columns, which is useful API information) and :92. → Keep sentence 1; move sentence 2 to `<remarks>`, which maps to `description` (verified).
- **OpenAPI schema / Portfolio.Common (13):** ChangeDetectionDtos.cs:3, :85, :112, :156; FeatureDto.cs:20; FiberMaterialDto.cs:5, :47; ResponseCoverageDtos.cs:25; SpatialNetworkDtos.cs:83 (3 sentences); SpatialOverlayDtos.cs:23; UpdateProfileDto.cs:3, :9; PortfolioJsonOptions.cs:6 (not in the API).
  - Property `<remarks>` is dropped, so a "one sentence" rule would **delete public API facts**, e.g. ChangeDetectionDtos.cs:112: "…NOT a calibrated probability of change", which is an honesty caveat.
  - → Decision D4. Recommended default: keep the facts; merge into one sentence where natural; otherwise allow two.
- **Not in OpenAPI (39):** free to shorten to one sentence, moving any "why" into a `//` comment or `<remarks>`. The worst are Benchmarks/Program.cs:9 (9 sentences), :115 (4); DeterministicPropertyRepository.cs:6 (6); NativeToggle.cs:3 (5); RedlandsDistributionNetwork.cs:5 (5); SyntheticChangeScene.cs:5 (4); AnonymousUserMiddleware.cs:12 (3, and wrong, §A6); DatabaseReadyHealthCheck.cs:7 (3); FleetRoutingService.cs:10 (3); Index.cshtml.cs:16/28/62.
- 14 of 74 operation summaries contain embedded `\r\n` because the `<summary>` spans lines. Collapse to a single line (cosmetic; Scalar shows it as the title).

**A5.5 Trivial `<param>` (49) and `<returns>` (7)** (only in Web controllers):
- 42 × `cancellationToken`/`ct`: "Request cancellation token." (24), "Cancellation token." (12 + 2 `ct`), "Cancellation token for the request." (4). Delete them (DOC-01).
- 7 others, all OpenAPI-visible: `id` "Collection ID." ×3, `dto` "Collection data." / "Update data." / "Feature data.", `layerId` "Layer identifier.". Deleting them removes text Scalar shows (D4; default: delete, since they add nothing).
- `<returns>` that only restate the type (7): "The collection.", "The created collection.", "The updated collection.", "List of collections.", "List of features.", "List of saved features.", "The created feature." None reach OpenAPI (not GET-only actions). Delete.
- Keep informative ones, e.g. `lat` "WGS84 latitude (-90 to 90)", `width` "Raster width in pixels, up to 512", and the `<returns>` of the 4 GETs.

### A6. Comments that are wrong today (fix regardless of the trim)

| File:line | Says | Truth |
|---|---|---|
| Program.cs:397-402 | "Google is the default challenge scheme, so an unauthenticated request … would otherwise 302 straight to accounts.google.com" | Program.cs:288 sets `DefaultChallengeScheme = Cookie`. The handler now only matters if something explicitly challenges Google on an `/api` path. Rewrite as a defensive note (keep the code) |
| Program.cs:305-314 | API 401 "prevents fetch/XHR calls from silently following a redirect to Google OAuth"; pages get "redirect so full-page navigation to Google OAuth works" | The cookie challenge redirects to `/Login` (LoginPath, :292), not to Google. Keep the 401-for-`/api` reason; fix the destination |
| Program.cs:494-496 | Banner "Swagger / API Explorer" | OpenAPI + Scalar (`AddOpenApi`, `MapScalarApiReference`) |
| Program.cs:500-502 | "Database Context (PostgreSQL via Render / Supabase)" | Neon.tech (agent-context §2, README) |
| Program.cs:261-262 | "AddMemoryCache kept for compatibility with any remaining IMemoryCache consumers" | `git grep IMemoryCache` finds no consumer. Dead registration (cross-ref refactor agent) |
| AnonymousUserMiddleware.cs:13 (XML summary) | Identity stored in `HttpContext.Items["AnonUserId"]` | Key is `"PortfolioIdentity"` (same file :23, :140; UserProfileService.cs:23; Program.cs:377). `AnonUserId` is the **cookie** name |
| UserProfileService.cs:14 (XML summary) | "Resolves the current user from HttpContext.Items["AnonUserId"]" | Same as above |
| SpatialGraphService.cs:443-446 | Skipping undeclared nodes "mirrors the native kernel, where the equivalent std::map lookup would silently insert a zero-cost phantom node instead" | The native engine is CSR now and drops such edges up front (spatial_graph_engine.cpp:58-61). `std::map` survives only in historical comments there |
| wwwroot/css/site.css:7 | "base + utilities are pulled in below." | They are linked from `_Layout.cshtml:91-92`. The very next comment (site.css:9-12) says so |

### A7. Keep-list (for owner veto)

52 entries. K = keep (may trim the wording); KR = keep but rewrite, because it is currently wrong or stale; A = add, because it is missing. The six items the brief names are marked ★.

| # | File:line | Act | One-line reason |
|---|---|---|---|
| 1 ★ | Program.cs:280-287 | K | Default challenge is Cookie so protected pages land on `/Login`, not Google (trim to 3 lines) |
| 2 ★ | Program.cs:433-442 | K | Rate limiting runs before auth and anon-profile creation; partitions by IP; shared-NAT trade-off |
| 3 | Program.cs:578-579 | K | Rate limiter sits after routing so per-endpoint policies resolve; static files deliberately not limited |
| 4 ★ | Program.cs:569-570 | K | HTTPS redirect is dev-only; Render terminates TLS, so redirecting in the container would loop |
| 5 | Program.cs:555-557 | K | Forwarded headers must be first so every middleware sees the real scheme/IP |
| 6 | Program.cs:230-233 | KR | Forwarded-header trust rationale; must change with the security fix that narrows proxies |
| 7 | Program.cs:38-40 | K | Never log the DB connection string (carries the password) |
| 8 | Program.cs:390-391 | K | `SaveTokens=false`: no feature reads Google tokens; keeps bearer tokens out of the cookie |
| 9 | Program.cs:294-295 | K | Dev uses `SameAsRequest` so cookies work over http://localhost |
| 10 | Program.cs:305-314 | KR | `/api` gets 401 instead of 302 so fetch callers can react; fix the "Google OAuth" destination (§A6) |
| 11 | Program.cs:397-402 | KR | Google redirect handler; now defensive only (§A6) |
| 12 | Program.cs:470-472, 603-604, 609 | K | Liveness runs no checks; probes opt out of rate limiting (dedupe to one place) |
| 13 | Program.cs:596 | K | OpenAPI/Scalar intentionally public in production (security agent may change the decision) |
| 14 | Program.cs:624-637 | K | Startup migration vs `--migrate` pre-deploy; "set both or neither" (trim) |
| 15 | Program.cs:654-657 | K | Startup migration failure is tolerated; `--migrate` failure must exit non-zero so Render aborts |
| 16 | SecurityHeadersMiddleware.cs:28-34 | K→KR | Why the CSP is minimal; replace when the CSP package lands |
| 17 | ApiExceptionMiddleware.cs:33-34 | K | Client aborts logged at Debug, not Error |
| 18 | ApiExceptionMiddleware.cs:46-49 | K | 5xx responses never echo `ex.Message` (information leak) |
| 19 | AnonymousUserMiddleware.cs:69-72 and 102-104 | K | Only browser (text/html) requests get a new anonymous profile; probes/API clients can't store cookies (dedupe) |
| 20 | AnonymousUserMiddleware.cs:91-92 | K | Throttles `LastActiveDate` writes to once per 5 minutes |
| 21 | AnonymousUserMiddleware.cs:124-127 | K | `Secure` is tied to environment, not `IsHttps` (TLS ends at Render's proxy) |
| 22 ★ | each `IsNativeInvocationException` (e.g. SpatialClusterNativeBridge.cs:81; 12 bridges) | **A** | Includes `InvalidOperationException` on purpose: a negative native status logs once and silently falls back to managed (known wart, docs/plans/README.md:111-115) |
| 23 | NativeToggle.cs:3-15 | K | Why the `PORTFOLIO_DISABLE_NATIVE` kill switch exists (static-ctor probe; benchmark needs both paths). Trim to ≤3 lines |
| 24 ★ | ViewshedService.cs:76-80 and viewshed_kernel.cpp:13-18 | K | Explicit half-up rounding in both paths; language defaults diverged on .5 (parity) |
| 25 ★ | SpatialGeometryService.cs:75 and spatial_geometry_kernel.cpp:122 | K | Same Sutherland-Hodgman passes in the same order on both sides (parity) |
| 26 ★ | native/spatial_graph_engine/include/spatial_graph_engine.h:70-73 | K | Distances are parallel to `nodes[]`, not node id; avoids the one-based/zero-based trap |
| 27 ★ | ISpatialGraphService.cs:23-27; ResponseCoverageService.cs:63-64 | K | Same index-versus-node-id caveat on the managed side |
| 28 | native/change_detection_kernel/include/change_detection_kernel.h:86-89 | K | Blob arrays are indexed `id − 1` |
| 29 | FleetRoutingService.cs:198 | K | Matrix index of a zero-based stop; depot occupies row/column 0 |
| 30 | SpatialGraphService.cs:443-446 | KR | Skipping undeclared nodes (fix the stale `std::map` claim, §A6) |
| 31 | native/network_trace_kernel/src/network_trace_kernel.cpp:35 | K | Never `operator[]` on a missing node (latent-bug class) |
| 32 | SpatialGraphService.cs:17 | K + extend | Cached static graph; add "shared instance — callers must never mutate it" |
| 33 | native/facility_location_kernel/CMakeLists.txt:12-15; native/vrp_solver_kernel/CMakeLists.txt:12-13 | K | No fast-math on purpose (infinities; parity of accept/reject decisions) |
| 34 | each native/*/CMakeLists.txt post-build copy (e.g. cat_risk_kernel:38-40) | K | The web host loads from its own output dir; without the 2nd copy the UI says "Native: No" |
| 35 | FleetRoutingService.cs:386-387 | K | Index-pair tiebreak makes the savings merge order, and the solution, deterministic |
| 36 | FleetRoutingService.cs:308-310 | K | Shift-clock walk; unreachable legs carry infinity and fail the due-time test |
| 37 | BatchGeocodingService.cs:186-187 | K | Mean score over matched rows only; unmatched rows must not dilute it |
| 38 | BatchGeocodingController.cs:36 | K | ~2 MB upload cap paired with the service's row cap |
| 39 | ReverseGeocodingService.cs:79 | K | Encodes all caller-supplied values (URL injection) |
| 40 | SavedSearchService.cs:58-63 | K (trim) | "Not found" and "not yours" are indistinguishable by design (no disclosure) |
| 41 | UserProfileService.cs:45-46 | K | Identity precedence: authenticated identity in Items first, anonymous fallback |
| 42 | UserProfileSeedService.cs:27 | **A** | Why InMemory skips the transaction (EF InMemory does not support transactions; tests use it) |
| 43 | NativeStructs.cs:78 | K | Current year is passed in so the kernel is deterministic in tests |
| 44 | ChangeDetectionService.cs:23-24 | K | Why 128 histogram bins |
| 45 | Pages/Index.cshtml.cs:28-32 | K | Résumé size and date are read from the file so they can't go stale |
| 46 | wwwroot/css/site.css:9-12 | K | base/utilities are `<link>`ed, not `@import`ed, so `asp-append-version` cache-busts them (delete stale :7) |
| 47 | wwwroot/js/preferences.js:205-206 | K | Theme deliberately ignores live OS changes once the visitor has chosen |
| 48 | Pages/Index.cshtml:490 `<!-- Duplicates for seamless loop -->` | K (→ Razor `@* *@`) | Explains the duplicated card markup; it is the anchor used by docs/plans/README.md:303. Drop it if the frontend package removes the duplication |
| 49 | Tests/Services/NativeParityTests.cs:6-16, 195-196 | K | Why the parity tests drive native directly; why the reference implementations are duplicated |
| 50 | Portfolio.Tests.csproj:10-21; Directory.Build.props; Directory.Packages.props:4-5, 17-18; coverlet.runsettings:9-18; ci.yml:28-30, 130-139, 194-195, 201-203, 223-231 | K | Config "why" (sole suppression; CI-only warnings-as-errors; OpenApi pin; coverage exclusions; ratchets; parity job; deploy gate) |
| 51 | BatchGeocodingController.cs:120 `// kept for test compatibility` | K (pending D6) | Explains why a live `[Obsolete]` endpoint still exists |
| 52 | AddressStandardizationService.cs:86, 201-202 | K | Parsing-order rationale (tail anchors; directional tokens aren't city/state) |

### A8. Trimming rules for the Sonnet implementers

Apply in this order, per file. **Never** touch `Portfolio.Services/Data/RedlandsRoadNetwork.cs`, `**/Migrations/**`, `wwwroot/lib/**`, or anything on the keep-list (§A7) except as its row says.

**R1. Banners and dividers: delete.**
- Delete any comment line made only of divider characters (`- = * # ~ _ ─ ━ ═`), and any titled banner in any of the 6 styles in §A4.1.
- If a banner block also contains prose (a sentence beyond the title), keep that prose as a plain comment directly above the code it explains.
- In files over 300 lines, a navigation title may survive as a single plain line (`// Rendering`, `/* Footer */`) with no divider characters.
- Remove `#region`/`#endregion`.

```csharp
// before                                         // after
// --------------------------                     // Partitioned per-client (IP) limiting. …
// Rate Limiting                                  builder.Services.AddRateLimiter(…
// --------------------------
// Partitioned per-client (IP) limiting. …
builder.Services.AddRateLimiter(…
```

**R2. Commented-out code: delete,** together with any comment that only explains why it is commented out (inventory.js:15-19).

**R3. TODO/FIXME/HACK: delete if stale** (the code it describes is gone or done). Otherwise keep it and add an owner/issue reference.

**R4. Restating comments: delete** a `//` or `/* */` comment when **all** of these hold:
- (a) after dropping stop-words, every remaining word appears in the next statement's identifiers or keywords, or in the enclosing member's name;
- (b) it contains no reason word (because, so, otherwise, must, never, avoid, why, until, unless, instead, only, note, deliberately);
- (c) it carries no unit, range, format, provenance or invariant the code doesn't show (km, ms, 0–100, inclusive, UTC, one-based, "Numerical Recipes", "idempotent").

```csharp
// Merge anonymous profile if present            → delete
Guid? anonId = null;
// Skip header line.                             → delete
// price range in $thousands                     → KEEP (unit not visible in code)
// Inclusive range [min, max].                   → KEEP (semantics)
// 1. Load and apply hard filters via repository → delete (step label restates the call)
```

Section labels in models and DTOs (`// Address`, `// Financial`): delete in classes under ~25 members; keep in seed data where they name real places (`// South Redlands historic district`).

`//` member descriptions that duplicate the interface's `<summary>` (UserProfileService, UserProfileRepository): delete, but keep any clause the interface doesn't state (e.g. "Read-only (AsNoTracking)", "Persists immediately") as a one-line comment.

**R5. `<summary>`: one sentence, one line where it fits in 120 columns.**
- Controller actions (OpenAPI `summary`): never delete. Move any second sentence carrying API information to `<remarks>` (it becomes the operation `description`).
- Portfolio.Common public types and members (OpenAPI schema): never delete. Property `<remarks>` is **dropped** by the generator, so do not move facts there. Merge into one sentence, or leave two sentences for the 13 listed in §A5.4 (decision D4).
- Everything else: one sentence. Move a needed "why" to a `//` comment above the member, or into `<remarks>`.

```csharp
// before (controller action)                                  // after
/// <summary>                                                  /// <summary>Accepts a CSV of addresses, enqueues a geocoding job, and returns 202 with a status URL.</summary>
/// Accepts a CSV file upload of addresses, enqueues a         /// <remarks>The CSV must have columns: Id, Address, City, State, Zip.</remarks>
/// background geocoding job, and returns 202 Accepted with
/// a status URL the client can poll.
/// The CSV must have columns: Id, Address, City, State, Zip.
/// </summary>
```

**R6. `<param>`:**
- Delete every `<param name="cancellationToken">` / `<param name="ct">`. Portfolio.Web must have `1573` in NoWarn **first**, or CI fails.
- Delete a `<param>` whose words are only the parameter name plus {the, a, data, identifier, id, value}.
- Keep any that add units, ranges, allowed values or format.

**R7. `<returns>`:** delete when it only restates the return type ("The collection.", "List of features."). Keep the rest. On GET actions it is the 200-response description.

**R8. Encoding:** files must be UTF-8 without BOM (`.editorconfig charset = utf-8`). Fix the 4 files in §A4.2. Never "fix" migrations; `dotnet format` flags 3 of them for BOM, so exclude them.

**R9. cshtml:** HTML comments (`<!-- -->`) are **sent to every browser** (150 of them in 12 files). Delete restating ones. Convert any keeper to a Razor comment `@* … *@` (server-only).

**R10. Never change code while trimming.**
- The diff for a comment package must be comment, XML-doc and whitespace only. Check with `git diff -w --word-diff` plus a build.
- For any change to Portfolio.Common XML docs or controller XML docs, diff `/openapi/v1.json` before and after (§1 recipe). The only expected differences are the descriptions deliberately changed.

### A9. Batching: run last, aligned with code ownership

Every comment batch runs **after** the refactor or frontend packages that touch the same files. Each batch is one PR-sized commit, disjoint by folder, so the batches can run in parallel with each other.

| Batch | Scope | Approx. units | Must wait for | Notes |
|---|---|---|---|---|
| C0 | **DOC-01 + DOC-02 + DOC-03 + DOC-04** (correctness, not trim) | 42 params + 9 comments + 4 files + 13 adds | the package touching each file (controllers: try/catch consolidation; Program.cs: split) | Small; can go early if the owners of those files agree |
| C1 | `Portfolio.Common/**` | 18 non-XML + 13 multi-sentence XML | DTO input-cap security fixes (if they edit DTOs) | OpenAPI diff required (R10) |
| C2 | `Portfolio.Web/Controllers/Api/**` | 7 params, 7 returns, 2 summaries, 14 one-lining, `#region` | controller try/catch consolidation | OpenAPI diff required |
| C3 | `Portfolio.Web/{Program.cs, Middleware, HealthChecks, Pages/**/*.cs, Areas/**/*.cs}` | ~100 | Program.cs split; CSP/forwarded-headers/error-leak fixes | Keep-list items 1-21 live here |
| C4 | `Portfolio.Services/Services/**`, `Interfaces/**`, root `*.cs` | ~230 | the 5 service splits | Banners in ChangeDetection, Fleet, Response, UserProfileSeed |
| C5 | `Portfolio.Services/Native/**`, `Data/**` (not RedlandsRoadNetwork.cs) | ~100 | bridge dedup | Add the wart comment (DOC-04) |
| C6 | `Portfolio.Repositories/**` (not Migrations) | 40 | repository refactors | `//` member descriptions |
| C7 | `Portfolio.Tests/**` | 125 banners + ~150 other (keep 758 AAA) | **all** test-adding packages (TST-*) and TST-10 | Largest by file count |
| C8 | `Portfolio.Benchmarks/**` | 45 | nothing | Independent, any time |
| C9 | `Portfolio.Web/Pages/**/*.cshtml` | 170 (150 HTML) | frontend packages (Index card dedup, page layout) | R9 |
| C10 | `Portfolio.Web/wwwroot/js/**` | 447 | frontend JS packages (shared fetch/toast modules, XSS fixes) | R2 for inventory.js |
| C11 | `Portfolio.Web/wwwroot/css/**` | 208 (54 mojibake) | CSS token consolidation | Also fix css/README (DH-07) |
| C12 | `native/**` (.cpp/.h) | 159 | native/bridge packages | Mostly keep; ~8 banners |
| C13 | `dotnet format` pass + CI format gate (CI-01) | 118 files | **everything** | Last commit of Stage 2 |

---

## B. Tests and CI (Goal 6)

### B1. Baseline, run the way CI runs it (scratch clone `repo`)

```
export ContinuousIntegrationBuild=true    # same as ci.yml env
dotnet restore                       → exit 0 (NuGet audit active; no NU190x)
dotnet build --no-restore -c Release → 0 warnings, 0 errors
dotnet test Portfolio.Tests --no-build -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
                                       --logger "trx;LogFileName=test-results.trx" --results-directory TestResults
                                     → Passed 697, Failed 0, Skipped 0
py -I coverage_gate.py  (ci.yml:82-114 extracted verbatim, env values substituted)
                                     → Line 83.65% (12172/14551) min 80; Branch 67.36% (2134/3168) min 60; exit 0
```

### B2. Coverage detail (same report)

**Per assembly:** Common 35.00% line (14/40), 0% branch (0/8) · Repositories 47.80% (326/682), 47.29% (35/74) · **Services 90.73% (11,180/12,321), 75.91% (1,989/2,620)** · **Web 43.23% (652/1,508), 23.60% (110/466)**.

**Lowest-covered classes (≥ 20 coverable lines):**

| Coverage | Class (file) | Note |
|---|---|---|
| 0% (0/451, 0/26 br) | `Program` (Program.cs) | 30% of Web's coverable lines |
| 0% (0/69, 0/32 br) | AnonymousUserMiddleware | runs on every request |
| 0% (0/68) | UserProfileRepository | + Fiber{Shipment 42, Material 39, Order 34, Client 24, InventoryTransaction 13}, SavedFeature 36, Property 33, SavedSearch 32, Collection 31 repositories: **all 0%** |
| 0% (0/33, 0/16 br) | ApiExceptionMiddleware | RFC 7807 + leak guard |
| 0% (0/26) | Areas/Account ProfileModel | |
| 0% (9, 9, 12, 12, 12) | SecurityHeadersMiddleware, DatabaseReadyHealthCheck, SpatialClusterController, SpatialOverlayController, ViewshedController | 3 controllers have **no test class** |
| 15.7-34.0% | all 12 `*NativeBridge` classes (e.g. NetworkTraceNativeBridge 20/127) | native-only code; §B3 |
| 29.6% (8/27) | HomeSearchPreferencesDto | `Validate` (IValidatableObject) 0/19 |
| 50.0% | Pages IndexModel | |
| 63.8% (44/69) | FiberMaterialService | `ReceiveStockAsync` 0/25 (uses a DB transaction) |
| 73.5% | ArcGisService | 46.7% branch |
| 74.9% (140/187) | HomeScoringService | `ScoreWithNativeKernel` 0/44 (native-only) |
| 76.9% | ProfileController | `SetAuthenticatedClaim` untested |

**Lowest methods (≥ 8 lines):** `Program.<Main>$` 0/451 · `AnonymousUserMiddleware.InvokeAsync` 0/65 · `HomeScoringService.ScoreWithNativeKernel` 0/44 · `NativeScoringBridge.MapProperty` 0/28 · `FiberMaterialService.ReceiveStockAsync` 0/25 · `GeoStreamNativeBridge.ProcessBatch` 0/24 · `ApiExceptionMiddleware.WriteProblemDetailsAsync` 0/21 (0/12 br) · `HomeSearchPreferencesDto.Validate` 0/19 (0/8 br) · `UserProfileRepository.RunInTransactionAsync` 0/16 · `FiberShipmentRepository.UpdateAsync` 0/16.

**Hidden from coverage:** `coverlet.runsettings:22` excludes `[Obsolete]` members. That hides 3 **live** endpoints: HomeFinderController `Score` (`POST homefinder/score`), `GetPropertyLegacy` (`GET homefinder/properties/{id}`), and BatchGeocodingController `GeocodeAddresses` (`POST geocoding/batch/sync` and `/api/batchgeocoding/sync`). See ADD-03 and D6.

### B3. Native-parity job reproduced (Windows, MSVC 14.44, CMake 3.31.6)

Steps (mirroring ci.yml:140-199):
1. `dotnet build -c Debug`.
2. For each of the 13 `native/*` dirs: `cmake -S … -B <short dir>` and `cmake --build … --config Release`.
3. Copy the 13 DLLs into `Portfolio.Tests/bin/Debug/net10.0`.
4. `dotnet test --no-build -c Debug` with native present → **697/697 passed**.
5. `PORTFOLIO_DISABLE_NATIVE=1 dotnet test …` → **697/697 passed**.

Coverage with native present proves the kernels **loaded**: every bridge jumps from 15.7-34.0% to 63.2-78.1% (e.g. NetworkTraceNativeBridge 15.7 → 74.3%, VrpSolverNativeBridge 20.5 → 72.8%).

| Run (Debug, same build) | Line | Branch |
|---|---|---|
| Managed only (`PORTFOLIO_DISABLE_NATIVE=1`) | 82.35% (13,554/16,460) | 67.77% |
| Native present | **77.99%** (12,837/16,460) | **51.54%** |
| Union of the two | **87.72%** (14,439/16,460) | 69.49% |

(Debug produces more sequence points than Release, so the Debug denominators differ from the CI gate's 14,551.)

Implications:
1. A coverage gate must **never** be added to the native job as-is. It would fail at 80/60 because services skip their managed fallbacks when native is present.
2. The bridges (~1,100 lines) are only exercised in a job that collects no coverage (CI-07 is optional).
3. The 12 native-gated tests report "Passed" in the managed job without asserting anything (TST-08).

### B4. Code the named refactors will touch, and the characterization tests to write first

General rule: write these **before** the refactor, run them green on `main`'s code, then refactor without editing them. Values below marked "measured" came from a throwaway probe in scratch clone `exp` on the managed path (production behaviour).

**B4.1 Program.cs split into `AddPortfolio*` extension methods.** Program.cs is 0% covered.

(a) **Extract-then-test the helpers** (TST-05). Move `ParsePostgresConnectionString` (Program.cs:675-743), `NormalizeRedisConnectionString` (:748-792) and `MaskRedisPassword` (:797-812) verbatim into an `internal static class` in Portfolio.Web, plus `InternalsVisibleTo("Portfolio.Tests")`. Then pin:
- Postgres:
  - a `Host=` string passes through unchanged;
  - `postgres://u:p%40ss@db.example.com:6543/app?sslmode=disable` → Host, Port 6543, Username `u`, Password `p@ss`, Database `app`, SslMode Disable;
  - `postgresql://u:p@host/db` → Port 5432, SslMode **Require** (default);
  - `postgres://u:p@[::1]/db` → Host `127.0.0.1`;
  - missing db / missing password / wrong scheme / malformed → `ArgumentException`.
  - Also pin that the malformed-input message **contains the raw string** (Program.cs:686), which leaks the password. Cross-ref security agent (ADD-07); their fix flips that assertion.
- Redis: `""` → `""`; `"h:6379,password=x"` passes through; `redis://:pw@h` → `h:6379,password=pw,abortConnect=False`; `rediss://default:pw@h:6380` → `h:6380,password=pw,ssl=True,abortConnect=False`; `rediss://h` → `h:6380,ssl=True,abortConnect=False`.
- Mask: `h:6380,password=pw,ssl=True` → `h:6380,password=***,ssl=True`; matching is case-insensitive.

(b) **Options tests per extension** (no host). Build `ServiceCollection` + in-memory config, call the extension, resolve options and assert:
- `AuthenticationOptions.DefaultScheme == DefaultChallengeScheme == "Cookies"`.
- Cookie: `LoginPath "/Login"`, `LogoutPath "/Logout"`, `SameSite Lax`, `ExpireTimeSpan 8h`, `SlidingExpiration`, `SecurePolicy` SameAsRequest in Development and Always otherwise.
- Google: `CallbackPath "/signin-google"`, `SaveTokens false`, scopes include profile and email.
- Policy `"Authenticated"` is the only policy.
- Invoke `OnRedirectToLogin` and `OnRedirectToAccessDenied` directly with a `DefaultHttpContext`: `/api/...` → 401/403 and no `Location`; `/Projects/...` → 302.
- `RateLimiterOptions.RejectionStatusCode == 429`.
- With empty Redis config, `IBatchJobStore` is `InMemoryBatchJobStore` (singleton) and `IDistributedCache` is the memory cache.
- Resolving `IBatchGeocodingService` yields an instance whose `HttpClient` came from `AddHttpClient`; this pins which of the duplicate registrations wins (ADD-06).

(c) **Host smoke tests** (TST-06, decision D1). `WebApplicationFactory<Program>` with `ASPNETCORE_ENVIRONMENT=Development`, `RunMigrationsOnStartup=false`, dummy Google values, and `PortfolioDbContext` swapped to EF InMemory via `ConfigureTestServices`:
- `GET /health/live` → 200 with `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy: frame-ancestors 'none'; base-uri 'self'; object-src 'none'` (values observed live).
- `GET /api/v1/fiber/orders` unauthenticated → **401** with no redirect; `GET` of an `[Authorize]` page → 302 to `/Login?ReturnUrl=…`.
- 16 `POST`s to an `expensive` endpoint from one IP → the 16th returns 429. 305 × `GET /health/live` → no 429 (opt-out).
- `GET /openapi/v1.json` → 200; snapshot paths + schemas (normalized) to guard JSON shapes; assert no request-body description contains "cancellation" (after DOC-01).

**B4.2 FleetRoutingService split** (397 lines; 92.9% line / 85.1% branch).
- Golden presets (**measured**; README claims hold; MaxIterations 1000, preset fleet parameters):
  - `morning`: 2/3 vehicles, 35.04 km, final objective 85.043
  - `fullday`: **4/5**, 61.38 km, 161.381
  - `tight`: **6/6**, 77.00 km, 226.999
  - all feasible, 0 unserved
- Pin `VehiclesUsed`, `Feasible`, `UnservedStopIds`, `TotalDistanceKm` (1e-2), `FinalObjective` (1e-3) and per-vehicle `StopIds` sequences (deterministic; the native path is bit-identical per native/README.md).
- Uncovered:
  - `Validate` lines 152, 157, 161, 166, 168, 174 → `[Theory]` (capacity ≤0 or NaN, negative fixed cost, non-finite depot, demand ≤0, demand > capacity, negative service time) → `ArgumentException`.
  - `TruncateToFleet` lines 451-462 (0%): a 2-vehicle request whose windows force more routes than vehicles → `Routes.Count ≤ 2`, dropped stops appear in `UnservedStopIds`, longer routes kept.
  - `OrOptMove` :549, `RouteFeasible` :324, `BuildResult` :672/681/685, `ExpandRoutePathsAsync` :595-596.

**B4.3 ResponseCoverageService split** (90%+).
- Golden (**measured**; README:370 holds). Shipped scenario has 450 demand points, 24 candidates, existing `[1,2]`. Baseline is 76.40% within 4 min, p90 4.68 min, mean 2.48. At p = 4:
  - mean objective → chosen `[1,2,3,5]` (95.96%, p90 2.42)
  - **p90 objective → `[1,2,3,4]` (94.06%, p90 2.42, NFPA pass)**
  - max-coverage → `[8,12,13,14]` (98.11%, p90 3.57)
- Pin chosen ids and stats to 1e-2.
- Uncovered: `OptimizeAsync` 122, 124, 126, 144, 169-171; `ValidateDemandPoints` 207, 209; `ValidateCandidates` 221, 223; `ValidateStationCount` 229, 233; `ValidateBands` 245; `WeightedPercentile` 508, 520; `Evaluate` 550-552.

**B4.4 UserProfileSeedService split** (1,464 lines; one 1,169-line method; 99% covered by a test that asserts only `Any()`).

Pin for a fresh user (EF InMemory, as the existing test does):
- exact row counts per entity: clients, materials, orders, shipments, inventory transactions, properties. There are 12/12/17/11/23/2 `Add(` call sites, so confirm actual counts when recording;
- an ordered fingerprint of business fields, excluding ids and timestamps;
- **idempotency**: a second call adds 0 rows;
- **isolation**: seeding user B leaves user A's rows untouched; all rows carry B's id.

Uncovered: line 30 (transaction path, relational only) and 1444-1461 (catch/rollback).

**B4.5 ChangeDetectionService and SpatialGraphService splits.**
- **Change**:
  - Golden on the shipped scene with the page's default parameters (read them from `Pages/Projects/Change/Index.cshtml`). Pin `Threshold`, `ChangedPixels`, blob count and each blob's (Area, MinX, MinY, MaxX, MaxY), and that each of the 4 ground-truth boxes overlaps a blob ("4 of 4 recovered", README:342, **not verified by me**).
  - Uncovered: `DetectAsync` validation 61, 65, 69, 78, 82, 95, 120-121, 144, 160; `ErodeOnce` 350-351; `LabelComponentsManaged` 503-505; `FindRoot` 564-566; `UnionLabels` 581-582 (use a U-shaped mask that merges two provisional labels).
- **Graph**:
  - On the Redlands network, Dijkstra and A* from a fixed node to `EsriHqNodeId 2530`: pin `TotalCost`, `NodeIds` count and endpoints, `DistanceKm`, `EstimatedMinutes`, and `ExploredNodes(A*) < ExploredNodes(Dijkstra)`.
  - Pin the service-area count for a fixed `MaxCost`, plus a distances checksum.
  - **Immutability guard:** hash nodes and edges of `GetGraphAsync()` before and after running route, fleet and response optimizations, and assert unchanged and `ReferenceEquals` across calls.
  - Uncovered: 55-59, 80, 82, 88-89, 123, 148, 159, 164, 189, 287, 337-345, `ValidateGraph` 530, 532, 536, 549.

**B4.6 Security fixes** (CSP, forwarded headers, rate limiting, input caps).
- **SecurityHeadersMiddleware** (0%): invoke with `DefaultHttpContext` and a no-op next; assert the 4 headers and the exact CSP string. The CSP package updates the expected value in the same commit.
- **ApiExceptionMiddleware** (0%), one row per exception:
  - `ArgumentException`/`ArgumentNullException` → 400 "Bad Request" with `ex.Message`
  - `KeyNotFoundException` → 404
  - `InvalidOperationException` → 409
  - `UnauthorizedAccessException` → 401
  - `OperationCanceledException` with `RequestAborted` → 499
  - anything else → **500 with the generic detail, never `ex.Message`**
  - Content-Type `application/problem+json`, `instance` = path.
- **AnonymousUserMiddleware** (0%, 32 branches): EF InMemory + `FakeTimeProvider` (exists, Tests/Helpers) + a mocked `IAuthenticationService`. Cases:
  - `Items` already set → passthrough;
  - authenticated user with a GoogleId claim → `Items["PortfolioIdentity"] = claim.UserId`;
  - no cookie and `Accept: text/html` → profile created, seed called, cookie set (HttpOnly, Lax, ~1 year, Secure outside Development);
  - no cookie and JSON Accept → no profile, no cookie;
  - valid cookie with a missing profile → created;
  - `LastActiveDate` older than 5 min → updated; newer → no save.
- **Forwarded headers:** after extracting the options, assert the current `ForwardedHeaders` value and the cleared `KnownProxies`/`KnownIPNetworks`. At host level (D1), show that two different `X-Forwarded-For` values get separate rate-limit budgets (the spoofing risk); the security fix inverts this test.
- **Input caps:** a reflection test that every `[HttpPost]`/`[HttpPut]` action has `[RequestSizeLimit]` (or is on an explicit exemption list), and, once the security package adds them, that every spatial controller has `[EnableRateLimiting("expensive")]`. Today 9 of 25 controllers have it. Per service, a `[Theory]` with "limit + 1 → `ArgumentException`".
- **HomeSearchPreferencesDto.Validate** (0/19): `Validator.TryValidateObject(dto, ctx, results, true)` for out-of-range weights and weights not summing to ~1.

**B4.7 Controller try/catch consolidation.**

There are 52 catch clauses (27 `ArgumentException`, 12 `Exception`, 4 `InvalidOperationException`, 3 `KeyNotFoundException`, 5 Polly, 1 `BrokenCircuitException`) and three different error shapes:
- `{ error = ex.Message }` (400/409);
- `ProblemDetails` 500 (Fiber controllers catch `Exception`);
- 503 ProblemDetails with `retryAfterSeconds` (circuit breaker).

**Pin the body text, not just the type.** An `ArgumentException` thrown with `nameof(request)` serializes as `"<msg> (Parameter 'request')"`; ViewshedService.cs:119/121/123 omit `nameof` and so produce different text.

Add the missing tests first: `SpatialClusterController.RunDbscan`, `SpatialOverlayController.SpatialJoin`, `ViewshedController.Compute` (Ok and 400-with-body each), `ProfileController.SetAuthenticatedClaim`, and the FiberShipments Create/Update/UpdateStatus error paths.

**Design consequence:** if translation moves to a filter or middleware, the 38 `Assert.IsType<BadRequestObjectResult>` unit tests break, because the exception propagates out of the action. Either test the filter in isolation and change those tests to `Assert.ThrowsAsync`, or use host-level tests (D1).

**B4.8 Route and contract guards** (TST-03).
- Reflection over `ControllerBase` subclasses → sorted `(verb, "api/v1/<prefix>/<template>")` list vs a committed snapshot (77 entries, including the 3 `IgnoreApi` aliases).
- Parse `wwwroot/js/api-config.js` for `/api/v1/...` strings and assert each matches a controller template.
- This guards "public routes don't change" and the "single source of truth for client routes" contract.

### B5. Test-convention violations (agent-context §9)

| # | Violation | Evidence |
|---|---|---|
| V1 | Several SUTs per test class | SpatialComputeControllerTests (5 controllers: CatRisk, GeoStream, RasterTerrain, SpatialGeometry, SpatialNetwork); SpatialComputeServiceTests (4 services, **plus a controller test** `SpatialNetworkController_GetGraph_…` at :944); HotspotViewshedOverlayServiceTests (3 services) |
| V2 | Actions with no test | SpatialClusterController.RunDbscan, SpatialOverlayController.SpatialJoin, ViewshedController.Compute (controllers 0%), ProfileController.SetAuthenticatedClaim; the `[Obsolete]` HomeFinder aliases `Score` and `GetPropertyLegacy` (and invisible to coverage) |
| V3 | Missing validation or not-found cases | HomeFinderController.Search: the `!ModelState.IsValid` branch (HomeFinderController.cs:42) is untested, and unreachable under `[ApiController]` (automatic 400); FiberShipments Create/Update/UpdateStatus: happy path only; FiberMaterials/Orders Create/Update: Ok and 500 only, no 400/404 |
| V4 | Error **bodies** never asserted | 38 `Assert.IsType<BadRequestObjectResult>`, no `{ error }` check |
| V5 | Native-gated tests pass vacuously | 12 tests use `return` when unavailable (TST-08) |
| V6 | "Assert the result, not whether native ran" | **No violations** in the managed suite. `NativeParityTests` asserting `NativeAccelerated` inside native-gated tests is intended. SpatialComputeControllerTests.cs:244 and :350 assert a **mocked** `NativeAccelerated = true` passes through, which is acceptable but could be renamed `…_PassesServiceResultThrough` |
| V7 | A doc tells agents to break the rule | docs/plans/README.md:260-261 still says "The happy-path test asserts `Assert.False(result.NativeAccelerated);` first". 0 such asserts remain (good) (DH-06) |
| V8 | Two conflicting "how to write a test" docs | agent-context §9 ("`Mock<IXxx>` fields") vs docs/plans/README.md:254-256 ("construct the SUT inline … no shared fields"). Both patterns exist; pick one per test type |
| V9 | Trivial | UserProfileSeedServiceTests.cs:6 `using Xunit;` duplicates the global using |

### B6. CI review (`.github/workflows/ci.yml`)

**What's gated today:**
- Restore, with NuGet audit. NU1902/3/4 are errors via Directory.Build.props:24-27 with `NuGetAuditMode=all`, `NuGetAuditLevel=low`, **so vulnerable direct and transitive packages already fail the build**. `dotnet list package --vulnerable` would be redundant.
- Release build with warnings-as-errors.
- Tests plus the 80/60 coverage ratchet.
- Native job: 13 kernels built, the staged count must equal the CMakeLists count, tests with and without native.
- Docker image builds.
- Deploy hook (inert without the secret).
- Top-level `permissions: contents: read`, concurrency cancel and timeouts are present.

**Missing:** format gate (CI-01), dependency updates (CI-02), PR dependency review (CI-03), CodeQL (CI-04), native load assertion (CI-05), per-assembly floor and deterministic report pick (CI-06), C++ warnings (CI-08), and a complexity gate (owned by the complexity agent: CA1502 or Roslyn metrics).

**Cross-ref the security agent** for action SHA pinning (all `uses:` are tag-pinned `@v4`/`@v3`/`@v6`), per-job permissions, and image scanning.

Sketches (pin actions to SHAs per the security agent; `<sha>` below):

```yaml
# ci.yml — test job, after "Restore" (CI-01; only after the one-time format pass and decision D3)
      - name: Verify formatting
        run: dotnet format Portfolio.sln --verify-no-changes --no-restore --exclude Portfolio.Repositories/Migrations --verbosity minimal

# ci.yml — native-parity job (CI-05, with TST-08)
      - name: Test with native kernels present
        env:
          PORTFOLIO_REQUIRE_NATIVE: '1'      # NativeAvailabilityTests fails if any of the 13 bridges did not load
        run: dotnet test Portfolio.Tests --no-build -c Debug --logger "console;verbosity=normal"

# ci.yml — new job (CI-03)
  dependency-review:
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@<sha>
      - uses: actions/dependency-review-action@<sha>
        with:
          fail-on-severity: low
```

```yaml
# .github/dependabot.yml (CI-02) — CPM (Directory.Packages.props) is supported by the nuget ecosystem
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule: { interval: weekly }
    groups:
      aspnetcore-efcore: { patterns: ["Microsoft.*", "Npgsql.*", "Asp.Versioning.*"] }
      test: { patterns: ["xunit*", "Moq", "coverlet.*", "Microsoft.NET.Test.Sdk"] }
  - package-ecosystem: github-actions
    directory: "/"
    schedule: { interval: weekly }
  - package-ecosystem: docker
    directory: "/"
    schedule: { interval: weekly }
```

```yaml
# .github/workflows/codeql.yml (CI-04)
name: CodeQL
on:
  push: { branches: [main] }
  pull_request: { branches: [main] }
  schedule: [{ cron: '23 4 * * 1' }]
permissions:
  contents: read
jobs:
  analyze:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    permissions:
      contents: read
      security-events: write
    strategy:
      fail-fast: false
      matrix:
        include:
          - { language: csharp, build-mode: none }
          - { language: javascript-typescript, build-mode: none }
          - { language: c-cpp, build-mode: manual }
    steps:
      - uses: actions/checkout@<sha>
      - uses: github/codeql-action/init@<sha>
        with:
          languages: ${{ matrix.language }}
          build-mode: ${{ matrix.build-mode }}
      - if: matrix.language == 'c-cpp'
        run: |
          for d in native/*/; do k=$(basename "$d")
            cmake -S "$d" -B "build/native/$k" -DCMAKE_BUILD_TYPE=Release && cmake --build "build/native/$k"
          done
      - uses: github/codeql-action/analyze@<sha>
```

```yaml
# ci.yml — coverage gate hardening (CI-06): replace "root = ET.parse(reports[0])" with
          reports = sorted(glob.glob('TestResults/**/coverage.cobertura.xml', recursive=True))
          import hashlib
          if len({hashlib.md5(open(r,'rb').read()).hexdigest() for r in reports}) != 1:
              sys.exit('::error::Expected exactly one distinct coverage report.')
          root = ET.parse(reports[0]).getroot()
          # optional per-package floor:
          web = next(p for p in root.iter('package') if p.get('name') == 'Portfolio.Web')
          if float(web.get('line-rate'))*100 < float('${{ env.MIN_WEB_LINE_COVERAGE }}'): failed = True
```

### B7. `dotnet format --verify-no-changes` status

| Checkout | Result | Detail |
|---|---|---|
| Windows (CRLF via `core.autocrlf=true`), whole solution | **exit 2**, 121 files | WHITESPACE 1,250 changes / 42 files · FINALNEWLINE 63 files · IMPORTS 35 files · CHARSET (BOM) 7 files, **3 of them migrations** |
| Same, `--exclude Portfolio.Repositories/Migrations` | **exit 2, 118 files** | Applying gives 118 files, +1,356/−1,236 lines; ignoring whitespace, 42 files +258/−138 (import order, final newlines) |
| After applying, build with CI flags + tests | **0 warnings, 0 errors; 697/697 pass** | scratch clone `fmt2` |
| Simulated Linux/CI checkout (all `.cs` LF), `.editorconfig` unchanged | exit 2, **41,219 ENDOFLINE changes in 319 files** on top of the above | the index is LF (`git ls-files --eol`: all 328 `.cs` are `i/lf`); `.editorconfig` says `end_of_line = crlf` |
| LF checkout, `end_of_line` removed from `.editorconfig` | exit 2 with only the same 118 genuine files; **after the format pass, exit 0** | measured with SDK 10.0.401 |

Biggest offenders: FiberMaterialsController.cs (177 whitespace edits), FiberOrdersController.cs (139), HomeScoringServiceTests.cs (123), HomeFinderController.cs (122), NativeScoringBridgeTests.cs (100). `RedlandsRoadNetwork.cs` is **not** touched by the formatter.

---

## C. Documentation hygiene (Goal 7)

### C1. `.github/copilot-instructions.md` (1,153 lines): 50 stale statements

| # | Line(s) | Says | Source truth |
|---|---|---|---|
| 1 | 15-22 | Lists 4 native demos | 18 projects, 13 kernels |
| 2 | 24-32 | "Projects (5)" | 6 projects in Portfolio.sln (+ Portfolio.Benchmarks) |
| 3 | 52 | 5 kernels; "Only the scoring kernel currently has a native-vs-managed parity test" | 13 kernels; NativeParityTests (geometry clip, viewshed) + NativeScoringBridgeTests; CI native job |
| 4 | 74-75 | Controllers use `[Route("api/[controller]")]` | All 25 use explicit `api/v{version:apiVersion}/<segment>` |
| 5 | 98 | `UnauthorizedAccessException` "not currently caught" | ApiExceptionMiddleware.cs:56 maps it to 401 |
| 6 | 143-144 | Uncaught exceptions fall through to `UseExceptionHandler("/Error")` | ApiExceptionMiddleware (Program.cs:585) returns RFC 7807 first |
| 7 | 148 | "All services, all repositories, and all API controllers carry `ILogger<T>`" | 7 repositories, 5 services, 2 controllers (HomeFinder, UserProfile) have none |
| 8 | 155 | "No Debug/Trace levels exist" | `LogDebug` at UserProfileSeedService.cs:1446 and ApiExceptionMiddleware.cs:36 |
| 9 | 175-176 | "No Singleton, no Transient registrations" | Singletons: TimeProvider (Program.cs:46), IBatchJobStore (:219/:224); typed `AddHttpClient` clients are transient (:89/:130/:155/:180) |
| 10 | 178-181 | ArcGisService "registered twice" | Once, via `AddHttpClient` (Program.cs:89) |
| 11 | 185-190 | Geocoding services registered as scoped **and** `AddHttpClient`, "preserve both" | True (Program.cs:129-130, 154-155, 179-180), but the later typed-client registration wins; the `AddScoped` lines are dead (ADD-06) |
| 12 | 195 | `AddMemoryCache` kept "for any remaining IMemoryCache consumers" | None exist |
| 13 | 200-202 | Registers `IFiberClientService` | Does not exist |
| 14 | 208-209 | `AddVersionedApiExplorer` | `.AddApiExplorer(...)` (Program.cs:488) |
| 15 | 217-219 | `[Route("api/[controller]")]`; "one controller uses `api/features`" | See #4 |
| 16 | 229 | Always `CancellationToken cancellationToken` | BatchGeocodingController uses `ct` |
| 17 | 244-247 | "No [Required]/data-annotation validation, no ModelState checks" | DataAnnotations in CollectionDto, FiberMaterialDto, FiberOrderDto, HomeSearchPreferencesDto; `IValidatableObject` in HomeSearchPreferencesDto.cs:9; ModelState check at HomeFinderController.cs:42 |
| 18 | 249-252 | "Swagger is always on"; every action needs `<param>`/`<returns>` | OpenAPI + Scalar; the token `<param>` corrupts request-body docs (§A5.3) |
| 19 | 261 | Mappings are `*Map.cs` | Also `Fiber*Mapping.cs` (5 files) |
| 20 | 271-272 | Includes `Collection` and `UserNotes` | No `UserNote` entity exists |
| 21 | 274-276 | `SavedFeatureService.CreateAsync` uses a transaction | No transaction there; they live in UserProfileRepository.RunInTransactionAsync, FiberMaterialService.ReceiveStockAsync, UserProfileSeedService |
| 22 | 290 | `HttpContext.Items["AnonUserId"]` | `Items["PortfolioIdentity"]` |
| 23 | 307-308 | HomeFinder takes `userId` as a query parameter | Uses `IUserProfileService.GetCurrentUserId()`; 401 if none (HomeFinderController.cs:95/114/129) |
| 24 | 309-312 | `FiberClientsController` | Does not exist |
| 25 | 321-322 | Cookie `SecurePolicy = Always`, `SameSite = None` | SameAsRequest in Development, Always otherwise; **SameSite=Lax** (Program.cs:296-299) |
| 26 | 331 | "HTTPS redirection + HSTS enabled in non-development" | HSTS non-dev; HTTPS redirection **dev-only** (Program.cs:569-574) |
| 27 | 346-353 | Common folders | Missing `Configuration/`, `Constants/` |
| 28 | 456-458 | Route `api/reversegeocoding` | `api/v1/geocoding/reverse` |
| 29 | 487-490 | Routes `api/addressstandardization/...` | `api/v1/addresses/parse` and `api/v1/addresses/validate` |
| 30 | 520-524 | Fiber routes `api/v1/fiberorders` etc. + clients | `api/v1/fiber/{orders,materials,shipments,dashboard}`; no clients controller |
| 31 | 526-527 | "Each controller exposes standard CRUD" | Dashboard is `GET stats` only; shipments add `PUT {id}/status`; materials add `POST {id}/receive` |
| 32 | 536 | `IFiberClientService` / `FiberClientService` | Does not exist |
| 33 | 551 | `CreateFiberClientDto`, `UpdateFiberClientDto` | Only `FiberClientDto` and `CreateFiberClientDto` (FiberClientDto.cs:6, 42) |
| 34 | 555-556 | `FiberOrder.Shipments` navigation | Removed (migration `RemoveUnusedFiberNavsAndNameIndex`) |
| 35 | 562-569 | HomeFinder `/score`, `/properties/{id}`, `userId` query param | Primary `/search`, `/property/{id}`; old paths are `[Obsolete]` aliases hidden from OpenAPI |
| 36 | 637 | NativeScoringBridgeTests: "9 parity + 11 managed" | 20 tests: 7 native-gated + 13 always-run |
| 37 | 646-648 | "currently implemented native libraries" = 5 | 13 |
| 38 | 656-667 | Kernel dirs have `tests/` and `benchmarks/`; separate CMake test targets | No kernel has either; benchmarking is the C# Portfolio.Benchmarks |
| 39 | 736-737 | Every native service has native-gated parity tests | 3 kernels have them (scoring, geometry clip, viewshed) |
| 40 | 742-743 | Benchmarks distinguish "native SIMD time" | No kernel has a SIMD path (native/README.md:182-183) |
| 41 | **783-784** | Geometry managed fallback = "**bounding-box vertex clamp**" | **Managed is Sutherland-Hodgman** (SpatialGeometryService.cs:75). Clamping was the **native defect** the parity check found (native/README.md:93-100). An agent following this line would reintroduce the bug |
| 42 | 792-795 | Geometry DTOs `Points`, `BoundingBox`, `ClippedVertices`, `VertexCount` | `PolygonClipRequestDto {Subject, MinX, MinY, MaxX, MaxY}`; `PolygonOperationResultDto {Vertices, NativeAccelerated}` |
| 43 | 846-849 | `RouteResultDto` `PathNodeIds`, `CoordinatePath` | `NodeIds`, `Path`, `ExploredNodes`, `ExploredNodeIds`, `DistanceKm`, `EstimatedMinutes`, `AlgorithmUsed` |
| 44 | 854-855 | Dockerfile sets `ASPNETCORE_URLS` | Sets `ASPNETCORE_HTTP_PORTS=${PORT:-10000}` (Dockerfile:41) |
| 45 | 979-980 | "DO NOT edit `Portfolio.Repositories/SavedFeatureRepository.cs` (the duplicate at the project root)" | No such file |
| 46 | 1002-1003 | "No integration tests or DB tests exist" | Two classes use EF InMemory (UserProfileSeedServiceTests.cs:18, UserProfileServiceTests.cs:32) |
| 47 | 1070 | "355 passing, 34 native-gated" | 697 executed (609 attributes); 12 native-gated |
| 48 | 1079-1080 | Namespaces block-scoped; "match this style in new files" | Mixed; `.editorconfig` prefers file-scoped (suggestion) |
| 49 | 1082 | "No `#region` blocks anywhere" | ProfileController.cs:36 |
| 50 | 1094-1095, 1116, 1133-1135 | "C# 12"; `Items["AnonUserId"]`; "Swagger XML comments" | C# 14 (net10); `PortfolioIdentity`; OpenAPI/Scalar |

**Recommendation: slim it, don't patch it** (DH-01). Most of the 1,153 lines are a duplicate, drifting inventory of routes, DTO fields and counts. Replace with about 80 lines:
- the invariant rules: layering, both identity systems, DTO location, error shapes, native fallback contract, "never hand-edit RedlandsRoadNetwork.cs", migrations via `dotnet ef`, the test rules, the DO NOT list;
- plus "for facts see `docs/agent-context.md`".

**Prerequisite:** `docs/agent-context.md` and `docs/cleanup-prompt.md` are **untracked** (`git status`), so the pointer dangles until agent-context.md is committed.

### C2. Repo-name mismatch

`git remote -v` → `https://github.com/brianpcoffey/gis-portfolio.git`.

- **Wrong (`portfolio_v2`):** README.md:444, 445, 530; docs/render-deployment.md:49.
- **Correct already:** README.md:3 (CI badge), 54 links in Index.cshtml and Projects/Index.cshtml, the résumé PDF link (`https://github.com/brianpcoffey/gis-portfolio`, found in the PDF bytes), agent-context.md:100.

### C3. README.md claims vs source

| Line | Claim | Truth |
|---|---|---|
| 18, 495 | "680 Tests" badge; "680 tests, all passing" | **697** executed (609 attributes) |
| 31, 46-52 | "4-layer architecture across 5 projects" + table of 5 | 6 projects (Portfolio.Benchmarks missing) |
| 444-445, 530 | `portfolio_v2` | `gis-portfolio` |
| 475-478, 486 | `dotnet run --project Portfolio.Web`, then open `https://localhost:5001` | Without `--launch-profile`, the first profile (`http`) serves **http://localhost:5000** only (launchSettings.json; agent-context §7) |
| 532 | Render: "migrations run automatically at startup — no manual migration step required" | Program.cs:624-637 documents Render's Pre-Deploy `--migrate` with `RunMigrationsOnStartup=false` (Render's actual settings aren't in the repo: F) |
| 107 | "Cookie-based session management with `SecurePolicy = Always`" | Always only outside Development |
| 122 | "Structured logging with `ILogger<T>` across all services, repositories, and controllers" | 14 classes have no logger (C1 #7) |
| 349, 363, 378 | Sections lack the `---` separator and blank line used elsewhere | Cosmetic |
| 82-89, 153, 217, 245, 278, 326, 343, 372, 387 | Speedup figures | **All consistent with native/README's table** (§C4) |
| 160, 173, 186, 199, 212, 226, 240, 254, 273, 287, 301, 315, 331, 348, 362, 377, 391 | API routes | **All match the controllers** |
| 370, 384 | Response (76.4% → 94.1%, p90 4.68 → 2.42) and Fleet ("2 of 3 / 4 of 5 / all 6") headline numbers | **Reproduced exactly** (§B4.2-3) |

### C4. Speedup figures: verdict and full inventory

**C4.1 Verdict.** The committed table (native/README.md:44-64, 21 rows) gives a range of **0.37×–2.26×**. With the README's stated rule (win ≥ 1.15×, loss ≤ 0.85×) that is **9 wins / 5 losses / 7 ties**:
- Wins: 1.22, 1.31, 1.90, 1.48, 1.53, 1.66, 1.73, 2.26, 1.92
- Losses: 0.59, 0.37, 0.77, 0.83, 0.45
- Ties: 0.99, 0.94, 0.98, 0.92, 1.02, 0.99, 1.01

So **agent-context.md:141 and README.md:82 are correct.** The earlier owner note ("0.32×–2.26×, native loses on 8 of 21") is not supported:
- `git log -S'0.32' -- native/README.md README.md` returns nothing, so "0.32×" never appeared in either file. The first harness run (`2f9a7bb`) had fan triangulation at **0.34×** with range "0.34× to 2.27×"; the re-run after the parity fixes (`5d18a67`) gives 0.37×.
- "Loses 8" comes from native/README.md:314-315: "Native wins 8 of the 21 workloads, loses 8, and is indistinguishable on 5". Re-counting the same table:
  - at ±15%: 9/5/7
  - at ±5%: 9/7/5
  - at a strict 1.0: 11/10
  - **No threshold yields 8/8/5**, and the first table gave 11/5/5. That sentence is wrong and should read 9/5/7.
- Update the owner's memory note too (outside the repo).

**C4.2 Every place that states a speedup or win/loss figure:**

| Location | Says | Status |
|---|---|---|
| native/README.md:44-64 | the 21-row table | **authoritative** |
| native/README.md:166 | network_trace "turned it into the **1.24×** win above" | ✗ table: 1.01× (sweep), 1.48× (trace + restore) |
| native/README.md:169 | vrp "produced **2.27×**" | ✗ 2.26× |
| native/README.md:176 | fan triangulation "**0.34×**" | ✗ 0.37× |
| native/README.md:123-128 | "Superseded numbers": "new" values 1.65×, 1.26×, 1.06×, 0.98× | ✗ these are themselves superseded; table: 1.53× / 0.45× (Dijkstra; matrix 1.31×) / 0.99× / 0.92× |
| native/README.md:314-315 | wins 8 / loses 8 / ties 5 | ✗ 9 / 5 / 7 |
| native/README.md:321-322 | vrp 2.26×, overlay 1.90×, facility 1.73× | ✓ |
| README.md:82, 86-89 | 9 / 5 / 7; range 0.37–2.26; 2.26, 1.90, 0.59, 0.37 | ✓ |
| README.md:93 | network_trace 2.5× slower, vrp 1.7× slower (historical) | ✓ consistent with native/README |
| README.md:153, 217, 245, 278 | 0.99×; 0.59×; 1.22× and 0.83×; 0.94× | ✓ |
| README.md:326, 343, 357 | catrisk ring "below 1.0"; change "within noise"; outage "modest gain" | ✓ (0.92; 0.99; 1.01/1.48) |
| README.md:372, 387 | Response "~1.5×–1.75×"; Fleet "~2.3× at 120 stops" | ✓ (1.53–1.73; 2.26) |
| docs/resume-update.md:46 | "0.37×–2.26×" | ✓ |
| docs/agent-context.md:141; docs/cleanup-prompt.md:102 | 0.37×–2.26×, 9/5/7 | ✓ |
| Portfolio.Benchmarks/Program.cs:18 | "1.66x figure previously published" (historical) | ✓ |
| **CatRisk/Details.cshtml:62, 70-73, 99** | "Native is only about **1.1×** faster"; ad-hoc table (5.4/5.9 ms …); "measures at 1.1×" | ✗ withdrawn; table: ring **0.92×** (a loss), MC 1.02× |
| **Change/Details.cshtml:65, 73-79, 83, 114** | "best-of-50 per stage" table; CVA **1.46×**, morphology 1.09×; "cat_risk_kernel measures ~1.1×" | ✗ withdrawn methodology (native/README.md:120-123); pipeline 0.99× |
| **Fleet/Details.cshtml:64, 72-74** | "about **2.3×**"; ad-hoc table (0.64 vs 1.66 ms …) | ~ headline OK (2.26×); table rows are ad-hoc numbers not in native/README |
| **Outage/Details.cshtml:64, 72-74, 109** | faster at 267 elements, slower at **4,603**; "**1.3×** … and **0.87×** at 4,603" | ✗ native/README.md:127-129: the 4,603-element figure "could not be reproduced at all"; table: 1.01× / 1.48× |
| **Response/Details.cshtml:67, 75-78, 113** | "between **1.2×** and **2.2×**" | ✗ table: 1.53×, 1.66×, 1.73× (the 2.2× is the superseded 2.23×) |
| docs/plans/*.md | no figures (04-response-coverage.md:320 is an interview question) | ✓ |
| .github/copilot-instructions.md | no ratios; AVX2/SIMD language (592-593, 602, 742-743, 815) | see C1 #40 |

**C4.3 Implied-performance wording that the benchmarks don't back** (D5; site copy):
- "designed around a **SIMD-friendly** native terrain kernel": Pages/Index.cshtml:283 **and** :660 (duplicate card), Pages/Projects/Index.cshtml:178. Terrain is 0.83×/1.22×; no kernel uses SIMD (native/README.md:182-183).
- "Why C++ here" bullets written before the benchmark harness:
  - HomeFinder/Details.cshtml:59 ("SIMD-friendly batch") and :96 ("demonstrates SIMD-friendly data layout"); measured 0.99×.
  - Terrain/Details.cshtml:59 ("SIMD … can materially affect tile-generation latency") and :76 ("AVX2-friendly compiler flags").
  - Clustering/Details.cshtml:55 ("strong SIMD/cache-locality candidate"); measured 0.94×.
  - Overlay/Details.cshtml:55 ("natural vectorization … target"); it wins 1.90× without vectorization.
  - Viewshed/Details.cshtml:35 ("obvious SIMD/tiling candidate") and :55 ("exactly the workload where … vectorization matter"); measured 0.98×.
- Interview *questions* (GeoStream:97, Terrain:96, CatRisk:100) are fine as they are.

### C5. Other docs

**C5.1 native/README.md**: the 6 figure errors in §C4.2, plus:
- :232-233 "the suite asserts `NativeAccelerated == false` and must exercise the managed fallback". Stale since `5d18a67` (path-agnostic suite, README.md:495).
- :329-352 "Verifying parity" says only scoring has a parity test, the others assert `NativeAccelerated == false`, and catrisk parity is "not yet automated". Stale: NativeParityTests exists, the CI native job runs the whole suite with kernels present, and Benchmarks checksums all 21 workloads.
- :361-375 the file tree's last four entries use `└──` (cosmetic). :385 (`scoring_kernel.cpp`) ✓.

**C5.2 docs/plans/README.md** (written before the five verticals shipped). Add a "historical design doc; current conventions: docs/agent-context.md" banner and fix:
- :87-89: the `std::map` latent bug was **fixed** by the CSR rewrite (spatial_graph_engine.cpp:58-61).
- :260-261: `Assert.False(result.NativeAccelerated)` reverses the current rule.
- :264-266: "416 declared methods / ~494 executed" → 609 / 697.
- :291: refers to an "eight directories" count in native/README that no longer exists.
- :318: the post-build step copies to **two** dirs, Services and Web.
- :328-331: no native in Docker "is worth fixing"; now a deliberate decision (native/README.md:310-323).
- :337-345: the 6-section Details template applies to the 8 newer pages; agent-context says 8 sections for all (see C6 #13).
- :125 (ViewshedService `nameof` omissions): still true at ViewshedService.cs:119/121/123, so keep.
- :64 (5,765 lines, 587 KB): ✓ 600,840 bytes.

**C5.3 docs/render-deployment.md**:
- :49 repo name.
- No **Pre-Deploy Command** (`dotnet Portfolio.Web.dll --migrate`) and no `RunMigrationsOnStartup=false`, while the Step 4 log excerpt (:95-101) expects "Database migration successful." at startup.
- :220 "Enable Auto-Deploy from main" vs ci.yml:223-231: only a green build deploys if Auto-Deploy is **off** and `RENDER_DEPLOY_HOOK` is set. State the trade-off.
- :80 port note ✓ (Dockerfile:41). :117-118 log texts ✓ (Program.cs:217, 223).

**C5.4 docs/resume-update.md**:
- :19 "Developed 13 **high-performance** C++ modules" contradicts :46 ("No 'SIMD', 'AVX2', or 'high-performance computing' claims") and agent-context §1.
- :21 "680 automated tests" (697) and "gates deployment on every commit" (only with the deploy hook and auto-deploy off).
- :18 "role-based authorization" (one policy, no roles; Program.cs:416-420).
- :8-10 "currently serves the previous export" is time-sensitive.
- Whether the PDF itself says "high-performance" is **unverified**: its text is glyph-encoded and I used no PDF library.

**C5.5 wwwroot/css/README.md**:
- :12 "site.css — entry point; `@import`s base.css then utilities.css". Wrong: `_Layout.cshtml:91-92` links them directly and site.css has no `@import` (site.css:9-12).
- :19 "gis.css is the shared base for all /Projects/* pages": 17 of 18 (not Network); `spatialcompute.css` (11 pages) isn't mentioned.

### C6. Accuracy of `docs/agent-context.md` (16 items)

| # | Line | Says | Truth |
|---|---|---|---|
| 1 | 51 | "~680 tests (README/badge figure; ~609 attributes)" | 697 executed; 609 attributes ✓ |
| 2 | 64 | coverlet excludes Migrations and Benchmarks | Also excludes `[Obsolete]` members, which hides 3 live endpoints (B2) |
| 3 | 70 | "plans/ (00–05 design docs for the 5 vertical projects)" | 00 is the graph-engine prerequisite; 01-05 are the verticals |
| 4 | 74, 185 | Common has "NO behavior"; DTOs have "no validation attributes" | HomeSearchPreferencesDto implements `IValidatableObject.Validate` (:71); DataAnnotations in 4 DTO files (Collection 5, FiberMaterial 18, FiberOrder 13, HomeSearchPreferences 10 attributes) |
| 5 | 79 | "one pair per feature (25 services)" | 25 interfaces, **26** service classes (UserProfileSeedService has no interface; Program.cs:86) |
| 6 | 80 | `{Domain}NativeStructs/Interop/Bridge.cs` | ChangeDetection and Viewshed have no Structs file; scoring is NativeStructs / NativeScoringInterop / NativeScoringBridge |
| 7 | 84 | `Abstractions/IBatchJobStore, InMemoryBatchJobStore, RedisBatchJobStore, AssemblyInfo` | Only `IBatchJobStore.cs` is in Abstractions/; the other three are at the project root |
| 8 | 90 | Pages list | Missing AccessDenied; `Projects/Details.cshtml.cs` is unrouted (no .cshtml) |
| 9 | 140 | "the 14-file checklist" | 19 items (docs/plans/README.md:270-292) |
| 10 | 141 | "Parity (checksum equality) is enforced by NativeParityTests and in CI" | Checksum parity across all 21 workloads is Portfolio.Benchmarks only (outside CI). NativeParityTests covers geometry clip and viewshed (5 tests) + 7 gated scoring tests. CI runs the suite with kernels present (verified 697/697) |
| 11 | 149 | Anonymous id stored in `Items["AnonUserId"]`; cookie "Secure" | `Items["PortfolioIdentity"]`; Secure only outside Development (AnonymousUserMiddleware.cs:128) |
| 12 | 188 | 7 spatial controllers lack `expensive` "(others have it)" | 9 of 25 have it; Collections, Features, Fiber×4, Profile, SavedFeature and UserProfile also lack it (CRUD, likely intended; Features proxies the ArcGIS sample server) |
| 13 | 193 | "Details pages follow a fixed 8-section order" | 10 pages do; **8 pages** (CatRisk, Change, Clustering, Fleet, Outage, Overlay, Response, Viewshed) use the 6-section order (Overview → Algorithm → API Layer → Services and Native Boundary → Tradeoffs → Interview) |
| 14 | 198 | "One class per service/controller; `Mock<IXxx>` fields" | 3 multi-SUT classes (B5 V1); docs/plans prescribe inline SUTs (V8) |
| 15 | 208 | `std::map::operator[]` latent bug "in native shortest-path code" | Fixed (CSR). Keep only as a general caution |
| 16 | — (missing) | — | Add: the cancellation-token OpenAPI defect until fixed; native-present coverage is below the gate; EF InMemory test precedent; agent-context.md itself is untracked |

Verified correct (selected): 18 projects; 4 migrations; rate limits 300/15; health opt-outs; 13 kernels; pipeline order (matches Program.cs:555-590); API surface (25 routes); default challenge = Cookie; security-header values (observed live); speedups; 198 commits; remote; front-end library versions (ArcGIS 4.29 ×6, Leaflet 1.9.4 ×2, vendored D3 and jQuery 3.7.1); Docker port; road network 2,530 / 3,187 / EsriHq 2530.

---

## D. Additional recommendations

| ID | Item | Effort | Impact | Low-risk for this cleanup? |
|---|---|---|---|---|
| ADD-01 | `Directory.Packages.props:23` declares `xunit` 2.9.3, which no project references (Tests uses `xunit.v3`) | XS | tidy | Yes (cross-ref refactor agent) |
| ADD-02 | **EF InMemory is used by 2 test classes, transitively, via Portfolio.Repositories.** If the refactor removes it from Repositories (a brief candidate), add a direct `PackageReference` to Portfolio.Tests.csproj **in the same commit**, or the test build breaks | XS | prevents a broken build | Yes |
| ADD-03 | `coverlet.runsettings` excludes `[Obsolete]` members, hiding 3 live endpoints. Either retire them (D6) or drop `Obsolete` from `ExcludeByAttribute` | XS | honest coverage | Yes, after D6 |
| ADD-04 | Add `global.json` (`"version": "10.0.401", "rollForward": "latestFeature"`); this machine's SDK changed mid-session | XS | reproducible builds | Yes |
| ADD-05 | Dead `AddMemoryCache()` (Program.cs:262) | XS | tidy | Yes (refactor agent) |
| ADD-06 | Dead `AddScoped<IXxx, Xxx>` before `AddHttpClient<IXxx, Xxx>` for 3 geocoding services (Program.cs:129, 154, 179). Pin with the B4.1(b) test, then delete | S | removes a misleading pattern that copilot-instructions mandates | Yes |
| ADD-07 | Program.cs:686 puts the **whole connection string** (password included) in an exception message that gets logged on startup failure; contradicts :38-40 | XS | secret hygiene | Cross-ref security agent |
| ADD-08 | 150 HTML comments in cshtml ship to every visitor (R9) | S | small payload and info hygiene | Yes (batch C9) |
| ADD-09 | Stop hard-coding test counts in prose (README badge and text, agent-context, plans README, resume-update). Drop the static "680 Tests" badge; the CI badge already exists | XS | ends recurring drift | Yes |
| ADD-10 | HomeFinderController.cs:42 `if (!ModelState.IsValid)` is unreachable under `[ApiController]`; invalid input returns a `ValidationProblemDetails` 400, not the `{ error }` shape | S | consistency | Decision (error-shape change is visible) |
| ADD-11 | Commit `docs/agent-context.md` (it's untracked); copilot-instructions and the Stage-2 implementers depend on it | XS | durable orientation | Yes |
| ADD-12 | Add a doc-generation regression test: parse `Portfolio.Web.xml` and assert no controller `<param>` for `cancellationToken`/`ct`, and every public action has a `<summary>` | S | keeps DOC-01 fixed | Yes |
| ADD-13 | Golden tests double as **claim guards**: when a README or Details figure is derived from a shipped scenario (Response 76.4/94.1, Fleet 2/3, 4/5, 6/6), assert it in a test so copy can't drift from code | S | honesty | Yes (with TST-04) |

---

## E. Decisions needed (proposed defaults)

| ID | Decision | Recommended default |
|---|---|---|
| D1 | Allow host-level tests (`WebApplicationFactory`, EF InMemory swap, no real DB)? agent-context says "No DB/integration tests … ask first" | **Yes.** No database, no network; it's the only way to pin pipeline order, headers, 401/429 and OpenAPI shape across the Program.cs split and the security fixes |
| D2 | Repository tests on EF InMemory? (precedent: 2 classes) | Optional, P3. InMemory semantics differ from Npgsql, so the value is moderate |
| D3 | Line-ending policy before the format gate | **Remove `end_of_line = crlf` from `.editorconfig`.** The index is already LF (`text=auto`) and Windows checkouts stay CRLF via autocrlf. Verified that an LF checkout then passes after one format pass. Alternative: `.gitattributes` `*.cs text eol=crlf` |
| D4 | OpenAPI-visible doc text: (a) the 13 two-sentence Portfolio.Common summaries; (b) the 7 trivial OpenAPI `<param>`s | (a) keep the facts (merge or allow 2 sentences); property `<remarks>` is dropped by the generator. (b) delete |
| D5 | Site copy: replace the withdrawn benchmark figures on 5 Details pages and the SIMD wording on cards and 7 Details bullets | **Yes**, as a consistency fix pointing at native/README's table. Owner reviews the wording |
| D6 | Legacy endpoints `POST homefinder/score`, `GET homefinder/properties/{id}`, `POST geocoding/batch/sync` and `/api/batchgeocoding/sync` (hidden from OpenAPI and coverage; the last is "kept for test compatibility") | Owner decides (public-facing). If kept: un-exclude from coverage and test them. If removed: update README.md:160 and agent-context §10 |
| D7 | copilot-instructions.md: patch in place, or slim to a pointer | **Slim** (DH-01) |

---

## F. Not verified / caveats

- **CodeQL eligibility:** I made no GitHub API calls, so repo visibility and GHAS status are unknown. Default CodeQL is free only for public repos.
- **Render's actual settings** (Pre-Deploy command, `RunMigrationsOnStartup`, Auto-Deploy state, whether `RENDER_DEPLOY_HOOK` exists) aren't in the repo. The README and render-deployment migration statements are flagged from Program.cs comments only.
- **Résumé PDF wording:** the text is glyph-encoded; I could only confirm its links (`gis-portfolio`, `brianpcoffey.com`).
- **"4 of 4 recovered"** (Change Detection, README:342) is computed in the browser (Change/app.js); I did not reproduce it. Response and Fleet claims were reproduced.
- **Native job on Linux:** I reproduced it on Windows/MSVC, not Ubuntu/GCC. CI's job is the authority for `.so` builds.
- The **"restates" and "label" counts are heuristic** (scripts in `scratchpad/dt/scripts/`). Banner, TODO, commented-out-code and XML counts are exact. Implementers apply R4 per file; expect roughly 300-450 deletions in the label/restating group.
- Coverage figures for the native-present and union runs are Debug builds (Windows). The gate figure (83.65/67.36) is the CI-equivalent Release run.
- SDK used: 10.0.400-preview for the first build, test and format run (11:24-11:29); 10.0.401 for everything after 11:34 (format counts identical on both).