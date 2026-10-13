# Findings: Frontend (Goal 5): consistency, responsiveness, UI/UX, accessibility, performance

Stage 1, read-only analysis. Repo state: `main` @ `9d9ea02`. Author: frontend analysis agent.
Scope: the 18 demo pages and their Details pages, plus home, `_Layout`, `/Projects`, Login, Privacy,
Accessibility, Account/Profile, Error, AccessDenied, the duplicated carousel markup, and JS/CSS duplication.
Out of scope (owned elsewhere): XSS/`innerHTML` safety and CSP design (security agent), C# complexity,
comments/docs. Section 9 lists the places where my changes touch the same lines.

Evidence labels: **(live)** means measured in the running app with a DOM/geometry assertion. **(static)** means
read from source (`file:line`) or computed from CSS values with a script.

---

## 0. Summary for the planner

**Live audit ran.** I used the clone at `9d9ea02` on `http://localhost:5080` (Development) in the built-in browser
pane. I covered 42 URLs (7 site pages, 17 demo pages, 18 Details pages) at 360, 768 and 1280 px, each in light
and dark, with the default compute action run on 12 demos at 360 and 1280. I also did a keyboard pass (real
`Tab`/`Enter`/`Space` key events) on Home, Clustering, Network and Response. The audit took no screenshots.

Headline numbers:

| Metric | Result |
|---|---|
| Horizontal page overflow | **0 failures** on every audited page, width and theme. Wide content is already contained. |
| Hard-coded colour literals outside the `base.css` token blocks | **387 in 29 files**: 277 in CSS, 110 in JS, 0 in `.cshtml`. 35 of them are just `var()` fallbacks. Three competing status palettes are in use: Bootstrap ×28, Chakra/Open Color ×53, and accent-alpha `rgba(56,161,105,x)` ×29. |
| Focus visibility (2.4.7) | **Broken site-wide** on `.btn-outline-accent` and plain `.btn`. On the home page only 9 of 75 tab stops draw a real ring; 65 are weak and 1 shows nothing (live). |
| Bypass / focus order | The skip link does nothing because `navigation.js` swallows it (live). After any "Run" click, focus is dropped to `<body>` on 11 demo pages (live on Clustering; the code is shared). |
| Keyboard operability | Viewshed observer placement is mouse-only. Response isochrones beyond the first station are mouse-only. Outage exposes 267 tab stops inside one SVG. |
| Target size (2.5.8) | Outage conductor hit lines are about 5 px at 360 (estimated) and 11.3–11.5 px at 768/1280 (measured), with 258–267 failing per width. Toast close buttons are 18×18. |
| Names / labels | 3 ArcGIS map surfaces have no name. 7 controls fail label-in-name. 4 generic `div`s carry an `aria-label`. |
| Status messages (4.1.3) | 10 of 12 compute demos never announce results; only Response and Fleet call `announce()`, and GeoStream shows only a transient toast. |
| Motion | The carousel has no pause control (hover/focus only). Under reduced motion it strands 15 of 18 cards. `navigation.js` smooth-scroll ignores reduced motion. |
| Contrast (computed and live) | **19 text pairs fail AA** (14 measured live), plus **7 non-text groups** below 3:1 (form-control borders 1.18–2.17, dark modal close 1.43, chart palettes, faint focus rings). |
| Performance | No `Cache-Control` on any static file (local or production). Production gets Brotli from Render/Cloudflare. jQuery + DataTables (212 KB raw) load on all 45 Razor pages but only 3 use them. Typeahead (47 KB) loads but is never called. 53 page-level stylesheet `<link>`s lack `asp-append-version` (1 has it). 10.5 MB of unreferenced vendor files ship in `wwwroot/lib`. |

**Top 5 (do first)**
1. **FE-A01 Focus rings.** Bootstrap's `.btn:focus-visible { outline:0 }` and `.nav-link:focus-visible` override the global ring. For the custom `.btn-outline-accent`, focus actually *removes* the border (the hover/focus variables are undefined). One CSS fix in `preferences.css`/`site.css` repairs it across the whole site.
2. **FE-A02 Skip link and in-page anchors.** `navigation.js:16-38` calls `preventDefault()` on every `a[href^="#"]` and never moves focus.
3. **FE-C01/FE-A03 Home carousel.** Keyboard focus lands on off-screen cards (x=-2690 px while the slider spans 75..1191). Reduced motion strands 15 of 18 cards. The loop seam is off by 12 px. The 28 KB duplicate markup can become one tuple-array partial rendered twice. **No JS depends on the duplicate**; the loop is pure CSS (`layout.css:345-361`).
4. **FE-A07/FE-A08 State Explorer.** Anonymous visitors are redirected to `/Login` by `api-fetch.js:36-43` when `GET /api/v1/collections` returns 401. The ArcGIS theme CSS is missing on State Explorer and Reverse Geocoding, so the zoom buttons render 0×0 or unstyled.
5. **FE-J01 + FE-P03/P01 Shared modules and loading.** Replace 12× `showAlert`/`hideAlert`, 11× `setBusy` (the focus-dropping one), 10× `escapeHtml`, 4× `announce` and 3 toast implementations with one `ui.js`. Load jQuery/DataTables only on the 3 pages that use them. Add `Cache-Control` for versioned assets.

**Not verified (Stage 2 must check; scripts in §11):**
- PlantOps and Account/Profile: they need sign-in, which I was not allowed to do.
- State Explorer's populated state: my run got 500s from `GET /api/v1/features` (`FileNotFoundException: System.Net.Http.Json 10.0.0.0` in `ArcGisService.QueryFeaturesCoreAsync`). The DLL is present in runtime 10.0.12, so this looks environmental. I flagged it for the backend agents and did not investigate.
- ArcGIS popups and Batch Geocoding results: these need external ArcGIS calls or uploads.
- Animations in a visible window: the pane was hidden, so rAF and IntersectionObserver never fired and transitions did not advance.
- OS-level `prefers-reduced-motion`: I tested the site's own `data-motion="reduce"` preference instead.
- 320 px reflow, 150% text scale, and screen-reader output.

---

## 1. Method and environment

- **Static scripts** (scratch only, not in the repo):
  - `inventory.py`: per-page CSS/JS, inline blocks, CDN loads.
  - `colors.py`: colour literals per file.
  - `contrast.py`: WCAG ratios from the CSS values, with alpha compositing.
  - `balance.py`: stack-based tag balance for `.cshtml`.
- **Live**: I cloned to scratch, copied the untracked `appsettings.Development.json` (never printed), and ran
  `dotnet run --project Portfolio.Web --no-launch-profile --urls http://localhost:5080` with
  `ASPNETCORE_ENVIRONMENT=Development`. In the built-in browser I used `resize_window` at 360×800, 768×1024 and
  1280×800, and `javascript_tool` assertions served from the clone's `wwwroot/__audit/fe-audit.js`. The full text is
  in Appendix A, so Stage 2 can reuse it. Each run toggles light/dark through the site's own
  `SitePreferences.set('theme', …)` and measures both.
- **Test hook (scratch clone only):** to audit State Explorer's layout without the 401 redirect, I added a
  `sessionStorage['__feAuditNoAuthRedirect']` guard to the *clone's* `api-fetch.js`. The repo is untouched. All other
  pages ran unmodified code.
- **Caveat, hidden pane.** The browser pane was hidden during the audit, so `requestAnimationFrame` and
  `IntersectionObserver` callbacks did not fire and CSS transitions did not advance. Where that matters (focus
  styles, toasts, the skip-link slide), I measured end states with transitions suppressed, using the site's own
  `html.theme-switching` kill switch. CSS keyframe animations (the carousel) did advance.
- **Production headers:** I sent one read-only `curl -I` to `https://brianpcoffey.com/lib/bootstrap/css/bootstrap.min.css`
  and `/css/base.css`. Both return `Content-Encoding: br`, `cf-cache-status: DYNAMIC`, and **no `Cache-Control`**.
- **Cleanup:** I reset the viewport to the desktop preset, cleared the test origin's storage, closed the tab, stopped
  the server (port 5080 is free) and stopped the browser preview.

---

## 2. Baseline measurements (for PLAN.md §1)

### 2.1 Global payload on every page (raw bytes; production serves Brotli)
| Group | Files | Raw bytes | gzip -9 |
|---|---|---:|---:|
| Vendor CSS | bootstrap.min.css 232,808 · fontawesome all.min.css 73,898 · DataTables 22,712 + 12,168 | 341,586 | 56,879 |
| Site CSS | base 3,824 · utilities 749 · site 8,961 · layout 13,307 · components 11,054 · preferences 13,155 | 51,050 | 12,984 |
| Vendor JS | jquery-3.7.1 87,535 · DataTables 87,224 + 2,361 · bootstrap.bundle 80,727 | 257,847 | 84,906 |
| Site JS | api-config 5,404 · api-fetch 6,191 · toasts 5,364 · site 483 · preferences 13,439 · navigation 2,808 · animations 1,917 | 35,606 | 10,725 |
| Fonts | fa-solid-900.woff2 158,220 · fa-brands-400.woff2 118,684 (the brands font is fetched for 2 footer glyphs) | 276,904 | — |
| **jQuery + DataTables (used on 3 pages: AddressStandardization, BatchGeocoding, PlantOps)** | | **212,000** | 66,430 |

Render-blocking: all 10 stylesheets in `<head>` (`_Layout.cshtml:79-96`). Every script is at the end of `<body>`
with **no `defer`/`async`** (`_Layout.cshtml:172-182`). The only exception is State Explorer's `type="module"` files.

### 2.2 Per-page inventory (page-level loads on top of the global set)
Page CSS `<link>`s are emitted **inside `<main>`** (body), not in `<head>`. None of the 53 page-level stylesheet
tags has `asp-append-version`; the one exception is `network.css`.

| Page | Page CSS (bytes) | Page JS (bytes) | CDN | Inline | Notes |
|---|---|---|---|---|---|
| Home `/` | – | – | – | `<script>` `copyEmailContact()` (`Index.cshtml:979-985`): **dead, never called** | HTML 76.8 KB raw / 8.7 KB gz; 28 KB is the duplicate cards |
| `/Projects` | – | – | – | – | third copy of the card content (differs, see FE-C01) |
| Login | – | – | `developers.google.com/.../g-logo.png` (`Login.cshtml:23`) | – | external image |
| Privacy, Accessibility, Error, AccessDenied | – | – | – | – | |
| Account/Profile | profile.css 7,313 | – | – | 2 `style=` | auth; **not live-audited** |
| StateExplorer | gis.css 11,782 · **typeahead.css 1,973** | **typeahead.bundle 45,070 (unused)** · 6 ES modules 60,630 | js.arcgis.com/4.29 | 5 `style=` | **no Esri theme CSS** (FE-A08) |
| HomeFinder | gis.css · homefinder.css 6,649 | app.js 28,599 | ArcGIS 4.29 + theme CSS | `<script>` Esri theme loader (`Index.cshtml:202-215`) · 12 `style=` | |
| PlantOps | gis.css · plantoperationsdashboard.css 6,974 | **d3 279,708** · **toasts.js loaded twice** (`Index.cshtml:219`) · dashboard/orders/shipments/inventory 52,561 | ArcGIS + theme | `<script>` Esri theme loader (`:15-28`) | auth; **not live-audited**; `<div>` at `:50` never closed |
| BatchGeocoding | gis.css · batchgeocoding.css 13,469 | app.js 15,628 (uses DataTables) | – | – | |
| ReverseGeocoding | gis.css · reversegeocoding.css 13,750 | app.js 15,191 | ArcGIS 4.29 | – | **no Esri theme CSS** |
| AddressStandardization | gis.css · addressstandardization.css 15,650 | **d3 279,708** · app.js 22,136 (uses jQuery) | – | 3 `style=` | |
| GeoStream / Geometry / Terrain / Clustering / Viewshed / Overlay / CatRisk / Change / Outage | gis.css · spatialcompute.css 4,832 | app.js 4.5–22.8 KB | – | 0–7 `style=` (Change 7) | |
| Network | network.css 6,457 (versioned) · leaflet.css | leaflet.js (SRI) · app.js 18,909 | unpkg leaflet 1.9.4 | 1 `style=` | does not load gis.css |
| Response / Fleet | gis.css · spatialcompute.css · leaflet.css | leaflet.js · app.js 27,077 / 22,948 | unpkg leaflet 1.9.4 | 2 / 3 `style=` | |
| Details ×18 | gis.css (only for `.theme-surface`) | – | – | – | |

Libraries are only loaded where used, with two exceptions: jQuery/DataTables (global) and typeahead.
ArcGIS is used on all 4 pages that load it, Leaflet on all 3, and D3 on both. The two `leaflet@` spellings
(`Network/Index.cshtml:9` uses `@`, Fleet/Response use `@@`) render identically.

### 2.3 Design tokens (`base.css:4-50`)
`--primary #1a365d`, `--primary-light #2c5282`, `--accent #38a169`, `--accent-light #48bb78`, `--background #f7fafc`,
`--surface #fff`, `--text #1a202c`, `--text-muted #5a6a7d`, `--border #e2e8f0`, `--shadow`, `--transition`,
`--hero-gradient`, `--accent-on-fill #2c7a53`, `--accent-on-fill-h #215c3f`, `--accent-text #2c7a53`, `--accent-soft #e6f4ec`.

Dark mode (`body.dark-mode`) overrides background, surface, text, text-muted, border, shadow, hero-gradient,
accent-text and accent-soft. It does **not** override `--primary`, `--primary-light`, `--accent`, `--accent-light`,
`--accent-on-fill` or `--accent-on-fill-h`.

Tokens that are **missing** and are therefore hard-coded instead:
- status colours (success, warning, danger, info) in text, fill and soft variants;
- a categorical chart palette;
- a focus-ring colour;
- an input-border colour;
- "on-gradient" white tints;
- an accent RGB triplet (`rgba(56,161,105,x)` is hard-coded ×29).

### 2.4 Hard-coded colour literals per file (excluding the `base.css` token blocks and `lib/`)
| Count | File | Worst examples |
|---:|---|---|
| 37 | css/homefinder.css | dark overrides hard-code the token values: `#2d3748` (= dark `--surface`) `:88,94,100`, `#f7fafc` `:39,89,95`, `#4a5568` `:44,90`. These bypass high-contrast mode. |
| 32 | css/gis.css | `#featureDetails` forced to `#ffffff !important` (`:306-325`); dark placeholders forced to `#ffffff` (`:327-333`); `#ffc107`/`rgba(13,110,253,…)` selection colours (`:13-21,399-415`) |
| 30 | css/addressstandardization.css | tier colours `:173-193`, dark tiers `:307-327`, `color:#fff` ×4 |
| 27 | css/network.css | 16 are `var(--x, #fallback)` (`:11-128`); `.destination-chip #198754` (`:80`); route `#2563eb` (`:173`); swatches `#0d9488/#f59e0b` (`:218-219`) |
| 24 | css/layout.css | white tints on the hero/contact/footer (`:149-166, 474-553`) |
| 22 | css/profile.css | danger `#e53e3e/#c53030/#fc8181` (`:76-96, 255-271`) |
| 22 | css/site.css | Google button `#fff/#444/#ddd` (`:142-170`), `#ffd700` toggle (`:79`) |
| 22 | js/Response/app.js | `BAND_COLORS` (`:12`), KPI inline colour (`:223`), marker colours (`:247-296`), `mixHex` stops (`:509-511`) |
| 18 | css/components.css | dark alert palette (`:213-235`), focus ring rgba (`:76,254`) |
| 16 | css/preferences.css | high-contrast palette (intentional), `#fff` on the skip link |
| 16 | css/spatialcompute.css | shape fills `:70-80`, swatches `:111-121`, point halos `:48-59` |
| 15 | css/batchgeocoding.css | KPI/score colours `:108-155` (Chakra palette) |
| 15 | js/Fleet/app.js | 10-colour vehicle palette (`:10-12`), marker colours (`:240-264`) |
| 14 | css/reversegeocoding.css | location-type badges `:133-150` |
| 14 | js/Network/app.js | canvas marker/route colours (`:153-346`) |
| 11 | js/Outage/app.js | device/state colours (`:20-25`) |
| 10 | js/Clustering/app.js | 10-colour palette (`:6-9`) |
| 8, 7, 7 | js/Overlay, js/CatRisk, js/Change | palettes / `mixHex` |
| ≤4 | plantoperationsdashboard.css, StateExplorer/{mapManager,stateStore,app,uiManager}.js, AddressStandardization, ReverseGeocoding, HomeFinder, Viewshed app.js | |

