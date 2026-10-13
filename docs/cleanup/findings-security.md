# Findings — Goal 1: Security and compliance

Stage 1 (read-only) analysis for the cleanup plan. Author: security analysis agent, 2026-10-12.
Input to `docs/cleanup/PLAN.md`. Every finding cites `file:line` in the current tree (HEAD `9d9ea02`),
and every number comes from a command listed in [Appendix A](#appendix-a-commands-run-and-raw-results).

**How to read this file**

- §0 is the summary. §1 is the full finding table. §2–§9 hold the detailed evidence per brief item.
  §10 has the work packages. §11 lists owner decisions. §12 covers cross-agent file overlaps. §13 lists what I could not verify.
- **Severity.** I use DISA categories with this meaning:
  - **CAT I:** exploitable remotely and unauthenticated, with one or a few requests, causing an outage or a compromise.
  - **CAT II:** exploitable with a precondition, or partial impact.
  - **CAT III:** hardening or defence-in-depth.
  Severities are rated by impact in this deployment, not copied from STIG defaults.
- **References.**
  - STIG V-IDs come from the *Application Security and Development STIG V6R4 (2025-09-09)* rule list on stigviewer.com, and are cited only where I confirmed them.
  - Container and Kubernetes items cite the requirement by paraphrased title, because I did not verify those V-IDs.
  - ASVS numbers are **OWASP ASVS 4.0.3**.
- **Where measurements ran.** On the owner's machine: i5-13600K (20 logical cores), .NET SDK 10.0.401, ASP.NET Core runtime 10.0.12. Code paths are managed only (`PORTFOLIO_DISABLE_NATIVE=1`), which is what production runs.
- **Production comparison.** A Render instance is much smaller: 512 MB RAM per `docs/render-deployment.md:60`; I did not verify its CPU share. Production wall-clock costs will be higher than the figures here.
- **Live probes.** These ran against my own clone (`scratchpad/sec/repo`) on `127.0.0.1:5099`:
  - Development environment.
  - An unreachable DB connection string.
  - Dummy Google client values.
  - `RunMigrationsOnStartup=false`.

  Production was only touched by four passive requests with `Accept: */*`, which creates no anonymous profile (see A.9).

## 0. Summary

| | Count |
|---|---|
| Findings | **34** (CAT I: **2**, CAT II: **7**, CAT III: **25**, of which SEC-20 and SEC-21 are accepted by default) plus 9 rows that are accepted or not applicable |
| HTML sinks in `wwwroot/js` (excluding `lib/`) | 149 pattern matches in 21 files, of which 66 are `innerHTML` writes in 16 files (17 files contain `innerHTML`; one only reads it in an escape helper). **10 are UNSAFE**: 2 directly exploitable, 2 mitigated by the ArcGIS sanitizer, and 6 latent (fed by server-controlled data, or a raw-HTML helper whose current callers are safe). There are 10 local `escapeHtml` copies, and **8 of them do not escape quotes**. |
| Vulnerable NuGet packages (direct and transitive) | **0** (`dotnet list package --vulnerable --include-transitive`) |
| Vulnerable front-end libraries | Leaflet 1.9.4 is affected by **CVE-2025-69993** (HTML injection via `bindPopup`; no fixed release). The app uses `bindTooltip`, which goes through the same HTML path. No CVEs found for the vendored jQuery 3.7.1, Bootstrap 5.3.3, DataTables 1.13.7, D3 7.9.0 or jquery-validation 1.21.0. |
| Outdated | Microsoft.* 10.0.10 → 10.0.12 (the .NET 10.0.11 and 10.0.12 runtimes shipped security fixes; no package was flagged). Every CI action is one or more major versions behind. |

**Top five items**

1. **SEC-01 (CAT I): one unauthenticated request burns minutes of CPU.**
   - Mechanism: seven compute controllers have no per-endpoint rate limit, and their validators cap cell *count* but not *work*.
   - Measured viewshed: `POST /api/v1/viewshed/compute` with `width=250000,height=1` (500 KB body) took **237 s** of CPU. Cancellation is checked once per row, so with one row it is effectively never checked.
   - Measured heatmap: `POST /api/v1/raster/heatmap` with 500×500 cells and 10,000 points (596 KB body) took **29.2 s**.
   - Measured DBSCAN: `POST /api/v1/clustering/dbscan` with 5,000 identical points (70 KB) allocated **313 MB**.
   - The UI never sends more than a 40×40 viewshed, a 24×24 terrain grid, or a 3-point heatmap, so caps can be tight.
2. **SEC-02 (CAT I): one 25 MB body costs about 1.2 GB of memory.**
   - Mechanism: endpoints without `[RequestSizeLimit]` fall back to Kestrel's 30,000,000-byte default.
   - Measured: `POST /api/v1/addresses/parse` with a 25 MB `rawAddress` returned 200 in 3.5 s, and the server working set went from **152 MB to 1,346 MB**. A 512 MB instance would not survive that.
   - A 31 MB body returns **500 instead of 413** and is logged at Error with a stack trace.
3. **SEC-05 (CAT II): XSS in the Batch Geocoding results.**
   - Mechanism: DataTables writes array cell data as HTML. `originalAddress` (from the user's CSV, echoed back) and `matchedAddress` (from ArcGIS) are not escaped (`BatchGeocoding/app.js:260-297`).
   - Verified with the repo's own vendored DataTables 1.13.7 in headless Edge: an `onerror` payload fired. The same value through `render.text()` stayed text.
4. **SEC-04 (CAT II): the anonymous `AnonUserId` cookie can act as a Google-linked account, and sign-in can take over another profile.**
   - Mechanism:
     - An anonymous profile *becomes* the Google account at sign-in (`UserProfileService.cs:169-201`).
     - `AnonymousUserMiddleware.cs:75-99` later accepts that same GUID from an unauthenticated cookie.
     - The merge path never checks whether the profile it adopts already has a `google_id`.
     - `SetClaimAsync` is an upsert (`UserProfileRepository.cs:118-134`), so it overwrites the victim's `google_id`.
   - Precondition: the attacker needs the victim's GUID. The Profile page displays it (`Profile.cshtml:72`) and Warning-level logs record it.
5. **SEC-10 (CAT II, unverified on Render): the rate-limit partition key is the right-most X-Forwarded-For entry.**
   - Mechanism: `KnownProxies` and `KnownIPNetworks` are cleared and `ForwardLimit` is 1 (`Program.cs:234-241`).
   - Measured: rotating the right-most entry evades the 15/min "expensive" limit; rotating only left entries does not.
   - Production runs behind Cloudflare and then Render. Whether the right-most entry is the client or a shared edge hop is undocumented. If it is a shared hop, every visitor shares one budget and one client can 429 everyone. It needs a five-minute owner check (§4.5).

**What I could not verify** is listed in §13. The main items:

- Render's exact XFF hop layout.
- Google profile-picture hosts and the `/scalar` page's needs under the target CSP.
- The size of real saved-feature geometries.
- Visual tile rendering under an enforced CSP (only the `view.when()` resolution and violation reports were checked).

## 1. Finding table

Columns follow the brief. "Reference" gives the STIG rule (V-ID and title), the ASVS 4.0.3 requirement, and the CWE.

| ID | Title | Reference | Location | Sev | Evidence | Fix (summary; details in §10) | Risk of fix |
|---|---|---|---|---|---|---|---|
| **SEC-01** | Unbounded per-request compute on un-rate-limited endpoints | V-222594 *restrict the ability to launch DoS attacks*; V-222667 *protections against DoS*; ASVS 11.1.4, 5.1.4; CWE-400, CWE-407, CWE-770, CWE-834 | `ViewshedService.cs:116-124` (shape check: cells only), `:53-101` (ray walk; cancellation at `:55` per row only); `RasterTerrainService.cs:41-62,94-120` (no cells×points budget); `SpatialClusterService.cs:86-100` (`seeds.AddRange` without dedup); missing `[EnableRateLimiting]` on `GeoStreamController.cs:13-16`, `SpatialGeometryController.cs:13-16`, `RasterTerrainController.cs:13-16`, `SpatialNetworkController.cs:13-16`, `SpatialClusterController.cs:12-15`, `ViewshedController.cs:12-15`, `SpatialOverlayController.cs:12-15` | **CAT I** (by impact: a single unauthenticated request) | Harness (A.4): viewshed strip at W=20k took 1.0 s, 50k 7.9 s, 100k 37.0 s, **250k 237.3 s**; heatmap with 10k points 29.2 s; DBSCAN with 5k identical points 313 MB allocated (267 MB peak working set); overlay 0.48 s per 1.15 MB body. Live (A.6): 20 back-to-back viewshed POSTs from one client all returned 200; a 90 KB W=30k request took 3.1 s over HTTP | Work caps: viewshed side ≤ 512, heatmap cells×points ≤ 25M, DBSCAN seed dedup, cancellation inside inner loops. Add a "compute" rate policy to the seven controllers. Add a global concurrency limiter for compute (SEC-WP1, SEC-WP2) | Narrows inputs the public API accepts (UI unaffected; see §5.4). DBSCAN labels must stay identical (`NativeParityTests`). New 429s for heavy scripted use |
| **SEC-02** | No request-size limit on several write endpoints; one request costs >1 GB | V-222594; V-222609 *not subject to input handling vulnerabilities*; ASVS 5.1.4, 11.1.4; CWE-770 | No `[RequestSizeLimit]`: `AddressStandardizationController.cs:37,76`, `ProfileController.cs:62,85,109`, `UserProfileController.cs:49,122`, `SavedFeatureController.cs:47`, `CollectionsController.cs:63,93`, all Fiber* POST/PUT, `HomeFinderController.cs:36,49,86`. No Kestrel limit is configured in `Program.cs`. `AddressStandardizationService.cs:99-112,183-187` has no length cap | **CAT I** | Live 25 MB `/addresses/parse`: HTTP 200 in 3.47 s, server working set **152 → 1,346 MB**. Harness: 1,421 MB allocated. 31 MB body: **HTTP 500**, logged at Error as `BadHttpRequestException: Request body too large` (A.6) | Set Kestrel `MaxRequestBodySize` to 1 MB, keeping the explicit higher limits already on compute actions. Add per-endpoint limits and field length caps. Map `BadHttpRequestException` to its own status (SEC-WP2, SEC-WP3) | A legitimate large saved-feature geometry could be rejected; the cap is an owner decision (D3) |
| **SEC-03** | Each cookieless HTML request creates a profile plus 75 seeded rows | V-222594; ASVS 11.1.4; CWE-770, CWE-400 | `AnonymousUserMiddleware.cs:73,100-120` (seed at `:120`); `UserProfileSeedService.cs` (77 `_db.*.Add(` calls, 75 per user: 12 clients, 12 materials, 17 orders, 11 shipments, 23 inventory transactions, each behind its own `AnyAsync`); no cleanup anywhere (`grep` for BackgroundService/IHostedService/purge found none) | CAT II | Static count (A.5). Fiber data is only readable through `[Authorize(Policy="Authenticated")]` controllers (`FiberOrdersController.cs:12` etc.), so seeding it for anonymous visitors only amplifies. Every crawler hit and every bot does this; the only limit is the global 300/min/IP, so up to 22,800 rows/min/IP | Seed at Google sign-in instead of anonymous creation. Throttle new-identity creation per IP. Owner decision on a retention purge (SEC-WP4) | `SeedForUserAsync` also inserts global `Properties` (`:1328-1436`); prove these are already supplied by the `ExpandRedlandsProperties` migration (`InsertData`) with a characterization test |
| **SEC-04** | The anonymous GUID cookie can assume a Google-linked profile; the sign-in merge overwrites another account's `google_id` | *Enforce approved authorizations for logical access* (title only); V-222579 *protect against session fixation*; ASVS 4.2.1, 3.2.1; CWE-639, CWE-287, CWE-384 | `AnonymousUserMiddleware.cs:75-99`; `UserProfileService.cs:169-201` (adopts `anonId` with no `google_id` check); `UserProfileRepository.cs:118-134` (upsert); GUID displayed at `Profile.cshtml:72`, returned by `GET /api/v1/profile` (`ProfileController.cs:44-57`), logged at `SavedFeatureService.cs:101,111`, `CollectionService.cs:119`, `UserProfileService.cs:88` | CAT II | Code path (§6.2). An unauthenticated request with `AnonUserId=<linked GUID>` reaches `GET /api/v1/profile`, which returns the `email`, `name` and `google_id` claims, plus saved-feature CRUD and non-protected claims. Signing in with a *new* Google account while carrying that cookie rewrites the victim's `google_id` | (1) The middleware ignores cookies that map to Google-linked profiles. (2) The merge adopts only unlinked profiles. (3) Stop logging raw IDs. (4) Owner decision on showing the ID on the Profile page (SEC-WP4) | Changes behaviour only for already-linked profiles; needs tests for the merge paths |
| **SEC-05** | Stored or CSV-borne XSS in Batch Geocoding results | V-222602 *protect from XSS*; ASVS 5.3.3; CWE-79 | `BatchGeocoding/app.js:260-297` (`r.originalAddress` at `:271`, `r.matchedAddress` at `:273`; DataTables `data:` with no `render` at `:281-291`) | CAT II (impact is the uploader's own session, delivered by a crafted CSV) | Headless Edge probe with the vendored DataTables 1.13.7 + jQuery 3.7.1: `<body data-xss="fired">`; control with `render.text()` stayed `&lt;img…` (A.7) | `render: $.fn.dataTable.render.text()` on columns 1 and 3 (SEC-WP5) | None (identical display for normal addresses) |
| **SEC-06** | Stored self-XSS via shipment `status` in an inline handler; server accepts any status | V-222602; V-222606 *validate all input*; ASVS 5.3.3, 5.1.3; CWE-79, CWE-20 | `PlantOperationsDashboard/shipments.js:49` (`onclick="showShipmentStatusModal(${row.id}, '${row.status}')"`); `FiberShipmentDto.cs:18,63` (no validation); `FiberShipmentService.cs:71-75` | CAT III (data is owner-scoped, so a user can only attack themselves) | Code: a JS string inside an HTML attribute (HTML-decoding happens before execution, so escaping would not help) | Delegated listeners with `data-` attributes. Server-side status allowlist {Pending, In Transit, Delivered, Exception} and length checks (SEC-WP5, SEC-WP6) | Status values outside the four become 400 (the UI only offers the four) |
| **SEC-07** | Latent unsafe HTML sinks and inconsistent escape helpers | V-222602; ASVS 5.3.3; CWE-79, CWE-116 | `Network/app.js:169,180`; `HomeFinder/app.js:340-356,402,683`; `PlantOperationsDashboard/dashboard.js:22-25,159-161`; `ReverseGeocoding/app.js:210-213`; 10 `escapeHtml` copies (§3.2) | CAT III | §3.1 table. Leaflet CVE-2025-69993 covers this class for popups, and tooltips share the code path | One vetted helper (`wwwroot/js/dom-safe.js`) plus DOM building; replace all copies (SEC-WP5) | Touches 13 JS files that the frontend agent will also refactor (§12) |
| **SEC-08** | No script/style CSP (minimal policy only) | V-222602 (defence in depth); ASVS 14.4.3; CWE-693 | `SecurityHeadersMiddleware.cs:35-36` | CAT II | Production header observed: `frame-ancestors 'none'; base-uri 'self'; object-src 'none'` (A.9) | Nonce-based policy (§2), Report-Only first, report endpoint (SEC-WP7 → SEC-WP8) | Can break ArcGIS/Leaflet pages if enforced too early; mitigated by the Report-Only phase and the headless probes (§2.6) |
| **SEC-09** | ArcGIS CDN script and theme CSS without SRI (Leaflet SRI is present and correct) | ASVS 14.2.3, 10.3.2; CWE-829, CWE-494 | `HomeFinder/Index.cshtml:204,216`; `PlantOperationsDashboard/Index.cshtml:17,218`; `ReverseGeocoding/Index.cshtml:188`; `StateExplorer/Index.cshtml:219` | CAT III | Leaflet hashes match jsDelivr npm metadata. ArcGIS: `js.arcgis.com/4.29/` → 301 → `/4.29/init.js` (1,477,706 B, `ACAO: *`, `Last-Modified` 2024-03-28) (A.8) | `integrity` + `crossorigin` on `init.js` and both theme CSS files (SEC-WP9); owner decision D5 | Fails closed if Esri edits the 4.29 files in place; it does not protect the ~100 dynamically loaded AMD modules |
| **SEC-10** | Forwarded-header trust is open; the rate-limit partition is the right-most XFF entry | ASVS: no direct requirement; CWE-348, CWE-290 | `Program.cs:234-241` (cleared lists, default `ForwardLimit=1`), `:447-448` (partition key) | CAT II (unverified on Render) | Live (A.6): (a) no XFF → 429 on request 16; (b) rotating single XFF → all 200; (c) rotating the left entry with a fixed right-most → 429; (d) rotating the right-most → all 200. Production is behind Cloudflare (`Server: cloudflare`, `CF-RAY`, `rndr-id`) | Explicit `ForwardLimit`; config-driven `KnownIPNetworks` set after verification (§4.5); a global concurrency limiter independent of IP (SEC-WP2, SEC-WP10) | A wrong CIDR silently collapses all clients into one partition, so verify first |
| **SEC-11** | `AllowedHosts: "*"`; Host header reflected in canonical/og URLs and the batch `StatusUrl` | ASVS: no direct requirement; CWE-20 (host header injection) | `appsettings.json:8`; `appsettings.Production.json:9`; `_Layout.cshtml:52,61,68-69,76`; `BatchGeocodingController.cs:68-72` | CAT III | Live: `Host: evil.example` produced `<link rel="canonical" href="http://evil.example/">`. Production edge returns **403** for a foreign Host (A.9), so this is defence-in-depth (it matters for compose/k8s) | Explicit host list in Production (D8) | A missing host (for example the onrender subdomain) returns 400 for that whole host, so it must be verified on deploy |
| **SEC-12** | Exception middleware leaks framework messages and maps status codes wrongly | V-222610 *error messages … without revealing information*; V-222611; ASVS 7.4.1; CWE-209 | `ApiExceptionMiddleware.cs:50-59` (any `ArgumentException`, `InvalidOperationException`, `KeyNotFoundException` or `UnauthorizedAccessException` becomes 4xx with `ex.Message`), `:69-72` (no `HasStarted` guard); compute controllers echo `ex.Message` (e.g. `ViewshedController.cs:49-53`); `SpatialGraphService.cs:245,323` (indexer throws `KeyNotFoundException`) | CAT II | Live (A.6): `"The given key '99999' was not present in the dictionary."` as **404**; a serializer exception text with a learn.microsoft.com URL as **400**; missing identity as **409 Conflict**; oversized body as **500**. EF Core and Npgsql `InvalidOperationException` messages can name entities and properties | Surface `ex.Message` only for exceptions whose `TargetSite` is in a `Portfolio.*` assembly; generic text otherwise; map `BadHttpRequestException`; add a `HasStarted` guard; fix the graph indexers (SEC-WP3, SEC-WP1) | Text of *framework-originated* errors changes (the JSON shape does not); review tests that assert messages |
| **SEC-13** | Non-finite numbers bind, and several DTO doubles are unvalidated | V-222606; ASVS 5.1.4; CWE-20, CWE-1284 | `GeoStreamProcessorService.cs:24-32,51-63` (per-event lat/lon/speed); `RasterTerrainService.cs:21-38,41-62` (elevation, azimuth/altitude, extent, points); `ViewshedService.cs:20-43` (elevation); `ReverseGeocodingService.cs:43-47` (NaN passes the range check); `CatRiskService.cs:145-146,169-170` (no magnitude bounds) | CAT III | Harness (A.4): `{"epsilon":"NaN"}` and `"Infinity"` accepted, `1e400` becomes ∞; query-string `double` accepts NaN/±∞. Live: speed `"NaN"` → 400 carrying the serializer's text; a 1e-300 grid size saturates to cell `(2147483647,2147483647)` | Finite and magnitude guards in every service (shared helper); `GridSizeDegrees ≥ 1e-6`; `InsuredValue ≤ 1e12` (SEC-WP1) | Rejects absurd inputs only |
| **SEC-14** | Security headers incomplete | ASVS 14.4.5 (HSTS), 14.3.3 (version banners), 8.2.1 (anti-caching), 14.4.x; CWE-693, CWE-200, CWE-525 | `Program.cs:563-567` (`UseHsts` defaults); `SecurityHeadersMiddleware.cs:17-38` (no Permissions-Policy, COOP or CORP); Kestrel `Server` header on; no `Cache-Control` on personal API JSON | CAT III | Production: `strict-transport-security: max-age=2592000` (30 days, no `includeSubDomains`), `x-render-origin-server: Kestrel`; local: `Server: Kestrel` (A.6, A.9) | HSTS one year (`includeSubDomains` per D10); `Permissions-Policy`; COOP `same-origin`; CORP `same-origin`; `AddServerHeader=false`; `Cache-Control: no-store` on `/api` responses without explicit caching and on authenticated pages (SEC-WP2, SEC-WP7) | Low. HSTS `includeSubDomains` is irreversible for a year, so it is an owner decision |
| **SEC-15** | Oversized-body errors surface as 500 plus an Error log | V-222610; ASVS 7.4.1; CWE-209, CWE-779 | `ApiExceptionMiddleware.cs:58` (default arm) | CAT III | Live 31 MB body: 500 and a stack trace at `fail:` (A.6) | Map `Microsoft.AspNetCore.Http.BadHttpRequestException` to `ex.StatusCode` and log at Information (SEC-WP3) | None |
| **SEC-16** | Sensitive data in logs and exception messages | V-222444 *must not write sensitive data into the application logs*; ASVS 7.1.1, 7.1.2; CWE-532 | Connection strings inside exception text: `Program.cs:686` (`$"Invalid connection string format: {connectionString}"`), `:765` (Redis). Internal user IDs (a bearer value per SEC-04) at Warning, which still reaches production: `SavedFeatureService.cs:101,111`, `UserProfileService.cs:88`, `CollectionService.cs:119`, `CollectionRepository.cs:54`, `SavedFeatureRepository.cs:62`, `UserProfileRepository.cs:90`; at Information (off in production): `AnonymousUserMiddleware.cs:134-137` etc. User-entered addresses: `AddressStandardizationService.cs:123-125,175`, `BatchGeocodingService.cs:237` (one Warning per unmatched row, up to 10,000 per job), `:287` | CAT III | Code reading; production log level is Warning (`appsettings.Production.json:5`) | Drop values from exception messages; hash or truncate IDs; do not log addresses; one per-job summary log (SEC-WP2, SEC-WP4, SEC-WP6, SEC-WP15) | None |
| **SEC-17** | Data Protection key ring unencrypted at rest | ASVS 6.4.1, 6.4.2; CWE-312, CWE-522 | `Program.cs:516-547` (no `ProtectKeysWith*`) | CAT III | Runtime log in my run: `No XML encryptor configured. Key {…} may be persisted to storage in unencrypted form.` Anyone holding the Upstash credentials can forge auth, antiforgery and TempData cookies | `ProtectKeysWithCertificate` (D13), or accept with rationale | Adds secret or certificate management; a wrong certificate means everyone is logged out |
| **SEC-18** | Session lifetimes | V-222389 *terminate non-privileged sessions after 15 minutes idle*; V-222578; ASVS 3.3.1, 3.3.2; CWE-613 | `Program.cs:300-301` (8 h sliding, no absolute limit, no server-side revocation); `AnonymousUserMiddleware.cs:121-133` (1-year raw-GUID cookie, never rotated) | CAT III | Code | D14: keep the 8 h idle and add a 24 h absolute limit via `OnValidatePrincipal`; record the STIG 15-minute deviation | UX: Google users re-authenticate daily |
| **SEC-19** | Session middleware registered but unused | ASVS 14.2.2 (remove unneeded features); V-222613 (title) | `Program.cs:264-272,582`; the only use is `Logout.cshtml.cs:14` (`Session.Clear()`) | CAT III | `grep HttpContext.Session` (A.5) | Remove `AddSession`, `UseSession` and the `Clear()` call (pipeline-order change; document it) (SEC-WP14) | Low |
| **SEC-20** | OpenAPI and Scalar public in production | V-222600 *must not disclose unnecessary information* | `Program.cs:596-598` | CAT III → **accepted (default)** | Local: `/openapi/v1.json` returned 200 (325,283 B, 56 paths); `/scalar` returned 302. Intentional per `agent-context.md` §2 | D7: keep public (it showcases the API); legacy routes are already hidden via `IgnoreApi` | — |
| **SEC-21** | API CSRF relies on SameSite plus JSON content type | V-222603 *protect from CSRF*; ASVS 4.2.2; CWE-352 | Cookie `SameSite=Lax` at `Program.cs:299` and `AnonymousUserMiddleware.cs:129`; no API antiforgery | CAT III → **accepted (default)** | Live: `text/plain` and form-encoded POSTs to a JSON action → **415**; CORS preflight → **405** (no CORS); `POST /Logout` without a token → **400**; `onrender.com` is on the Public Suffix List (submitted by Render) | D19: optionally require an `X-Requested-With` header on unsafe `/api` methods, set by `api-fetch.js` | That option breaks third-party and Scalar "try it" calls unless they send the header |
| **SEC-22** | Missing length and format validation on persistence inputs | V-222606; ASVS 5.1.3, 5.1.4; CWE-20, CWE-770 | `UserClaimMap.cs:19-20` (ClaimValue is unbounded `text`), `ProfileController.cs:62-80` (anonymous; no count cap); `SavedFeatureMap.cs:15-18` (LayerId, FeatureId and GeometryJson unbounded); `CollectionService.cs:70,101-102` (Color not format-checked; reaches `style=` at `uiManager.js:386`); `FiberShipmentDto.cs` (no limits; unbounded `Route` list at `:42`, serialized at `FiberShipmentService.cs:54`); `SaveSearchRequest.cs` (Name and PropertyIds exceed `SavedSearchMap.cs:16,18`, giving a DB error → 500) | CAT III | Code reading | Service-level caps: claim value ≤ 1,000 characters, ≤ 50 claims per user; hex colour regex; shipment field lengths and route ≤ 1,000 points; return 400 instead of a DB exception (SEC-WP4, SEC-WP6) | New 400s for abusive payloads only |
| **SEC-23** | ArcGIS feature proxy: `layerId` dot-segment traversal; no "expensive" limit | ASVS 12.6.1 (outbound allowlist), 11.1.4; CWE-22, CWE-918 | `ArcGisService.cs:39-40`; `FeaturesController.cs:13-16,40-47` | CAT III | Harness: `layerId=".."` → `…/services/Census/query?…` (URI normalisation); `%2F` blocks deeper traversal. UI sends `layerId=3` (`StateExplorer/featureService.js:14-16`) | Validate `layerId` as `^\d{1,3}$` and bbox as four finite numbers; `[EnableRateLimiting("expensive")]` (SEC-WP2) | Non-numeric `layerId` gets 400 instead of `[]` |
| **SEC-24** | Batch geocoding jobs have no global cap or TTL | V-222594; ASVS 11.1.4, 12.1.1; CWE-770 | `BatchGeocodingService.cs:153-202` (fire-and-forget `Task.Run` per job; final `UpdateAsync` at `:201` outside the `try`), `:293` (10,000 rows); `InMemoryBatchJobStore.cs:7-31` (no eviction) | CAT III | Code. About 2 Redis commands per row (GET+SET) plus 1 per 5 rows, so a 10,000-row job is about 22,000 commands, against the "10,000 commands/day" Upstash free tier the repo documents (`docs/render-deployment.md:14`) | Bounded job queue (one or two workers); TTL in the in-memory store; owner decision on row cap (samples are 10–12 rows) (SEC-WP15) | Queued jobs wait longer under load |
| **SEC-25** | Container runs as root; images unpinned; no healthcheck | Container Platform SRG / Docker STIG: *containers must not run as root*, *use trusted, verified images* (paraphrased; IDs not verified); CWE-250, CWE-1357 | `Dockerfile:4,29` (tags `sdk:10.0` and `aspnet:10.0`, no digest), no `USER`, no `HEALTHCHECK`, `:37` (`mkdir` as root) | CAT II | Code | `USER $APP_UID` (the aspnet image ships user `app`, UID 1654) with `chown` on `/app/DataProtection-Keys`; digest pins plus Dependabot "docker" (SEC-WP11) | Directory permissions; digest pins need an update process or patches stall |
| **SEC-26** | Kubernetes manifests lack pod hardening, NetworkPolicy and Redis auth | Kubernetes STIG / Container Platform SRG (paraphrased): non-root, no privilege escalation, read-only root FS, drop capabilities, network segmentation, no automounted service-account tokens; CWE-250, CWE-923 | `k8s/deployment.yaml:18-51` (no `securityContext`, `image: portfolio:latest` at `:20`, default `automountServiceAccountToken`); `k8s/redis.yaml:16-32` (no auth or securityContext; holds the DP keys); no NetworkPolicy file; `k8s/namespace.yaml` has no PSA labels; `k8s/secret.yaml:9-13` invites in-place editing | CAT III (manifests are not used by the Render production deploy) | Code | securityContext, `automountServiceAccountToken: false`, default-deny NetworkPolicies, Redis `requirepass`, PSA `restricted`, `secret.example.yaml` plus gitignore (SEC-WP11) | None for Render |
| **SEC-27** | Compose publishes unauthenticated Redis on all interfaces | CWE-284, CWE-306 | `docker/docker-compose.yml:21-23` (`6379:6379`) | CAT III | Code (Redis holds the DP key ring in that setup) | Remove the mapping or bind `127.0.0.1` (SEC-WP11) | None |
| **SEC-28** | CI supply chain | V-222614 *patches up to date* (process); ASVS 14.1.x (build and deploy); CWE-829, CWE-1357 | `ci.yml:44,46,53,122,145,147,152,210,212,215` (mutable tags); no `.github/dependabot.yml`; no scheduled vulnerability run; no CodeQL or secret scanning in the repo (repo is **public**: anonymous `git ls-remote` works) | CAT III | A.10. Good already: `permissions: contents: read` (`:18-19`), `pull_request` rather than `_target`, the secret used only in deploy (`:239-248`) | Pin the SHAs in §7.3; Dependabot (nuget, github-actions, docker); weekly `schedule:`; CodeQL default setup (D20) (SEC-WP12) | None |
| **SEC-29** | Dependency hygiene | V-222614; V-222613; V-222658; ASVS 14.2.1; CWE-1104, CWE-1395 | `Directory.Packages.props:11-24` (10.0.10 pins); unused vendored libraries `StateExplorer/Index.cshtml:11,218` (typeahead 1.3.1 loaded but never initialised), `_ValidationScriptsPartial.cshtml:1-2` (never rendered) | CAT III | A.1, A.11 | Bump Microsoft.* to 10.0.12; remove dead libraries (D18) (SEC-WP13) | Patch-level; CI gates |
| **SEC-30** | Native C ABI hardening (no native code ships to production) | V-222612 *not vulnerable to overflow attacks*; ASVS 5.4.x; CWE-125, CWE-787, CWE-190, CWE-681, CWE-758 | `viewshed_kernel.cpp:25-41` and `raster_terrain_kernel.cpp:21-34` (elevation pointer with no length; `int` `width*height` overflow is UB); `scoring_kernel.cpp:206-235` (`void`, no output length, no `try`); `geostream_processor.cpp:41-42` (`static_cast<int>` of an unbounded double is UB; .NET saturates → parity gap); `spatial_cluster_kernel.cpp` seeds mirror SEC-01 | CAT III | Code (§9) | D17: add length parameters and 64-bit size math, or document as accepted while managed validation precedes every call (SEC-WP16) | An ABI change touches the header, the C# struct/interop/bridge, the native source and the parity tests together |
| **SEC-31** | Committed default DB credential; the `DATABASE_URL` fallback is dead in Production | V-222642 *must not contain embedded authentication data*; V-222662; CWE-798, CWE-1392 | `appsettings.json:11` (`Password=postgres`, localhost); `appsettings.Production.json:10-12` sets `""`; `Program.cs:503-505` uses `??` | CAT III | Harness `config` probe: `GetConnectionString` returns **`""`, not null**, so `?? DATABASE_URL` is never reached in Production even though `docs/render-deployment.md:70` documents it | Use `string.IsNullOrWhiteSpace` fallbacks; move the dev default out of the committed file (D15) (SEC-WP2) | Fresh-clone convenience if the default is removed |
| **SEC-32** | `/health/ready` is unthrottled and makes one DB round trip per call | V-222594; ASVS 11.1.4; CWE-770 | `Program.cs:610-613` (`DisableRateLimiting`); `DatabaseReadyHealthCheck.cs:28` | CAT III | Code. Neon autosuspend means each probe can wake compute | Cache the readiness result for 10 s, or keep rate limiting on `/ready` and exempt only `/live` (SEC-WP2) | Render's configured health path is unknown (§13) |
| **SEC-33** | Public caching on GETs that can carry `Set-Cookie: AnonUserId` | CWE-524, CWE-384 | `[ResponseCache(Duration=3600)]` at `CatRiskController.cs:40`, `OutageController.cs:40`, `ResponseCoverageController.cs:40`, `FleetRoutingController.cs:45`; cookie issued by `AnonymousUserMiddleware.cs:121-133` for `Accept: text/html` | CAT III | Production `cf-cache-status: DYNAMIC` (Cloudflare does not cache these today) | Skip identity creation on `/api/*`, or add `Cache-Control: private, no-store` whenever the cookie is issued (SEC-WP4) | None |
| **SEC-34** | No exhaustive guard on authorization and rate-limit attributes | ASVS 4.1.3; CWE-862 | Convention only (`agent-context.md` §6, §9); no `FallbackPolicy`; existing tests cover a curated list (`ApiVersioningAttributeTests.cs:76-140`) | CAT III | All 25 controllers comply today (A.5) | A reflection test asserting that every action has explicit auth metadata and every compute controller has a limiter policy (SEC-WP0) | None |

**Accepted or not-applicable rows (checked, no action needed)**

| Topic | Status | Evidence |
|---|---|---|
| Open redirect via `ReturnUrl` (Login) | N/A, compliant | `Login.cshtml.cs:16` uses `Url.IsLocalUrl`; live `ReturnUrl=//evil.example` falls back to `/`. Logout has no `returnUrl` |
| Google OAuth configuration | Compliant | `Program.cs:381-414`: `SaveTokens=false`, minimal scopes; live challenge carries **PKCE `code_challenge_method=S256`**. The comment at `:397-402` is stale (it says Google is the default challenge); comment owners should fix it |
| Cookie flags | Compliant: V-222575 (HttpOnly), V-222576 (Secure) | Auth cookie at `Program.cs:296-299`; `AnonUserId` at `AnonymousUserMiddleware.cs:121-133`. `__Host-` prefix not used; renaming logs everyone out (optional, not recommended now) |
| CORS | Compliant | No `AddCors`; live preflight returns 405 with no `Access-Control-*` headers |
| Razor Pages antiforgery | Compliant | Razor Pages validate POSTs by default; `Logout.cshtml.cs:8` adds `[ValidateAntiForgeryToken]`; live `POST /Logout` without a token returns 400 |
| IDOR in repositories | Compliant | Every per-user query filters by owner (`CollectionRepository.cs:23,31,64`, `Fiber*Repository`, `SavedFeatureRepository.cs:24-72`, `SavedSearchRepository.cs:20-60`). The three ID-taking user endpoints check ownership (`UserProfileController.cs:75,100,178`). See SEC-04 for the identity-layer gap |
| SQL injection | N/A | EF Core LINQ only; `grep -E 'FromSql|ExecuteSql|SqlQuery|NpgsqlCommand|CommandText'` over Web, Services and Repositories (excluding migrations) is empty |
| HTTP→HTTPS | Compliant at the edge | Production `http://` returns a 301 to `https://` (Cloudflare). In-app redirect is dev-only by design (`Program.cs:569-574`) |
| Secrets in git history | Not found | A pattern scan of all history found only placeholders (`.env.example`, `docs/render-deployment.md`, commit `ef50b62`). The `Portfolio/appsettings.Development.json` committed in `74a9af1` (removed in `c0badc6`) held only logging settings (A.12). Not an exhaustive gitleaks/trufflehog run |

## 2. Content Security Policy

### 2.1 Inventory of external origins (Pages/**/*.cshtml, Areas/**, and JS)

52 `.cshtml` files were scanned (`grep -rhoE '(src|href)="https?://…'` plus a full `https?://` sweep).

| Origin | Used for | Where | CSP directive |
|---|---|---|---|
| `https://js.arcgis.com` | ArcGIS 4.29 loader (`init.js`), AMD modules, workers, t9n JSON, theme CSS, fonts | `<script>` at `HomeFinder/Index.cshtml:216`, `PlantOperationsDashboard/Index.cshtml:218`, `ReverseGeocoding/Index.cshtml:188`, `StateExplorer/Index.cshtml:219`; theme base URL in inline loaders at `HomeFinder/Index.cshtml:204` and `PlantOperationsDashboard/Index.cshtml:17` | script-src, style-src, connect-src, font-src |
| `https://*.arcgis.com`, `https://*.arcgisonline.com` | Basemap portal items, vector tiles, sprites, glyphs, raster imagery (satellite, hybrid) | Runtime (basemaps `streets-navigation-vector`, `satellite`, `hybrid`, `topo-vector`, `dark-gray-vector`; options at `StateExplorer/Index.cshtml:59-62`, `HomeFinder/Index.cshtml:160-165`, `PlantOperationsDashboard/Index.cshtml:130-135`) | img-src, connect-src, font-src |
| `https://unpkg.com/leaflet@1.9.4/` | Leaflet JS and CSS (SRI present) | `Fleet/Index.cshtml:10-11,114-115`, `Network/Index.cshtml:9-10,177-178`, `Response/Index.cshtml:10-11,120-121` | script-src, style-src |
| `https://{a,b,c}.tile.openstreetmap.org` | OSM raster tiles: Leaflet `<img>` (`Fleet/app.js:47`, `Network/app.js:45`, `Response/app.js:56`) **and** ArcGIS `osm` basemap `fetch()` (PlantOps default `PlantOperationsDashboard/Index.cshtml:135`, HomeFinder option `:165`) | Runtime | img-src **and connect-src** (verified, §2.6) |
| `https://developers.google.com` | Google "G" logo | `Login.cshtml:23` | img-src |
| `https://*.googleusercontent.com` | Google profile picture (claim `picture`) | `_LoginPartial.cshtml:36`, `Areas/Account/Pages/Profile.cshtml:38` | img-src (host not verified, §13) |
| `https://github.com`, `https://linkedin.com`, `https://mail.google.com` | Plain links (navigation only) | 56 + 2 + 3 hrefs | none needed |
| `https://accounts.google.com` | OAuth (server-side 302, top-level navigation) | `Login.cshtml.cs:23` via `Challenge` | none (not a form post) |

### 2.2 Inline code that a strict policy affects

| Kind | Count | Sites | Needed for the enforced policy |
|---|---|---|---|
| Inline `<script>` blocks | **5** | `Pages/Shared/_Layout.cshtml:8-35` (theme bootstrap before first paint), `:99` (dark-mode body class); `Pages/Index.cshtml:979-985` (`copyEmailContact`, **dead**: no caller in any `.cshtml`/`.js`); `Projects/HomeFinder/Index.cshtml:202-215` and `Projects/PlantOperationsDashboard/Index.cshtml:15-29` (identical Esri theme loaders) | Nonce via tag helper (no move needed); delete the dead block |
| Inline `<style>` blocks | 0 | — | — |
| `style=` attributes in Razor | **44** in 13 files (`AddressStandardization` 3, `CatRisk` 2, `Change` 7, `Clustering` 1, `Fleet` 3, `HomeFinder` 12, `Network` 1, `Outage` 3, `Overlay` 1, `PlantOperationsDashboard` 2, `Response` 2, `StateExplorer` 5, `Areas/Account/Pages/Profile.cshtml` 2) | Kept: `style-src-attr 'unsafe-inline'` (ArcGIS needs it anyway, see §2.6) |
| `style=` emitted by JS strings | **18** in 8 files (`BatchGeocoding` 1, `CatRisk` 1, `Change` 3, `Fleet` 1, `HomeFinder` 6, `Outage` 2, `Overlay` 1, `StateExplorer/uiManager.js` 3) | Kept, as above |
| Inline event handlers in Razor (`on*=`), `javascript:` URLs | 0, 0 | — | — |
| Inline event handlers emitted by JS | **9** | `PlantOperationsDashboard/inventory.js:52,53,54,101`; `orders.js:55,56`; `shipments.js:47,48,49` | **Must** become delegated listeners (`script-src-attr 'none'`) |
| `eval` / `new Function` in first-party JS | 0 | — | — |
| `new Function` in vendored JS | 1 (`lib/d3/d3.min.js`, d3-dsv object converter) | Not reached: no `d3.csv/tsv/dsv/csvParse` call in `wwwroot/js` | none |
| Dead third-party script | typeahead 1.3.1 | `StateExplorer/Index.cshtml:11,218`: loaded, never initialised (no `.typeahead(`/`Bloodhound` in JS); the Details page claims it at `StateExplorer/Details.cshtml:24` | Remove (D18) |

### 2.3 Choice: per-request nonce (not hashes)

I recommend a **per-request nonce** generated in `SecurityHeadersMiddleware`, plus a Razor tag helper that stamps `nonce="…"` on every `<script>` and `<style>` element. Reasons:

1. The inline scripts are edited regularly (`_Layout.cshtml:8-35` holds user preferences; the Esri loaders). With hashes, any whitespace edit silently breaks the theme or map bootstrap unless a build step recomputes them. There is no bundler in this repo.
2. Inline scripts are page-dependent (`@section Scripts`). One global header with every page's hashes is unwieldy; nonces need no bookkeeping.
3. HTML responses are dynamic and not edge-cached (production `cf-cache-status: DYNAMIC`, A.9), so per-request nonces cost nothing in cacheability. The cost is 16 bytes from `RandomNumberGenerator` per request.
4. **No `'strict-dynamic'`.** jQuery's `.html()` re-executes `<script>` elements in inserted HTML through a trusted script, which is a documented `strict-dynamic` bypass gadget. The four PlantOps modules use `$modals.html(...)`. An allowlist plus nonce keeps an injected inline `<script>` blocked, and ArcGIS's dynamic AMD loads are covered by the host allowlist (verified, §2.6).
5. **Implementation notes for the planner.**
   - `Areas/Account/Pages/_ViewImports.cshtml` exists separately from `Pages/_ViewImports.cshtml`; the tag helper must be registered in **both**.
   - The tag helper must target `script` and `style` (not a `TagHelperComponent`, which only targets head and body by default).
   - Tag helpers already run on `<script asp-append-version>`; multiple tag helpers can target the same element.

### 2.4 Target policy

The same text is used for Report-Only in phase 1 and enforcement in phase 2. `{N}` is the request nonce.

```
default-src 'self';
script-src 'self' 'nonce-{N}' https://js.arcgis.com https://unpkg.com/leaflet@1.9.4/;
script-src-attr 'none';
style-src 'self' 'nonce-{N}' https://js.arcgis.com https://unpkg.com/leaflet@1.9.4/;
style-src-attr 'unsafe-inline';
img-src 'self' data: blob: https://*.arcgis.com https://*.arcgisonline.com https://tile.openstreetmap.org https://*.tile.openstreetmap.org https://*.googleusercontent.com https://developers.google.com https://unpkg.com/leaflet@1.9.4/;
font-src 'self' data: https://js.arcgis.com https://*.arcgis.com;
connect-src 'self' https://*.arcgis.com https://*.arcgisonline.com https://tile.openstreetmap.org https://*.tile.openstreetmap.org;
worker-src 'self' blob:;
child-src 'self' blob:;
frame-src 'none';
object-src 'none';
base-uri 'self';
form-action 'self';
manifest-src 'self';
frame-ancestors 'none';
report-uri /csp-report; report-to csp
```

Plus `Reporting-Endpoints: csp="/csp-report"`, and `upgrade-insecure-requests` in Production only.

- `frame-ancestors` is ignored in Report-Only, so keep the current enforced minimal CSP header as a separate header until phase 2.
- `'unsafe-eval'` and `'wasm-unsafe-eval'` are **not** included. §2.6 shows ArcGIS 4.29 runs with eval blocked for the modules these pages use.
- `style-src-attr 'unsafe-inline'` is required by Esri's own guidance (developers.arcgis.com FAQ) and by the 62 existing style attributes. Removing it is not achievable while ArcGIS is in use, so treat it as an accepted residual.
- The `/scalar` page: Scalar.AspNetCore 2.16.15 serves its JavaScript from the package (`scalar.js` and `scalar.aspnetcore.js` embedded; no CDN URL found in the DLL). Confirm in Report-Only that it needs nothing beyond `'self'`, and if it does, scope a relaxed policy to `/scalar` only.

### 2.5 SRI

| Asset | Status | Recommendation |
|---|---|---|
| Leaflet `leaflet.js` and `leaflet.css` 1.9.4 (unpkg) | **Present and correct.** `sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=` and `sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=` match jsDelivr's npm metadata (A.8); unpkg sends `ACAO: *` and `max-age=31536000`; `crossorigin=""` present | Keep. Self-hosting is an option (D6) |
| ArcGIS 4.29 loader | **Missing.** `https://js.arcgis.com/4.29/` 301-redirects to `/4.29/init.js` (1,477,706 B, `ACAO: *`, `Last-Modified: 2024-03-28`, `max-age=86400, s-maxage=31557600`) | Point the tag at `…/4.29/init.js` with `integrity="sha384-…"` and `crossorigin="anonymous"`. The hash is computed by the implementer, which needs a file download (owner OK). Covers the loader only: the ~100 AMD modules and workers it fetches cannot carry SRI (D5) |
| ArcGIS theme CSS (`esri/themes/{light,dark}/main.css`) | **Missing.** Swapped at runtime by the inline loaders (`HomeFinder/Index.cshtml:202-215`, `PlantOperationsDashboard/Index.cshtml:15-29`) | Set `link.integrity` and `link.crossOrigin` inside the loader for each theme (two hashes) |

### 2.6 What ArcGIS 4.29 and Leaflet actually need (measured, A.8)

I served minimal pages through `scratchpad/sec/cspprobe/tools/serve.py`. The ArcGIS pages exercised `Map`, `MapView`, `GraphicsLayer`, `Graphic`, `Point`, `PopupTemplate` (opened, with HTML title and content), `webMercatorUtils`, and a basemap sweep (`dark-gray-vector`, `satellite`, `hybrid`, `topo-vector`, `streets-navigation-vector`, `osm`). That is the full set of esri modules the site imports (`grep "esri/…"`). I loaded them in headless Edge (`--use-angle=swiftshader`) under the §2.4 policy (without the reporting directives and the Google image hosts; nonce fixed to `TEST`), with a `securitypolicyviolation` collector.

| Run | Result |
|---|---|
| Report-Only, `arcgis.html` (streets basemap, graphic, popup template) | View-ready. Violations: `script-src eval` at `js.arcgis.com/4.29/:80` and at `esri/views/MapView.js:324` only. Leaflet page: **no violations** |
| Report-Only, `arcgis2.html` (`dojoConfig.has["csp-restrictions"]=true`, popup opened, basemap sweep) | The same two eval reports (so the flag does not help in 4.29), plus **`connect-src` for `{a,b,c}.tile.openstreetmap.org`** from the `osm` basemap (ArcGIS fetches OSM tiles with `fetch()`). No `worker-src`, `img-src`, `font-src` or `style-src` violations |
| **Enforced**, no `'unsafe-eval'`, OSM added to `connect-src` | `arcgis.html` **view-ready**; `arcgis2.html` **popup opened and full sweep completed**. Each logs one blocked `script-src eval` at `init.js:80` (capability detection with a fallback); the `MapView.js` eval no longer occurs. Leaflet fine |

So:

- **4.29 does not need `'unsafe-eval'` or `'wasm-unsafe-eval'`** for these features.
- It needs `worker-src blob:` (used, allowed, no report).
- It needs `connect-src` for `*.arcgis.com`, `*.arcgisonline.com` and the OSM tile hosts.
- Expect one `eval` report per ArcGIS page load, so filter it in the report endpoint.

Limits of this check: visual tile rendering was not verified (no screenshots), and only the module set above was exercised.

### 2.7 Rollout

1. **Phase 0 (SEC-WP7)**
   - Add the nonce middleware and tag helper, and emit `Content-Security-Policy-Report-Only` with §2.4. Keep the current enforced header.
   - Add an anonymous `/csp-report` endpoint:
     - `[RequestSizeLimit(8_192)]` and its own rate policy (for example 30/min/IP).
     - Accepts `application/csp-report` and `application/reports+json`; `[IgnoreAntiforgeryToken]`.
     - Logs one sanitised Warning line: effective directive, blocked URL reduced to scheme and host, document path without query, source origin and line.
     - Drops the known `js.arcgis.com/4.29/` eval report.
     - Returns 204.
   - This is a **new public route**, so it is an owner decision (D4).
   - Delete the dead `copyEmailContact` block and the unused typeahead include (D18).
2. **Phase 1 (SEC-WP5)**: remove the 9 JS inline handlers and fix the sinks.
3. **Soak for 1–2 weeks** with production traffic. Also run the headless probe from §2.6 against every page (18 demos plus Home, Login, Profile, Accessibility, Privacy and `/scalar`) in the Development build.
4. **Phase 2 (SEC-WP8)**: switch to enforced (keep `report-uri`) once the only reports are the filtered ArcGIS eval.

## 3. HTML sinks in `wwwroot/js` (excluding `lib/`)

**Method.** I grepped for `innerHTML`, `insertAdjacentHTML`, `outerHTML`, `.html(`, `.append(`, `.after(`, `render:`, `bindPopup`, `bindTooltip`, `popupTemplate`, `document.write`, `eval`, `new Function`, string `setTimeout`/`setInterval`, `DOMParser` and `createContextualFragment`. I then read every hit in context, and also inspected DataTables `data:` arrays and every toast helper.

**Results.** 149 matches in 21 files. 0 hits for `insertAdjacentHTML`, `outerHTML`, `document.write`, `eval`, `new Function`, string timers, `DOMParser` and `bindPopup`. The 8 `return d.innerHTML` lines inside escape helpers are reads, not sinks, and one further match is a comment (`dashboard.js:61`). **User data**, per the brief, means anything saved from user input and echoed back, CSV uploads, and ArcGIS responses.

### 3.1 Classification

| File:line | Sink | Data | Class |
|---|---|---|---|
| `BatchGeocoding/app.js:281` (cells from `:271` `r.originalAddress`, `:273` `r.matchedAddress`) | DataTables `data:` array, no `render` → cell `innerHTML` | Uploaded CSV (echoed back) and ArcGIS | **UNSAFE: exploitable** (verified, A.7) |
| `PlantOperationsDashboard/shipments.js:49` | `onclick="…'${row.status}'"` inside a DataTables `render` | User-saved shipment status (no server validation) | **UNSAFE: exploitable** (stored self-XSS; JS-in-attribute context) |
| `PlantOperationsDashboard/dashboard.js:159-161` | ArcGIS `PopupTemplate` title and content | User-saved shipment fields, unescaped | **UNSAFE: mitigated** by the ArcGIS HTML sanitizer (Esri uses `arcgis-html-sanitizer` since 4.14; still escape) |
| `ReverseGeocoding/app.js:210-213` | ArcGIS `popupTemplate` | ArcGIS `matchedAddress` and `locationType`, unescaped | **UNSAFE: mitigated** (sanitizer) |
| `Network/app.js:169-170` | Leaflet `bindTooltip` string | `hq.label` from `/network/graph`, unescaped | **UNSAFE: latent** (labels are compiled from OSM into `RedlandsRoadNetwork.cs`; Leaflet CVE-2025-69993 class) |
| `Network/app.js:180` | Leaflet `bindTooltip` string | `node.label`, unescaped | **UNSAFE: latent** (same) |
| `HomeFinder/app.js:340-356` | ArcGIS `PopupTemplate` | `p.street` (title) and `p.zipCode` unescaped; `propertyType`/`brokeredBy` escaped | **UNSAFE: latent** (server-seeded `Properties`; sanitizer) |
| `HomeFinder/app.js:402` (written at `:422`) | `aria-label="… ${escapeHtml(p.street)}"` | HomeFinder `escapeHtml` (`:454-456`) does not escape `"` | **UNSAFE: latent** (attribute breakout; server-seeded data) |
| `HomeFinder/app.js:683` | Local `showToast` (`:668`) writes `${message}` raw | Callers: literals; server validation text (`:511`); escaped (`:657`) | **UNSAFE: latent sink** |
| `PlantOperationsDashboard/dashboard.js:22-25` | Local `showToast` writes `${message}` raw. **Overrides the safe global toast** on that page (`function showToast` at `:7`, `window.plantOpsToast` at `:53-57`, loaded after `toasts.js`) | All current callers are literals (grep of every `plantOpsToast(` call) | **UNSAFE: latent sink** |
| `StateExplorer/uiManager.js:386` | `innerHTML` with `style="background:${escapeHtml(col.color)}"` | User collection colour (5-character escaper) | Safe for HTML; **CSS-context gap** (self data, ≤ 50 characters; SEC-22) |
| `StateExplorer/uiManager.js:324, 479, 556-570, 645` | `innerHTML` | Feature, saved and collection names, ArcGIS attributes, descriptions; all through the 5-character `escapeHtml` (`:13-21`) | Safe (escaped) |
| `AddressStandardization/app.js:260` | `$.html` | Parsed components via `escapeHtml`/`dash()` (`:49-60`) | Safe (escaped; text context) |
| `AddressStandardization/app.js:295` | `$.html` | `cfg.icon` from a static map (`:273-282`) | Safe (static) |
| `CatRisk/app.js:419` | `innerHTML` | `escapeHtml(c.name)`; numbers; computed colour | Safe (escaped) |
| `Change/app.js:477` | `innerHTML` | `escapeHtml(label)`; numbers | Safe (escaped) |
| `Fleet/app.js:188, 256, 268` | `innerHTML` / `bindTooltip` | `escapeHtml(stop.label / depotLabel / unserved ids)`; numbers; palette | Safe (escaped) |
| `GeoStream/app.js:74` | `innerHTML` | JSON numbers only | Safe (numeric) |
| `HomeFinder/app.js:183, 193` | `innerHTML` | Client-computed numbers | Safe (static/numeric) |
| `HomeFinder/app.js:583` | `innerHTML` | `escapeHtml(s.name)` in text; numeric ids | Safe (escaped) |
| `Outage/app.js:149, 197, 451` | `innerHTML` | `escapeHtml(label/action/feederName)`; numbers | Safe (escaped) |
| `Overlay/app.js:163` | `innerHTML` | `escapeHtml(z.name)`; numbers | Safe (escaped) |
| `Response/app.js:194, 250, 300, 334, 357` | `innerHTML` / `bindTooltip` | Numbers; `escapeHtml(candidate.label)` | Safe (escaped/numeric) |
| `ReverseGeocoding/app.js:87, 170` | `innerHTML` | `escapeHtml(matchedAddress / locationType)`; numbers | Safe (escaped) |
| `PlantOperationsDashboard/inventory.js:35-40, orders.js:38-45, shipments.js:32-36` | DataTables `render` | `render.text()`, `render.number`, dates, `plantOpsEscape` (5-character, `dashboard.js:64-72`) | Safe (escaped) |
| `PlantOperationsDashboard/inventory.js:46-58, orders.js:51-57, shipments.js:42-48` | DataTables action `render` | `plantOpsEscape(row label)` in `aria-label`; `${row.id}` (server int) in `onclick` | Safe from XSS; **CSP blocker** (inline handlers) |
| `PlantOperationsDashboard/inventory.js:106,143,238; orders.js:140; shipments.js:90,199,272` | `$modals.html` | String fields through `plantOpsEscape` in `value="…"`/text; numbers; static option lists | Safe (escaped); `inventory.js:101` has an inline handler (CSP) |
| `PlantOperationsDashboard/dashboard.js:191, 208-210` | `.after`, `.html` | Literals | Safe (static) |
| Clears and literal strings: `CatRisk/app.js:206,282`; `Change/app.js:373,455`; `Clustering/app.js:123,144`; `Fleet/app.js:80,279,358`; `GeoStream/app.js:61,62`; `Geometry/app.js:114`; `HomeFinder/app.js:389,490,526,532,571,579,631`; `Network/app.js:126,350,370`; `Outage/app.js:143,255`; `Overlay/app.js:51,117`; `Response/app.js:155,376`; `StateExplorer/uiManager.js:293,297,334,344,355,372,374,469,481,536,573,600`; `Terrain/app.js:67`; `Viewshed/app.js:112` | `innerHTML` | `""` or literal markup | Safe (static) |
| D3 `.append("tag")`: `AddressStandardization/app.js:121,127,134,177,189,192,206,216`; `PlantOperationsDashboard/dashboard.js:230-403` (30 calls) | Element creation, `.text()` | — | Safe (no HTML parsing) |
| `BatchGeocoding/app.js:192` | `FormData.append` | — | Not a DOM sink |
| Global `toasts.js:12-59` (`showToast`), `confirmDialog` `:71-122`, `StateExplorer/uiManager.js:415-433` | `createTextNode` / `textContent` | API error text (`api-fetch.js:56-64` passes `body.error`) | Safe (text) |

### 3.2 Escape helpers today

There are 10 local copies:

- **Escape `&`, `<` and `>` only, not quotes:** the `textContent` → `innerHTML` trick in `AddressStandardization/app.js:49-54`, `CatRisk/app.js:455-459`, `Change/app.js:527-531`, `Fleet/app.js:514-518`, `Outage/app.js:488-492`, `Overlay/app.js:166-170`, `Response/app.js:580-584` and `ReverseGeocoding/app.js:115-119`; and the regex version in `HomeFinder/app.js:454-456` (which also maps `0` to `""`).
- **Escape all five characters:** `StateExplorer/uiManager.js:13-21` and `PlantOperationsDashboard/dashboard.js:64-72` (`plantOpsEscape`).

The quote-blind copies are safe only in element-text context.

### 3.3 Proposal: one vetted helper plus DOM building

Add `wwwroot/js/dom-safe.js`, loaded in `_Layout.cshtml` right after `api-fetch.js` (`:177`):

```js
// window.PortfolioDom — the only sanctioned way to put untrusted text into markup.
(function (g) {
  "use strict";
  var MAP = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
  function escapeHtml(v) { return v === null || v === undefined ? "" : String(v).replace(/[&<>"']/g, function (c) { return MAP[c]; }); }
  // el("span", { class: "badge", "data-id": id, text: label }, child1, child2) — attributes via setAttribute, text via textContent.
  function el(tag, attrs) { var n = document.createElement(tag); if (attrs) Object.keys(attrs).forEach(function (k) {
      if (k === "text") n.textContent = attrs[k]; else if (k.slice(0, 2) !== "on") n.setAttribute(k, attrs[k]); });
    for (var i = 2; i < arguments.length; i++) { var c = arguments[i]; if (c !== null && c !== undefined) n.append(c); } return n; }
  g.PortfolioDom = { escapeHtml: escapeHtml, el: el };
}(window));
```

The rules for implementers:

1. Prefer `el()`/`textContent`. Use `escapeHtml` only inside quoted attribute or text contexts. Never use it in `on*`, `style`, `href`/`src` or script contexts.
2. Leaflet tooltips get `el(...)` nodes; Leaflet accepts `HTMLElement` content.
3. ArcGIS popups use a `content` function that returns a node, or escaped strings.
4. Colours go through `element.style.backgroundColor = value` after a `^#[0-9a-fA-F]{6}$` check.
5. Delete the 10 local copies and both local `showToast` overrides.

There is no JS test infrastructure, so verification is the headless probe from A.7/A.8 per page, plus an optional CI grep that fails on new `innerHTML = …${…}` lines lacking `escapeHtml(` (optional, owner's call).

## 4. Forwarded headers and the rate-limit partition

### 4.1 What the code does

- `Program.cs:234-241` enables `XForwardedFor | XForwardedProto` and **clears** `KnownIPNetworks` and `KnownProxies`, which means "trust any immediate peer". `ForwardLimit` stays at its default of **1**.
- The middleware therefore replaces `RemoteIpAddress` with the **right-most** `X-Forwarded-For` entry, and the scheme with the right-most `X-Forwarded-Proto`.
- Both rate limiters partition on that value (`Program.cs:447-448`).

### 4.2 Measured semantics (live, A.6: 16 requests to `GET /api/v1/catrisk/book`, policy "expensive", 15/min)

| Case | XFF sent | Result |
|---|---|---|
| a | none | 15 × 200, then **429** |
| b | rotating single entry `203.0.113.N` | 16 × 200 (**bypass**) |
| c | rotating left entry, fixed right `198.51.100.N, 192.0.2.77` | 15 × 200, then **429** |
| d | fixed left, rotating right `192.0.2.88, 198.51.100.N` | 16 × 200 (**bypass**) |

Whoever appends the last entry owns the partition key.

### 4.3 Exposure by deployment

| Deployment | Who appends the right-most entry | Spoofable? |
|---|---|---|
| Render (production) | The Render/Cloudflare edge. Production headers show `Server: cloudflare`, `CF-RAY`, `cf-cache-status`, `rndr-id`, and `x-render-origin-server: Kestrel` (A.9), so the chain is client → Cloudflare → Render proxy → container | **No**, if the last hop appends the real client IP. **But** if Render's proxy appends the Cloudflare edge IP after Cloudflare's client entry, the key is a *shared edge IP*: all users at that PoP share 300/min and 15/min, and one abuser can 429 everyone. Render does not document this. A 2021 Render staff reply says the real client is first; a 2024 community report says Render "only appends" and suggests reading the second-to-last entry. Independent projects record the same open question (sources in Appendix B) |
| `docker/docker-compose.yml` | The client (port `8080:8080` published directly) | **Yes**, fully |
| `k8s/` (ClusterIP, no ingress manifest) | Whatever ingress is added later | Depends; unsafe by default |

Render does **not** publish its internal proxy address ranges (it publishes outbound IPs for allowlisting only), so `KnownProxies` cannot be set from documentation.

### 4.4 Narrowest safe configuration

1. Make the trust explicit and configurable, with no behaviour change by default:
   ```csharp
   options.ForwardLimit = cfg.GetValue("ForwardedHeaders:ForwardLimit", 1);
   foreach (var cidr in cfg.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
       options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
   // keep Clear() only when the list is empty (current behaviour)
   ```
2. After the owner check in §4.5, set `KnownNetworks` to the observed source range of Render's proxy (expected RFC 1918, e.g. `10.0.0.0/8`). Then a direct connection from outside that range cannot inject headers, and the compose and k8s defaults stop being spoofable.
3. If §4.5 shows a Cloudflare hop appended last, use `ForwardLimit=2` **with** `KnownNetworks` covering Render's proxy range. The middleware then walks past the trusted Render hop and takes the Cloudflare-appended client IP. Do **not** raise `ForwardLimit` without `KnownNetworks`: that hands the key back to the client (case d above).
4. Independent of the partition, add a **global concurrency limiter** for compute endpoints (SEC-WP2). It bounds total CPU even if partitioning is wrong or spoofed.

### 4.5 Owner verification (five minutes, production)

From one machine, send 16 requests to `GET https://brianpcoffey.com/api/v1/catrisk/book` with `-H "X-Forwarded-For: 203.0.113.$i"` and the default `Accept`.

- If none return 429, the key is client-controlled. That is a critical surprise; stop and fix.
- Then run 10 requests from your phone hotspot and 10 from home within the same minute. If either side sees 429 before 15 requests, the buckets are shared (an edge IP).
- Optionally, for 10 minutes, log one sampled Information line containing the count of XFF entries received and `Request.Headers["X-Original-For"]` (the peer address before forwarding) to learn the proxy CIDR. Remove it afterwards.

## 5. Rate limiting, request size, and per-DTO validation

### 5.1 Coverage per controller and action (25 controllers; `grep` A.5)

Global limit: 300 requests/min/IP, fixed window (`Program.cs:450-456`). The "expensive" policy is 15/min/IP (`:458-464`). One limiter instance per IP is shared by **all** "expensive" endpoints.

| Controller (base route) | Actions | Auth | Per-endpoint limit | `[RequestSizeLimit]` |
|---|---|---|---|---|
| AddressStandardization (`/api/v1/addresses`) | POST parse, POST validate | Anon (class) | expensive | **none** (30 MB default) |
| BatchGeocoding (`/api/v1/geocoding/batch`) | POST, GET `{jobId}/status`, POST `sync` and legacy `/api/batchgeocoding/sync` | Anon | expensive | 2 MB on POSTs |
| CatRisk (`/api/v1/catrisk`) | GET book, POST accumulation, POST simulate | Anon | expensive | 8 MB |
| ChangeDetection (`/api/v1/change`) | GET scene, POST detect | Anon | expensive | 16 MB |
| Collections (`/api/v1/collections`) | GET, GET `{id}`, POST, PUT, DELETE | Authenticated | global only | **none** |
| Features (`/api/v1/features`) | GET (outbound ArcGIS call) | Anon | **global only** | n/a |
| FiberDashboard (`/api/v1/fiber/dashboard`) | GET stats | Authenticated | global only | n/a |
| FiberMaterials (`/api/v1/fiber/materials`) | GET, GET `{id}`, POST, PUT, DELETE, POST `{id}/receive` | Authenticated | global only | **none** |
| FiberOrders (`/api/v1/fiber/orders`) | GET, GET `{id}`, POST, PUT, DELETE | Authenticated | global only | **none** |
| FiberShipments (`/api/v1/fiber/shipments`) | GET, GET `{id}`, POST, PUT, PUT `{id}/status`, DELETE | Authenticated | global only | **none** |
| FleetRouting (`/api/v1/fleet`) | GET scenario, POST optimize | Anon | expensive | 4 MB |
| GeoStream (`/api/v1/geostream`) | POST events | Anon | **global only** | 2 MB |
| HomeFinder (`/api/v1/homefinder`) | POST search, POST score (legacy), GET property/`{id}`, GET properties/`{id}` (legacy) [Anon]; POST/GET searches, GET/DELETE searches/`{id}` [Auth] | per action | expensive (class) | **none** |
| Outage (`/api/v1/outage`) | GET network, POST trace, POST restore | Anon | expensive | 4 MB |
| Profile (`/api/v1/profile`) | GET, POST claims, DELETE claims/`{type}` [Anon]; PUT me/claims [Auth] | per action | global only | **none** |
| RasterTerrain (`/api/v1/raster`) | POST hillshade (8 MB), POST heatmap (4 MB) | Anon | **global only** | yes |
| ResponseCoverage (`/api/v1/response`) | GET scenario, POST isochrone, POST optimize | Anon | expensive | 4 MB |
| ReverseGeocoding (`/api/v1/geocoding/reverse`) | GET | Anon | expensive | n/a |
| SavedFeatures (`/api/v1/features/saved`) | GET, POST, DELETE `{id}` | Anon (anonymous identity) | global only | **none** |
| SpatialCluster (`/api/v1/clustering`) | POST dbscan | Anon | **global only** | 4 MB |
| SpatialGeometry (`/api/v1/geometry`) | POST triangulate, POST clip | Anon | **global only** | 1 MB |
| SpatialNetwork (`/api/v1/network`) | GET graph, POST route, POST service-area | Anon | **global only** | 4 MB |
| SpatialOverlay (`/api/v1/overlay`) | POST spatial-join | Anon | **global only** | 4 MB |
| UserProfile (`/api/v1/users`) | GET me, GET me/claims [Anon]; PUT me, GET `{userId}`, GET google/`{googleId}`, PUT/DELETE me/claims, DELETE `{userId}` [Auth plus owner checks] | per action | global only | **none** |
| Viewshed (`/api/v1/viewshed`) | POST compute | Anon | **global only** | 8 MB |

Outside the controllers:

- `/health/live` and `/health/ready` use `DisableRateLimiting` (SEC-32).
- OpenAPI and Scalar get the global limit only.
- Razor pages get the global limit and the Kestrel default body size.
- Form limits are framework defaults (e.g. a 128 MB multipart limit); the only multipart action is capped at 2 MB, so this is fine.

**UI call patterns**, which decide what limits are safe:

- All seven compute pages are button-driven (`addEventListener("click", …)`), except Viewshed cell clicks, which call `run()` immediately (`Viewshed/app.js:142-146`).
- Network map clicks only set the origin (`Network/app.js:78-81,207-215`).
- StateExplorer calls `/features` once per load (`featureService.js:14-16`).
- So the "expensive" 15/min limit would break the Viewshed exploration UX. Use a separate "compute" tier (D1).

### 5.2 DoS-able inputs (all pass current validation; measured, A.4 and A.6)

| Endpoint | Input | Body | Cost | Current limit |
|---|---|---|---|---|
| `POST /api/v1/viewshed/compute` | `width=250000,height=1`, flat terrain, observer `(0,0)` | ~500 KB | **237.3 s CPU** (O(W²); 20k: 1.0 s, 50k: 7.9 s, 100k: 37.0 s) | global 300/min |
| same, via HTTP | `width=30000` | 90 KB | 3.1 s wall | global |
| `POST /api/v1/raster/heatmap` | 500×500 cells, 10,000 points | 596 KB | **29.2 s** (O(cells × points); 1,000 points: 2.0 s) | global |
| `POST /api/v1/clustering/dbscan` | 5,000 identical points, `epsilon=1`, `minPoints=1` | 70 KB | **313 MB allocated**, 267 MB peak working set (O(n²) seed list) | global |
| `POST /api/v1/overlay/spatial-join` | 10,000 points outside 200 zones × 100 vertices | 1.15 MB | 0.48 s (O(points × vertices)) | global |
| `POST /api/v1/addresses/parse` | 25 MB `rawAddress` | 25 MB | **+1.19 GB working set**, 3.5 s | expensive |
| `POST /api/v1/change/detect` | 512×512×8, minimal JSON | 8.4 MB (cap 16 MB) | 102 MB allocated, 0.33 s | expensive (15 concurrent ≈ 1.5 GB) |
| `POST /api/v1/catrisk/simulate` | 5,000 locations × 12,000 events, event radius 1e6 km (no upper bound) | 2 MB | 2.8 s | expensive |
| `POST /api/v1/response/optimize` | 125 candidates × 2,000 demand points, p=20, mode 1 (p90), `maxIterations=500` | ~0.2 MB | 4.4 s (mode 0: 0.24 s) | expensive |
| `POST /api/v1/fleet/optimize` | 120 stops, 20 vehicles, 5,000 iterations | ~20 KB | 0.83 s, 303 MB allocated | expensive |
| Any HTML GET without a cookie | `Accept: text/html` | 0 | 1 profile + 75 rows + ~80 queries (SEC-03) | global |

### 5.3 Per-DTO validation status

| Service (validation lines) | Collection caps | NaN/∞ | Gaps |
|---|---|---|---|
| `SpatialClusterService.cs:27-40` | points ≤ 5,000 | ✓ | No seed dedup (SEC-01) |
| `GeoStreamProcessorService.cs:24-32` | events ≤ 10,000 | Grid size and threshold ✓ | Per-event lat/lon/speed unchecked; NaN lat/lon pass the range test (`:51`); no lower bound on grid size |
| `SpatialGeometryService.cs:25-29,62-68,141-145` | ≤ 5,000 | Points ✓ | Box `Min>=Max` passes with NaN (harmless) |
| `RasterTerrainService.cs:21-38,41-62,143-151` | cells ≤ 250,000; points ≤ 10,000 | Cell size and radius ✓ | Elevation, azimuth/altitude, extent and point x/y/weight unchecked; **no cells × points budget** |
| `ViewshedService.cs:20-43,116-124` | cells ≤ 250,000 | Cell size and observer height ✓ | Elevation unchecked; **no maximum side**; cancellation per row only |
| `SpatialOverlayService.cs:23-56` | points ≤ 10,000, zones ≤ 200, vertices ≤ 20,000 | ✓ | Zone name unbounded (within 4 MB) |
| `SpatialGraphService.cs:527-551` | nodes ≤ 5,000, edges ≤ 20,000, matrix ≤ 250,000 | ✓ | `KeyNotFoundException` at `:245,323` for edges to undeclared nodes (SEC-12) |
| `CatRiskService.cs:134-174,107-110` | locations ≤ 5,000, events ≤ 20,000, evaluations ≤ 6e7 | ✓ | No upper bounds on `InsuredValue` (sums overflow to ∞ and the serializer throws) or event `RadiusKm` |
| `ChangeDetectionService.cs:35-85,175-195` | ≤ 512×512, bands ≤ 8, open iterations ≤ 5, blobs ≤ 5,000 | ✓ (`ToFiniteArray`) | — |
| `OutageTraceService.cs:193-214` | elements ≤ 5,000, overrides ≤ 100 | n/a | — |
| `ResponseCoverageService.cs:204-252,121-126` | demand ≤ 2,000, candidates ≤ 200, stations ≤ 20, iterations ≤ 500 | ✓ | p90 mode is 4.4 s per request; consider `maxIterations ≤ 100` (D2) |
| `FleetRoutingService.cs:143-176` | stops ≤ 120, vehicles ≤ 20, iterations ≤ 5,000 | ✓ | Allocation-heavy (303 MB) but bounded |
| `ReverseGeocodingService.cs:43-47` | n/a | **NaN passes** | Outbound ArcGIS call with `NaN,NaN` |
| `AddressStandardizationService.cs:99-112` | none | n/a | **No length cap** (SEC-02) |
| `BatchGeocodingService.cs:293-332` | rows ≤ 10,000; 2 MB | n/a | No global job cap (SEC-24) |
| Persistence services | see SEC-22 | — | Claims, saved features, colour, shipments, saved search |

JSON binding facts (harness `json` probe, MVC's default `JsonSerializerDefaults.Web` with `AllowReadingFromString`):

- `"NaN"`, `"Infinity"` and `1e400` bind to non-finite doubles.
- A bare `NaN` and the string `"1e400"` fail to bind.
- Query-string `double` accepts `NaN` and `Infinity`.
- **Every double needs an explicit finite check.** A global `NumberHandling=Strict` would also stop clients that send numbers as strings, so it is not recommended.

### 5.4 Caps that kill the measured inputs but keep the UI working

| Cap | UI maximum today | Proposed |
|---|---|---|
| Viewshed side | 40 (`Viewshed/app.js:51-54`, `Viewshed/Index.cshtml:38`) | `max(width,height) ≤ 512` (500×500 measured at 0.5 s) |
| Terrain/hillshade side | 24 (`Terrain/Index.cshtml:39,43`) | ≤ 512 |
| Heatmap cells × points | 576 × 3 (`Terrain/app.js:45-56`) | ≤ 25,000,000 (about 0.3 s) |
| DBSCAN | ~130 points (`Clustering/app.js:55-66`) | keep 5,000; dedup seeds (memory O(n)) |
| Overlay | ≤ 4,000 points (`Overlay/app.js:40`) | keep; rely on the "compute" tier and concurrency |
| GeoStream grid size | ≥ 0.001 (`GeoStream/Index.cshtml:40`) | ≥ 1e-6 |
| Address `rawAddress` | Single line | ≤ 1,000 characters; `[RequestSizeLimit(16_384)]` |
| Batch rows | Samples are 10–12 rows (`wwwroot/samples/batch-geocoding/*.csv`) | D21 (default 1,000) |

## 6. Authentication, authorization, sessions, headers, logging

### 6.1 Controller attribute compliance

All 25 controllers carry explicit metadata at class or action level (A.5). Mixed controllers (`HomeFinderController`, `ProfileController`, `UserProfileController`) annotate every action, and none has a class-level `[AllowAnonymous]` (which would short-circuit authorization; see the comment in `HomeFinderController.cs:16-19`). Pages: `Areas/Account/Pages/Profile.cshtml.cs:10` and `Pages/Projects/Details.cshtml.cs:8` require "Authenticated"; the rest are anonymous. There is no `FallbackPolicy`, and adding one is **not** recommended (it would put every page behind login). Instead, SEC-34 adds a reflection test.

### 6.2 SEC-04 in detail (identity mixing)

1. An anonymous visitor gets `AnonUserId=G` (`AnonymousUserMiddleware.cs:111-133`).
2. At first Google sign-in, `CreateOrUpdateFromGoogleAsync` finds no profile for that Google ID, reads the `AnonUserId` cookie (`UserProfileService.cs:171-180`), and **adopts G as the Google account's id** (`:182-188`). It then writes `google_id`, `email`, `name` and `picture` claims on G (`:201-207`). `OnSignedIn` deletes the cookie (`Program.cs:370-374`).
3. From then on, any request **without** an auth cookie but with `AnonUserId=G` passes `AnonymousUserMiddleware.cs:75-99` (it only checks that a profile exists) and sets `Items["PortfolioIdentity"]=G`.
   - The `[AllowAnonymous]` endpoints then act as the Google user: `GET /api/v1/profile` returns all claims (`ProfileController.cs:44-57`); `GET /api/v1/users/me`; saved-feature list, create and delete; `POST /api/v1/profile/claims` for non-protected types such as `display_name`, which renders in the victim's nav bar (`_LoginPartial.cshtml:12-16`).
4. Takeover path: the attacker sets `AnonUserId=G` and signs in with a **new** Google account.
   - Step 2 adopts G again and `SetClaimAsync(G, google_id, attacker)` **overwrites** the victim's `google_id` (`UserProfileRepository.cs:118-134` is an upsert).
   - The attacker's Google login now maps to the victim's collections, fiber data and saved searches. The victim's next login creates a new, empty profile.
5. Where G leaks:
   - Shown on the Profile page (`Profile.cshtml:72`).
   - Returned by `/api/v1/profile` and `/api/v1/users/me`.
   - Logged at Warning, which production keeps (SEC-16).
   - Any XSS (SEC-05/06/07) can read it through `fetch('/api/v1/profile')`.

The fix is to keep the two identities separate, as `agent-context.md` §6 already requires: in both places, check `HasClaimAsync(userId, google_id)` before honouring an anonymous identity.

### 6.3 Other identity and session checks

- **Cookies:** the auth cookie uses Lax, HttpOnly and Secure in Production, 8 h sliding (`Program.cs:290-302`). `AnonUserId` uses HttpOnly, Secure outside Development, Lax and a 1-year expiry (`AnonymousUserMiddleware.cs:121-133`). The session cookie is unused (SEC-19).
- **Antiforgery:** Razor POSTs are protected (verified). API mutations rely on SameSite plus a JSON-only content type, which returned 415 for simple content types in the live probe (SEC-21).
- **OAuth:** compliant, including PKCE (§1, accepted rows).
- **Open redirect:** none (§1).
- **CORS:** none configured, which is correct.
- **`AllowedHosts`:** see SEC-11.
- **OpenAPI and Scalar:** see SEC-20.
- **`ApiExceptionMiddleware`:** see SEC-12 and SEC-15.
- **Logging:** see SEC-16.
- **Data Protection:** see SEC-17. The key name is `DataProtection-Keys` in the same Upstash database as the cache; the cache uses the instance prefix `portfolio:`, so keys do not collide. If Redis is unreachable at startup, the code falls back to the container filesystem (`Program.cs:527-533`). On Render that filesystem is ephemeral, so keys reset on each restart and everyone is logged out. That is an availability note, not a vulnerability.
- **HSTS and security headers:** see SEC-14. Production HSTS is 30 days without `includeSubDomains`. Because the in-app redirect is dev-only, HSTS is emitted only when `X-Forwarded-Proto: https` is honoured, which depends on SEC-10.

## 7. Containers, Kubernetes, CI

### 7.1 Dockerfile and compose (`Dockerfile:1-45`, `docker/*.yml`)

| Check | Status |
|---|---|
| Non-root user | **No** `USER` instruction. The `aspnet:10.0` image provides `app` (UID 1654, `$APP_UID`). `/app/DataProtection-Keys` is created as root at `:37`, so the fix needs `chown`. The app binds 10000 (`:41`), which is fine for non-root |
| Base images pinned by digest | **No** (`:4`, `:29`). The floating tag picks up runtime patches at each build, which matters because 10.0.11 and 10.0.12 shipped CVE fixes (Appendix B). Pin with Dependabot updates rather than freezing |
| Secrets in layers | **OK.** `.dockerignore:23-25` excludes `.env`, `appsettings.Development.json` and `DataProtection-Keys/`; only `/app/publish` reaches the runtime stage |
| Healthcheck | None (Render uses its own; k8s uses probes). Optional |
| Minimal runtime | `apt-get install libgssapi-krb5-2` (`:33`). A chiseled image would block that, so it is optional |
| Compose Redis | `6379:6379` on all interfaces, no auth (`docker-compose.yml:21-23`), holding the DP keys → SEC-27 |
| Non-security note | `ENV ASPNETCORE_HTTP_PORTS=${PORT:-10000}` resolves at build time (`:41`), so compose's `PORT=8080` and mapping `8080:8080` (`docker-compose.yml:9-12`) reach nothing. Forward this to the refactor/docs owners |

### 7.2 Kubernetes manifests

- `k8s/deployment.yaml:18-51`:
  - Has: resource requests and limits ✓, liveness and readiness probes ✓.
  - Missing: `securityContext` (runAsNonRoot, runAsUser 1654, allowPrivilegeEscalation false, readOnlyRootFilesystem plus an emptyDir for `/tmp`, `capabilities: drop: [ALL]`, `seccompProfile: RuntimeDefault`); `automountServiceAccountToken: false`.
  - Uses a mutable `:latest` image.
- `k8s/redis.yaml`: no auth, no securityContext, no readiness probe, and holds the DP key ring.
- No `NetworkPolicy` exists. Recommended: default-deny; allow ingress to the app on 8080 from the ingress namespace; allow app → redis on 6379; allow egress for DNS, 443 and 5432.
- `k8s/namespace.yaml`: no Pod Security Admission labels (`pod-security.kubernetes.io/enforce: restricted`).
- `k8s/secret.yaml:1-13`: placeholder `stringData` meant to be edited in place. Rename it to `secret.example.yaml` and gitignore `k8s/secret.yaml`; this protects a **public** repo.

### 7.3 CI (`.github/workflows/ci.yml`)

Already good:

- `permissions: contents: read` (`:18-19`).
- Triggers are `push`, `pull_request` and `workflow_dispatch`; there is no `pull_request_target`.
- `RENDER_DEPLOY_HOOK` is read only in `deploy` on `push` to `main` (`:235-248`); GitHub masks it.
- Timeouts are set.
- NuGet audit errors fail the restore (`Directory.Build.props:23-26`).

Pins for the **current** majors, which preserve behaviour (`git ls-remote`, A.10). All are lightweight tags pointing straight at commits:

| Action @ tag in use | Pin to (commit) | Tag at that commit | Latest major (separate decision) |
|---|---|---|---|
| `actions/checkout@v4` (`:44,145,210`) | `11d5960a326750d5838078e36cf38b85af677262` | v4.4.0 | v7 |
| `actions/setup-dotnet@v4` (`:46,147`) | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` | v4.3.1 | v6 |
| `actions/cache@v4` (`:53,152`) | `0057852bfaa89a56745cba8c7296529d2fc39830` | v4.3.0 | v6 |
| `actions/upload-artifact@v4` (`:122`) | `ea165f8d65b6e75b540449e92b4886f43607fa02` | v4.6.2 | v7 |
| `docker/setup-buildx-action@v3` (`:212`) | `8d2750c68a42422c14e847fe6c8ac0403b4cbd6f` | v3.12.0 | v4 |
| `docker/build-push-action@v6` (`:215`) | `10e90e3645eae34f1e60eeb005ba3a3d33f178e8` | v6.19.2 | v7 |

Missing:

- `.github/dependabot.yml` (ecosystems `nuget`, `github-actions` and `docker`, weekly).
- A scheduled run: `on: schedule: - cron: '0 7 * * 1'` running `dotnet restore` plus `dotnet list package --vulnerable --include-transitive`, failing on any finding.
- CodeQL default setup and secret-scanning push protection. Both are repository settings and free for public repos (D20).

## 8. Dependencies

| Item | Version | Status | Evidence |
|---|---|---|---|
| NuGet, all projects, direct and transitive | — | **0 vulnerable** | A.1 `dotnet list package --vulnerable --include-transitive` |
| Outdated (top-level) | Microsoft.AspNetCore.* / EF Core / Extensions 10.0.10 → **10.0.12**; Http.Resilience 10.8.0 → 10.10.0; Asp.Versioning 10.0.0 → 10.2.1; Scalar 2.16.15 → 2.18.0; Microsoft.OpenApi 2.7.5 → 3.10.2 (**keep 2.7.5**, the 3.x API is incompatible, `Directory.Packages.props:18-20`); test packages (xunit.v3 3.2.2 → 4.0.2 is a major) | Patch bumps recommended | A.1 `--outdated` |
| Deprecated | `Microsoft.Testing.Platform.MSBuild` 1.9.1 (transitive, test only, "Legacy") | Informational | A.1 `--deprecated` |
| .NET runtime (image) | `aspnet:10.0` floating | The 10.0.11 and 10.0.12 runtimes fixed several CVEs (Fedora advisories list CVE-2026-62871… and CVE-2026-58649…) | Appendix B; local runtime is 10.0.12 |
| jQuery | 3.7.1 | No CVEs (the 2020 XSS issues were fixed in 3.5.0) | Appendix B |
| Bootstrap | 5.3.3 | Not affected by CVE-2024-6484 or CVE-2024-6531 (3.x/4.x carousel) | Appendix B |
| DataTables | 1.13.7 | No CVE found (CVE-2020-28458 was fixed in 1.10.22; CVE-2015-6584 and CVE-2021-36713 are for older versions). The 1.x line is legacy; 2.x is current | Appendix B |
| D3 | 7.9.0 | No CVE found | Appendix B |
| Font Awesome | 6.7.2 (CSS and fonts) | No CVE | Header of `lib/fontawesome/css/all.min.css` |
| jquery-validation / unobtrusive | 1.21.0 / 4.0.0 | Not affected (CVE-2025-3573 fixed in 1.20.0). **Never loaded**: `_ValidationScriptsPartial` has no user | grep (A.5) |
| typeahead.js | 1.3.1 | No CVE found; unmaintained; **loaded but unused** | §2.2 |
| Leaflet (CDN) | 1.9.4 | **CVE-2025-69993** (CVSS 6.1, published 2026-04-14): `bindPopup` HTML injection, all versions ≤ 1.9.4, no fixed release. `bindTooltip` uses the same content path | osv.dev (Appendix B); mitigated by SEC-WP5 |
| ArcGIS Maps SDK (CDN) | 4.29 | No SDK CVE found. Snyk reports 0 issues for `arcgis-js-api` 4.29.10. The release dates from March 2024 and is old | Appendix B |

## 9. Native code reachable from requests

Production ships no native code (`agent-context.md` §5). Every managed service validates before calling a bridge, and bridges size their buffers from validated values (e.g. `ViewshedNativeBridge.cs:53-63` passes `Elevation.ToArray()` with width×height elements and `output.Length`). The issues below are therefore **defence-in-depth** for anyone running with the kernels present (local, Benchmarks, CI parity). They still matter because a native access violation cannot be caught in .NET Core (no `SEHException`; the process dies) and so cannot "fall back".

| Kernel | Issue | Location |
|---|---|---|
| viewshed | `elevation` pointer has **no length parameter** (breaks the documented pointer-plus-int ABI rule); `outputLength < width * height` uses signed `int` multiplication (overflow is UB); no NaN guard | `native/viewshed_kernel/src/viewshed_kernel.cpp:25-41` |
| raster hillshade | Same missing elevation length; `int` overflow in the check | `native/raster_terrain_kernel/src/raster_terrain_kernel.cpp:21-34`; heatmap `:61-76` has the same `width * height` |
| scoring | `ScoreProperty`/`ScorePropertyBatch` return `void`, take no output length, do not null-check and have no `try/catch` (breaks the status-code convention) | `native/portfolio_scoring/src/scoring_kernel.cpp:206-235` |
| geostream | `static_cast<int>(std::floor(x / grid))` with an unbounded `x/grid` is UB in C++ (the managed side saturates to `int.MaxValue`, measured), so tiny grid sizes give a parity gap | `native/geostream_processor/src/geostream_processor.cpp:41-42` |
| cluster | The `seeds` vector grows O(n²) like the managed code (SEC-01) | `native/spatial_cluster_kernel/src/spatial_cluster_kernel.cpp:32-60` |
| overlay | `accumulated += ringSizes[z]` is int and unchecked (bounded today by managed validation) | `native/spatial_overlay_kernel/src/spatial_overlay_kernel.cpp:41-50` |

The minimal managed-side mitigation (part of SEC-WP1, no ABI change) is a lower bound on grid size and finite checks before any bridge call. The full ABI fix is SEC-WP16 (D17).

## 10. Work packages

**Global rules for every package.**

- Run the gates from `cleanup-prompt.md` Stage 2: `dotnet build -c Release -p:ContinuousIntegrationBuild=true`; `dotnet test` twice (once with `PORTFOLIO_DISABLE_NATIVE=1`); coverage at or above the 80% line / 60% branch ratchets.
- Write characterization tests first.
- Do not change routes or JSON shapes unless the package says so.
- Do not touch `RedlandsRoadNetwork.cs` or migrations.

The sizes are S (≤ ½ day), M (1–2 days) and L (3+ days).

### SEC-WP0: Security test scaffolding and characterization (M)

- **Goal:** a place to test pipeline-level behaviour (headers, limits, error mapping, auth metadata) before anything changes.
- **Files:**
  - `Directory.Packages.props` (add `Microsoft.AspNetCore.Mvc.Testing`, versioned to match the Microsoft.* packages: 10.0.10 now, 10.0.12 after WP13; D16) and `Portfolio.Tests/Portfolio.Tests.csproj`.
  - New: `Portfolio.Tests/Integration/PortfolioWebFactory.cs`, `Portfolio.Tests/Integration/SecurityHeadersTests.cs`, `Portfolio.Tests/Integration/RateLimitingTests.cs`, `Portfolio.Tests/Middleware/ApiExceptionMiddlewareTests.cs`, `Portfolio.Tests/Middleware/SecurityHeadersMiddlewareTests.cs`.
  - Extend: `Portfolio.Tests/Controllers/ApiVersioningAttributeTests.cs` (or a new `AuthorizationMetadataTests.cs`).
- **Steps:**
  1. Build `WebApplicationFactory<Program>`. `Program` is reachable through the existing `InternalsVisibleTo` in `Portfolio.Web/AssemblyInfo.cs`. Configure:
     - `UseEnvironment("Development")`.
     - `UseSetting("RunMigrationsOnStartup","false")`.
     - `ConnectionStrings:DefaultConnection` pointing at an unreachable host.
     - Dummy `Authentication:Google:*` values.
  2. Send API requests with `Accept: application/json` so `AnonymousUserMiddleware` never touches the DB (`AnonymousUserMiddleware.cs:73,105-109`).
  3. Characterize, as currently true:
     - the current headers;
     - 429 on the 16th "expensive" request;
     - XFF partition semantics (§4.2 cases a–d);
     - error mapping (`KeyNotFoundException` → 404, oversized body → 500);
     - Host reflection.
  4. Add a reflection test: every public action on every `ControllerBase` in `Portfolio.Web` has an `AuthorizeAttribute` with `Policy=="Authenticated"` or an `AllowAnonymousAttribute`, at action or class level. Every controller in {GeoStream, SpatialGeometry, RasterTerrain, SpatialNetwork, SpatialCluster, Viewshed, SpatialOverlay, Features} has an `EnableRateLimitingAttribute`. This is red until SEC-WP2, so mark it with `Skip` and a reason, or land it with WP2.
- **Acceptance:** the new tests pass on the unmodified code (characterization), and CI is green.
- **Risk:** the factory may need `DataProtection-Keys` under the test working directory; Development mode already does that.
- **Rollback:** delete the test files and the package line.
- **Dependencies:** none.

### SEC-WP1: Compute work caps and finite validation (M) [SEC-01, SEC-12 (graph), SEC-13]

- **Files:** `Portfolio.Services/Services/ViewshedService.cs`, `RasterTerrainService.cs`, `SpatialClusterService.cs`, `GeoStreamProcessorService.cs`, `SpatialGraphService.cs` (lines `239-250`, `317-325` only), `CatRiskService.cs` (validation only), `ReverseGeocodingService.cs` (`:43-47`). Tests go in `Portfolio.Tests/Services/HotspotViewshedOverlayServiceTests.cs`, `SpatialComputeServiceTests.cs`, `CatRiskServiceTests.cs` and `ReverseGeocodingServiceTests.cs`. Optional: the native `spatial_cluster_kernel.cpp` dedup.
- **Steps:**
  1. Viewshed: `private const int MaxDimension = 512;` and throw `ArgumentException` if `width > MaxDimension || height > MaxDimension`. Check cancellation every 4,096 target cells inside the inner loop.
  2. Hillshade/heatmap: `MaxDimension = 512`; `const long MaxHeatmapWork = 25_000_000;` and throw if `(long)width*height*points > MaxHeatmapWork`. Check cancellation per row (already done) **and** every 1,024 cells.
  3. Raster/viewshed: validate that every elevation value, azimuth, altitude, extent and point (x, y, weight) is finite.
  4. DBSCAN: add `int[] queuedIn` initialised to -1. In the expansion, mark the initial neighbours and append a neighbour to `seeds` only if `queuedIn[i] != clusterId`, then set it. Labels stay identical: later duplicates of a point are no-ops in the current algorithm (`SpatialClusterService.cs:88-100`). Add a test comparing against a copy of the old method on 1,000 random cases.
  5. GeoStream: skip or invalidate events with non-finite lat/lon (keep the current behaviour of counting them as invalid, not throwing) and reject non-finite speed. Require `GridSizeDegrees >= 1e-6`.
  6. SpatialGraph Dijkstra/A*: replace `distances[edge.To]`/`gScore[edge.To]` with `TryGetValue` and skip unknown nodes, matching `ComputeDistances` at `:448-449`.
  7. CatRisk: `InsuredValue ≤ 1e12`, event `RadiusKm ≤ 5,000`.
  8. ReverseGeocoding: `if (!double.IsFinite(latitude) || …) throw new ArgumentException(...)`.
- **Tests:**
  - Each new cap gives `ArgumentException` (400 at the controller).
  - Boundary values pass: viewshed 512×488, the UI maximum of 40×40, heatmap at budget.
  - DBSCAN with 5,000 identical points allocates < 32 MB (`GC.GetAllocatedBytesForCurrentThread`).
  - A route with an edge to an undeclared node returns a result, not an exception.
  - NaN cases for each guarded field.
- **Acceptance:**
  - The harness inputs from §5.2 are rejected or finish in < 1 s.
  - All existing tests and the native parity job stay green.
  - `NativeAccelerated` semantics are unchanged.
- **Risk:** narrows the accepted public API inputs (owner D2). If a parity test exercises sizes above the new caps, adjust the test input, never the managed fallback.
- **Rollback:** revert per service.
- **Dependencies:** none (can run in parallel with WP0).
- **Overlap:** the complexity agent targets `SpatialGraphService` and the other large services. Land WP1 first; it changes only validation and two indexers.

### SEC-WP2: Pipeline limits and config hardening in `Program.cs` plus controller attributes (M) [SEC-01, SEC-02, SEC-10 (config surface), SEC-14 (HSTS, Server header), SEC-16 (`Program.cs:686,765`), SEC-23, SEC-31, SEC-32]

- **Files:** `Portfolio.Web/Program.cs`; controllers `GeoStreamController.cs`, `SpatialGeometryController.cs`, `RasterTerrainController.cs`, `SpatialNetworkController.cs`, `SpatialClusterController.cs`, `ViewshedController.cs`, `SpatialOverlayController.cs`, `FeaturesController.cs`, `AddressStandardizationController.cs`, `ProfileController.cs`, `UserProfileController.cs`, `SavedFeatureController.cs`, `CollectionsController.cs`, `FiberMaterialsController.cs`, `FiberOrdersController.cs`, `FiberShipmentsController.cs`, `HomeFinderController.cs`; `Portfolio.Web/appsettings.json` (new `ForwardedHeaders` section, empty).
- **Steps:**
  1. Rate limiter (`Program.cs:443-465`):
     - Add a policy `"compute"`: sliding window, D1 default 60/min/IP, 6 segments.
     - Change `GlobalLimiter` to `PartitionedRateLimiter.CreateChained(<existing per-IP 300/min>, <concurrency limiter>)`. The concurrency limiter keys requests whose endpoint metadata `EnableRateLimitingAttribute.PolicyName` is "compute" or "expensive" into one shared partition (`ctx.GetEndpoint()?.Metadata`, available because `UseRateLimiter` runs after `UseRouting`), with `PermitLimit = Math.Max(2, Environment.ProcessorCount)` and `QueueLimit = 16`. All other requests get `GetNoLimiter`.
     - Add a `"csp"` policy at 30/min/IP for WP7.
  2. Attributes: `[EnableRateLimiting("compute")]` on the seven compute controllers; `[EnableRateLimiting("expensive")]` on `FeaturesController`.
  3. Kestrel: `builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 1_000_000; o.AddServerHeader = false; });`. The existing larger `[RequestSizeLimit]` values on compute actions still apply per endpoint.
  4. Per-endpoint `[RequestSizeLimit]` values (D3 defaults):
     - Address parse/validate: 16,384.
     - Profile and user claims: 8,192.
     - Saved features POST: 2,000,000.
     - Collections: 4,096.
     - Fiber POST/PUT: 65,536.
     - HomeFinder POST: 16,384.
  5. HSTS: `builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = <D10>; o.Preload = false; });`.
  6. `Program.cs:686` and `:765`: drop `{connectionString}` from the messages.
  7. `Program.cs:503-505`: use `string.IsNullOrWhiteSpace` fallbacks, so `DATABASE_URL` works as documented. Keep `ConnectionStrings__DefaultConnection` precedence.
  8. ForwardedHeaders: the config-driven form in §4.4, with the default unchanged.
  9. `/health/ready`: remove `.DisableRateLimiting()` and add a 10 s cached result (a small `IHealthCheck` decorator or a static cache in `DatabaseReadyHealthCheck`). This depends on the owner confirming Render's health path (§13).
  10. `FeaturesController.cs:40-47`: validate `layerId` (`^\d{1,3}$`) and `bbox` (four comma-separated finite doubles), else 400.
- **Tests:**
  - From WP0: integration tests for 413 on oversized bodies (address 20 KB, claims 9 KB); the "compute" limit (61st request → 429); the concurrency queue (five parallel slow requests, none fail at a permit limit of 4 with queue 16).
  - `Server` header absent; HSTS value in a non-Development factory run.
  - Connection-string error text has no secret.
  - The `DATABASE_URL` fallback is used when `DefaultConnection` is `""`.
  - Health ready is cached.
  - `layerId` validation.
- **Acceptance:**
  - The WP0 reflection test turns green.
  - Every UI flow stays within limits. Verify with the browser-assertion protocol on Viewshed: 20 cell clicks in a minute all succeed.
- **Risk:**
  - Concurrency queueing adds latency under load.
  - The 1 MB Kestrel default could hit an endpoint I missed. Mitigation: the WP0 test enumerates POST/PUT actions without `[RequestSizeLimit]`.
- **Rollback:** revert the commit.
- **Dependencies:** WP0. **Must land before** the complexity agent splits `Program.cs` into `AddPortfolio*` extensions, and before the refactor agent dedups controller try/catch (§12).

### SEC-WP3: Exception middleware hygiene (S) [SEC-12, SEC-15]

- **Files:** `Portfolio.Web/Middleware/ApiExceptionMiddleware.cs`; tests `Portfolio.Tests/Middleware/ApiExceptionMiddlewareTests.cs`.
- **Steps:**
  1. Add `static bool IsOwn(Exception ex) => ex.TargetSite?.DeclaringType?.Assembly.GetName().Name?.StartsWith("Portfolio.", StringComparison.Ordinal) == true;`. Use `ex.Message` as `detail` only when `IsOwn(ex)`; otherwise use a fixed text per status (for example "The request was invalid.").
     - `throw new …` inside Portfolio code (including async state machines) counts as own.
     - Framework throw helpers (`Dictionary` indexer, serializer, `ArgumentNullException.ThrowIfNull`, `ThrowIfNullOrWhiteSpace`) are not own, so they get generic text. That is intended.
     - A null `TargetSite` counts as not own.
  2. Add an arm `BadHttpRequestException bad => (bad.StatusCode, "Bad Request", "The request was rejected.")` **before** the default arm, and log it at Information without a stack trace.
  3. If `context.Response.HasStarted`, log and `throw;` instead of writing ProblemDetails.
  4. Keep the ProblemDetails shape and status mapping for own exceptions unchanged.
- **Tests:**
  - Framework `KeyNotFoundException` → 404 with generic detail.
  - Service `ArgumentException` → 400 with the original message.
  - `BadHttpRequestException(413)` → 413.
  - Response already started → no second write.
- **Acceptance:** the WP0 characterization tests are updated deliberately (message text only).
- **Risk:** UI code showing `detail` sees generic text for framework errors. Compute controllers catch `ArgumentException` themselves, so the next bullet is optional: also route the controllers' `catch (ArgumentException ex) => BadRequest(new { error = ex.Message })` through the same `IsOwn` check (touches 15 controllers; do it with the refactor agent's controller dedup, §12).
- **Dependencies:** WP0.

### SEC-WP4: Identity separation and anonymous-identity throttling (M) [SEC-03, SEC-04, SEC-22 (claims), SEC-33, SEC-16 (identity logs)]

- **Files:** `Portfolio.Web/Middleware/AnonymousUserMiddleware.cs`; `Portfolio.Services/Services/UserProfileService.cs`; `Portfolio.Repositories/Interfaces/IUserProfileRepository.cs` and `Repositories/UserProfileRepository.cs` (add `HasClaimAsync(Guid, string)`; the log line at `:90`); optionally `Areas/Account/Pages/Profile.cshtml` (D11); tests `UserProfileServiceTests.cs` and a new `AnonymousUserMiddlewareTests.cs` (EF InMemory provider; see the §12 note on that package).
- **Steps:**
  1. Middleware cookie path (`:75-99`): if the profile exists **and** `HasClaimAsync(userId, google_id)` is true while the request is unauthenticated, do not set `Items`, delete the cookie, and proceed anonymously. If the profile doesn't exist, keep the current behaviour (create the row).
  2. Middleware new-identity path (`:100-133`):
     - Remove `SeedForUserAsync` (`:120`).
     - Before creating a profile, acquire from a static `PartitionedRateLimiter` keyed by `RemoteIpAddress`, 10 new identities/min. If denied, continue without an identity (the page still renders).
     - Skip identity creation for `/api/*` paths (SEC-33). This changes when the anonymous cookie is first issued (API navigations no longer issue it), so it is internal behaviour only.
  3. `CreateOrUpdateFromGoogleAsync` (`UserProfileService.cs:169-201`):
     - Adopt the anonymous profile only if `!HasClaimAsync(anonId, google_id)`; otherwise create a new profile.
     - Call `_seedService.SeedForUserAsync(userId, ct)` in the adoption branch too. It is idempotent: every insert is guarded by `AnyAsync`.
  4. `SetClaimAsync`: value ≤ 1,000 characters, at most 50 claims per user (count via the repository), type ≤ 200 (it already maps to 200).
  5. Logs: replace raw `{UserId}` with a short hash (`Convert.ToHexString(SHA256(userId))[..8]`) in `AnonymousUserMiddleware.cs:134-137` and `UserProfileRepository.cs:90`.
- **Tests:**
  - Anonymous cookie for a linked profile → no identity.
  - Sign-in with a cookie pointing to a linked profile → new profile, victim's `google_id` unchanged.
  - Sign-in adopting an unlinked anonymous profile → seeded and linked.
  - The 11th new identity in a minute from one IP gets no cookie.
  - `/api` requests create no profile.
  - Claim caps.
  - **Characterization first:** the `Properties` inserted by `UserProfileSeedService.cs:1328-1436` must already exist after migrations. Assert against `RedlandsPropertySeedData` that every street in the seed's property list is present.
- **Acceptance:** a fresh anonymous visit creates 1 profile row (not 76), and Google sign-in still shows seeded fiber data.
- **Risk:** existing anonymous profiles keep their old seeded rows (harmless). Removing the seed affects the anonymous HomeFinder only if `Properties` were never migrated; the characterization test above guards that.
- **Rollback:** revert.
- **Dependencies:** WP0. It does not conflict with WP6 (WP6 avoids `UserProfileService` and `UserProfileRepository`).

### SEC-WP5: Client-side XSS fixes and the shared helper (L) [SEC-05, SEC-06 (client), SEC-07; CSP blockers]

- **Files:**
  - New `Portfolio.Web/wwwroot/js/dom-safe.js`.
  - `Pages/Shared/_Layout.cshtml` (one `<script src="~/js/dom-safe.js" asp-append-version="true">` after `:177`).
  - `wwwroot/js/BatchGeocoding/app.js`, `PlantOperationsDashboard/{dashboard,inventory,orders,shipments}.js`, `Network/app.js`, `HomeFinder/app.js`, `ReverseGeocoding/app.js`, `StateExplorer/uiManager.js`.
  - The 8 files that own local `escapeHtml` copies (`AddressStandardization`, `CatRisk`, `Change`, `Fleet`, `Outage`, `Overlay`, `Response`, `ReverseGeocoding`).
- **Steps:**
  1. BatchGeocoding: `columns[1]` and `columns[3]` get `render: $.fn.dataTable.render.text()`.
  2. PlantOps:
     - Replace the 9 `onclick=` sites with `data-action="edit|delete|receive|status"` and `data-id` (plus `data-status` taken from `row.status` via `setAttribute`, never interpolated). Add one delegated `$(document).on('click','[data-action]',…)` per module.
     - Delete `showToast`, `createToastContainer` and `plantOpsToast` from `dashboard.js:6-57`; the global `toasts.js:62-66` already provides them.
     - Escape the popup fields at `dashboard.js:159-161`.
  3. `Network/app.js:169,180`: tooltip content via `PortfolioDom.el("strong",{text:…})`.
  4. HomeFinder:
     - Popup title and content (`:340-356`) built from escaped values.
     - `aria-label` (`:402`) via the 5-character escape.
     - `showToast` (`:668-690`) builds its DOM with `textContent`.
     - Replace the local `escapeHtml` (`:454-456`).
  5. `ReverseGeocoding/app.js:210-213`: escape the title and content.
  6. `uiManager.js:386`: build the badge with `el()` and set `style.backgroundColor` after the hex check.
  7. Replace every local `escapeHtml` with `PortfolioDom.escapeHtml`, and delete the copies.
- **Tests (no JS harness exists):** per-page DOM assertions with the A.7/A.8 headless-Edge method, or the desktop browser pane with `javascript_tool`. Screenshots hang on OSM tiles (`agent-context.md` §11).
  - Upload a CSV row `1,<img src=x onerror=…>,Redlands,CA,92373`: the text is shown and nothing runs.
  - PlantOps buttons still open the right modal and delete the right row.
  - `document.querySelectorAll('[onclick]').length === 0` on PlantOps after the tables render.
  - Tooltip and popup text renders correctly.
- **Acceptance:**
  - `grep -rnE 'on[a-z]+="' wwwroot/js` → 0.
  - `grep -rn 'function escapeHtml' wwwroot/js` → 0.
  - The headless CSP probe shows no `script-src-attr` reports.
- **Risk:** behaviour drift in PlantOps CRUD; mitigate with click-through verification of every button.
- **Dependencies:** none. **Must land before** the frontend agent's JS refactors (the 600–700-line JS files) or be merged into them (§12).

### SEC-WP6: Server-side validation for persistence and log hygiene (M) [SEC-06 (server), SEC-16, SEC-22]

- **Files:** `Portfolio.Services/Services/FiberShipmentService.cs`, `SavedFeatureService.cs`, `CollectionService.cs`, `SavedSearchService.cs`, `AddressStandardizationService.cs`; `Portfolio.Repositories/Repositories/CollectionRepository.cs` (`:54` log), `SavedFeatureRepository.cs` (`:62` log); tests in the matching `*ServiceTests.cs`.
- **Steps (imperative validation, per conventions in `agent-context.md` §9):**
  - Shipments: status ∈ {Pending, In Transit, Delivered, Exception}; strings ≤ 100 (tracking, carrier, city), state ≤ 50; `Route.Count ≤ 1,000`; all coordinates finite.
  - Saved features: LayerId and FeatureId ≤ 64, Name ≤ 200 (matches the column), Description ≤ 2,000, GeometryJson ≤ D3 (default 1 MB).
  - Collections: Color matches `^#[0-9a-fA-F]{6}$` (the default `#6c757d` already does).
  - Saved searches: Name ≤ 200 and `PropertyIds.Length ≤ 50` (join ≤ 500 characters), returning 400 instead of a DB exception.
  - Address: `rawAddress.Length ≤ 1,000`. Log only the score and length, not the address (`:123-125,175`).
  - Hash IDs in the remaining service and repository Warning logs (`SavedFeatureService.cs:101,111`, `CollectionService.cs:119`, repositories).
- **Tests:** a boundary and an over-limit case per rule.
- **Risk:** previously stored over-limit data still reads fine (validation is on write only).
- **Dependencies:** none. Does not touch `UserProfileService`.

### SEC-WP7: Security headers and CSP Report-Only (M) [SEC-08 phase 0, SEC-14, §2]

- **Files:**
  - `Portfolio.Web/Middleware/SecurityHeadersMiddleware.cs`.
  - New `Portfolio.Web/TagHelpers/CspNonceTagHelper.cs`.
  - `Portfolio.Web/Pages/_ViewImports.cshtml` and `Portfolio.Web/Areas/Account/Pages/_ViewImports.cshtml` (`@addTagHelper *, Portfolio.Web`).
  - New `Portfolio.Web/Controllers/CspReportController.cs` (route `/csp-report`, `[AllowAnonymous]`, `[EnableRateLimiting("csp")]`, `[RequestSizeLimit(8192)]`, `[IgnoreAntiforgeryToken]`, `[ApiExplorerSettings(IgnoreApi = true)]`).
  - `Pages/Index.cshtml` (delete the dead `:979-985` block).
  - `Pages/Projects/StateExplorer/Index.cshtml` (remove the typeahead `:10-11,218` per D18; the frontend or docs owner fixes `Details.cshtml:24`).
- **Steps:**
  1. Middleware:
     - Generate the nonce: `Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))`, stored in `HttpContext.Items["csp-nonce"]`.
     - Emit the existing enforced header unchanged, plus `Content-Security-Policy-Report-Only` (§2.4).
     - Emit `Reporting-Endpoints`, `Permissions-Policy: accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()` (no page uses geolocation: `grep navigator.geolocation` over `wwwroot/js` and `Pages` is empty; `clipboard-write` stays at its default), `Cross-Origin-Opener-Policy: same-origin` and `Cross-Origin-Resource-Policy: same-origin`.
     - For `/api` responses, register `Response.OnStarting` to add `Cache-Control: no-store` when no `Cache-Control` has been set (so `[ResponseCache]` actions keep theirs).
  2. Tag helper: `[HtmlTargetElement("script")]` and `[HtmlTargetElement("style")]`, setting `nonce` from `ViewContext.HttpContext.Items`.
  3. The report endpoint as described in §2.7.
- **Tests:**
  - Middleware unit tests: header values, nonce length and uniqueness per request.
  - Integration: the page HTML contains `nonce="<same value>"` on every `<script>`; `/csp-report` accepts both content types, rejects more than 8 KB with 413, and returns 204.
  - The ArcGIS eval filter.
- **Acceptance:**
  - Report-Only is live and the enforced header is unchanged.
  - The headless probe over all pages lists only the expected ArcGIS eval and the known JS inline-handler reports (fixed by WP5).
- **Risk:** report flooding; mitigated by rate limit, size cap and sampling.
- **Dependencies:** WP2 (for the "csp" policy), WP0.

### SEC-WP8: Enforce the CSP (S) [SEC-08 phase 2]

- **Files:** `SecurityHeadersMiddleware.cs`.
- **Steps:** after a 1–2 week soak with only filtered reports, emit the §2.4 policy as `Content-Security-Policy` (merging the old enforced directives) and keep reporting.
- **Tests:** an integration header test, and the headless probe on all pages with **enforcement** showing view-ready and no new reports.
- **Rollback:** switch back to Report-Only (one line).
- **Dependencies:** WP5, WP7, and owner sign-off.

### SEC-WP9: SRI for ArcGIS (S) [SEC-09]

- **Files:** `Pages/Projects/{HomeFinder,PlantOperationsDashboard,ReverseGeocoding,StateExplorer}/Index.cshtml` (the script tag and the two inline theme loaders).
- **Steps:**
  1. Change `src` to `https://js.arcgis.com/4.29/init.js` and add `integrity="sha384-<hash>" crossorigin="anonymous"`.
  2. In the loaders, set `link.crossOrigin = "anonymous"; link.integrity = dark ? H_DARK : H_LIGHT;` before `href`.
  3. Hashes: `curl -s <url> | openssl dgst -sha384 -binary | openssl base64 -A`. This needs the owner's OK to download.
- **Tests:** a headless probe of each page reaches view-ready; a deliberately wrong hash blocks the loader (manual check).
- **Risk:** fails closed if Esri republishes 4.29.
- **Dependencies:** WP7 (nonce on the inline loaders). Decision D5.

### SEC-WP10: Host filtering and forwarded-header values (S) [SEC-10, SEC-11]

- **Files:** `Portfolio.Web/appsettings.Production.json` (`AllowedHosts`; `ForwardedHeaders:KnownNetworks`/`ForwardLimit` once known).
- **Steps:**
  1. Run §4.5.
  2. Set `"AllowedHosts": "brianpcoffey.com;www.brianpcoffey.com;<service>.onrender.com"` (D8).
  3. Set `KnownNetworks` (D9).
- **Tests:** a factory test running with Production config, where `Host: evil.example` → 400.
- **Acceptance:** after deploy, `curl -I` on both hostnames returns 200 and the §4.5 checks behave as expected.
- **Risk:** a typo in the hostname takes that host down; verify immediately after deploy (Render auto-deploys `main`).
- **Dependencies:** WP2.

### SEC-WP11: Container and Kubernetes hardening (M) [SEC-25, SEC-26, SEC-27]

- **Files:** `Dockerfile`; `docker/docker-compose.yml`; `k8s/deployment.yaml`, `k8s/redis.yaml`, `k8s/namespace.yaml`; new `k8s/networkpolicy.yaml`; `k8s/secret.yaml` → `k8s/secret.example.yaml`; `.gitignore`; `k8s/README.md` (one line).
- **Steps:**
  1. Dockerfile:
     - Pin `sdk:10.0@sha256:…` and `aspnet:10.0@sha256:…`; resolve the current digests with `docker buildx imagetools inspect`.
     - `RUN mkdir -p /app/DataProtection-Keys && chown $APP_UID /app/DataProtection-Keys`.
     - `USER $APP_UID` before `ENTRYPOINT`.
  2. Compose: drop `ports` for Redis, or use `127.0.0.1:6379:6379`.
  3. k8s:
     - Pod and container `securityContext` (§7.2) and `automountServiceAccountToken: false`.
     - Image tag placeholder (`portfolio:<version>`).
     - Redis `--requirepass` from the Secret, with `Redis__ConnectionString` updated in the example.
     - NetworkPolicies.
     - PSA label.
- **Tests:**
  - CI `docker` job builds.
  - Local `docker run` → `curl /health/live` is 200; `docker exec … id -u` prints 1654.
  - `kubectl apply --dry-run=client -f k8s/` validates.
- **Risk:** a Render deploy failure if the user change breaks file access. Production uses Redis, so filesystem keys are unused (`Program.cs:516-525`). The `--migrate` pre-deploy runs as the same user.
- **Dependencies:** none (Dependabot docker updates come from WP12).

### SEC-WP12: CI supply chain (S) [SEC-28]

- **Files:** `.github/workflows/ci.yml`; new `.github/dependabot.yml`.
- **Steps:**
  1. Replace each `uses:` with `owner/repo@<sha> # vX.Y.Z` (§7.3 table).
  2. Add a `schedule:` trigger plus a `vulnerability-scan` job (restore + `dotnet list package --vulnerable --include-transitive`, failing if the output contains "has the following vulnerable packages").
  3. Add Dependabot for nuget, github-actions and docker, weekly, with grouped patch updates.
- **Tests:** the workflow runs green on the branch PR.
- **Risk:** none; Dependabot opens PRs (it does not merge them).
- **Dependencies:** none.

### SEC-WP13: Dependency updates and dead libraries (S) [SEC-29]

- **Files:** `Directory.Packages.props`; `wwwroot/lib/typeahead/*` and `wwwroot/lib/jquery-validation*` (only if D18 approves); `Pages/Shared/_ValidationScriptsPartial.cshtml`.
- **Steps:**
  1. Bump Microsoft.* 10.0.10 → 10.0.12 and Http.Resilience → 10.10.0. Leave Asp.Versioning and Scalar minor bumps to the owner. Keep OpenApi at 2.7.5.
  2. Delete the unused libraries per D18.
- **Tests:** the full suite and the docker build.
- **Dependencies:** WP0, which also edits `Directory.Packages.props`, so run them sequentially.

### SEC-WP14: Data Protection and session cleanup (S–M) [SEC-17, SEC-18, SEC-19]

- **Files:** `Program.cs` (`:264-272`, `:300-301` + `OnValidatePrincipal`, `:516-547`, `:582`); `Pages/Logout.cshtml.cs:14`.
- **Steps:**
  1. Remove the session middleware (`UseSession` is removed from the documented pipeline order, so update `agent-context.md` §4 and the `copilot-instructions` owners).
  2. Absolute lifetime: on sign-in, store an `auth_time` claim; in `OnValidatePrincipal`, reject the principal (`context.RejectPrincipal()`) when it is more than 24 h old (D14).
  3. `ProtectKeysWithCertificate` only if D13 says yes.
- **Tests:** logout still works; a 25 h-old principal is rejected (with `FakeTimeProvider`-style clock injection in the cookie event).
- **Dependencies:** WP2 (same file); land after it.

### SEC-WP15: Batch-geocoding job bounds (M) [SEC-24, SEC-16 (per-row logs)]

- **Files:** `Portfolio.Services/Services/BatchGeocodingService.cs`; `Portfolio.Services/InMemoryBatchJobStore.cs`; `Program.cs` (registration of a hosted queue worker; after WP2).
- **Steps:**
  1. Replace the fire-and-forget `Task.Run` (`:153`) with `Channel<BatchWorkItem>` capacity 8, consumed by a `BackgroundService` with 1–2 workers. When the channel is full, reject with 503 plus `Retry-After`, which is a new status on an existing route (owner-visible; D21).
  2. Move the final `UpdateAsync` inside `try`/`finally`.
  3. Add a 24 h TTL to the in-memory store (prune on write).
  4. Replace the per-row warning (`:237`) with one summary per job.
  5. Set `MaxRows` per D21.
- **Tests:** a queue-full path, TTL eviction, and that the summary log is emitted once.
- **Dependencies:** WP2. Overlaps the complexity agent if it touches this service.

### SEC-WP16: Native ABI hardening (L; optional) [SEC-30]

- **Files:** `native/{viewshed_kernel,raster_terrain_kernel,portfolio_scoring,geostream_processor,spatial_cluster_kernel}/…` (headers and sources); `Portfolio.Services/Native/*Interop.cs`, `*Bridge.cs` and `*Structs.cs` for those kernels; `Portfolio.Tests/Services/NativeParityTests.cs`.
- **Steps:**
  1. Add `elevationLength` parameters.
  2. Compute sizes as `static_cast<long long>(width) * height`.
  3. Clamp or validate before float→int casts.
  4. Return status codes from scoring.
  5. Dedup DBSCAN seeds.
- **Tests:** CI `native-parity` (builds all 13 kernels and runs with and without native).
- **Risk:** this is an ABI change, so all four layers change together (`cleanup-prompt.md` hard constraint).
- **Dependencies:** WP1. Decision D17.

### Ordering and parallelism

```
WP0 ──┬─> WP2 ──┬─> WP7 ──> WP9
      │         ├─> WP10 (after owner check §4.5)
      │         ├─> WP14
      │         └─> WP15
      ├─> WP3
      ├─> WP4
      └─> WP13
WP1, WP5, WP6, WP11, WP12: no dependency (start in parallel with WP0)
WP8 after WP5 + WP7 + soak;  WP16 optional, after WP1
```

These have disjoint files and can run in parallel: {WP1, WP4, WP5, WP6, WP11, WP12, WP3}. WP2 owns `Program.cs` and all controller files until it merges.

## 11. Decisions needed from the owner

| # | Decision | Recommended default |
|---|---|---|
| D1 | Interactive compute rate tier and global concurrency | "compute" = 60/min/IP sliding window for geostream, geometry, raster, network, clustering, viewshed, overlay; "expensive" stays 15/min. Global compute concurrency = max(2, CPU count), queue 16 |
| D2 | Input caps that narrow the public API (routes and JSON shape unchanged) | Viewshed and terrain sides ≤ 512; heatmap cells×points ≤ 25M; GeoStream grid ≥ 1e-6; CatRisk `InsuredValue` ≤ 1e12, event radius ≤ 5,000 km; Response `maxIterations` ≤ 100 |
| D3 | Request body limits | Kestrel default 1 MB; per-endpoint: address 16 KB, claims 8 KB, saved feature 2 MB (geometry ≤ 1 MB, **unverified real sizes**), collections 4 KB, fiber 64 KB, homefinder 16 KB |
| D4 | CSP approach and the new public `/csp-report` route | Nonce-based (§2.3); add `/csp-report` (anonymous, 8 KB, 30/min/IP, hidden from OpenAPI); Report-Only for 2 weeks, then enforce |
| D5 | SRI on ArcGIS `init.js` and themes (fails closed if Esri edits files) | Yes (4.29 unchanged since 2024-03-28) |
| D6 | Leaflet: keep unpkg with SRI, or self-host in `wwwroot/lib` | Keep unpkg (SRI verified), with the path-scoped CSP source |
| D7 | OpenAPI and Scalar public in production | Keep public (accepted, SEC-20) |
| D8 | Exact `AllowedHosts` list | `brianpcoffey.com;www.brianpcoffey.com;<render-service>.onrender.com` (owner supplies the Render hostname) |
| D9 | Forwarded-header trust after §4.5 | `KnownNetworks` = observed Render proxy CIDR; `ForwardLimit` 1 (or 2 if a Cloudflare hop is appended), never without `KnownNetworks` |
| D10 | HSTS `includeSubDomains`/preload | `max-age` 1 year, **no** `includeSubDomains` until all brianpcoffey.com subdomains (Squarespace DNS) are confirmed HTTPS-only; no preload |
| D11 | Profile page shows the internal User ID | Hide it (or show the first 8 characters) until WP4 lands |
| D12 | Retention purge of anonymous profiles | Yes, later: delete profiles without `google_id` inactive for more than 90 days (needs a hosted service or Render cron) |
| D13 | Encrypt the DP key ring at rest | Accept for now (document that Upstash credentials are as sensitive as the keys); revisit if Redis is shared |
| D14 | Session lifetime vs STIG V-222389 (15 min idle) | Keep 8 h sliding plus a 24 h absolute limit; record the STIG deviation as accepted for a public demo with no sensitive data |
| D15 | Committed localhost `Password=postgres` | Replace it with an empty value plus a README note (user-secrets or `appsettings.Development.json`) |
| D16 | Add the `Microsoft.AspNetCore.Mvc.Testing` test dependency | Yes (test-only) |
| D17 | Native ABI hardening (no native code ships) | Defer; document managed validation as the control |
| D18 | Remove the unused typeahead (and fix the `StateExplorer/Details.cshtml:24` claim) and the unused jquery-validation libraries | Yes, both (keeps site claims honest) |
| D19 | Require a custom header on unsafe `/api` methods (CSRF hardening) | No (accepted: SameSite plus JSON-only plus no CORS, all verified) |
| D20 | Enable CodeQL default setup and secret-scanning push protection (repo settings) | Yes |
| D21 | Batch geocoding: rows per job and queue-full behaviour | `MaxRows` 1,000 (samples are 10–12 rows); queue of 8 jobs, 2 workers; 503 plus `Retry-After` when full |

## 12. File overlaps with the other agents

| File(s) | Security packages | Other agents' planned work | Resolution |
|---|---|---|---|
| `Portfolio.Web/Program.cs` | WP2, WP14, WP15 (registration) | Complexity agent: split into `AddPortfolio*` extension methods and dedup the four resilience pipelines | **Security first.** WP2/WP14 land, then the split carries the new rate limiter, Kestrel and HSTS settings intact. The pipeline order changes only by removing `UseSession` (WP14), which must be documented |
| `Controllers/Api/*.cs` (17 files) | WP2 (attributes), optional WP3 step | Refactor agent: dedup try/catch; Comments agent: XML doc trimming | WP2 first; the refactor keeps `[EnableRateLimiting]`/`[RequestSizeLimit]` (the WP0 reflection test enforces it); comments go last |
| `Middleware/ApiExceptionMiddleware.cs` | WP3 | Refactor (if controllers start relying on middleware mapping) | WP3 first |
| `wwwroot/js/**` (13 files plus 8 helper owners) | WP5 | Frontend agent: shared modules, toast/inline-error helper, JS file splitting | WP5 first, **or** fold `dom-safe.js` into the frontend agent's shared module in the same PR; their toast unification must keep the `textContent` behaviour of `toasts.js` |
| `Pages/Shared/_Layout.cshtml`, `Pages/_ViewImports.cshtml`, `Areas/Account/Pages/_ViewImports.cshtml`, `Pages/Index.cshtml`, project `Index.cshtml` pages | WP5 (one script tag), WP7, WP9 | Frontend agent: layout and design-system work; duplicated carousel markup in `Index.cshtml` | Small, line-local edits; sequence after or alongside the frontend WPs |
| `Portfolio.Services/Services/{Viewshed,RasterTerrain,SpatialCluster,GeoStreamProcessor,SpatialGraph,CatRisk,ReverseGeocoding}Service.cs` | WP1 | Complexity agent: `SpatialGraphService` and others | WP1 changes only validation and two indexers; land first |
| `UserProfileService.cs`, `AnonymousUserMiddleware.cs`, `UserProfileRepository.cs` | WP4 | Complexity (`UserProfileSeedService` split) | WP4 does not edit the seed service, only its call sites |
| `Portfolio.Repositories/Portfolio.Repositories.csproj` (`Microsoft.EntityFrameworkCore.InMemory`) | WP4 tests want the InMemory provider | Refactor agent proposes removing it from the production project | Move the reference to `Portfolio.Tests.csproj` rather than deleting it outright |
| `Directory.Packages.props` | WP0, WP13 | Refactor (unused packages) | Sequential |
| `.github/workflows/ci.yml` | WP12 | CI-quality goal (analyzer and format checks) | One PR, or WP12 first |
| `Dockerfile`, `docker/`, `k8s/` | WP11 | Docs agent (port note in §7.1) | WP11 first |

## 13. Not verified, and why

1. **Render's XFF hop layout** (SEC-10). It needs a request to production with spoofed headers (which consumes the shared rate-limit budget), so it is left to the owner (§4.5).
2. **Google profile-picture host** (`*.googleusercontent.com`) under the CSP. Signing in would need real credentials.
3. **`/scalar` under the target CSP.** Static inspection found no CDN URL; it was not loaded under the policy.
4. **Visual tile rendering under the enforced CSP.** Only `view.when()` resolution, the basemap sweep and violation reports were checked; no screenshots.
5. **Real `GeometryJson` sizes for saved states** (D3 cap). That needs ArcGIS calls, which I did not make from the app.
6. **Render's configured health-check path** (SEC-32). It lives in the Render dashboard.
7. **ArcGIS `init.js` and theme SRI hashes.** They require downloading the files.
8. **Behaviour with the database present** (cookie flags on the issued `AnonUserId`, seed row counts at runtime). The live run used an unreachable DB by design; the counts are static (77 `.Add(` calls).
9. **Exhaustive secret scan of history.** I ran a pattern scan, not gitleaks/trufflehog.
10. **CVE coverage for front-end libraries** relies on web advisory databases (Appendix B), not an SCA tool. There is no npm in the repo.
11. **Production CPU costs.** All timings are from an i5-13600K; a Render instance is much smaller (512 MB per the repo doc; CPU share not verified).

## Appendix A: Commands run and raw results

Scratch root: `C:\Users\brian\AppData\Local\Temp\claude\C--Users-brian-source-repos-Portfolio\6e74e331-4143-4f68-9411-0e96a263f57d\scratchpad\sec` (not in the repo).

**A.1 Package audit** (clone at `sec/repo`; `dotnet restore Portfolio.sln`, then):
- `dotnet list Portfolio.sln package --vulnerable --include-transitive` → "has no vulnerable packages" for all 6 projects.
- `dotnet list Portfolio.sln package --outdated` → the versions in §8.
- `dotnet list Portfolio.sln package --deprecated --include-transitive` → `Microsoft.Testing.Platform.MSBuild 1.9.1 (Legacy)` in Portfolio.Tests.

**A.2 Clone and build.** `git clone C:\Users\brian\source\repos\Portfolio sec\repo`; `dotnet build Portfolio.Web/Portfolio.Web.csproj -c Release` → Build succeeded.

**A.3 Local app run** (clone; `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://127.0.0.1:5099`, `RunMigrationsOnStartup=false`, `ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=1;…`, dummy Google values, `PORTFOLIO_DISABLE_NATIVE=1`; `dotnet bin/Release/net10.0/Portfolio.Web.dll`). Startup log includes `No XML encryptor configured. Key … may be persisted to storage in unencrypted form.` The process was stopped afterwards.

**A.4 Harness** (`sec/harness`, console app referencing the clone's `Portfolio.Services`; each scenario in its own process). Output format is `elapsed_ms / allocated_MB / peak_ws_MB`:
```
dbscan-random 5000:      45 / 6.2 / 35
dbscan-identical 5000:   314 / 313.2 / 267      body~70038 B
viewshed-square 500:     505 / 0.5 / 32
viewshed-strip 20000:    1006 / 0.3 / 34
viewshed-strip 50000:    7873 / 0.6 / 34
viewshed-strip 100000:   36990 / 1.0 / 35       body~200102 B
viewshed-strip 250000:   237348 / 2.1 / 38      body~500102 B
heatmap 100:             232 / 5.9 / 38
heatmap 1000:            1979 / 6.0 / 38
heatmap 10000:           29224 / 8.4 / 40       body~595549 B
overlay (10k pts x 20k vertices): 483 / 5.7 / 39   body~1152488 B
address 1 MB:            203 / 59.5 / 100
address 25 MB:           2392 / 1421.5 / 1080
change (512x512x8 random, JSON 80,822,210 B > 16 MB cap): 69 / 38.0 / 387
change-json (minimal JSON 8,388,712 B):  333 / 102.2 / 149
catrisk-sim 5000x12000:  2764 / 7.3 / 42
response mode 0:         238 / 24.0 / 114     (2 iterations)
response mode 1 (p90):   4444 / 27.9 / 113    (10 iterations)
fleet 120 stops/20 veh:  829 / 303.4 / 114    (solveMs 283, matrixMs 184)
json: {"epsilon":NaN} -> JsonException | "NaN" -> OK NaN | "Infinity" -> OK | 1e400 -> OK Infinity | "1e400" -> JsonException | 1e308 -> OK
      query double 'NaN' -> NaN | 'Infinity' -> Infinity | '-Infinity' -> -Infinity | '1e400' -> Infinity
      serialize +Inf -> ArgumentException
graph-missing-node: System.Collections.Generic.KeyNotFoundException: The given key '99999' was not present in the dictionary.
geostream-tinygrid (1e-300): cells=(2147483647,2147483647) center=(-90,-180)
config: GetConnectionString -> "" (length 0; '?? DATABASE_URL' NOT reached)
uri: layerId='..' -> https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/query?where=1=1&f=json&outFields=*
     layerId='../..' -> …/MapServer/..%2F../query…   layerId='%2e%2e' -> …/MapServer/%252e%252e/query…
```

**A.5 Static greps** (repo, read-only):
- Controller attributes: `grep -nE '^\s*\[(Authorize|AllowAnonymous|EnableRateLimiting|RequestSizeLimit|Route|Http…)' Controllers/Api/*.cs`.
- Seed inserts: `grep -o -E '_db\.\w+\.Add\(' UserProfileSeedService.cs | sort | uniq -c` → FiberClients 12, FiberInventoryTransactions 23, FiberMaterials 12, FiberOrders 17, FiberShipments 11, Properties 2.
- `grep -rn 'HttpContext.Session'` → only `Logout.cshtml.cs:14`.
- `grep -rn 'BackgroundService|IHostedService|Purge|ExecuteDelete'` → none.
- `grep -rnE 'FromSql|ExecuteSql|SqlQuery|NpgsqlCommand|CommandText'` (Web, Services, Repositories, excluding migrations) → none.
- `grep -rn 'navigator.geolocation'` (`wwwroot/js`, `Pages`) → none.
- Sink counts: `grep -rnE '<sink patterns>' --include=*.js wwwroot/js` → 149 lines in 21 files; `innerHTML\s*=` → 66 lines in 16 files; helper reads → 8.
- CSP greps (§2).

**A.6 Live probes against A.3** (`curl`):
- Rate limit and XFF cases (a)–(d): see §4.2.
- `POST /api/v1/network/route` with an edge to node 99999 → `{"title":"Not Found","status":404,"detail":"The given key '99999' was not present in the dictionary."}`.
- `POST /api/v1/clustering/dbscan` with `"epsilon":"NaN"` or `1e400` → 400 `Epsilon must be a finite value greater than zero.` (proves non-finite values reach the service).
- `POST /api/v1/geostream/events` with speed `"NaN"` → 400 with `.NET number values such as positive and negative infinity cannot be written as valid JSON…`; same for heatmap `minX:"NaN"`.
- `GET /api/v1/features/saved` (no identity) → 409 `No current user identity is available.`
- `GET /` with `Host: evil.example` → `<link rel="canonical" href="http://evil.example/" />`, `og:url`, `og:image` reflected.
- Response headers on `/`: `Server: Kestrel`, nosniff, DENY, referrer policy, minimal CSP.
- `OPTIONS` preflight from a foreign origin → 405, no `Access-Control-*`.
- `/openapi/v1.json` → 200, 325,283 B, 56 `/api/…` paths; `/scalar` → 302.
- 25 MB `POST /api/v1/addresses/parse` → 200, 3.47 s; server PID working set 152 → 1,346 MB (peak 1,346).
- 31 MB → 500; log: `Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException: Request body too large. The max request body size is 30000000 bytes.`
- 20 viewshed POSTs from one XFF → 20 × 200; a 90,113 B W=30,000 viewshed → 200 in 3.13 s.
- `text/plain` or form-encoded to a JSON action → 415. `POST /Logout` without a token → 400.
- `GET /Login?handler=Google&ReturnUrl=//evil.example` → 302 to `accounts.google.com` with `code_challenge_method=S256` (client id and state redacted).

**A.7 DataTables probe** (`sec/dtprobe`; vendored `jquery-3.7.1.min.js` and `jquery.dataTables.min.js` copied from the clone; served on `127.0.0.1:5098`; `msedge --headless=new --dump-dom --virtual-time-budget=5000`):
- After load: `<body data-probe="ran" data-xss="fired">`.
- Table without render: `<td><img id="probeImg" src="x" onerror="…">`.
- `render.text()` table: `<td>&lt;img id="probeImg" …`.

**A.8 CSP and CDN probes** (`sec/cspprobe`; `tools/serve.py` (Report-Only) and `tools/serve_enforce.py` (enforced) on 5097/5096; headless Edge with `--use-angle=swiftshader --enable-unsafe-swiftshader --virtual-time-budget=20000/40000`). Results are in §2.6. CDN headers:
- `curl -I -L https://js.arcgis.com/4.29/` → `301 Location: /4.29/init.js` → `200 application/javascript, Content-Length 1477706, ACAO *, Cache-Control max-age=86400,s-maxage=31557600, Last-Modified Thu, 28 Mar 2024 05:54:03 GMT`.
- unpkg Leaflet → `ACAO *`, `max-age=31536000`.
- jsDelivr metadata `data.jsdelivr.com/v1/packages/npm/leaflet@1.9.4?structure=flat`: `/dist/leaflet.js` hash `20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=` (147,552 B), `/dist/leaflet.css` hash `p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=` (14,806 B).
- All probe servers were stopped afterwards.

**A.9 Production (passive; 2026-10-12 18:53 UTC; four requests, `Accept: */*`)**:
- `HEAD https://brianpcoffey.com/` → 200; `content-security-policy: frame-ancestors 'none'; base-uri 'self'; object-src 'none'`, `strict-transport-security: max-age=2592000`, `x-frame-options: DENY`, `x-content-type-options: nosniff`, `referrer-policy: strict-origin-when-cross-origin`, `Server: cloudflare`, `x-render-origin-server: Kestrel`, `rndr-id`, `cf-cache-status: DYNAMIC`, `CF-RAY`.
- `HEAD http://brianpcoffey.com/` → 301 to `https://`.
- `HEAD /css/base.css` → same security headers, ETag and Last-Modified, no Cache-Control.
- `GET` with `Host: evil.example` → **403** (Cloudflare).

**A.10 Supply chain.**
- `git ls-remote https://github.com/<action> refs/tags/<tag>` gave the SHAs in §7.3; the latest majors came from `--tags`.
- `git ls-remote https://github.com/brianpcoffey/gis-portfolio HEAD` → `9d9ea02…`, anonymous, so the **repo is public**.

**A.11 Vendored library versions** (file headers): jQuery v3.7.1, DataTables 1.13.7, Bootstrap v5.3.3, D3 v7.9.0, typeahead 1.3.1, jquery-validation v1.21.0, jquery-validation-unobtrusive v4.0.0, Font Awesome Free 6.7.2. `new Function` occurs once in `lib/d3/d3.min.js` and 0 times in jQuery, DataTables and Bootstrap.

**A.12 History scan.**
- `git log --all -p` with a masked awk filter for Google client secret/ID, `rediss://…:pw@`, Neon `postgres://…:pw@…neon`, `npg_…` → 5 matches, all placeholders in commit `ef50b62` (`.env.example`, `docs/render-deployment.md`).
- `git show 74a9af1:Portfolio/appsettings.Development.json` (masked shape) → only `DetailedErrors` and `Logging`.

**Measurement machine:** i5-13600K (14 cores / 20 threads), 31.7 GB RAM, .NET SDK 10.0.401, Microsoft.AspNetCore.App 10.0.12.

## Appendix B: External sources consulted (read-only)

- Render X-Forwarded-For: feature request "Send the correct X_FORWARDED_FOR" (feedback.render.com; 2021 staff reply and 2024 user report); community thread "Accessing client IPs in a Node Express App" (community.render.com/t/…/36282; the moved domain could not be fetched); GitHub issue C-Azzone416/tabletop-sim#301 (hop count unresolved).
- ArcGIS Maps SDK for JavaScript FAQ (developers.arcgis.com/javascript/latest/faq/): `wasm-unsafe-eval`, `style-src-attr 'unsafe-inline'`, `worker-src blob:`. Esri bug BUG-000148351 and Esri Community threads (popup HTML sanitizer since 4.14).
- Public Suffix List (publicsuffix.org/list/public_suffix_list.dat): `onrender.com` entry under "Render", submitted by Anurag Goel.
- Leaflet CVE-2025-69993 (osv.dev/vulnerability/CVE-2025-69993): `bindPopup` XSS, ≤ 1.9.4, CVSS 6.1, published 2026-04-14.
- Bootstrap CVE-2024-6484 and CVE-2024-6531 scope; jquery-validation CVE-2025-3573 (fixed in 1.20.0); DataTables CVE history; Snyk `arcgis-js-api` 4.29.10.
- .NET 10.0.11 and 10.0.12 security releases (Fedora advisories listing CVE-2026-62871… and CVE-2026-58649…).
- DISA ASD STIG V6R4 rule list (stigviewer.com/stig/application_security_and_development/).
