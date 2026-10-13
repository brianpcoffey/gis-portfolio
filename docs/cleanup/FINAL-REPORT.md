# Portfolio cleanup — final report

Date: 2026-10-13 (UTC). Plan: `docs/cleanup/PLAN.md` (approved 2026-10-12, about 15 PRs). Repository: `brianpcoffey/gis-portfolio`. Live site: brianpcoffey.com (Render, auto-deploys `main`).

## Outcome

All 16 planned PRs are merged and live, plus a CI repair (#50) and your wave-1 integration PR (#39). Production was verified after every deploy using read-only checks (pages, headers, a demo run, the API shape).

The last merge was #61 (`main` = `dc403e5`, 2026-10-13 06:41 UTC). Every CI job passed on `main`: build/test/coverage with the new format and coverage gates, native parity, Docker, and the Render deploy. Production checks afterwards were clean.

**50 of 53 work packages are done.** WP-51 and WP-52 wait on you (see "Your decisions and actions"). WP-53 was deferred by default. Behaviour stayed the same except for the changes you approved in §4 of the plan, listed below.

## Before → after

| Metric | Before (2026-10-12) | After |
|---|---|---|
| Tests | 697 | **1,416** (1,404 + 12 native-gated skips in CI; 1,416 / 1,416 with the kernels) |
| Coverage, line / branch | 83.64% / 67.36% | **90.38% / 79.39%** |
| CI coverage floors | 80 / 60 | **89 / 78** overall, **83 / 66** for Portfolio.Web (ratchets) |
| Production members over CA1502 15 | 17 | **0**, gated in CI |
| Production members over 15 on cyclomatic or cognitive | 35 | **9**, all cognitive-only; 3 are seeded data generators |
| Worst member | `SeedForUserAsync`: 1,444 lines, CA1502 251, cognitive 236 | `RedlandsPropertySeedData.Build`: cognitive 35, CA1502 15 |
| `Program.cs` | 813 lines (`<Main>$` CA1502 61) | 39 lines; startup code in `Portfolio.Web/Startup/*` |
| `UserProfileSeedService.cs` | 1,464 lines | 165 lines (data in `Data/FiberDemoSeedData.cs`) |
| `dotnet format --verify-no-changes` | fails on 118 files | clean, gated in CI (incl. unused usings) |
| Build warnings | 0 | 0 (warnings-as-errors, now incl. CA1502) |
| Hard-coded colour literals (frontend) | 387 | 96 |
| HTML comments shipped to browsers | 277 per crawl | 0 |
| Dead vendor files in the image | ~10.5 MB | removed |
| `.github/copilot-instructions.md` | 1,152 lines | 71 (invariants + pointer to `docs/agent-context.md`) |
| Code diff, excluding docs and vendored libs | — | 434 files, +27,318 / −17,319 (tests: +11,881 / −2,296) |

Native performance claims stay honest: 0.37×–2.26×, 9 wins / 5 losses / 7 ties at ±15%, and no SIMD or "high-performance" wording. The scoring kernel's comments now say it is no faster than managed (0.99×).

## What changed, PR by PR

| PR | Wave | Packages | What changed |
|---|---|---|---|
| [#34](https://github.com/brianpcoffey/gis-portfolio/pull/34) | 0 | WP-00 | Plan, findings and agent orientation committed |
| [#35](https://github.com/brianpcoffey/gis-portfolio/pull/35) | 1 | 01–03 | Host-level test factory, characterization unit tests, golden tests for the demos |
| [#36](https://github.com/brianpcoffey/gis-portfolio/pull/36) | 1 | 04–08 | Sanitized error bodies; connection-string handling; compute input caps; client-side XSS fixes (`dom-safe.js`); persistence validation and log redaction |
| [#37](https://github.com/brianpcoffey/gis-portfolio/pull/37) | 1 | 09, 10, 13 | Container and k8s hardening; CI supply chain (pinned SHAs, dependency review, native-run honesty); measurement tools under `tools/` |
| [#38](https://github.com/brianpcoffey/gis-portfolio/pull/38) | 1 | 11, 12 | Honest benchmark and site claims; core CSS tokens, focus ring and input borders |
| [#39](https://github.com/brianpcoffey/gis-portfolio/pull/39) | 1 | — | Wave-1 integration (merged by you) |
| [#50](https://github.com/brianpcoffey/gis-portfolio/pull/50) | — | — | Restored green CI after two Dependabot bumps broke `main` (xUnit 4 / MTP; analyzer rules) |
| [#51](https://github.com/brianpcoffey/gis-portfolio/pull/51) | 2 | 14, 15, 19 | Per-IP rate limiting (`compute` / `expensive` / `csp`) plus a shared CPU limiter, body limits; identity separation; **fixed a latent Google sign-in failure** (nested transaction in seeding); dependency patches |
| [#52](https://github.com/brianpcoffey/gis-portfolio/pull/52) | 2 | 16–18 | Shared JS modules (`PortfolioDom` / `Ui` / `Format` / `Charts`), home page, GIS stylesheet consolidation |
| [#53](https://github.com/brianpcoffey/gis-portfolio/pull/53) | 3 | 20, 21, 23 | CSP Report-Only with per-request nonce and `/csp-report`, COOP/CORP/Permissions-Policy; `Program.cs` → `Startup/*`; OpenAPI request-body docs |
| [#54](https://github.com/brianpcoffey/gis-portfolio/pull/54) | 3 | 22, 24 | Dead-code batch; `defer` on all scripts and per-page asset loading; `_ProjectHero` partial |
| [#55](https://github.com/brianpcoffey/gis-portfolio/pull/55) | 4 | 25, 29–31 | Data-driven seed (identical rows); shared Dijkstra/A* search; `NativeKernelLoader` (−404 lines); address parser decomposition |
| [#56](https://github.com/brianpcoffey/gis-portfolio/pull/56) | 4 | 26–28, 32, 33 | Fleet / Response / ChangeDetection solver splits; every remaining member over the complexity threshold refactored |
| [#57](https://github.com/brianpcoffey/gis-portfolio/pull/57) | 4 | 34–37 | Fiber error mapping (404/409/400); 24 h absolute auth lifetime; batch geocoding queue and worker (503 + `Retry-After`, 1,000-row cap); static asset caching |
| [#58](https://github.com/brianpcoffey/gis-portfolio/pull/58) | 4 | 38–42 | All 18 demo pages and the site pages: shared modules, accessibility (announcements, focus, names, contrast), number formats, State Explorer anonymous mode, theme-independent map colours; colour literals 353 → 96 |
| [#59](https://github.com/brianpcoffey/gis-portfolio/pull/59) | 5–6 | 43–47 | Controller exception filters (33 try/catch blocks removed); JS script splits; dead vendor files; ArcGIS SRI; one test class per SUT |
| [#60](https://github.com/brianpcoffey/gis-portfolio/pull/60) | 7 | 48, 49 | Comment and XML-doc trim (269 files; many wrong comments fixed); documentation brought to the current code |
| [#61](https://github.com/brianpcoffey/gis-portfolio/pull/61) | 8 | 50 | CA1502 gate, `dotnet format` pass and CI gate, coverage ratchets plus a Web floor |

Each PR body lists that wave's checks, the independent review's findings and how each was resolved.

## Deliberate behaviour changes (all within the approved decisions)

- **Errors (D-04).**
  - Fiber endpoints return 404 / 409 / 400 instead of 500.
  - A framework- or BCL-originated `ArgumentException` returns the generic `{"error":"The request was invalid."}`.
  - Oversized bodies return 413.
- **Limits (D-02, D-10).**
  - Per-IP rate limits: compute 60/min, expensive 15/min, csp 30/min.
  - CPU concurrency limiter.
  - Batch geocoding: 1,000 rows, queue of 8, 503 with `Retry-After`.
- **Identity (D-05, D-11).** Anonymous and Google identities are strictly separate. 24 h absolute auth lifetime, 8 h sliding window kept.
- **Frontend (D-09).**
  - Focus ring and input borders.
  - Number formats: 1 decimal; compact money; Yes/No.
  - State Explorer keeps anonymous visitors on the page with a sign-in hint.
  - Live-region announcements and focus kept on the button.
  - Map marks keep their light-theme colours in dark mode, because the basemaps stay light.
- **Security headers (D-03).** CSP Report-Only plus `/csp-report`; COOP/CORP; Permissions-Policy. The enforced CSP is unchanged until WP-51.
- **ArcGIS SRI (D-15).** The ArcGIS 4.29 `init.js` and theme CSS are pinned. If Esri changes those bytes, the map stops loading (fail-closed).
- **Static caching.** Versioned assets are `immutable` for 1 year; others cache for 1 h.
- **Copy (D-08).** Honest benchmark and site claims; no HTML comments are sent to browsers.

## Your decisions and actions

1. **WP-51: enforce the CSP.** The Report-Only soak started 2026-10-13 ~01:58 UTC. After 1–2 weeks, check the `/csp-report` warnings in the Render logs (the known ArcGIS `eval` reports are filtered out). Watch HomeFinder (inline style attributes, allowed by `style-src-attr`), PlantOps, `/scalar` and State Explorer. If those are clean, say "enforce the CSP" and WP-51 is a small PR; rollback is one line.
2. **WP-52: host filtering and forwarded headers (D-12).** This needs your 5-minute production check (findings-security §4.5) and the Render hostnames. Until then `AllowedHosts` stays `*` and forwarded headers are trusted for one hop only.
3. **WP-53: native ABI hardening.** Deferred by default (large). Say if you want it.
4. **D-20: CodeQL default setup and secret-scanning push protection.** These are repository settings for you to toggle.
5. **Legacy route decision (D-06).** The unversioned `POST /api/batchgeocoding/sync` alias has returned **404 since 2026-05**: API versioning requires a URL version, so nobody can have used it for five months. The demo uses `/api/v1/geocoding/batch/sync`. Options:
   - (a) **delete the alias** — recommended;
   - (b) mark it `[ApiVersionNeutral]` to revive it;
   - (c) leave it.

   The two `[Obsolete]` HomeFinder aliases (`POST homefinder/score`, `GET homefinder/properties/{id}`) still work but are excluded from coverage. Same choice.
6. **Your local checkout.** `C:\Users\brian\source\repos\Portfolio` is still on the pre-cleanup commit, with untracked copies of `docs/agent-context.md`, `docs/cleanup-prompt.md` and `docs/cleanup/`. `main` now tracks those paths, so move or delete the untracked copies (keep this report somewhere) before `git pull`.
7. **Render.** The repo can't confirm the dashboard settings: auto-deploy off (CI deploys `main` via the hook) and the pre-deploy `--migrate` command. `docs/render-deployment.md` describes the intended setup.

## Follow-ups found but not changed (behaviour or out of scope)

1. **PlantOps orders.** `FiberOrderService` never generates `OrderNumber`, so new and seeded orders show a blank "Order #". The code now carries a "known gap" note. An unused `var now` sits beside it.
2. **HomeFinder scoring doc.** `IHomeScoringService.GetPropertyByIdAsync` is documented as returning a scored property but returns it unscored (rank 0).
3. **ArcGIS load failures.** If the API fails or is blocked by SRI, `PortfolioEsri.load()` rejects with no message to the user. HomeFinder and State Explorer await map init before wiring their other buttons, so the whole page goes inert, not just the map.
4. **State Explorer console noise.** Anonymous visitors log an expected 401 in the console; the page could skip the collections request when the server already knows the visitor is anonymous.
5. **Light-mode contrast.** `--status-success-text` on its soft tint is about 4.46:1 for `.tier-high` and `.profile-alert`, just under 4.5.
6. **Clustering palette.** The 8-colour palette repeats from cluster 9 on.
7. **HomeFinder saved searches.** Naming a saved search still uses `prompt()`.
8. **PlantOps accessibility.** The chart-summary live regions are never filled, and a dashboard load error can be announced up to 4 times.
9. **Dead code to delete.** `Pages/Shared/_Layout.cshtml.css` (an unreferenced scaffold) and the `.login-card` CSS rule.
10. **Misleading test names.** Three tests named `…ReachesEsriHq…FromRowA` actually route to node 105.
11. **Undocumented kernel headers.** Three native headers have no ABI comments: geostream, raster terrain, spatial geometry.
12. **Keep `docs/cleanup/` in the repo.** Some code comments cite sections of `docs/cleanup/findings-security.md`.

## How the work was done

- **Planning and implementation.** Opus wrote and reviewed the plan; Sonnet agents implemented one work package each, in isolated worktrees on `cleanup/<area>` branches.
- **Review.** Each group was reviewed by an independent Opus agent before its PR, and every should-fix finding was applied.
- **Gates.** Every group ran:
  - a CI-mode build with 0 warnings;
  - the tests with coverage and the coverage gate;
  - a run with `PORTFOLIO_DISABLE_NATIVE=1`;
  - a run with all 13 kernels and `PORTFOLIO_REQUIRE_NATIVE=1`;
  - a secret scan;
  - browser checks using DOM assertions against a pre-change build. Auth pages used a scratch-only local test sign-in (D-16), never committed; geocoding pages used stubbed requests, so no ArcGIS geocoding calls were made.
- **Merging.** PRs were merged after green CI under your standing instruction, with read-only production checks after each deploy.