### 2.5 Dark-mode coverage per stylesheet
| File | `body.dark-mode` rules | `!important` | `@media` | Notes |
|---|---:|---:|---:|---|
| components.css | 63 | 18 | 1 | the global Bootstrap/DataTables dark layer |
| gis.css | 52 | **71** | 0 | specificity wars; overrides `.btn-close`, focus, modals |
| batchgeocoding.css | 40 | **50** | 0 | `:192-347` repeats components.css `:352-436` |
| homefinder.css | 28 | 31 | 1 | hard-coded dark values (see 2.4) |
| addressstandardization.css | 27 | 12 | **0** | no responsive rules |
| reversegeocoding.css | 25 | 9 | 2 | |
| plantoperationsdashboard.css | 23 | 35 | 5 | invalid `box-shadow: 0 0 0 2px var(--accent)33` (`:16`) |
| network.css / spatialcompute.css | 0 / 0 | 0 / 0 | 1 / 0 | token-only (good), but their literal colours never adapt |

### 2.6 Duplication hot spots
**JS helper functions** (same name and shape in N page scripts):

| Helper | Copies |
|---|---:|
| `showAlert` / `hideAlert` | 12 |
| `setBusy` | 11 |
| `escapeHtml` | 10 (6 spatial + AddressStd, HomeFinder, ReverseGeocoding, StateExplorer/uiManager) |
| `announce` | 4 (HomeFinder, Response, Fleet, preferences.js) |
| `appendText` / `appendLine` (SVG, byte-identical) | 4 |
| `xFor` / `yFor` scales | 3–4 |
| seeded `rng` | 3 |
| `colorFor` | 3 |
| `mixHex`, `buildProjection` / `projectX/Y`, `drawHistogram`, `buildLegend`, `clamp01`, `round`, `loadScenario` | 2 each |
| toast implementations | **3**: toasts.js (DOM-built), plus `HomeFinder/app.js:668-701` and `PlantOperationsDashboard/dashboard.js:7-41`, both `innerHTML` and both overriding `window.showToast` |
| Leaflet map + OSM tile setup | identical ×3 (`Network/app.js:38-48`, `Response/app.js:49-59`, `Fleet/app.js:40-50`) |
| Esri theme loader | inline ×2 (`HomeFinder/Index.cshtml:202-215`, `PlantOperationsDashboard/Index.cshtml:15-28`) |

**CSS blocks defined twice or more:**
- `.fade-in` (`utilities.css:4-16`, `components.css:4-13`, with different durations; components.css wins)
- `.skill-tag` (`layout.css:223-239`, `components.css:45-62`; components.css wins)
- hero responsive rules (`utilities.css:21-33`, `components.css:118-130`, identical)
- `html/body/main` resets (`base.css:66-85`, `layout.css:567-581`)
- nav underline (`layout.css:36-54`, `preferences.css:443-463`, both partially applied)
- modal theming (`site.css:294-306`, `components.css:276-290`, `gis.css:210-229` with `!important`)
- `.project-hero` (`gis.css:447-456`, `spatialcompute.css:1-5`, plus `network.css:15-19` as `.network-hero`)
- `.btn-accent`/`.btn-outline-accent` (`site.css:20-48`, redefined in `reversegeocoding.css:197-224` and `addressstandardization.css:228-262`)
- dark DataTables (`components.css:352-436`, `batchgeocoding.css:192-301`, `plantoperationsdashboard.css`)
- focus outline (`preferences.css:67-71`, `gis.css:259-262`)

### 2.7 Number formatting
- `toFixed()` is called 158×, `toLocaleString()` 9× (HomeFinder, PlantOps) and `Intl.NumberFormat` 0×.
- Displayed KPIs (live) show no common convention: `"$832.9M"`, `"2.66%"`, `"94.1%"` next to `"100.0%"`, `"1.76 / 1.09 mi"` under a label "Distance (km / mi)", `"2.42 min"`, `"61.4 km"`, `"(12,12)"`, `"4 / 5"`, `"4 of 4"`, `"ASTAR"`, `"Tri"`, `"Shade"`.
- The native flag reads "Yes/No" on 7 pages and `"Native: ON/OFF"` on Network (`Network/app.js:389`).
- HomeFinder slider labels show unformatted `$800000` (`HomeFinder/Index.cshtml:38`).

### 2.8 `innerHTML` sites per file (to show overlap only; the security agent owns these)
StateExplorer/uiManager 17 · HomeFinder 12 · Response 6 · Outage 6 · Fleet 5 · Overlay 4 · Change 4 · CatRisk 4 ·
ReverseGeocoding 3 · Network 3 · GeoStream 3 · PlantOps/dashboard 2 · Clustering 2 · Viewshed 1 · Terrain 1 ·
Geometry 1 · AddressStandardization 1.

There are also 18 `style="…"` attributes inside JS-built HTML strings across 8 files, and 44 `style=` attributes in `.cshtml` markup.

---

## 3. Live audit matrix

Each cell summarises 360/768/1280 in light and dark. Every page had **no horizontal overflow, no duplicate ids and no
`<img>` without `alt`**, so those columns are omitted. Contrast counts are distinct failing elements from `run()`;
"gw" marks a gradient worst case.

| Page | Failing targets | Unnamed controls | Contrast L / D | Other live findings |
|---|---|---|---|---|
| Home | 0 | 0 | 0 / 0 (+ hero gradient: `.hero-desc` 4.20 / 4.12 / 4.40; `.contact-link` 3.71–3.97 @360, 4.36 @768) | 9/75 real focus rings; carousel focus off-screen; skip link broken; reduced motion → 3 of 18 cards visible; seam 12 px |
| /Projects | 0 | 0 | 0 / 0 | |
| Login | 0 | 0 | **1** (`.login-back-link` 1.00) / 0 | |
| Privacy, Accessibility, AccessDenied | 0 | 0 | 0 / 0 | AccessDenied not run at 768 |
| Error | 0 | 0 | 1 (`code` 4.29) / 0 | |
| Account/Profile | – | – | – | redirects to /Login (auth): **not verified** |
| StateExplorer | toast close 18×18 @360 | 1 (esri surface) | **4 / 5** (btn-primary 3.63/4.29; `.text-warning` 1.63; `.text-success`/`.text-primary` 2.65/2.66 dark; toast 3.60/4.32) | anonymous → /Login redirect; esri zoom 0×0; console: 401 collections, 500 features (env) |
| HomeFinder | 0 | 1 (esri surface) | 0 / 1 (`#weightStatus .text-muted` 4.39) | slider names = `maxPrice`, `minSqft`… (label-in-name ×6); `.form-range` green box in dark; modal `.btn-close` filter:none in dark |
| PlantOps | – | – | – | redirects to /Login (auth): **not verified** |
| BatchGeocoding | 0 | 0 | 0 / 0 (gw 4.42) | `aria-label` on `#spinnerOverlay` div |
| ReverseGeocoding | 0 | 1 (esri surface) | 0 / 0 (gw 4.42) | no Esri theme CSS (zoom widget `position:static`, 820 px wide) |
| AddressStandardization | 0 | 0 | 1 / 1 ("Try an example" 3.25/3.69) + **NLP badge 1.18** (gw) | badge rule scoped outside its element |
| GeoStream | toast close 18×18 @360 | 0 | after run: 2 / 2 (`.geo-cell-item strong` 3.25/3.69; anomaly 4.16/2.57) | label-in-name (textarea); `aria-label` on `#geoStage` div |
| Geometry, Terrain, Clustering | 0 | 0 | 0 / 0 (gw 4.42) | Terrain/Viewshed `aria-label` on grid divs; focus lost to `<body>` after Run (Clustering, live) |
| Viewshed | 0 | 0 | 0 / 0 | 576–1,600 grid cells, `tabIndex=-1`, 11–16 px: observer is **mouse-only** |
| Overlay, CatRisk, Change, Response, Fleet | 0 | 0 | after run: 1 / 1 each (`.geo-cell-item strong`) | Response station list = plain DIVs; Fleet chart text ≈ 9 px at 360 |
| Outage | **258–267** (`line[role=button]`; hit width ≈5 px @360 est., 11.3 @768, 11.5 @1280 measured) | 0 | 0 / 0 | **267 tab stops** inside the SVG |
| Network | 0 | 0 | 5 / 1 (`.destination-chip` 3.89/2.38; `.kpi-val` 3.10 @360 only) | page height `calc(100vh-62px)` vs 75 px navbar; "ASTAR", "Native: OFF" |
| Details ×18 | 0 | 0 | 0 / 0 | the 8 newer pages have 6 H2 sections, the older 10 have 8 (docs owner) |

Console errors across the whole sweep: only State Explorer's (401 collections ×2, 500 features, `getFeatures Error`).
Failed requests: the same three.

---

## 4. Findings: accessibility (WCAG 2.2 AA)

Severity: **P0** breaks an AA criterion site-wide or blocks a task. **P1** is an AA failure on one feature or page.
**P2** is best practice or a usability/consistency issue. **P3** is polish.

### FE-A01 · P0 · Focus indicators missing or degraded on Bootstrap-based controls (2.4.7, 1.4.11)
- **Evidence (live, transitions suppressed):**
  - `#themeToggle`: no outline, no box-shadow.
  - `.btn-outline-accent` (hero CTA, "View all projects", "View résumé", every card's Demo/info button, every demo's secondary action, `#responseIsochroneBtn`): **no ring**. On focus the border turns `rgba(0,0,0,0)`, i.e. it gets *worse*.
  - `.btn-accent`: no ring, only the hover darkening plus a 35% soft shadow.
  - `.nav-link`: Bootstrap's `rgba(13,110,253,.25)` 4 px ring (≈1.4:1, off-brand).
  - Form controls: a 25% green ring (`components.css:74-77`).
  - Radios/checks: a Bootstrap blue 25% ring.
  - `focusIndicators()` on Home: **75 tab stops → 1 none, 65 weak**. On Response: 30 → 1 none (`.form-range`), 18 weak.
- **Root cause (static):**
  - The global ring `:focus-visible { outline: 3px solid var(--accent) }` (`preferences.css:67-71`, specificity 0,1,0) loses to Bootstrap's `.btn:focus-visible { outline:0; box-shadow: var(--bs-btn-focus-box-shadow) }` and `.nav-link:focus-visible { outline:0; box-shadow:… }`.
  - `.btn-accent`/`.btn-outline-accent` (`site.css:20-48`) set colours directly instead of Bootstrap's `--bs-btn-*` variables. As a result `--bs-btn-focus-shadow-rgb`, `--bs-btn-hover-border-color` and `--bs-btn-hover-bg` fall back to undefined or transparent.
  - Faint rings are also hard-coded in `layout.css:78-81` (hamburger, 1.46:1), `gis.css:281-287` (input-group, 1.13:1) and `gis.css:417-421` (feature-card dark, 1.17:1).
- **Fix:**
  1. Add a token `--focus-ring` (light `#2c7a53` 4.99/5.23:1; dark `#68d391` 8.79/6.46:1) and `--focus-ring-on-dark: #fff` for gradient and dark surfaces.
  2. In `preferences.css`, after Bootstrap, add `:focus-visible, .btn:focus-visible, .nav-link:focus-visible, .form-control:focus-visible, .form-select:focus-visible, .form-check-input:focus-visible, .form-range:focus-visible, .page-link:focus-visible, .list-group-item-action:focus-visible, .dropdown-item:focus-visible, .btn-close:focus-visible { outline: 3px solid var(--focus-ring); outline-offset: 2px; box-shadow: none; }`. Add `.hero-section, .contact-section, .site-footer, .login-section { --focus-ring: var(--focus-ring-on-dark); }`.
  3. Rewrite `.btn-accent`/`.btn-outline-accent` (`site.css:20-48`) on Bootstrap's variable API (`--bs-btn-color`, `-bg`, `-border-color`, `-hover-*`, `-active-*`, `-disabled-*`, `--bs-btn-focus-shadow-rgb: 44,122,83`) so hover, focus and active stop degrading.
  4. Delete `gis.css:259-262`, the `outline:none` in `gis.css:281-287` and `:417-421`, and `layout.css:78-81`.
- **Files:** `base.css`, `preferences.css`, `site.css`, `gis.css` (via FE-WP5), `layout.css` (via FE-WP3).
- **Risk:** low. This is a visible change: a 3 px green ring appears on keyboard focus only.
- **Verify:** press Tab once, then `focusIndicators()` on every page must return `none: 0, weak: 0`. Also check `focusProbe('.btn-outline-accent').after.bc` ≠ `rgba(0, 0, 0, 0)`.
- **Size:** S (CSS only).

### FE-A02 · P0 · Skip link and in-page anchors do not move focus; smooth scroll ignores reduced motion (2.4.1, 2.4.3, 2.3.3 advisory)
- **Evidence (live, real keys):** Tab → `.skip-link` → Enter. Afterwards `document.activeElement` is still `A.skip-link`, `location.hash` is empty, and the next Tab lands on `.navbar-brand`.
- **Root cause (static):** `navigation.js:16-38` binds every `a[href^="#"]` (in practice the skip link and the hero CTAs `#projects`/`#contact`; the nav's `/#…` links do not match), calls `e.preventDefault()`, then `scrollTo({behavior:'smooth'})` (`:30-33`) without focusing the target. `main#mainContent` already has `tabindex="-1"` (`_Layout.cshtml:148`), so the native behaviour would have worked.
- **Fix:**
  - Skip `.skip-link` entirely in `initSmoothScroll`, or for all intercepted anchors do the following after scrolling: `if (!target.hasAttribute('tabindex') && !target.matches('a,button,input,select,textarea,[tabindex]')) target.setAttribute('tabindex','-1'); target.focus({ preventScroll: true }); history.pushState(null, '', targetId);`
  - Use `behavior: document.documentElement.dataset.motion === 'reduce' ? 'auto' : 'smooth'`.
  - Re-save the file as UTF-8 (`:21` has a cp1252 0x97 byte).
- **Files:** `wwwroot/js/navigation.js`.
- **Risk:** low.
- **Verify:** Tab, Enter on the skip link → `activeElement === document.getElementById('mainContent')`, and the next Tab is the first focusable element inside `main`. With `SitePreferences.set('motion','reduce')`, `scrollTo` is called with `behavior:'auto'` (wrap `window.scrollTo` with a spy).
- **Size:** S.

### FE-A03 · P0 · Home carousel: focus obscured, no pause control, reduced motion strands cards, loop seam (2.4.11, 2.2.2, 2.1.1)
- **Evidence (live, 1280):**
  - Focus does pause the track (`animation-play-state: paused` via `layout.css:365-368`), but real Tabs land on `US State Explorer Demo x=-2690`, then `-2324`, then `-1588`, while the slider spans 75..1191. `scrollLeft` stays 0 (cards sit *left* of the container, so the browser cannot scroll to them). Keyboard users tab through 54 invisible links.
  - Under `data-motion="reduce"` the transform is `none`, the slider has `overflow-x:hidden` and only **3 of 18** real cards are visible. Pointer users cannot reach the rest (only via "View all projects").
  - Seam: the track is 13,440 px, the 50% translate is 6,720 px, but `card[18].left - card[0].left = 6,732 px`. That gives a 12 px jump every 45 s (`gap: 1.5rem` makes −50% half a gap short).
- **Root cause (static):** `layout.css:317-368`; `preferences.css:135-153` sets duration to ~0 but leaves `overflow:hidden`. There is no pause control, and hover/focus is the only mechanism, which doesn't work for touch.
- **Fix:** see FE-C01. It adds `carousel.js` (~40 lines, vanilla):
  1. A visible **Pause/Play** toggle button (`aria-pressed`) in the section header, for 2.2.2.
  2. On `focusin` inside `.projects-slider`: add `.is-manual` to the slider (`animation: none` on the track, `overflow-x: auto; scroll-snap-type: x mandatory`), then `card.scrollIntoView({ inline: 'nearest', block: 'nearest' })`.
  3. Under reduced motion or when paused, use the same manual mode: a horizontally scrollable, snap-aligned row with duplicates hidden (`.projects-track > [aria-hidden="true"] { display:none }` in manual mode).
  4. Seam: `@keyframes scrollProjects { to { transform: translateX(calc(-50% - var(--track-gap) / 2)); } }` with `--track-gap: 1.5rem`, or give each card `margin-right: var(--track-gap)` and drop `gap`.
- **Files:** `Pages/Index.cshtml`, `wwwroot/css/layout.css` (carousel block `:317-368`), new `wwwroot/js/carousel.js`.
- **Risk:** medium. This is visible behaviour on the hero section of the site; option B in Decisions keeps it a pure marquee.
- **Verify:**
  - Focus the 8th real card's Demo link: its rect is inside the slider rect.
  - With `motion=reduce`, `slider.scrollWidth > clientWidth` and `getComputedStyle(slider).overflowX === 'auto'`, and all 18 real cards are reachable by scrolling.
  - Seam: `card[18].left - card[0].left === trackWidth/2 + gap/2` is matched by the keyframe value.
  - The pause button toggles `animationPlayState`.
- **Size:** M.

### FE-A04 · P1 · "Run" actions drop focus to `<body>` and complete silently (2.4.3, 4.1.3)
- **Evidence (live, Clustering):** focus `#clusterRunBtn` and press Enter. While busy `activeElement` is the button; after completion it is `BODY`. `#a11yAnnounce` stays empty while the KPI changes to "3".
- **Root cause (static):** `setBusy()` sets `button.disabled = true` on the focused button in 11 files (Clustering `:170-173`, Geometry `:117`, Terrain `:99`, Viewshed `:148`, Overlay `:172`, CatRisk `:440`, Change `:511`, Outage `:473`, Response `:564-568`, Fleet `:498-502`, Network `:394-398`), so Chrome's focus fixup moves focus to body. Only Response, Fleet and HomeFinder call `announce()`.
- **Fix:** shared `PortfolioUi.busy()` / `runAction()` (FE-J01):
  - Keep the activated button focusable during the request (`aria-disabled="true"`, a `.is-busy` class, and a click guard instead of `disabled`), or restore focus afterwards if it fell to `body`.
  - Set `aria-busy="true"` on the results panel.
  - On success call `PortfolioUi.announce(summary)` (e.g., "3 clusters, 18 noise points"); on failure fill the existing `role="alert"` box.
- **Files:** the 11 `app.js` above plus the new `ui.js`.
- **Risk:** low.
- **Verify:** real Enter on Run → after completion `activeElement === runBtn` and `#a11yAnnounce.textContent !== ''`; double-clicking issues one request (watch `read_network_requests`).
- **Size:** M (spread over the page batches).

### FE-A05 · P1 · Viewshed observer can only be placed with a mouse (2.1.1, 2.5.8)
- **Evidence:** live, 576 cells at 11 px (360) / 16 px (1280), `tabIndex=-1`, no role. Static: cells built in `Viewshed/app.js:118-139` with `addEventListener("click", onCellClick)`; the hint is at `Viewshed/Index.cshtml:54`.
- **Fix:** add two labelled number inputs, "Observer column" and "Observer row" (`min=0`, `max=size-1`), synced with clicks, in the controls panel. This is the equivalent control for 2.5.8's exception and the keyboard path for 2.1.1. Optional: render the raster to `<canvas>` per the repo convention ("big rasters to `<canvas>`", up to 1,600 divs today).
- **Files:** `Projects/Viewshed/Index.cshtml`, `js/Viewshed/app.js`.
- **Risk:** low.
- **Verify:** set the inputs via keyboard and Run → KPI "Observer" shows the new `(x,y)`.
- **Size:** S.

### FE-A06 · P1 · Outage diagram: 267 tab stops and 5–11 px targets (2.5.8, 2.4.3)
- **Evidence:** live, `#outageSvg [tabindex="0"]` = 267; hit width 1.8 viewBox units = 11.3 px @768 and 11.5 @1280 (measured), about 5 px @360 (estimated); `targets.failing` = 258–267 per width. Static: `Outage/app.js:331-357` (each hit line gets `role=button`, `tabindex=0` and its own key handler).
- **Fix:**
  - Add an accessible "Fault location" `<select>` (grouped by feeder, `option` per conductor) plus a "Place fault" button in the controls panel. This is the equivalent control satisfying 2.5.8.
  - Set the SVG hit lines to `tabindex="-1"` (keep the pointer path), or implement a roving tabindex with arrow keys (one tab stop).
  - Keep the `role="group"` on the SVG (`Outage/Index.cshtml:81-84` explains why it is not `img`).
- **Files:** `Projects/Outage/Index.cshtml`, `js/Outage/app.js`.
- **Risk:** low.
- **Verify:** `document.querySelectorAll('#outageSvg [tabindex="0"]').length <= 1`; choosing an option and pressing the button places the fault (KPI "Customers Affected" changes); `run().targets.failing === 0`.
- **Size:** M.

### FE-A07 · P1 (UX/behaviour; needs a decision) · State Explorer bounces anonymous visitors to /Login
- **Evidence (live):** loading `/Projects/StateExplorer` anonymously requests `GET /api/v1/collections → 401` and the page navigates to `/Login?returnUrl=%2FProjects%2FStateExplorer` within about 1 s. Static: `api-fetch.js:36-43` redirects on any 401, which pre-empts `collectionService.js:6-9`, whose comment says a failed collections fetch should yield an empty list "rather than ... brick[ing] the whole page".
- **Fix (recommended default):**
  - Give `apiFetch(url, { redirectOn401: false })` an opt-out.
  - `collectionService.getAll()` uses it and treats 401 as "not signed in": empty list plus a "Sign in to use collections" hint in the My Collections card. Disable "+ New" with a tooltip.
  - Saved features use the anonymous GUID, so they keep working.
- **Files:** `js/api-fetch.js`, `js/StateExplorer/collectionService.js`, `js/StateExplorer/uiManager.js`, `Projects/StateExplorer/Index.cshtml`.
- **Risk:** low. It is behaviour-changing, and in the direction the code comment intended.
- **Verify:** anonymous load → `location.pathname === '/Projects/StateExplorer'` after 5 s, the collections card shows the sign-in hint, and the console shows no 401-driven navigation.
- **Size:** S.

### FE-A08 · P1 · ArcGIS pages: theme CSS missing on two pages, nested and unnamed `role="application"`
- **Evidence (live):**
  - State Explorer: no `esri/themes` stylesheet; `.esri-zoom` is 611×0 and the "Zoom in" button is **0×0**.
  - Reverse Geocoding: no theme CSS; the zoom widget is `position:static`, transparent, 820 px wide.
  - HomeFinder, which injects the theme: zoom 32×64, buttons 32×32.
  - All three: inner `div.esri-view-surface[role=application]` has no accessible name, nested inside an outer `role="application"` div (`StateExplorer/Index.cshtml:98`, `HomeFinder/Index.cshtml:168`).
- **Fix:**
  - New `js/esri-theme.js` (the inline loader from `HomeFinder/Index.cshtml:202-215`, made reusable) loaded on all four ArcGIS pages before `js.arcgis.com/4.29`. Delete both inline copies (also a CSP blocker).
  - Remove the outer `role="application"`/`aria-label` from the map container div. Put the name on the view instead: if ArcGIS 4.29 supports `view.aria`/`aria-label` on the view surface, use it; otherwise set `view.container.querySelector('.esri-view-surface').setAttribute('aria-label', …)` after `view.when()`.
- **Files:** new `js/esri-theme.js`; `Projects/{StateExplorer,ReverseGeocoding,HomeFinder,PlantOperationsDashboard}/Index.cshtml`; `js/StateExplorer/mapManager.js`, `js/ReverseGeocoding/app.js`, `js/HomeFinder/app.js`, `js/PlantOperationsDashboard/dashboard.js` (one line each).
- **Risk:** low.
- **Verify:** on each page `document.querySelector('link[href*="esri/themes"]')` exists, `.esri-widget--button` is 32×32, `run().names.missing === 0`, and there is no nested `[role=application] [role=application]`.
- **Size:** S.

### FE-A09 · P1 · Text contrast failures (1.4.3). 19 pairs; values from `contrast.py` or live `run()`
| # | Element | Ratio | Theme / width | Source | Fix |
|---|---|---|---|---|---|
| T1 | "Back to portfolio" `.login-back-link` (white 65%) inside the white card | **1.00** (live) | light, all | `site.css:271-275`; `Login.cshtml:37-40` (the `.login-back-row` CSS at `site.css:265-269` is unused) | `color: var(--text-muted)`; hover `var(--accent-text)` |
| T2 | "NLP Parsing" `.as-badge-nlp` (white on transparent) | **1.18** (live) | light | `addressstandardization.css:9-15` scoped to `.address-standardization-page`, but the badge is in `.project-hero` outside it (`Index.cshtml:26` vs `:40`) | unscope the selector, or move it to `components.css` |
| T3 | `#statComparing.text-warning` | 1.63 (live) | light | `StateExplorer/Index.cshtml:144` | `--status-warning-text` |
| T4 | `#statSaved.text-success`, `#statSelected.text-primary` | 2.65 / 2.66 (live) | dark | `StateExplorer/Index.cshtml:132,138` | status tokens (adapt in dark) |
| T5 | `.destination-chip` `#198754` on tint | 3.89 / 2.38 (live) | both | `network.css:76-86` | `--status-success-text` + `--status-success-soft` |
| T6 | `--accent` (#38a169) used as text | 3.10–3.25 light, 3.69 on dark surface (live) | both | `.geo-cell-item strong` `spatialcompute.css:174-179` (GeoStream, CatRisk, Overlay, Change, Response, Fleet lists); `.text-accent` override `addressstandardization.css:265-267` ("Try an example"); `.btn-outline-accent { color: var(--accent) }` `reversegeocoding.css:213-224`, `addressstandardization.css:247-257`; `.dropdown-item:hover` `site.css:321-324`; `.kpi-val` `network.css:103-108` (fails <768 px, where 1.2rem = 16.8 px); `profile.css:30,205,277` | replace with `var(--accent-text)` (4.99/5.23 light, 6.46/8.79 dark); delete the page-level button redefinitions |
| T7 | `#btnNewCollection` (`btn-primary` + `.theme-btn{color:inherit}`) | 3.63 / 4.29 (live) | both | `gis.css:200-203`; `StateExplorer/Index.cshtml:74` | delete `.theme-btn{color:inherit}`; use `.btn-accent` |
| T8 | State Explorer toast (dark `--text` on `text-bg-danger`) | 3.60 / 4.32 (live) | both | `gis.css:231-246` + `StateExplorer/Index.cshtml:207-215` | drop the page's static toast; use `showToast` |
| T9 | `.geo-cell-anomaly strong` (`--bs-danger` on 6% tint) | 4.16 / 2.57 (live) | both | `spatialcompute.css:186-193` | `--status-danger-text` |
| T10 | `.hero-desc` over `--hero-gradient` | 4.20 @360, 4.12 @768, 4.40 @1280 (live, sampled at real gradient positions) | light | `layout.css:161-166`; `base.css:17` | raise to `rgba(255,255,255,.95)`, or darken the gradient's green stop to `#2c7a53` (`--accent-on-fill`) in the light hero (option; visible) |
| T11 | `.contact-link` over the gradient | 3.97 / 3.71 @360, 4.36 @768 (live) | light | `layout.css:497-509` | `background: rgba(0,0,0,.18)` behind the link, or the same gradient fix as T10 |
| T12 | `<code>` (Bootstrap `#d63384`) on `--background` | 4.29 (live, Error page) | light | Bootstrap var | `code { color: var(--code-color) }` with `#b02a6e` / dark `#f783ac` |
| T13 | HomeFinder `#weightStatus .text-muted` | 4.39 (live) | dark | `homefinder.css:18-20` | token |
| T14 | `.project-hero` subtitle `.theme-text-muted` at the 0% stop | 4.42 (gradient worst case, live) | light | `gis.css:447-451` | lighten the hero tint (`rgba(26,54,93,.06)`), or use `--text` for the subtitle |
| T15 | Reverse Geocoding location-type badges: white on `#38a169`/`#3182ce`/`#b7791f`; `.type-other` white on dark `--text-muted` | 3.25 / 4.03 / 3.64 / ≈2.2 dark (static) | both | `reversegeocoding.css:129-150` | status fill tokens (white text ≥5.2) |
| T16 | Reverse/AddressStd `.btn-outline-accent` text | 3.25 (static) | light | as in T6 | delete the overrides |
| T17 | HomeFinder popup "Listed by" `#888` | 3.54 (static; popup only) | light | `HomeFinder/app.js:354` | `--text-muted` |
| T18 | `.dropdown-item:hover/:focus` `--accent` | 3.10 (static) | light | `site.css:321-324` | `--accent-text` |
| T19 | Profile accent texts (`.profile-back-link:hover`, `code`, `.profile-alert`) | 2.9–3.25 (static) | light | `profile.css:30,205,277` | `--accent-text`/status tokens |

**Size:** M (many files, each change is one line). It lands inside the per-file packages (§10).

### FE-A10 · P1 · Non-text contrast (1.4.11). 7 groups
- **N1:** Form-control boundaries use `--border` (`#e2e8f0`/`#4a5568`): **1.18–1.23 light, 1.59–2.17 dark** (`components.css:67-83`; `base.css:14,41`). Fix: a new `--input-border: #7a8699` (3.52/3.69 light, 4.43/3.25 dark) for `.form-control`, `.form-select`, `.form-check-input` and the switch track (`preferences.css:345-353`). This is visible; a decision is listed.
- **N2:** Modal `.btn-close` in dark mode on every gis.css page has `filter: none !important` (`gis.css:226-229`) over Bootstrap's dark filter: black glyph at 50% on `#2d3748` = **1.43** (live, HomeFinder). Fix: delete `gis.css:226-229`; Bootstrap's `[data-bs-theme=dark] .btn-close` filter then applies.
- **N3:** Faint focus rings, see FE-A01.
- **N4:** Chart and marker colours:
  - Clustering palette: 5 of 10 below 3:1 on the light background (`#bfef45` 1.28, `#fabed4` 1.50, `#42d4f4` 1.68, `#f58231` 2.47, `#3cb44b` 2.56; `Clustering/app.js:6-9`).
  - Fleet: `#f59f00` 2.04 light, and 5 of 10 below 3:1 on the dark surface (`Fleet/app.js:10-12`).
  - Response: `#74b816` 2.33 and `#f59f00` 2.04 light (`Response/app.js:12`).
  - Outage: `#f59f00` 2.04 and `#0ca678` 2.97 light (`Outage/app.js:20-25`).
  - The `--primary-light` series (CatRisk curve `CatRisk/app.js:350`, Change histogram `Change/app.js:408`) is **2.05 on dark**.
  - Unclustered points use `--bs-info` at 1.87 light (`Clustering/app.js:131`).
  - Fix: theme-aware palettes (§6.1, validated ≥4:1 on both backgrounds).
- **N5:** Toast close (white X) on `text-bg-warning`/`text-bg-info`: 1.63 / 1.96 (`toasts.js:43`). Fix: use `btn-close-white` only for success/danger.
- **N6:** Viewshed "hidden" swatch `#3a3a45` on the dark background: 1.45 (`spatialcompute.css:115-117`).
- **N7:** HomeFinder score-bar track `rgba(255,255,255,.08)` on the white card: 1.00 (`homefinder.css:242-248`).

**Size:** M.

### FE-A11 · P1 · HomeFinder: slider names overwritten with internal keys, assertive status spam, unformatted values (2.5.3, 4.1.2, 4.1.3)
- **Evidence:**
  - Live: `aria-label` reads `maxPrice`, `maxMonthlyBudget`, `minBedrooms`, `minBathrooms`, `minSqft`, `maxCommuteMin`, while the visible labels say "Max Price: $800000" etc. Weight sliders also get key-derived names.
  - Static: `HomeFinder/app.js:222-226` overwrites the markup's `aria-label`s (`HomeFinder/Index.cshtml:39,43,47,51`).
  - `#weightStatus` is `role="alert"` (`Index.cshtml:66`) and is rewritten through `innerHTML` on **every** slider `input` event (`app.js:155-199`), so every step is announced assertively.
- **Fix:**
  - Delete `app.js:222-226` and the static `aria-label`s, so the `<label for>` names each slider.
  - Set `aria-valuetext` from `PortfolioFormat` (e.g., "$800,000").
  - Make the visible label show the formatted value.
  - Change `#weightStatus` to `role="status"` and only update its text when validity flips.
- **Files:** `Projects/HomeFinder/Index.cshtml`, `js/HomeFinder/app.js`.
- **Risk:** low.
- **Verify:** `run().labelInName.length === 0`; slider `aria-valuetext` matches `/^\$[\d,]+$/`.
- **Size:** S.

### FE-A12 · P2 · Naming, text alternatives and ARIA misuse
- **Label in name:** GeoStream `<label for="geoSampleJson">Sample events</label>` versus `aria-label="Sample telemetry JSON"` (`GeoStream/Index.cshtml:47-48`). Remove the `aria-label`.
- **`aria-label` on a generic `div`** is ignored by AT (live): `#geoStage` (`GeoStream/Index.cshtml:68`), `#terrainGrid` (`Terrain/Index.cshtml:66`), `#viewshedGrid` (`Viewshed/Index.cshtml:72`), `#spinnerOverlay` (`BatchGeocoding/Index.cshtml:135`). Fix: `role="img"` plus a dynamic label, or `role="group"` for the grids; the spinner gets `role="status"`.
- **Charts:** every SVG `role="img"` label is static ("Cluster scatter plot") and never describes the result. Fix: `PortfolioCharts.describe(svg, summary)` sets `aria-describedby` on a visually-hidden summary updated after each render. PlantOps already does this (`PlantOperationsDashboard/Index.cshtml:79-81,95-97,111-113`); copy that pattern.
- **Range values:** Terrain azimuth has no visible value (`Terrain/Index.cshtml:47-48`). Add an `<output>` and `aria-valuetext` ("315°").
- **Icons:** Font Awesome `<i>` elements exposed to AT (no `aria-hidden`): 5–25 per demo page (live `faExposed`). Most are inside named buttons or headings (Chrome folds the PUA glyph into the name). Fix: add `aria-hidden="true"` to every decorative `<i class="fa-…">` (mechanical; Details pages ×18 and demo heroes).
- **Repeated "Demo"/"GitHub" links** (18 each on Home and `/Projects`): in-context purpose passes 2.4.4, but link lists are ambiguous. Add `<span class="visually-hidden"> {Title}</span>`.
- **Network:** "Technical details" link name differs from every other page's "View technical details for …" (`Network/Index.cshtml:24`). Use the common partial (FE-D07).
- **State Explorer:** the comparison buttons use emoji as their name ("🔎", `uiManager.js:565`). Collections render `<button>`s nested inside a `<button>` (`uiManager.js:379-390`, invalid interactive nesting). Collection edit uses `prompt()` (`uiManager.js:187-189`).
- **Size:** M.

### FE-A13 · P2 · Heading structure on demo pages (1.3.1, 2.4.6)
- **Evidence (live):** 15 of 17 demo pages have only the `<h1>` (outline `"1"`). Panel titles are `<strong>` inside `.card-header` (e.g., `Clustering/Index.cshtml:34,62`). HomeFinder is the exception (`1232`).
- **Fix:** panel titles become `<h2 class="h6 mb-0">`. Do it through the panel pattern (FE-D04).
- **Size:** S per page (inside the page batches).

### FE-A14 · P2 · Toasts and the confirm dialog (2.2.1, 4.1.2, 2.5.8)
- **Evidence:**
  - `showToast` auto-removes every toast after 4 s, errors included (`toasts.js:54-58`).
  - The close button is 18×18 (live) and white on warning/info.
  - The slide classes exist only in `homefinder.css:222-236` and `plantoperationsdashboard.css:163-176`.
  - `confirmDialog` has `role="dialog"` with no accessible name and focuses the **destructive** button (`toasts.js:71-123`, `:120`).
  - HomeFinder and PlantOps override `window.showToast` with `innerHTML` copies (`HomeFinder/app.js:668-710`, `dashboard.js:7-57`).
  - PlantOps loads toasts.js twice (`Index.cshtml:219`) and has an unused `#plantOpsToastContainer` (`:52-56`).
- **Fix:**
  - Danger/warning toasts persist until dismissed; success/info last 6 s and pause on hover/focus.
  - Close button min 24×24.
  - Move the toast CSS into components.css.
  - `confirmDialog`: `aria-labelledby` pointing at the message paragraph, and focus Cancel.
  - Delete the two override copies, the duplicate script tag and the unused container.
- **Files:** `js/toasts.js`, `css/components.css`, HomeFinder/PlantOps js and cshtml, `homefinder.css`, `plantoperationsdashboard.css`.
- **Size:** S.

### FE-A15 · P2 · Reduced motion and auto-motion
Covered by FE-A02 (smooth scroll) and FE-A03 (carousel). Everything else uses `transition`/`animation`, which
`preferences.css:135-159` neutralises (live: `.fade-in` opacity 1 under `motion=reduce`). The OS media-query path
was not emulated (§11).

### FE-A16 · P3 · Scrollable regions and live-region hygiene
- `.result-list` (`max-height:320px; overflow:auto`, `spatialcompute.css:155-158`), `.route-steps-list` (`network.css:119-124`) and `.comparison-panel` are scrollers with no focusable content. They are keyboard-scrollable only in Chrome ≥130. Add `tabindex="0" role="region" aria-label`.
- `#fleetRouteList` and `#responseStationList` are `aria-live="polite"` *and* their pages call `announce()`, so screen readers announce twice. Keep one.
- **Size:** S.

---

## 5. Findings: design system and consistency

### FE-D01 · P1 · Token gaps cause hard-coded colours and dark-mode drift
Proposed additions to `base.css`, with values validated by `contrast.py` and both themes given:

| Token | Light | Dark | Validated |
|---|---|---|---|
| `--focus-ring` | `#2c7a53` | `#68d391` | 4.99–5.23 / 6.46–8.79 (≥3) |
| `--focus-ring-on-dark` | `#ffffff` | `#ffffff` | on hero/footer |
| `--input-border` | `#7a8699` | `#7a8699` | 3.52/3.69 · 4.43/3.25 (≥3) |
| `--accent-rgb` | `56,161,105` | `56,161,105` (unchanged, behaviour-preserving) | replaces 29 `rgba(56,161,105,x)` |
| `--status-success-text` / `-fill` | `#2c7a53` / `#2c7a53` | `#68d391` / `#2c7a53` | text ≥4.99; white on fill 5.23 |
| `--status-danger-text` / `-fill` | `#c53030` / `#c53030` | `#fc8181` / `#c53030` | text 5.22–5.47 / 4.91–6.68; white on fill 5.47 |
| `--status-warning-text` / `-fill` | `#975a16` / `#d69e2e` (dark text) | `#f6c26b` / `#d69e2e` | text 5.29–5.54 / 7.33–9.98; `#1a202c` on fill 6.83 |
| `--status-info-text` / `-fill` | `#2b6cb0` / `#2b6cb0` | `#90cdf4` / `#2b6cb0` | text 5.17–5.42 / 6.98–9.50; white on fill 5.42 |
| `--status-*-soft` | `rgba(var(--status-*-rgb), .12)` | `.18` | tints behind text |
| `--chart-1…8` | `#2563eb #c2410c #15803d #9333ea #0f766e #be123c #a16207 #4f46e5` | `#60a5fa #fb923c #4ade80 #c084fc #2dd4bf #fb7185 #facc15 #818cf8` | every colour ≥4.70 light and ≥4.02 dark on both backgrounds |
| `--chart-series` | `#2c5282` | `#63b3ed` | 7.60 / 5.25–7.15 |
| `--on-dark`, `--on-dark-muted` | `#fff`, `rgba(255,255,255,.9)` | same | replaces the layout.css white tints |
| `--code-color` | `#b02a6e` | `#f783ac` | replaces Bootstrap `#d63384` (4.29) |

Dark overrides for `--primary-light` are not needed once charts use `--chart-series`.
**Files:** `base.css`. **Size:** S. **Verify:** `contrast.py` re-run, plus `run()` light/dark fails = 0.

### FE-D02 · P2 · Replace the 387 hard-coded literals with tokens
The per-file plan is §2.4. The rules:
- Drop `var(--x, #fallback)` fallbacks (35).
- Status colours become `--status-*`.
- Chart and marker colours come from `PortfolioCharts.palette()`.
- On-gradient whites become `--on-dark*`.
- The homefinder/gis dark overrides that duplicate token values are deleted. They also break high-contrast mode, which re-points the tokens (`preferences.css:98-114`).
- Exempt with a comment: the Google button brand colours (`site.css:142-170`), the high-contrast palette, and the white-on-accent text.

**Verify:** `colors.py` count ≤ 40 (the exemptions). **Size:** L in total, split across the page batches.

### FE-D03 · P1 · One button system
- `.btn-accent`/`.btn-outline-accent` are redefined in `reversegeocoding.css:197-224` and `addressstandardization.css:228-262`; the redefinition uses failing `--accent` text.
- State Explorer uses off-brand Bootstrap variants: `btn-outline-warning`, `btn-outline-secondary`, `btn-outline-success` (`Index.cshtml:49-51`), `btn-primary` (`:74,183`), `bg-warning` header (`:101`), `btn-outline-info` (`uiManager.js:332`).
- Batch and HomeFinder use `btn-outline-secondary` for secondary actions.
- `.theme-btn {color:inherit}` (`gis.css:200-203`) breaks Bootstrap variants.

**Fix:**
- One definition in `site.css` built on Bootstrap's variable API (FE-A01).
- Secondary actions use `.btn-outline-accent`; neutral actions use `.btn-outline-secondary` themed by tokens.
- Delete the page redefinitions and `.theme-btn`.

The State Explorer recolouring (yellow/green to brand) is a **visible change**, so it is listed as a decision.
**Size:** S.

### FE-D04 · P2 · One panel/card pattern
Six variants exist today:
- `.card.spatial-card` (12 pages)
- `.card.theme-card` + `.theme-card-header` (Batch, AddressStd, StateExplorer, PlantOps)
- `.theme-surface.rounded.p-3` (HomeFinder, Details)
- `.rg-result-panel` (ReverseGeocoding)
- `.network-panel` (Network)
- `.project-card` / `.skill-card` / `.about-card` / `.resume-card` (home)

**Fix:**
- Promote `.spatial-card` to `.panel` in `components.css` (surface, border, radius .85rem, shadow) with `.panel__header` (containing `<h2 class="h6">`) and `.panel__body`.
- Keep `.theme-card` as an alias for one release.
- Move `.theme-surface` from gis.css to components.css so the 18 Details pages can drop gis.css (11.8 KB each).
- Delete dead CSS: `.feature-card` (gis.css `:1-21,399-421`, unused), `.note-item`, `.stat-badge`, `.custom-modal-content`, `.spinner-gis`, `.projects-back-link`, `.login-back-row` (all unused per grep).

**Size:** M.

### FE-D05 · P2 · One KPI tile
Four implementations exist:
- `spatialcompute.css:123-142` (`.kpi-card` / `.kpi-value`, `--accent` value)
- `batchgeocoding.css:80-122` (same class names, different sizes and colours)
- `network.css:89-116` (`.kpi-tile` / `.kpi-val`)
- State Explorer `.theme-stat .h4` with Bootstrap text colours (`Index.cshtml:123-148`)

PlantOps shows KPIs as badges (`Index.cshtml:59-65`).

**Fix:** `.kpi` / `.kpi__value` / `.kpi__label` in components.css. The value colour is `var(--text)`, with `.kpi--accent` using `--accent-text` and `.kpi--success/--danger` using status tokens. Use `<dl>` semantics or `aria-describedby` from value to label. **Size:** M.

### FE-D06 · P2 · One legend
Legends are implemented 5 different ways:
- `Clustering/app.js:153-168` (badge built with inline `style.*`)
- `CatRisk`, `Fleet`, `Overlay` (`style="width:0.7rem;…"` strings inside `innerHTML`)
- `Outage` (inline `style="background:…"` string)
- `Viewshed/Index.cshtml:74-78` (static swatch classes)
- `Network/Index.cshtml:78-87` (swatches inside a label)

**Fix:** `PortfolioCharts.legend(container, [{label, color}])` builds `<ul class="legend"><li><span class="legend__swatch" style="--swatch: …">` via the CSSOM (`el.style.setProperty('--swatch', c)`, which is CSP-safe). Add `.legend` CSS to components.css. **Size:** S.

### FE-D07 · P2 · One page header and layout
- `.project-hero` markup is copy-pasted in 17 Index pages (≈16 lines each). Network uses `.network-hero` (`container-fluid px-4`, `h1.h3`).
- The Details header is a different pattern (h1 plus a "Back to Demo" button).
- Layouts:
  - **A:** controls `col-lg-4` + results `col-lg-8`, used by 11 spatial pages and Batch.
  - **B:** a 3-column `container-fluid` `col-lg-3/6/3` map app (HomeFinder, StateExplorer).
  - **C:** map `col-lg-8` + panel `col-lg-4` (ReverseGeocoding).
  - **D:** input on top, results below (AddressStd).
  - **E:** full-bleed app shell (Network, `network.css:6-38`, with `height: calc(100vh - 62px)` while the navbar is actually 75 px, live).
- **Fix:**
  - `Pages/Shared/_ProjectHero.cshtml`, a partial taking `(Title, IconClass, Subtitle, Badges[], DetailsUrl, DetailsLabel)` via a tuple model (same precedent as `_PreferencesControls.cshtml:11-17`), used by all 17 Index pages plus Network.
  - Standardise on layout A for compute demos. Document B, C and E as named exceptions (`.layout-map-app`, `.layout-app-shell`).
  - Network: replace the hard-coded 62 px with `height: calc(100dvh - var(--nav-h))`, where `--nav-h` is set by a ResizeObserver in navigation.js, or use a flex column on `body`.
- **Size:** M.

### FE-D08 · P2 · One set of loading, empty and error states
- **Today:**
  - 12 pages use `.alert-danger.d-none[role=alert]` plus their own `showAlert`.
  - Batch, Reverse and AddressStd use dismissible `alert-warning fade d-none` plus toasts.
  - HomeFinder, StateExplorer, PlantOps, GeoStream, AddressStd and Batch use toasts.
  - Spinners: `spinner-border` (Batch, Reverse, AddressStd, PlantOps), `fa-spinner` (Network), none on the spatial pages (only disabled buttons).
  - Empty-state copy varies.
- **Fix:**
  - Inline errors go in the existing `role="alert"` box via `PortfolioUi.inlineAlert(el)`.
  - Transient success uses `showToast` (one implementation).
  - Loading: `PortfolioUi.busy()` sets `aria-busy` and shows a `.state-loading` row with a spinner and the text "Computing…" inside the results panel.
  - Empty: a `.state-empty` paragraph.
- **Size:** M (JS adoption happens in the batches).

### FE-D09 · P2 · Number and unit formatting
See §2.7 for the evidence. The proposal is `PortfolioFormat` (§6.2), with these conventions as defaults (a decision is listed):
- thousands separators always;
- percentages to 1 decimal;
- distances `61.4 km` (one decimal, NBSP before the unit);
- durations `2.4 min` (one decimal);
- money `$832.9M` compact / `$800,000` full;
- coordinates 5 decimals;
- booleans "Yes/No" everywhere, including Network's native flag;
- enum names humanised (`ASTAR` → `A*`, `Tri` → `Triangulate`, `Shade` → `Hillshade`).

**Size:** M.

### FE-D10 · P2 · Dark-mode overrides that bypass tokens and fight each other
- `homefinder.css:5-143`: hard-coded dark values. Also `body.dark-mode .form-range { background: var(--accent) }` paints a **green box behind every slider** (live: `rgb(56,161,105)`). Note that `:118-128` declares the rule twice with different values.
- `gis.css:306-346`: `#featureDetails` forced to `#ffffff !important` in dark. Dark placeholders are forced to `#ffffff`, so they look like typed text (`:327-333`). `#searchInput` text is forced to `--text-muted` (`:348-352`).
- `batchgeocoding.css:192-301` and `plantoperationsdashboard.css:3-31,198+` repeat `components.css:352-436`.
- Invalid declaration at `plantoperationsdashboard.css:16`.
- **Fix:** delete these and rely on tokens plus `components.css`.
- **Verify:** live dark `run()` contrast fails = 0; the `.form-range` background is transparent.
- **Size:** M.

### FE-D11 · P3 · Dark map tiles (option)
Leaflet tiles stay bright OSM in dark mode (live: `filter: none`). Option: `body.dark-mode .leaflet-tile-pane { filter: invert(1) hue-rotate(180deg) brightness(.95) contrast(.9); }`, or CARTO dark tiles. This is a **visible design change**, so it is listed as a decision. The ArcGIS pages already swap the Esri theme, but their basemaps stay light.

### FE-D12 · P3 · Encoding and dead code
- `navigation.js:21` and `StateExplorer/mapManager.js:32` contain cp1252 bytes (0x97), which violates `.editorconfig`.
- Mojibake `â”€` comments appear in `batchgeocoding.css`, `reversegeocoding.css` and `addressstandardization.css`.
- Dead code:
  - `copyEmailContact` (`Index.cshtml:979-985`)
  - `_ValidationScriptsPartial.cshtml` (referenced nowhere)
  - classes without CSS: `project-info-btn` (18 uses), `theme-body`, `theme-body-inverse`, `tracking-wide`, `ls-wide`, `login-divider`
  - `navbar-scrolled` (added by `navigation.js:45-56`, no CSS)
  - `PlantOperationsDashboard/Index.cshtml:50` `<div>` never closed (balance check; it is the only unbalanced page)

### FE-D13 · P3 · Cross-area notes (not mine; pass on)
- Home and `/Projects` copy says "designed around a **SIMD-friendly** native terrain kernel" (`Index.cshtml:283,660`; `Projects/Index.cshtml:178`). This conflicts with the "no SIMD claims" rule; it belongs to the content/docs owner.
- 8 Details pages (Clustering, Viewshed, Overlay, CatRisk, Change, Outage, Response, Fleet) have 6 H2 sections instead of the documented 8-section order (live heading outline `1222222`).

---

## 6. Proposals: unified pattern set and shared modules

All modules are vanilla, classic scripts that expose one namespace each on `window`, matching `api-fetch.js` and
`preferences.js`. There is no build step. Each is ≤150 lines.

### 6.1 `wwwroot/js/charts.js` → `window.PortfolioCharts`
```js
PortfolioCharts.token(name)            // getComputedStyle(document.body).getPropertyValue('--' + name).trim()
PortfolioCharts.palette(n)             // ['--chart-1'..'--chart-8'] resolved for the current theme (canvas/Leaflet need concrete colours)
PortfolioCharts.status(kind)           // 'success'|'danger'|'warning'|'info' → resolved --status-*-fill
PortfolioCharts.onThemeChange(cb)      // window 'themeChanged' listener; Leaflet/canvas layers re-style themselves
PortfolioCharts.svg.el(tag, attrs)     // createElementNS + setAttribute loop
PortfolioCharts.svg.text(svg, x, y, content, { size, anchor, fill, rotate })   // = today's appendText ×4
PortfolioCharts.svg.line(svg, x1, y1, x2, y2, { stroke, width })              // = today's appendLine ×4
PortfolioCharts.svg.clear(svg)         // replaceChildren() (no innerHTML)
PortfolioCharts.scale.linear([d0,d1],[r0,r1]) / .log(...)                     // = xFor/yFor ×4
PortfolioCharts.ticks(min, max, count) // nice ticks for the axis code in CatRisk/Response/Fleet/Change
PortfolioCharts.mix(hexA, hexB, t)     // = mixHex ×2
PortfolioCharts.rng(seed)              // seeded LCG (Clustering/Overlay/Viewshed, byte-identical sequence: seed*48271 % 2147483647)
PortfolioCharts.legend(container, items)   // FE-D06
PortfolioCharts.describe(svgOrCanvas, summaryText)   // visually-hidden summary + aria-describedby (FE-A12)
```
SVG pages keep `fill="var(--token)"`, which is already theme-aware (live: the computed `fill` resolves). Leaflet's
canvas renderer needs `palette()`/`token()`, plus re-styling on `themeChanged`.

**Adopters:**
- CatRisk, Change, Response and Fleet (svg, scale, ticks)
- Clustering, Overlay and Viewshed (rng, palette, legend)
- Outage (palette, legend, projection)
- Network, Response and Fleet (canvas marker colours)
- AddressStd and PlantOps (palette only, while D3 stays)

### 6.2 `wwwroot/js/format.js` → `window.PortfolioFormat`
```js
const nf = (o) => new Intl.NumberFormat('en-US', o);   // cached per options key
PortfolioFormat.number(v, { decimals = 0 } = {})
PortfolioFormat.percent(v, { decimals = 1, fraction = false } = {})   // 0.941 or 94.1 → "94.1%"
PortfolioFormat.money(v, { compact = false } = {})                     // "$800,000" | "$832.9M"
PortfolioFormat.unit(v, unit, { decimals = 1 } = {})                  // "61.4\u00a0km", "2.4\u00a0min"
PortfolioFormat.clock(minutes)                                         // Fleet formatClock
PortfolioFormat.coord(lat, lng, decimals = 5)
PortfolioFormat.yesNo(b)                                               // native flag everywhere
```
**Adopters:** every page that displays numbers (§2.7). It replaces `formatMoney`/`formatMoneyShort` (CatRisk
`:424-439`), `formatClock` (Fleet `:478`), `formatCount` (Response `:532`), `formatCurrency` (PlantOps orders `:17`)
and `formatBytes` (Batch `:65`).

### 6.3 `wwwroot/js/ui.js` → `window.PortfolioUi` (loaded globally after toasts.js)
```js
PortfolioUi.announce(msg)                       // single copy of the 4 announce() functions (#a11yAnnounce, clear + 40 ms)
PortfolioUi.inlineAlert(el) → { show(msg), hide() }   // replaces showAlert/hideAlert ×12 (textContent only)
PortfolioUi.busy({ buttons, region, label }) → done() // aria-busy, .is-busy, aria-disabled + click guard; keeps focus (FE-A04)
PortfolioUi.runAction({ trigger, alert, region, request, render, summary })  // the copy-pasted run() flow:
     // hide alert → busy → await request() → render(result) → announce(summary(result))
     // catch → alert.show(err.message) → finally done()
PortfolioUi.escapeHtml(s)                       // ONE vetted helper (coordinate with security; or their DOM builder)
PortfolioUi.el(tag, attrs, ...children)         // tiny DOM builder for the innerHTML rewrites (if security agrees)
```
**Adopters:** all 17 demo `app.js` files. `toasts.js` stays as the one toast implementation; FE-A14 changes are made there.

### 6.4 `api-fetch.js` changes (same globals)
- `apiFetch(url, { redirectOn401 = true, timeoutMs } = {})`. State Explorer collections pass `false` (FE-A07). `timeoutMs` uses an `AbortController`.
- `getApiError`: read `body.error || body.detail || body.title`. On `429`, return "Too many requests. Please wait a minute and try again." (the `"expensive"` policy allows 15/min/IP; the demos are click-heavy).
- Network adopts it: delete the local `apiPost` (`Network/app.js:403-412`) and the raw `fetch` (`:87`). These are the only api-fetch bypasses in the codebase.

### 6.5 `wwwroot/js/leaflet-setup.js` → `window.PortfolioMap` (loaded only after leaflet.js)
```js
PortfolioMap.create(id, { center = [34.0565, -117.1850], zoom = 13, label }) // L.map(...preferCanvas...) + OSM tileLayer + attribution + maxZoom 19
     // sets aria-label on the container from `label`; keeps Leaflet keyboard pan/zoom;
     // optional dark tile class toggled on themeChanged (FE-D11 decision)
```
**Adopters:** Network (`:38-48`), Response (`:49-59`) and Fleet (`:40-50`). Decide `role="application"` (currently on all three containers) once: keep it only if keyboard pan/zoom is documented in the hint text.

### 6.6 `wwwroot/js/esri-theme.js` (FE-A08)
Replaces the two inline loaders and adds the theme to State Explorer and Reverse Geocoding.

### 6.7 CSS pattern set (where each piece lives)
| Pattern | Lives in | Notes |
|---|---|---|
| Tokens (§5 FE-D01) | `base.css` | dark overrides beside them |
| Focus ring, reduced motion, high contrast | `preferences.css` | FE-A01 overrides after Bootstrap |
| `.btn-accent`, `.btn-outline-accent` (Bootstrap variable API) | `site.css` | delete the page copies |
| `.panel`, `.kpi`, `.legend`, `.state-loading`, `.state-empty`, `.result-list` region, toasts, status utilities, `.theme-surface` | `components.css` | `.theme-card` and `.spatial-card` stay as aliases during migration |
| `.project-hero` (one copy) | `components.css` | delete `gis.css:446-456`, `spatialcompute.css:1-5` and `network.css:15-19` |
| Page layout helpers `.layout-map-app`, `.layout-app-shell` | `layout.css` | |
| Page-specific only | `<page>.css` scoped under `.<page>-page` | must not redefine global components |

---

## 7. FE-C01 · Duplicated carousel markup (`Pages/Index.cshtml`)

**Facts (static plus a script diff):**
- 18 real cards (`Index.cshtml:105-488`) plus 18 duplicates (`:490-865`, 27,952 bytes).
- After removing `id`, `aria-labelledby`, `aria-hidden` and `tabindex`, the **duplicates are content-identical to the real cards** (script diff, 0 differences).
- The duplicates carry `aria-hidden="true"` and `tabindex="-1"` on each link (correct; live `focusableInAriaHidden: []`).

**JS that depends on the duplicate: none.**
- The loop is CSS only: `.projects-track { animation: scrollProjects 45s linear infinite }` with `translateX(-50%)` (`layout.css:345-361`), which assumes the track is exactly two identical halves.
- `animations.js:38-45` targets `.projects-grid` (the `/Projects` page), explicitly *not* the carousel.
- No other JS references `projects-track`, `projects-slider` or the card ids.
- Note: the duplication must remain *rendered* (two passes) for the CSS loop to work; only the *source* is deduplicated.

**Proposal (behaviour-preserving):**
1. New `Pages/Shared/_ProjectCard.cshtml` with model `(ProjectInfo Card, bool IsCopy)`, written as a value tuple, so no new model type is needed in `Portfolio.Web`. This keeps the "models live in Portfolio.Common" rule intact. The `_PreferencesControls.cshtml:11-17` tuple-array precedent covers it.
   ```cshtml
   @model (string Id, string Icon, string Title, string Desc, string[] Stack, string Demo, string Details, string DetailsLabel, bool IsCopy)
   <article class="project-card" aria-labelledby="@(Model.IsCopy ? null : "project-" + Model.Id)"
            aria-hidden="@(Model.IsCopy ? "true" : null)" inert="@Model.IsCopy">
     <div class="project-image" aria-hidden="true"><i class="fa-solid @Model.Icon fa-3x"></i></div>
     <div class="project-info">
       <h3 id="@(Model.IsCopy ? null : "project-" + Model.Id)" class="project-title">@Model.Title</h3>
       <p class="project-desc">@Model.Desc</p>
       <div class="project-stack" aria-hidden="true">@foreach (var s in Model.Stack) { <span>@s</span> }</div>
       <div class="mt-3">
         <a class="btn btn-outline-accent btn-sm me-2" href="@Model.Demo" tabindex="@(Model.IsCopy ? "-1" : null)">Demo<span class="visually-hidden"> of @Model.Title</span></a>
         ...
   ```
   Razor omits attributes whose value is `null` and renders `inert` only when `true`.
2. In `Index.cshtml`, declare a tuple array of the 18 projects in an `@{ }` block (or in a `_FeaturedProjects.cshtml` partial). Then `@for (var pass = 0; pass < 2; pass++) { foreach (var p in projects) { <partial name="_ProjectCard" model="(…, pass == 1)" /> } }`.
3. Optional improvement: `inert` on the copies replaces the per-link `tabindex="-1"`.
4. **`/Projects` (`Projects/Index.cshtml`) is a third copy that differs** (script diff):
   - the card order differs;
   - 4 stacks differ (State Explorer, Batch, Reverse, AddressStd);
   - 8 descriptions differ (Clustering, Viewshed, Overlay, CatRisk, Change, Outage, Response, Fleet);
   - 7 Demo URLs omit `/Index`.

   Sharing one catalog would change visible copy, so this is a **decision**. The default is to share the partial (layout via a `Variant` parameter, `h2` vs `h3`) but keep two description/stack fields, so no copy changes until the owner picks one.

**Files:** `Pages/Index.cshtml`, new `Pages/Shared/_ProjectCard.cshtml`, optionally `Pages/Projects/Index.cshtml`.
**Verify:**
- `document.querySelectorAll('.projects-track .project-card').length === 36`.
- The serialized text of `[aria-hidden=true]` cards (with ids/aria stripped) equals the first 18.
- No `id` is duplicated (`run().dupIds`).
- Animation `currentTime` advances, and the seam check from FE-A03 passes.
- The HTML of `/` is unchanged apart from attribute order (diff `curl` output before and after, normalised).

**Size:** S (the markup) + M (FE-A03 behaviour).

---

## 8. Performance

### FE-P01 · P1 · Static files have no `Cache-Control` (local and production)
- **Evidence:** local `curl -D -` on `/css/base.css?v=…` and `/lib/bootstrap/css/bootstrap.min.css` returns only `ETag`/`Last-Modified`. Production returns the same plus `cf-cache-status: DYNAMIC`. Code: `Program.cs:575` is `app.UseStaticFiles()` with no options.
- **Fix:**
  ```csharp
  app.UseStaticFiles(new StaticFileOptions {
      OnPrepareResponse = ctx => {
          var h = ctx.Context.Response.Headers;
          h.CacheControl = ctx.Context.Request.Query.ContainsKey("v")
              ? "public,max-age=31536000,immutable"   // asp-append-version URLs
              : "public,max-age=3600";                // unversioned (lib/, images, fonts)
      }});
  ```
  - This keeps the pipeline order (same middleware, same position).
  - `MapStaticAssets()` (.NET 9+) would also add fingerprinting and compression, but it moves static files behind routing, the rate limiter, `AnonymousUserMiddleware` (which sets a cookie) and auth. That changes the documented pipeline, so I do **not** recommend it.
  - Compression middleware is unnecessary in production (Render/Cloudflare already sends `br`).
- **Overlap:** the complexity agent is splitting `Program.cs` into extension methods. Put this in `UsePortfolioStaticFiles()` or coordinate the order.
- **Verify:** `curl -sI http://localhost:<port>/css/base.css?v=x` shows `Cache-Control: public,max-age=31536000,immutable`.
- **Size:** S.

### FE-P02 · P1 · 53 page-level stylesheet links lack `asp-append-version`, and all page CSS sits in `<body>`
- **Evidence:** 53 `<link rel="stylesheet" href="~/…">` tags in page `.cshtml` files have no version (only `Network/Index.cshtml:8` has one). The 3 page-level vendored scripts (d3 ×2, typeahead) also have none. Without versioning, FE-P01's long cache must **not** apply to them; with versioning they can be immutable.
- **Fix:** add `@await RenderSectionAsync("Styles", required: false)` in `_Layout.cshtml` after `preferences.css` (line 96). Each page moves its links into `@section Styles { <link … asp-append-version="true" /> }`. The cascade order is unchanged because page CSS still loads after the global CSS.
- **Verify:** `[...document.querySelectorAll('main link[rel=stylesheet]')].length === 0`, and every local stylesheet href has `?v=`.
- **Size:** S (mechanical, per page).

### FE-P03 · P1 · jQuery + DataTables load on every page; only 3 pages use them
- **Evidence:**
  - `_Layout.cshtml:85-86,172-174` loads them globally on all 45 Razor pages.
  - Usage by grep: `$(`/jQuery in AddressStandardization (28), BatchGeocoding (2) and the 4 PlantOps scripts; `DataTable(` in BatchGeocoding and PlantOps only; none in the layout scripts.
  - The cost is 212,000 B raw on the 42 pages that do not need it.
- **Fix:** remove the 2 CSS and 3 JS tags from `_Layout`. Add them to `@section Styles/Scripts` of AddressStandardization (jQuery only), BatchGeocoding (jQuery + DataTables) and PlantOps (jQuery + DataTables).
- **Verify:** on Home, `typeof window.jQuery === 'undefined'`. On Batch/PlantOps, `typeof $.fn.DataTable === 'function'`, and their console is free of errors.
- **Size:** S.

### FE-P04 · P2 · Unused or double loads
- typeahead.bundle (45 KB) and typeahead.css load on State Explorer but are never called (grep: no `typeahead(`/`Bloodhound`). Remove them; also drop the `.typeahead`/`.tt-*` rules in `gis.css:66-157,264-302,327-365`.
- `toasts.js` is loaded twice on PlantOps (`Index.cshtml:219`).
- D3 (280 KB raw / 92 KB gz) is used for small charts on AddressStd (a confidence meter and a sparkline) and PlantOps (3 charts). Option: re-implement with `PortfolioCharts` SVG helpers (decision; M). Default: keep, but add `defer`.

### FE-P05 · P2 · No `defer`
All 11 layout scripts and every page script are blocking end-of-body scripts.
- **Fix:** add `defer` to every external script in `_Layout` and the page sections, keeping order.
- Two inline scripts read state before paint and must stay inline (and so get a CSP hash/nonce): `_Layout.cshtml:8-35` and `:99`. Leave them non-deferred.
- Page scripts that use `DOMContentLoaded` still work under `defer` (deferred scripts run before `DOMContentLoaded`).
- The ArcGIS AMD loader plus app.js keep order with `defer`.
- `api-config.js` must stay before `api-fetch.js` (order is preserved).
- **Risk:** low.
- **Verify:** every page's console is clean, and each demo's primary action still works.
- **Size:** S.

### FE-P06 · P2 · Font Awesome weight
`fa-solid` (158 KB) and `fa-brands` (119 KB) woff2 files download on every page. The brands font serves 2 footer glyphs plus 2 contact icons. Option (decision; M–L): an SVG sprite for the ~70 icons in use, or a subset. Default: no change, since the files are cached after the first visit once FE-P01 lands.

### FE-P07 · P3 · 10.5 MB of unreferenced vendor files in `wwwroot/lib` (shipped in the Docker image)
| Path | Size |
|---|---|
| `lib/bootstrap/dist/**` (44 files) | 8.68 MB |
| `lib/jquery/dist/**` | 0.92 MB |
| `lib/fontawesome/webfonts/*.ttf` (fallbacks never requested by modern browsers) | 0.72 MB |
| `lib/jquery-validation` + `-unobtrusive` (only `_ValidationScriptsPartial.cshtml`, which is referenced nowhere) | 0.18 MB |
| `lib/typeahead` (after FE-P04) | 47 KB |
| `fontawesome/webfont/README.txt`, `webfonts/PLACEHOLDER.txt` | |

Grep finds no references to any of these. **Fix:** delete them (git). **Verify:** a full page sweep shows no 404s in `read_network_requests`. **Size:** S.

### FE-P08 · P3 · Home HTML weight
76.8 KB raw (8.7 KB br/gz), of which 28 KB is the duplicate card source. FE-C01 deduplicates the *source* but must keep the rendered duplicates, so the HTML size stays the same. A client-side clone (JS) would cut 28 KB but adds a JS dependency to the loop. **Not recommended**, since the size is negligible after compression.

---

## 9. Overlaps and sequencing with other agents

**Security (`innerHTML`/CSP):**
- My shared-helper adoption edits the same functions the security agent will rewrite: `escapeHtml` ×10, legend/list/table builders, toast `innerHTML` in HomeFinder `:668-710` and dashboard.js `:7-57`, and `StateExplorer/uiManager.js` (17 sites).
- **Recommendation:** the per-page batches (FE-WP6x) carry *both* the security agent's per-file `innerHTML` fixes and my helper adoption, in one package per file set. `PortfolioUi.escapeHtml` / `PortfolioUi.el` is the single vetted helper (security agent to approve its implementation in FE-WP2).

**CSP blockers that my packages remove:**
- the inline Esri loaders (`HomeFinder/Index.cshtml:202-215`, `PlantOperationsDashboard/Index.cshtml:15-28`) → FE-WP2/WP6c;
- dead `copyEmailContact` (`Index.cshtml:979-985`) → FE-WP3;
- 44 markup `style=` and 18 JS-string `style=` → classes, or CSSOM `el.style.setProperty` (CSP-safe), in the batches.

**Must stay inline** (need a nonce or hash): `_Layout.cshtml:8-35` (theme pre-paint) and `:99`.

**External origins the CSP must allow:**
- `js.arcgis.com` (script and style; Esri themes),
- `unpkg.com` (Leaflet; SRI already present),
- `*.tile.openstreetmap.org` (img),
- `developers.google.com` (Login logo img; could be inlined as SVG),
- ArcGIS basemap and tile hosts.

**Complexity agent:**
- The 600–700-line JS files (HomeFinder 731, StateExplorer/uiManager 669, StateExplorer/app 632, Response 594) are the same files as my batches C and B. Put the JS complexity split in the same package or sequence it after.
- `Program.cs` split: FE-P01 lives in the static-files extension.

**Docs/content:** FE-D13 (SIMD copy; Details section count).

---

## 10. Work packages (ordered; lanes run in parallel on disjoint files)

Every package must pass the verification protocol (§11) at 360/768/1280 in light and dark before commit.

```
Phase 1 (parallel):  FE-WP1 core CSS   |  FE-WP2 shared JS modules  |  FE-WP3 home page
Phase 2 (after WP1+WP2, parallel):     FE-WP4 layout & loading  |  FE-WP5 family stylesheets (gis.css, spatialcompute.css)
Phase 3 (after WP4+WP5, parallel):     FE-WP6a spatial-1 | FE-WP6b spatial-2 | FE-WP6c map/form pages | FE-WP6d Network+PlantOps | FE-WP6e site & Details
Phase 4:                               FE-WP7 dead assets & final sweep
```

| WP | Goal | Exact files | Steps (finding IDs) | Acceptance | Risk / rollback | Size |
|---|---|---|---|---|---|---|
| **FE-WP1** core CSS | tokens, focus rings, button variant API, form borders, toast CSS, dedupe | `css/base.css`, `css/preferences.css`, `css/site.css`, `css/components.css`, `css/utilities.css` | D01 tokens; A01 focus overrides plus the button variable API; A10-N1 `--input-border`; A14 toast CSS (moved in); D04/D05/D06 `.panel`/`.kpi`/`.legend`/states (new, additive); dedupe: delete `utilities.css:4-16` (`.fade-in`; the `components.css:4-13` copy is the one that wins today) and `utilities.css:21-33` (identical to `components.css:118-130`); T18 `site.css:321-324`; T12 code colour | `focusIndicators()` none/weak = 0 on Home, Response, Batch; contrast light/dark fails do not increase; visual: buttons look identical at rest | Low; revert the commit | M |
| **FE-WP2** shared JS | new modules, api-fetch/toasts upgrades | new `js/ui.js`, `js/format.js`, `js/charts.js`, `js/leaflet-setup.js`, `js/esri-theme.js`; `js/api-fetch.js`; `js/toasts.js` | §6.1–6.6; A07 (`redirectOn401` option only); A14 (toast persistence, 24 px close, `confirmDialog` name/focus) | Each module loads without error on a blank page; unit-style checks in the console (`PortfolioFormat.money(832900000,{compact:true})==='$832.9M'`; `PortfolioCharts.rng(1337)` first 5 values equal the current Clustering sequence) | Low (additive); revert | M |
| **FE-WP3** home page | carousel dedupe and a11y, skip link, hero contrast | `Pages/Index.cshtml`, new `Pages/Shared/_ProjectCard.cshtml`, new `js/carousel.js`, `css/layout.css`, `js/navigation.js` | C01; A03; A02; T10/T11; hamburger focus (`layout.css:78-81`); delete `layout.css:567-581` (duplicates base.css) and `layout.css:223-239` (`.skill-tag` duplicate; components.css wins); optionally merge the nav underline (`layout.css:36-54` + `preferences.css:443-463`), keeping today's 80% width; remove dead `copyEmailContact` | FE-A02/A03/C01 verify blocks pass; `run()` PASS; `grad.js` hero/contact ≥4.5 at 360/768/1280 | Medium (hero area); revert | M |
| **FE-WP4** layout and loading | asset loading and perf | `Pages/Shared/_Layout.cshtml`, `Program.cs` (static files only, coordinate with the complexity split), `Projects/AddressStandardization/Index.cshtml`, `Projects/BatchGeocoding/Index.cshtml`, `Projects/PlantOperationsDashboard/Index.cshtml` (Scripts/Styles sections only), `Projects/StateExplorer/Index.cshtml` (typeahead removal only) | P01, P02 (the Styles section in the layout), P03, P04 (typeahead, PlantOps double toasts), P05 (`defer`); add the `ui.js`/`format.js` tags to the layout | Console clean on all pages; Batch/PlantOps DataTables work; `Cache-Control` header present; no jQuery on Home | Low–medium (script order); revert | S |
| **FE-WP5** family stylesheets | gis.css and spatialcompute.css clean-up | `css/gis.css`, `css/spatialcompute.css` | A01 (delete `gis.css:259-262`, `:281-287` outline none, `:417-421`); N2 (delete `gis.css:226-229`); T7 (delete `.theme-btn`, `:200-203`); D10 (`#featureDetails`/placeholder `#fff`, `:306-365`); typeahead rules; `.project-hero` single copy; T6/T9 (`.geo-cell-item strong` → `--accent-text`, anomaly → status token); N6 swatch; `.kpi-*` → `.kpi` aliases; dead `.feature-card`/`.note-item`/`.stat-badge` | `run()` PASS on 2 gis pages + 2 spatial pages; dark modal `.btn-close` computed filter ≠ `none` | Low; revert | M |
| **FE-WP6a** spatial-1 | adopt ui/format/charts; a11y | Index + app.js of GeoStream, Geometry, Terrain, Clustering, Viewshed, Overlay | links → `@section Styles` (P02); `PortfolioUi.runAction` (A04); `PortfolioFormat`; `PortfolioCharts` (rng, legend, palette N4); panel h2 (A13); A12 (`aria-label` on generic, label-in-name, chart `describe`, Terrain output); A05 (Viewshed observer inputs); security `innerHTML` fixes for these files | Per-page protocol + A04 verify (focus stays, announce) + A05 verify | Low; per-page revert | M |
| **FE-WP6b** spatial-2 | same | Index + app.js of CatRisk, Change, Outage, Response, Fleet | as WP6a + A06 (Outage select, roving/`tabindex -1`) + Response station list as buttons (keyboard isochrone) + `PortfolioMap` (Response, Fleet) + svg helpers + palette N4 + Change inline styles → classes | protocol + A06 verify | Medium (Outage interaction); revert | L |
| **FE-WP6c** map/form pages | ArcGIS and form demos | HomeFinder (Index, app.js, homefinder.css), StateExplorer (Index, uiManager, app, collectionService, mapManager, featureService), ReverseGeocoding (Index, app.js, reversegeocoding.css), AddressStandardization (Index, app.js, addressstandardization.css), BatchGeocoding (Index, app.js, batchgeocoding.css) | A07 (State Explorer anonymous), A08 (esri-theme, names), A11 (HomeFinder sliders/status), A12 (State Explorer nested buttons, emoji names, `prompt()` → modal), T2/T3/T4/T6/T8/T13/T15/T16/T17, D03 buttons, D10 dark overrides, the HomeFinder `.form-range` green box, delete the local `showToast` copies, mojibake comments, Batch filter buttons `aria-pressed` (`Index.cshtml:182-186`), security `innerHTML` fixes | protocol; State Explorer stays on its URL anonymously; esri buttons 32×32 | Medium; per-page revert | L |
| **FE-WP6d** Network + PlantOps | app shell and auth page | Network (Index, app.js, network.css); PlantOps (Index, dashboard/orders/shipments/inventory.js, plantoperationsdashboard.css) | Network: api-fetch adoption (§6.4), `PortfolioMap`, T5/T6 (chip, kpi-val), native flag/"A*" formatting, `--nav-h` height, route list region; PlantOps: close the `<div>` at `:50`, remove duplicate toasts/container, invalid CSS `:16`, dark DataTables duplicates, esri-theme, status tokens | Network protocol; **PlantOps: static checks plus owner-run protocol (auth)** | Medium (PlantOps unverifiable by agents); revert | M |
| **FE-WP6e** site and Details | small pages | `Login.cshtml`, `Error.cshtml`, `Privacy.cshtml`, `Accessibility.cshtml`, `AccessDenied.cshtml`, `Areas/Account/Pages/Profile.cshtml` + `css/profile.css`, `Projects/Index.cshtml` (per decision D3), 18 × `Projects/*/Details.cshtml` | T1 login link; T19 profile; Details: `.theme-surface` from components.css (drop gis.css), `aria-hidden` on icons, links → Styles section; `/Projects` link names (A12) | protocol on all; Profile owner-verified (auth) | Low | M |
| **FE-WP7** dead assets and sweep | delete unreferenced files; encoding; final dedupe | `wwwroot/lib/bootstrap/dist/**`, `lib/jquery/dist/**`, `lib/jquery-validation*/**`, `lib/typeahead/**`, `lib/fontawesome/webfonts/*.ttf` (decision), `lib/fontawesome/webfont/README.txt`, `webfonts/PLACEHOLDER.txt`, `Pages/Shared/_ValidationScriptsPartial.cshtml`; re-save `js/navigation.js`, `js/StateExplorer/mapManager.js` as UTF-8 if not already touched | P07, D12 | full 42-URL sweep: no 404 in `read_network_requests`, console clean; `colors.py` ≤ 40 literals | Low; revert | S |

Dependencies:
- WP4 needs WP2 (it adds the script tags).
- WP6x need WP1, WP2, WP4 and WP5 (tokens, modules, the Styles section, family CSS).
- WP3 is independent: it touches only `layout.css` and home files.
- WP7 runs last.
- The security agent's `innerHTML` package should be merged into WP6a–e (§9), or run strictly after them.

---

## 11. Verification protocol (Stage 2)

**Setup (scratch clone only, never committed):**
1. Clone, copy `appsettings.Development.json`, and run on an alternate port, for example:
   `ASPNETCORE_ENVIRONMENT=Development dotnet run --project Portfolio.Web --no-launch-profile --urls http://localhost:5080`
2. Save Appendix A as `<clone>/Portfolio.Web/wwwroot/__audit/fe-audit.js` and Appendix B as `.../__audit/grad.js`. Static files are served live; no rebuild is needed.
3. Open the pane with `preview_start {url}`. Do **not** take screenshots.

**Per page, per width (360×800, 768×1024, 1280×800 via `resize_window`):**
```js
// javascript_tool (bump ?v= after editing the module):
await (async () => { const m = await import('/__audit/fe-audit.js?v=1');
  const r = await m.run({ wait: 1500 });                 // measures light AND dark itself
  return JSON.stringify({ assert: m.assert(r), r }); })()
```
- For a results state: `run({ wait: 1500, click: '#clusterRunBtn', afterClick: 3000 })`. The primary button ids are:
  `#geoRunBtn`, `#geometryTriangulateBtn`, `#terrainHillshadeBtn`, `#clusterRunBtn`, `#viewshedRunBtn`, `#overlayRunBtn`,
  `#catRunBtn`, `#changeRunBtn`, `#responseOptimizeBtn`, `#fleetOptimizeBtn`, `#btnSearch` (HomeFinder).
  For Network, first set `#networkStart.selectedIndex = 25` and dispatch `change`, then use `#networkRouteBtn`.
- **Focus indicators:** press `Tab` once with the `computer` tool (`key: "Tab"`) to establish keyboard modality, then run `m.focusIndicators()`. Expect `none: 0, weak: 0` after FE-WP1.
- **Console/network:** `read_console_messages {onlyErrors:true}` and `read_network_requests {urlPattern:'localhost'}` must show no errors or 4xx/5xx. Known exception: the State Explorer 500 if the `System.Net.Http.Json` environment issue reproduces.
- **Hero gradient contrast:** `(await import('/__audit/grad.js')).sample('#hero', ['.hero-subtitle','.hero-desc','.hero-cta .btn-outline-accent'])` must give every `min ≥ 4.5`. Do the same for `#contact` / `.contact-link`.

**Acceptance per package:** `assert()` returns `['PASS']` on every page the package touched, at all 3 widths. The
allowlist is `{ names: ['esri-view-surface'] }` until FE-WP6c lands. In addition, run each finding's own **Verify**
block.

**Keyboard checks (real keys):**
- **Skip link:** Tab, Enter → `document.activeElement.id === 'mainContent'`.
- **Run actions:** focus the Run button, press Enter, wait → `activeElement` is still the button and `#a11yAnnounce.textContent` is not empty.
- **Carousel:** Tab into a card → `link.getBoundingClientRect()` is within `.projects-slider`'s rect.

**Reduced motion:**
```js
SitePreferences.set('motion','reduce');
const s = document.querySelector('.projects-slider');
[getComputedStyle(s).overflowX, s.scrollWidth > s.clientWidth]   // ['auto', true] after FE-WP3
```
Then `SitePreferences.set('motion','auto')`.

**Not verified in Stage 1, Stage 2 must check:**
1. **PlantOps** (`/Projects/PlantOperationsDashboard`) and **Account/Profile** need Google sign-in. Agents must not sign in. The owner runs the same `run()`/`assert()`/`focusIndicators()` in a signed-in browser, or the plan accepts a static-only check.
2. **State Explorer populated state** (features list, compare, save modal): the 500 is environmental in my run. Re-check: `document.getElementById('statTotal').textContent !== '0'` after load.
3. **ArcGIS popups** (HomeFinder T17) and **Batch Geocoding results table** at 360 (upload `samples/batch-geocoding/us-landmarks.csv`, which calls ArcGIS externally, so it needs the owner's OK). Check `run()` and that `.table-responsive` contains the table (`scrollWidth > clientWidth` *inside* the wrapper only).
4. **Animations in a visible window:** the fade-in reveal, toast slide-in and skip-link slide. Run with the pane visible, or read end states with `html.theme-switching` set.
5. **OS-level `prefers-reduced-motion`** (not emulated): verify the `@media (prefers-reduced-motion: reduce)` path with an OS setting or CDP emulation.
6. **Reflow at 320 px and text scale 150%:** `resize_window {width:320,height:640}` plus `SitePreferences.set('fontScale', 1.5)`, then `run()`. Expect `pageScrollX false` and no clipped text.
7. **Screen-reader output** (live-region timing, names in context): manual check with NVDA or VoiceOver.

---

## 12. Decisions needed (recommended default in **bold**)

1. **D1 State Explorer anonymous visitors** (FE-A07): **stay on the page with collections disabled and a sign-in hint**, or keep the current redirect to `/Login`.
2. **D2 Carousel** (FE-A03): **(A) keep the auto-scrolling marquee, add a Pause button, switch to a manual scroll-snap row on focus or reduced motion, and fix the seam**; (B) replace it with a static scroll-snap row with prev/next buttons (visible change).
3. **D3 Card copy source** (FE-C01): **share `_ProjectCard` between Home and `/Projects` but keep both description/stack variants (no copy change)**, or unify on the `/Projects` copy (longer descriptions, other stacks), or unify on the Home copy.
4. **D4 Number formatting conventions** (FE-D09): **1 decimal for %, km and min; compact money; "Yes/No" native flag; humanised enum labels**. This changes displayed strings such as `2.42 min` → `2.4 min`.
5. **D5 Input border darkening** (FE-A10 N1, `#7a8699`): **accept** (an AA requirement; inputs gain a visibly darker border).
6. **D6 State Explorer recolouring** (FE-D03): **map the yellow/green/blue Bootstrap variants to the brand accent and status tokens**.
7. **D7 Dark map tiles** (FE-D11): **no change now** (option: CSS invert filter on the Leaflet tile pane in dark mode).
8. **D8 Hero contrast fix** (T10/T11): **raise the text opacity to .95 and add a dark tint behind the contact links**, or darken the gradient's green stop to `--accent-on-fill` (changes the hero colours).
9. **D9 D3 and Font Awesome slimming** (FE-P04/P06): **keep both (defer D3)**; the options are D3 → `PortfolioCharts` (M) and an FA SVG sprite (M–L).
10. **D10 Delete unreferenced vendor files** (FE-P07), including the FA `.ttf` fallbacks: **yes**.
11. **D11 Where the audit script lives:** **commit it outside `wwwroot`** (e.g., `tools/fe-audit/fe-audit.js`, not served) so future work can reuse it, or keep it in this document only.

---

## Appendix A: `fe-audit.js` (reusable verification module for Stage 2)

Save it as `<scratch clone>/Portfolio.Web/wwwroot/__audit/fe-audit.js` (never commit it under `wwwroot`).
Exports:
- `run(opts)`: overflow, targets, names, static a11y checks, and light+dark contrast.
- `assert(result, allow)`: pass/fail lines.
- `focusIndicators()`: needs one real Tab first.
- `tabOrder(n)` and `focusProbe(sel)`.

The tested copy from this audit is also in the agent scratchpad at
`C:\Users\brian\AppData\Local\Temp\claude\C--Users-brian-source-repos-Portfolio\6e74e331-4143-4f68-9411-0e96a263f57d\scratchpad\fe\repo\Portfolio.Web\wwwroot\__audit\fe-audit.js`.

```js
// fe-audit.js — DOM/geometry assertions for Portfolio pages. No screenshots.
// Usage (javascript_tool):  (await import('/__audit/fe-audit.js?v=1')).run({ wait: 1200 })
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const FOCUSABLE = 'a[href], area[href], button, input:not([type="hidden"]), select, textarea, summary, iframe, ' +
    '[tabindex], [contenteditable="true"], [role="button"], [role="link"], [role="checkbox"], [role="radio"], ' +
    '[role="switch"], [role="tab"], [role="menuitem"], [role="option"], [role="slider"], [role="combobox"]';

function describe(el) {
    if (!el || el.nodeType !== 1) return String(el);
    let s = el.tagName.toLowerCase();
    if (el.id) s += '#' + el.id;
    const cls = (typeof el.className === 'string' ? el.className : (el.className && el.className.baseVal) || '')
        .trim().split(/\s+/).filter(Boolean).slice(0, 3);
    if (cls.length) s += '.' + cls.join('.');
    const role = el.getAttribute('role');
    if (role) s += `[role=${role}]`;
    return s;
}
function path(el) { const p = []; let n = el; for (let i = 0; n && n.nodeType === 1 && i < 3; i++, n = n.parentElement) p.unshift(describe(n)); return p.join(' > '); }
function isRendered(el) {
    if (!el.isConnected || el.closest('[hidden]')) return false;
    const cs = getComputedStyle(el);
    if (cs.display === 'none' || cs.visibility === 'hidden' || cs.visibility === 'collapse') return false;
    const r = el.getBoundingClientRect();
    if ((r.width === 0 && r.height === 0) || (r.width <= 1 && r.height <= 1)) return false;
    if (cs.clip === 'rect(0px, 0px, 0px, 0px)' || cs.clipPath === 'inset(50%)') return false;
    return true;
}
const hiddenFromAT = (el) => !!el.closest('[aria-hidden="true"], [inert]');

function parseColor(str) {
    if (!str) return null;
    const m = str.match(/rgba?\(([^)]+)\)/);
    if (!m) {
        if (str === 'transparent') return [0, 0, 0, 0];
        const c = str.match(/color\(srgb ([\d.]+) ([\d.]+) ([\d.]+)(?: \/ ([\d.]+))?\)/);
        return c ? [c[1] * 255, c[2] * 255, c[3] * 255, c[4] === undefined ? 1 : +c[4]] : null;
    }
    const p = m[1].split(/[\s,/]+/).filter(Boolean).map(Number);
    return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1];
}
const over = (fg, bg) => [0, 1, 2].map((i) => fg[i] * fg[3] + bg[i] * (1 - fg[3])).concat(1);
function lum(c) { const f = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]); }
function ratio(a, b) { const l1 = lum(a), l2 = lum(b); return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05); }
const rgb = (c) => c ? `rgb(${c.slice(0, 3).map((v) => Math.round(v)).join(',')})` : '?';

// Composite translucent backgrounds up the tree; gradients yield every stop (caller takes the worst case).
function effectiveBg(el) {
    const layers = [];
    for (let n = el; n && n.nodeType === 1; n = n.parentElement) {
        const cs = getComputedStyle(n);
        const c = parseColor(cs.backgroundColor);
        if (cs.backgroundImage && cs.backgroundImage !== 'none') {
            if (/url\(/.test(cs.backgroundImage)) return { unknown: 'bg-url:' + describe(n) };
            const stops = (cs.backgroundImage.match(/rgba?\([^)]+\)/g) || []).map(parseColor).filter(Boolean);
            layers.push({ grad: stops, under: c && c[3] > 0 ? c : null, opaque: stops.length && stops.every((s) => s[3] >= 1) });
            if (layers[layers.length - 1].opaque || (c && c[3] >= 1)) break;
            continue;
        }
        if (c && c[3] > 0) { layers.push({ color: c }); if (c[3] >= 1) break; }
    }
    let bases = [[255, 255, 255, 1]];
    for (let i = layers.length - 1; i >= 0; i--) {
        const L = layers[i];
        if (L.color) bases = bases.map((b) => (L.color[3] >= 1 ? L.color : over(L.color, b)));
        else {
            const under = bases.map((b) => (L.under ? (L.under[3] >= 1 ? L.under : over(L.under, b)) : b));
            const next = [];
            for (const b of under) for (const s of L.grad) next.push(s[3] >= 1 ? s : over(s, b));
            bases = next.length ? next : under;
        }
    }
    return { colors: bases, gradient: layers.some((l) => l.grad) };
}

function overflowCheck() {
    const de = document.documentElement, cw = de.clientWidth;
    const res = { scrollW: de.scrollWidth, clientW: cw, innerW: innerWidth, pageScrollX: de.scrollWidth > cw };
    const offenders = [];
    for (const el of document.body.querySelectorAll('*')) {
        if (el.closest('.leaflet-container, .esri-view, .esri-ui, .visually-hidden, .visually-hidden-focusable:not(:focus)')) continue;
        const r = el.getBoundingClientRect();
        if (r.width <= 1 || r.height <= 1) continue;
        if (r.right <= cw + 0.5 && r.left >= -0.5) continue;
        let contained = false;
        for (let a = el.parentElement; a && a !== document.body && a !== de; a = a.parentElement) {
            if (getComputedStyle(a).overflowX !== 'visible') { contained = true; break; }
        }
        if (contained) continue;
        const cs = getComputedStyle(el);
        if (cs.position === 'fixed' && (cs.visibility === 'hidden' || +cs.opacity === 0)) continue;
        offenders.push({ el, txt: `${path(el)} [${Math.round(r.left)}..${Math.round(r.right)}]` });
    }
    const outer = offenders.filter((o) => !offenders.some((p) => p !== o && p.el.contains(o.el)));
    res.offenders = outer.slice(0, 6).map((o) => o.txt);
    res.offenderCount = outer.length;
    return res;
}

function textOf(el) {
    let s = '';
    for (const n of el.childNodes) {
        if (n.nodeType === 3) s += n.nodeValue;
        else if (n.nodeType === 1) {
            if (n.getAttribute('aria-hidden') === 'true') continue;
            const cs = getComputedStyle(n);
            if (cs.display === 'none' || cs.visibility === 'hidden') continue;
            if (n.tagName === 'IMG') s += ' ' + (n.getAttribute('alt') || '');
            else if (n.tagName.toLowerCase() === 'svg') { const t = n.querySelector('title'); s += ' ' + (n.getAttribute('aria-label') || (t && t.textContent) || ''); }
            else s += ' ' + (n.getAttribute('aria-label') || textOf(n));
        }
    }
    return s.replace(/\s+/g, ' ').trim();
}
function accName(el) {
    const lb = el.getAttribute('aria-labelledby');
    if (lb) { const t = lb.split(/\s+/).map((id) => { const r = document.getElementById(id); return r ? textOf(r) || r.textContent : ''; }).join(' ').trim(); if (t) return t; }
    const al = el.getAttribute('aria-label'); if (al && al.trim()) return al.trim();
    const tag = el.tagName;
    if (/^(INPUT|SELECT|TEXTAREA|METER|PROGRESS|OUTPUT)$/.test(tag)) {
        if (el.id) { const l = document.querySelector(`label[for="${CSS.escape(el.id)}"]`); if (l && textOf(l)) return textOf(l); }
        const wl = el.closest('label'); if (wl && textOf(wl)) return textOf(wl);
        if (tag === 'INPUT' && /^(button|submit|reset)$/.test(el.type)) return el.value || el.type;
        if (tag === 'INPUT' && el.type === 'image') return el.alt || '';
    } else { const t = textOf(el); if (t) return t; }
    const title = el.getAttribute('title'); if (title && title.trim()) return title.trim();
    if (el.placeholder) return '(placeholder only) ' + el.placeholder;
    return '';
}
function interactive() {
    return [...document.querySelectorAll(FOCUSABLE)].filter((el) => {
        if (el.matches('[tabindex="-1"]') && !el.matches('a[href], button, input, select, textarea, [role]')) return false;
        if (el.closest('[inert]')) return false;
        return isRendered(el);
    });
}
function targetSizeCheck(els) {
    const rects = els.map((el) => ({ el, r: el.getBoundingClientRect() }));
    const small = [];
    for (const o of rects) {
        const { el, r } = o;
        if (el.disabled || (r.width >= 24 && r.height >= 24) || (hiddenFromAT(el) && el.tabIndex < 0)) continue;
        const cs = getComputedStyle(el);
        if (el.tagName === 'A' && cs.display === 'inline' && el.parentElement && textOf(el.parentElement).length > textOf(el).length + 3) continue; // inline exception
        const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
        let collide = false;                       // spacing exception (24px circle)
        for (const p of rects) {
            if (p === o || p.el.contains(el) || el.contains(p.el)) continue;
            const q = p.r;
            if (q.width < 24 || q.height < 24) {
                if (Math.hypot(q.left + q.width / 2 - cx, q.top + q.height / 2 - cy) < 24) { collide = true; break; }
            } else if (Math.hypot(Math.max(q.left - cx, 0, cx - q.right), Math.max(q.top - cy, 0, cy - q.bottom)) < 12) { collide = true; break; }
        }
        small.push(`${describe(el)} ${Math.round(r.width)}x${Math.round(r.height)}${collide ? '' : ' (spacing-ok)'}`);
    }
    const failing = small.filter((s) => !s.endsWith('(spacing-ok)'));
    return { undersized: small.length, failing: failing.length, examples: failing.slice(0, 8) };
}
function nameCheck(els) {
    const missing = [], placeholderOnly = [];
    for (const el of els) {
        if (hiddenFromAT(el)) continue;
        const n = accName(el);
        if (!n) missing.push(path(el)); else if (n.startsWith('(placeholder only)')) placeholderOnly.push(describe(el));
    }
    return { missing: missing.length, examples: missing.slice(0, 8), placeholderOnly };
}
function staticChecks() {
    const out = {};
    out.imgNoAlt = [...document.querySelectorAll('img:not([alt])')].filter((i) => !i.closest('.leaflet-container, .esri-view')).map((i) => describe(i)).slice(0, 6);
    out.roleImgNoName = [...document.querySelectorAll('[role="img"]')].filter((e) => !accName(e)).map(describe).slice(0, 6);
    out.canvasNoText = [...document.querySelectorAll('canvas')].filter((c) => !c.closest('.leaflet-container, .esri-view') && !accName(c) && !c.textContent.trim()).map(describe);
    const seen = {}; document.querySelectorAll('[id]').forEach((e) => { seen[e.id] = (seen[e.id] || 0) + 1; });
    out.dupIds = Object.entries(seen).filter(([, n]) => n > 1).map(([id, n]) => `${id}x${n}`);
    const hs = [...document.querySelectorAll('h1,h2,h3,h4,h5,h6,[role="heading"]')].filter((h) => isRendered(h) && !hiddenFromAT(h));
    const levels = hs.map((h) => +(h.getAttribute('aria-level') || h.tagName[1]));
    const skips = []; for (let i = 1; i < levels.length; i++) if (levels[i] > levels[i - 1] + 1) skips.push(`h${levels[i - 1]}->h${levels[i]} "${textOf(hs[i]).slice(0, 30)}"`);
    out.headings = { count: hs.length, h1: levels.filter((l) => l === 1).length, outline: levels.join(''), skips: skips.slice(0, 5) };
    out.landmarks = { main: document.querySelectorAll('main, [role="main"]').length, application: [...document.querySelectorAll('[role="application"]')].map(describe) };
    out.ariaLabelOnGeneric = [...document.querySelectorAll('div[aria-label], span[aria-label], p[aria-label]')].filter((e) => !e.getAttribute('role') && !e.closest('.leaflet-container, .esri-view')).map(describe).slice(0, 8);
    out.focusableInAriaHidden = [...document.querySelectorAll('[aria-hidden="true"]')].flatMap((h) => [...h.querySelectorAll(FOCUSABLE)].filter((f) => f.tabIndex >= 0 && !f.disabled && isRendered(f))).map(describe).slice(0, 6);
    out.live = [...document.querySelectorAll('[aria-live], [role="status"], [role="alert"], [role="log"]')].map((e) => `${describe(e)}:${e.getAttribute('aria-live') || e.getAttribute('role')}`).slice(0, 12);
    out.faExposed = [...document.querySelectorAll('i[class*="fa-"]')].filter((i) => !i.closest('[aria-hidden="true"], [aria-label], [aria-labelledby]') && isRendered(i)).length;
    out.labelInName = [...document.querySelectorAll('input[aria-label], select[aria-label], textarea[aria-label]')].filter((el) => {
        if (!el.id) return false;
        const l = document.querySelector(`label[for="${CSS.escape(el.id)}"]`); if (!l) return false;
        const vis = textOf(l).toLowerCase().replace(/[:$%0-9.,]+/g, ' ').replace(/\s+/g, ' ').trim();
        return vis && !el.getAttribute('aria-label').toLowerCase().includes(vis.split(' ').slice(0, 2).join(' '));
    }).map((el) => `${describe(el)} aria-label="${el.getAttribute('aria-label')}"`).slice(0, 6);
    return out;
}
function contrastCheck() {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, { acceptNode: (n) => (n.nodeValue.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP) });
    const seen = new Set(), fails = [], gradFails = [];
    let checked = 0, unknown = 0;
    while (walker.nextNode()) {
        const el = walker.currentNode.parentElement;
        if (!el || seen.has(el)) continue; seen.add(el);
        if (el.closest('script, style, noscript, .leaflet-container, .esri-view, .visually-hidden, [aria-hidden="true"]') || !isRendered(el)) continue;
        if (el.closest('button:disabled, input:disabled, select:disabled, textarea:disabled, fieldset:disabled, [aria-disabled="true"], .disabled')) continue;
        const cs = getComputedStyle(el), isSvg = el instanceof SVGElement;
        const fg = parseColor(isSvg ? cs.fill : cs.color);
        if (!fg) { unknown++; continue; }
        const bg = effectiveBg(isSvg ? (el.ownerSVGElement || el).parentElement || el : el);
        if (bg.unknown) { unknown++; continue; }
        let worst = Infinity, wb = null, wf = null;
        for (const b of bg.colors) { const f = fg[3] < 1 ? over(fg, b) : fg; const r = ratio(f, b); if (r < worst) { worst = r; wb = b; wf = f; } }
        const size = parseFloat(cs.fontSize), weight = parseInt(cs.fontWeight, 10) || 400;
        const need = (size >= 24 || (size >= 18.66 && weight >= 700)) ? 3 : 4.5;
        checked++;
        if (worst < need - 0.01) (bg.gradient ? gradFails : fails).push(`${worst.toFixed(2)}<${need} ${describe(el)} "${walker.currentNode.nodeValue.trim().slice(0, 24)}" fg=${rgb(wf)} bg=${rgb(wb)} ${Math.round(size)}px/${weight}`);
    }
    const uniq = (l) => [...new Set(l.map((f) => f.replace(/ ".*?" /, ' ')))];
    // gradientWorstCase: text over a gradient, judged at its worst stop — confirm with grad.js before calling it a failure
    return { checked, unknown, fails: fails.length, examples: uniq(fails).slice(0, 10), gradientWorstCase: uniq(gradFails).slice(0, 5) };
}
function setTheme(t) {
    if (window.SitePreferences && typeof window.SitePreferences.set === 'function') window.SitePreferences.set('theme', t);
    else { document.documentElement.setAttribute('data-bs-theme', t); document.body.classList.toggle('dark-mode', t === 'dark'); }
}
export async function run(opts = {}) {
    if (opts.wait) await sleep(opts.wait);
    if (opts.click) { const b = document.querySelector(opts.click); if (b && !b.disabled) { b.click(); await sleep(opts.afterClick || 2500); } }
    const startDark = document.body.classList.contains('dark-mode');
    const out = { path: location.pathname + location.search, vw: innerWidth };
    out.overflow = overflowCheck();
    const els = interactive();
    out.targets = targetSizeCheck(els);
    out.names = nameCheck(els);
    Object.assign(out, staticChecks());
    if (opts.contrast !== false) {
        out.contrast = {};
        for (const t of opts.themes || ['light', 'dark']) {
            setTheme(t); await sleep(opts.themeWait || 250);
            out.contrast[t] = contrastCheck();
            if (t === 'dark') out.overflowDark = overflowCheck().pageScrollX;
        }
        setTheme(startDark ? 'dark' : 'light');
    }
    return out;
}
export function tabOrder(n = 25) {
    return [...document.querySelectorAll(FOCUSABLE)].filter((e) => e.tabIndex >= 0 && !e.disabled && isRendered(e) && !e.closest('[inert]'))
        .slice(0, n).map((e) => `${describe(e)} "${accName(e).slice(0, 30)}"`);
}
function snap(el) { const c = getComputedStyle(el); return { os: c.outlineStyle, ow: parseFloat(c.outlineWidth) || 0, oc: parseColor(c.outlineColor), bs: c.boxShadow, bc: c.borderColor, bg: c.backgroundColor, co: c.color, td: c.textDecorationLine }; }
function shadowAlpha(bs) { if (!bs || bs === 'none') return 0; return Math.max(0, ...(bs.match(/rgba?\([^)]+\)/g) || []).map((s) => (parseColor(s) || [0, 0, 0, 0])[3])); }
// Needs keyboard modality: press Tab once (computer tool) first. ring = outline or opaque box-shadow; weak = colour-only change; none = nothing.
export function focusIndicators(limit = 400) {
    const root = document.documentElement; root.classList.add('theme-switching');   // site's transition kill-switch
    const prev = document.activeElement;
    const els = interactive().filter((e) => e.tabIndex >= 0 && !e.disabled && !hiddenFromAT(e)).slice(0, limit);
    const none = [], weak = [];
    for (const el of els) {
        el.blur(); const b = snap(el);
        el.focus({ preventScroll: true, focusVisible: true });
        if (document.activeElement !== el || !el.matches(':focus-visible')) continue;
        const a = snap(el);
        const ring = (a.os !== 'none' && a.ow >= 1 && a.oc && a.oc[3] > 0.5) || shadowAlpha(a.bs) > 0.5;
        const changed = a.bs !== b.bs || a.bc !== b.bc || a.bg !== b.bg || a.co !== b.co || a.td !== b.td;
        const label = `${describe(el)} "${accName(el).slice(0, 24)}"`;
        if (!ring && !changed) none.push(label); else if (!ring) weak.push(label);
        el.blur();
    }
    root.classList.remove('theme-switching');
    if (prev && typeof prev.focus === 'function') prev.focus({ preventScroll: true });
    const uniq = (l) => [...new Set(l.map((s) => s.replace(/ ".*"$/, '')))];
    return { checked: els.length, none: none.length, weak: weak.length, noneKinds: uniq(none).slice(0, 10), weakKinds: uniq(weak).slice(0, 10) };
}
export function assert(r, allow = {}) {
    const fails = [];
    if (r.overflow && r.overflow.pageScrollX) fails.push('horizontal page scroll: ' + r.overflow.offenders.join('; '));
    if (r.overflowDark) fails.push('horizontal page scroll in dark mode');
    const t = (r.targets.examples || []).filter((x) => !(allow.targets || []).some((a) => x.includes(a)));
    if (t.length) fails.push('targets <24px without spacing: ' + t.join(', '));
    const n = (r.names.examples || []).filter((x) => !(allow.names || ['esri-view-surface']).some((a) => x.includes(a)));
    if (n.length) fails.push('controls without a name: ' + n.join(', '));
    if (r.imgNoAlt && r.imgNoAlt.length) fails.push('img without alt: ' + r.imgNoAlt.join(', '));
    if (r.dupIds && r.dupIds.length) fails.push('duplicate ids: ' + r.dupIds.join(', '));
    if (r.headings && r.headings.h1 !== 1) fails.push('h1 count = ' + r.headings.h1);
    if (r.headings && r.headings.skips.length) fails.push('heading skips: ' + r.headings.skips.join(', '));
    if (r.ariaLabelOnGeneric && r.ariaLabelOnGeneric.length) fails.push('aria-label on generic element: ' + r.ariaLabelOnGeneric.join(', '));
    if (r.labelInName && r.labelInName.length) fails.push('label-in-name: ' + r.labelInName.join(' | '));
    if (r.focusableInAriaHidden && r.focusableInAriaHidden.length) fails.push('focusable inside aria-hidden: ' + r.focusableInAriaHidden.join(', '));
    for (const k of Object.keys(r.contrast || {})) if (r.contrast[k].fails) fails.push(`contrast (${k}): ` + r.contrast[k].examples.join(' ; '));
    return fails.length ? fails : ['PASS'];
}
export function focusProbe(selector) {
    const el = typeof selector === 'string' ? document.querySelector(selector) : selector;
    if (!el) return 'not found: ' + selector;
    document.documentElement.classList.add('theme-switching');
    const b = snap(el); el.focus({ focusVisible: true }); const a = snap(el);
    document.documentElement.classList.remove('theme-switching');
    return { el: describe(el), focusVisible: el.matches(':focus-visible'), before: b, after: a };
}
```

## Appendix B: `grad.js` (actual colour under text on a 135° gradient)
```js
// (await import('/__audit/grad.js')).sample('#hero', ['.hero-desc'])  → ["… min=4.20"]
function parse(s) { const p = s.match(/[\d.]+/g).map(Number); return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1]; }
function lum(c) { const f = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]); }
function ratio(a, b) { const l1 = lum(a), l2 = lum(b); return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05); }
const over = (fg, bg) => [0, 1, 2].map((i) => fg[i] * fg[3] + bg[i] * (1 - fg[3])).concat(1);
function gradientAt(section, x, y) {
    const m = getComputedStyle(section).backgroundImage.match(/linear-gradient\((\d+)deg,\s*(.*)\)$/);
    if (!m) return null;
    const ang = +m[1] * Math.PI / 180;
    const stops = [...m[2].matchAll(/(rgba?\([^)]+\))\s*([\d.]+)%/g)].map((s) => ({ c: parse(s[1]), p: +s[2] / 100 }));
    const r = section.getBoundingClientRect(), W = r.width, H = r.height;
    const L = Math.abs(W * Math.sin(ang)) + Math.abs(H * Math.cos(ang));
    const t = 0.5 + ((x - (r.left + W / 2)) * Math.sin(ang) - (y - (r.top + H / 2)) * Math.cos(ang)) / L;
    let a = stops[0], b = stops[stops.length - 1];
    for (let i = 0; i < stops.length - 1; i++) if (t >= stops[i].p && t <= stops[i + 1].p) { a = stops[i]; b = stops[i + 1]; }
    const k = Math.max(0, Math.min(1, (t - a.p) / ((b.p - a.p) || 1)));
    return [0, 1, 2].map((i) => a.c[i] + (b.c[i] - a.c[i]) * k).concat(1);
}
export function sample(sectionSel, textSels) {
    const section = document.querySelector(sectionSel), out = [];
    for (const sel of textSels) for (const el of document.querySelectorAll(sel)) {
        if (!section.contains(el) || !el.getBoundingClientRect().width) continue;
        const fg = parse(getComputedStyle(el).color), own = parse(getComputedStyle(el).backgroundColor);
        let worst = Infinity;
        const range = document.createRange(); range.selectNodeContents(el);
        for (const box of range.getClientRects()) for (let i = 0; i <= 4; i++) for (let j = 0; j <= 2; j++) {
            const bg = gradientAt(section, box.left + box.width * i / 4, box.top + box.height * j / 2);
            const base = own[3] > 0 ? over(own, bg) : bg;
            worst = Math.min(worst, ratio(fg[3] < 1 ? over(fg, base) : fg, base));
        }
        const cs = getComputedStyle(el);
        out.push(`${sel} ${Math.round(parseFloat(cs.fontSize))}px/${cs.fontWeight} min=${worst.toFixed(2)}`);
    }
    return out;
}
```