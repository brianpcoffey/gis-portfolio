# Brian Patrick Coffey – Software Engineering Portfolio

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat&logo=csharp&logoColor=white)
![C++](https://img.shields.io/badge/C%2B%2B-20-00599C?style=flat&logo=cplusplus&logoColor=white)
![CMake](https://img.shields.io/badge/CMake-Build-064F8C?style=flat&logo=cmake&logoColor=white)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?style=flat&logo=bootstrap&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon.tech-4169E1?style=flat&logo=postgresql&logoColor=white)
![Entity Framework Core](https://img.shields.io/badge/EF_Core-Code--First-512BD4?style=flat&logo=dotnet&logoColor=white)
![ArcGIS](https://img.shields.io/badge/ArcGIS-JS_API-2C7AC3?style=flat&logo=esri&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Cache_%26_Job_State-DC382D?style=flat&logo=redis&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=flat&logo=docker&logoColor=white)
![Kubernetes](https://img.shields.io/badge/Kubernetes-Manifests-326CE5?style=flat&logo=kubernetes&logoColor=white)
![Google OAuth](https://img.shields.io/badge/Google-OAuth_2.0-4285F4?style=flat&logo=google&logoColor=white)
![Polly](https://img.shields.io/badge/Polly-Resilience-512BD4?style=flat&logo=dotnet&logoColor=white)
![OpenAPI](https://img.shields.io/badge/OpenAPI-Scalar-6BA539?style=flat&logo=openapiinitiative&logoColor=white)
![xUnit](https://img.shields.io/badge/xUnit-675_Tests-512BD4?style=flat&logo=dotnet&logoColor=white)
![Hosted on Render](https://img.shields.io/badge/Hosted_on-Render-46E3B7?style=flat&logo=render&logoColor=white)

## Overview

A .NET 10 Razor Pages and ASP.NET Core API portfolio focused on scalable backend engineering, GIS workflows, geocoding services, user-scoped persistence, and cloud-ready deployment patterns. The application is intentionally structured as a lead-level system design walkthrough: thin Razor/API entry points, service-layer business logic, repository-owned data access, typed external integrations, cache-backed geocoding workflows, and containerized deployment assets.

The current production deployment runs on **Render** with **PostgreSQL via Npgsql/EF Core**. The repository also includes a multi-stage Dockerfile, Docker Compose with Redis, and Kubernetes manifests that demonstrate how the same architecture can move to a horizontally scaled container platform. Azure, AKS, Azure Container Apps, or Azure Cache for Redis are future hosting targets rather than current production dependencies.

---

## Solution Architecture

The solution follows a classic 4-layer architecture across 5 projects:

```
Razor Page / API Controller  (Portfolio.Web)
        │
        ▼
   Service (interface)       (Portfolio.Services)
        │
        ▼
 Repository (interface)      (Portfolio.Repositories)
        │
        ▼
  PortfolioDbContext (EF Core) / HttpClient (ArcGIS)
```

| Project | Role |
|---|---|
| `Portfolio.Web` | ASP.NET Core host: Razor Pages + API controllers + middleware + DI root |
| `Portfolio.Services` | Business logic, orchestration, ArcGIS HTTP calls |
| `Portfolio.Repositories` | EF Core `DbContext`, repositories, fluent mappings, migrations |
| `Portfolio.Common` | POCO entity models, DTOs, enums, and ArcGIS wire models |
| `Portfolio.Tests` | xUnit + Moq unit tests for services and controllers |

---

## Native Spatial Compute Architecture

This portfolio uses a native-kernel pattern for compute-heavy spatial workflows where lower-level control is technically justified. ASP.NET Core owns the product/API layer, C# services validate inputs and map DTOs, and native C++20 libraries expose stable C ABIs consumed through P/Invoke. Each native-backed workflow must remain portable: `IsAvailable` gates the native path and a managed C# fallback preserves functionality when the shared library is absent.

Thirteen native integrations are implemented today, each with C++ source, a P/Invoke bridge, and a managed C# fallback — see the table below.

### What the benchmarks actually say

Six kernels have now been measured against their own managed fallbacks, always with
**bit-identical outputs** (checksums match; where floating point is involved the only
divergence is a final-ULP difference from summation order). The honest range is **0.87× to
2.6×** — not the order of magnitude the phrase "native kernel" usually implies.

| Kernel | Workload | Native | Managed | Speedup |
|---|---|---:|---:|---:|
| `vrp_solver_kernel` | CVRPTW solve, 120 stops | 7.33 ms | 16.78 ms | **2.29×** |
| `facility_location_kernel` | p-median 1,600×120, weighted mean | 12.0 ms | 26.8 ms | **2.23×** |
| `change_detection_kernel` | Otsu threshold, 512² | 0.24 ms | 0.50 ms | **2.11×** |
| `spatial_graph_engine` | one-to-all Dijkstra, 2,530 nodes | 6.2 ms | 10.3 ms | 1.66× |
| `change_detection_kernel` | CVA magnitude, 512²×4 bands | 1.18 ms | 1.72 ms | 1.46× |
| `facility_location_kernel` | p-median 1,600×120, weighted p90 | 1,164 ms | 1,685 ms | 1.45× |
| `network_trace_kernel` | trace + restore, 267 elements | 149 ms | 192 ms | 1.29× |
| `change_detection_kernel` | full detection pipeline, 512² | 4.93 ms | 6.13 ms | 1.24× |
| `spatial_graph_engine` | 40×40 cost matrix | 32.6 ms | 38.8 ms | 1.19× |
| `cat_risk_kernel` | Monte Carlo, 5,000 × 12,000 events | 568 ms | 645 ms | 1.13× |
| `network_trace_kernel` | trace + restore, 4,603 elements | 882 ms | 768 ms | **0.87×** |

**The pattern is consistent and it is not about the language.** Native wins where the inner
loop is flat, branch-free and allocation-free — Otsu's single histogram pass, the VRP move
evaluator over hoisted scratch buffers, the p-median arithmetic loop. It wins little or
nothing where the loop short-circuits on most iterations (morphological open, the
bounding-box reject in catastrophe accumulation), because the branch defeats
auto-vectorisation on both sides and RyuJIT already emits good scalar code. And it *loses*
outright when P/Invoke marshalling dominates: the 4,603-element trace is three calls each
re-marshalling the whole element array, which costs more than the traversal saves.

Two kernels were **slower than the C# they were written to replace** on first implementation,
and both for the same reason — allocation and hashing inside the hot loop:

- `network_trace_kernel` used `std::unordered_map<int, std::vector<int>>` for adjacency and
  measured **2.5× slower** than managed. Densified node ids plus a CSR incidence array with
  cached endpoint indices turned it into a 1.29× win.
- `vrp_solver_kernel` allocated a fresh `std::vector` per candidate move and measured
  **1.7× slower**. Hoisting three scratch buffers and reusing capacity produced the 2.29× win.

The .NET nursery allocator beats naive `malloc`/`new` in a tight loop. Writing C++ is not a
performance strategy; controlling memory layout and allocation is, and that is available in
both languages.

So the defensible claim here is the **boundary**, not the throughput: a stable C ABI, a
verified-identical managed fallback gated by `IsAvailable`, and measurements published
including the case where native lost. Making these genuinely fast would need explicit SIMD
intrinsics, thread-level parallelism, or batching to amortise the P/Invoke crossing — none of
which any kernel does today. Seven kernels remain unmeasured and should be assumed to sit in
the same range until they are.

> **In production these numbers do not apply.** The container build does not compile any
> native library, so Render runs the managed fallback for all thirteen and the UI correctly
> reports "Native: No". See `native/README.md`.

| Project | Native Library | Status | Native Role |
|---|---|---|---|
| Redlands Smart Home Finder | `portfolio_scoring` | Implemented | weighted property scoring with AVX2-friendly batch math |
| Live Location Stream | `geostream_processor` | Implemented | telemetry parsing, filtering, grid aggregation, and anomaly detection |
| Geometry Toolkit | `spatial_geometry_kernel` | Implemented | fan triangulation and bounding-box polygon clipping with stable C ABI |
| Terrain Analyzer | `raster_terrain_kernel` | Implemented | hillshade, slope/aspect, and Gaussian heatmap kernel over dense numeric arrays |
| Route Planner | `spatial_graph_engine` | Implemented | Dijkstra and A* shortest path, service-area computation, and route metric enrichment over a real OpenStreetMap Redlands road graph |
| Hotspot Clusterer | `spatial_cluster_kernel` | Implemented | density-based (DBSCAN) clustering with a tight neighbor-query distance loop over contiguous points |
| Viewshed Analyzer | `viewshed_kernel` | Implemented | line-of-sight ray casting over dense elevation grids with observer-height modeling |
| Spatial Overlay / Zone Tagger | `spatial_overlay_kernel` | Implemented | point-in-polygon spatial join (even-odd ray casting) over a flat zone-vertex buffer |
| Catastrophe Risk Analyzer | `cat_risk_kernel` | Implemented | O(n²) ring accumulation and Monte Carlo event-loss simulation over contiguous exposure/event buffers |
| Raster Change Detection | `change_detection_kernel` | Implemented | CVA magnitude, Otsu thresholding, morphological open, and two-pass union-find connected components over flat raster buffers |
| Outage Manager & Network Trace | `network_trace_kernel` | Implemented | downstream/upstream fault tracing, isolation-device search, and connectivity sweeps over a CSR incidence layout |
| Emergency Response Coverage Optimizer | `facility_location_kernel` | Implemented | p-median greedy seeding and Teitz-Bart vertex substitution with cached nearest/second-nearest distances, plus demand-weighted coverage statistics |
| Fleet Route Optimizer | `vrp_solver_kernel` | Implemented | CVRPTW metaheuristic — Clarke-Wright savings construction plus first-improvement 2-opt / Or-opt local search over flat cost and travel-time matrices |

The architectural goal is not "C++ everywhere." Native code is isolated behind measured, testable kernels for dense numeric processing, computational geometry, raster analysis, streaming telemetry, graph traversal, clustering, visibility, spatial overlay, and scoring. The managed fallback is production behavior, not only a test convenience.

---

## Features

### Authentication & Identity
- **Google OAuth 2.0 Sign-In** — secure login using Google accounts
- Cookie-based session management with `SecurePolicy = Always`
- **Anonymous user identity** — every visitor is silently assigned a GUID cookie (`AnonUserId`) by middleware, enabling per-user saved features and profile claims without requiring sign-in
- Two parallel identity systems: anonymous (GUID cookie) and authenticated (Google OAuth), each used by distinct services
- Sign-out with smooth redirection; login/logout paths at `/Login` and `/Logout`

### UI & UX
- Fully responsive design with Bootstrap 5
- Dark/light mode toggle with preference persistence
- Profile avatar dropdown with compact logout menu
- Card-based project showcase with icons, stack badges, and demo links
- Font Awesome icons throughout

### API & Backend
- Versioned RESTful JSON API controllers under `api/v1/` using `Asp.Versioning`
- OpenAPI document generation via ASP.NET Core OpenAPI, with Scalar as the interactive API reference UI (served in all environments)
- Structured logging with `ILogger<T>` across all services, repositories, and controllers
- `ApiExceptionMiddleware` for centralized error handling
- All async methods accept and forward `CancellationToken`
- Redis-backed `IDistributedCache` shared across geocoding services for deduplication and result caching, with in-memory fallback when `Redis:ConnectionString` is empty
- Redis-backed batch job state through `RedisBatchJobStore`, allowing any replica to serve async geocoding status requests
- Redis-backed ASP.NET Core Data Protection keys in production so encrypted cookies and antiforgery tokens remain valid across replicas
- Typed `HttpClient` registrations with Polly timeout, retry, jitter, and circuit breaker policies for ArcGIS dependencies
- EF Core migrations run automatically at startup via `db.Database.Migrate()`

---

## Projects Highlighted

### 🗺️ US State Explorer
Interactive ArcGIS feature exploration with anonymous saved-feature persistence and authenticated collections. The browser handles map rendering, while versioned APIs manage ArcGIS proxying, saved-feature ownership, collection CRUD, and user-scoped PostgreSQL queries.

- ArcGIS JavaScript map experience backed by ASP.NET Core API endpoints
- Anonymous GUID identity for saved features and authenticated identity for collections
- Repository-owned EF Core queries with owner filtering and transaction handling

**Stack:** ArcGIS JS API, ASP.NET Core, PostgreSQL

**API:** `GET /api/v1/features`, `/api/v1/features/saved`, `/api/v1/collections`

---

### 🏠 Redlands Smart Home Finder
Property scoring API that ranks Redlands homes using weighted preferences and stores named searches per user. The scoring service is isolated from controller and repository concerns so the ranking algorithm can evolve without changing the API surface. A native C++ kernel accelerates the compute-intensive ranking math when the shared library is present; the managed C# fallback activates automatically when it is not.

- Curated dataset of ~45 Redlands homes across five real neighborhoods (South Redlands historic, hillside/view, downtown/University, North Redlands, and East Redlands tracts), each with realistic coordinates, pricing, and Redfin/Zillow-style listing features (property type, garage, pool, stories, year built, price/sqft, HOA, days on market, school rating, and brokerage), seeded deterministically via `RedlandsPropertySeedData` and shipped through an EF Core migration
- Preference-based scoring for property search and comparison workflows using a ten-dimension weighted model
- Native C++ scoring kernel (`portfolio_scoring`) compiled with AVX2/`-O3 -march=haswell` for SIMD-friendly batch processing, called via P/Invoke from `NativeScoringBridge`
- Transparent managed fallback: `NativeScoringBridge.IsAvailable` gates the native path; identical C# helpers execute otherwise
- Saved-search CRUD with user-scoped persistence
- Repository abstraction that can evolve toward spatial indexes or search services

**Stack:** ArcGIS JS API, ASP.NET Core, PostgreSQL, C++20/CMake, P/Invoke

**API:** `POST /api/v1/homefinder/search`, `GET /api/v1/homefinder/property/{id}`, `/api/v1/homefinder/searches` (the earlier `score` / `properties/{id}` paths remain as deprecated aliases)

---

### 🗂️ Batch Geocoding
CSV address upload workflow that processes records asynchronously through ArcGIS `findAddressCandidates`. The API returns `202 Accepted` with a polling URL, while distributed job state allows scaled replicas to serve status requests.

- Channel-based producer/consumer pipeline with configurable concurrency
- `IDistributedCache` deduplication for repeated addresses and reduced ArcGIS calls
- Redis-backed or in-memory `IBatchJobStore` depending on environment configuration

**Stack:** ArcGIS, C#, .NET, Channels

**API:** `POST /api/v1/geocoding/batch`, `GET /api/v1/geocoding/batch/{jobId}/status`

---

### 📍 Reverse Geocoding
Interactive map workflow that resolves a clicked or manually entered coordinate into a street address. The service validates coordinate bounds, normalizes cache keys, and calls ArcGIS only when a cached result is unavailable.

- Coordinate grid-snapping so nearby lookups share cached results
- Sliding-expiration `IDistributedCache` for repeated coordinate lookups
- Typed ArcGIS reverse-geocode integration with service-level validation

**Stack:** ArcGIS, C#, .NET, Maps

**API:** `GET /api/v1/geocoding/reverse?lat={lat}&lng={lng}`

---

### 🪪 Address Standardization & Validation
Freeform address parsing and validation workflow that turns user-entered addresses into structured components, then scores the standardized result against ArcGIS geocoding.

- Regex-based parsing for house number, street, unit, city, state, and ZIP components
- Street-suffix normalization and standardized-address formatting
- ArcGIS validation mapped to `ConfidenceTier` values for downstream decision-making

**Stack:** ArcGIS, Address Parsing, C#, .NET

**API:** `POST /api/v1/addresses/parse`, `POST /api/v1/addresses/validate`

---

### 🏭 Plant Operations Dashboard
Authenticated operations dashboard for fiber orders, materials, shipments, and KPI aggregation. Domain services enforce user identity, while repositories handle EF Core owner filtering and PostgreSQL persistence.

- CRUD workflows for orders, materials, and shipments (clients exist as seeded reference data; there is no client CRUD endpoint)
- Dashboard aggregation for revenue, open orders, active shipments, and inventory alerts
- User-scoped service and repository boundaries for authenticated operations data

**Stack:** ASP.NET Core, DataTables, Esri GIS, PostgreSQL

**API:** `/api/v1/fiber/orders`, `/api/v1/fiber/materials`, `/api/v1/fiber/shipments`, `/api/v1/fiber/dashboard/stats`

---

### 📡 Live Location Stream
GPS-style telemetry batch processing pipeline that reduces high-volume sensor or vehicle events into spatial grid aggregates, speed metrics, and anomaly counts for a live-feed style GIS dashboard. A native C++ kernel handles the allocation-sensitive numeric loops when the shared library is present; the managed fallback activates automatically when it is not.

- Channel-oriented GPS event batching with configurable grid size and anomaly threshold
- Native C++ telemetry kernel (`geostream_processor`) processes contiguous event arrays via a stable C ABI
- Aggregate output shaped for ArcGIS JS rendering as point, grid, heatmap, or feature-layer graphics
- Stateless request/response MVP — no per-user persistence; future evolution paths include Redis-backed windows and SignalR live updates

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, ArcGIS JS API

**API:** `POST /api/v1/geostream/events`

---

### 🔷 Geometry Toolkit
Computational geometry API that transforms map-drawn coordinates into derived geometry ready for GIS visualization and downstream analysis. The architecture establishes the API and native bridge boundary for future robust algorithms; the current MVP implements fan triangulation and bounding-box clipping.

- SVG workbench frontend for point input, operation selection, and result rendering
- Native C++ geometry kernel (`spatial_geometry_kernel`) exposing `Geometry_TriangulateFan` and `Geometry_ClipToBoundingBox` through a stable C ABI
- C# owns preallocated output buffers; native code fills them without cross-runtime allocation
- Foundation for future Delaunay triangulation, Voronoi generation, polygon intersection, and topology validation with GEOS, CGAL, or Clipper2

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, SVG

**API:** `POST /api/v1/geometry/triangulate`, `POST /api/v1/geometry/clip`

---

### ⛰️ Terrain Analyzer
Raster terrain analysis API that generates hillshade and heatmap outputs from dense numeric grid inputs and renders the results as a browser-visible raster grid. The native C++ kernel provides SIMD-friendly dense array processing when available; the managed fallback computes slope/aspect and Gaussian-style kernel weights in C#.

- Hillshade computation from configurable sun azimuth/altitude and per-cell slope/aspect approximation
- Gaussian-style heatmap kernel from weighted point samples over a target raster extent
- Native C++ raster kernel (`raster_terrain_kernel`) exposing `Raster_GenerateHillshade` and `Raster_GenerateHeatmap`
- Stateless per-request processing; future evolution paths include PNG tile generation, GeoTIFF/COG ingestion, and Redis tile caching

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke

**API:** `POST /api/v1/raster/hillshade`, `POST /api/v1/raster/heatmap`

---

### 🗺️ Route Planner
Spatial graph routing API that computes shortest paths and service areas over a ~2,500-node / ~3,190-edge Redlands road network built from real OpenStreetMap data, rendered on a real Leaflet/OpenStreetMap map. Every node is an actual OSM point with true coordinates — real intersections plus curve vertices chosen by deviation-bounded (Douglas–Peucker) simplification, so no edge strays more than ~4 m from the true road — meaning both the network and computed routes trace the real shape of streets, ramps, and freeway curves on the base tiles. Coverage is dense across downtown Redlands and the area around Esri HQ. Supports Dijkstra and A* with a haversine heuristic; the native C++ engine accelerates Dijkstra when the shared library is present, while A* runs managed-only so the heuristic has full access to node coordinates at query time.

- **Dijkstra and A\*** algorithm selection via UI toggle; A* uses haversine great-circle distance as an admissible heuristic and explores measurably fewer nodes on typical road layouts
- Route metrics surfaced per request: `ExploredNodes`, `DistanceKm` (haversine sum along path), `EstimatedMinutes` (distance ÷ 40 km/h average road speed), and `AlgorithmUsed`
- ~2,500-node / ~3,190-edge Redlands road network (`RedlandsRoadNetwork`) generated from a dense OpenStreetMap extract (arterials down to residential/local streets) covering downtown Redlands and the area around Esri HQ: real intersections plus curve vertices selected by deviation-bounded Douglas–Peucker simplification (no edge strays more than ~4 m from the true road, so surface streets, on/off ramps, and freeway curves all follow their real geometry), reduced to the largest component reachable to Esri HQ (so every origin routes), with Esri HQ placed at its true campus coordinate as the fixed destination
- **Search-space visualization** — the API returns every node each algorithm settled (`ExploredNodeIds`); the map paints A*'s tight beam (teal) against Dijkstra's full flood (amber), making the efficiency difference visible, not just a number
- **Click-anywhere-to-snap** origin selection (nearest-intersection snapping), a junction-only origin picker, and canvas rendering for a smooth 3,000-edge map
- Leaflet 1.9 map on OpenStreetMap tiles with curved edge/route polylines, clickable `L.circleMarker` junctions, blue route polyline, amber service-area highlighting, and map auto-fit to route bounds
- Client fetches full `RoadGraphDto` once via `GET /api/v1/network/graph` and submits it with each route request, keeping the API stateless
- Native C++ routing engine (`spatial_graph_engine`) exposing `Graph_FindShortestPath` and `Graph_ComputeServiceArea`; managed fallback runs `PriorityQueue<TElement, TPriority>`-based Dijkstra/A* automatically when the library is absent
- Turn-by-turn panel with human-readable street-intersection labels; `ExploredNodes` metric drives interviewer discussion of graph search theory

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, Leaflet 1.9, OpenStreetMap

**API:** `GET /api/v1/network/graph`, `POST /api/v1/network/route`, `POST /api/v1/network/service-area`

---

### 🔥 Hotspot Clusterer
Density-based clustering API that groups spatial points into hotspots with DBSCAN and separates genuine clusters from scattered noise. Unlike k-means, DBSCAN discovers the number of clusters from the data and handles arbitrary cluster shapes, making it a natural fit for incident, complaint, and sensor-anomaly hotspot analysis. A native C++ kernel runs the allocation-sensitive neighbor-query distance loop when the shared library is present; the managed C# fallback reproduces the identical algorithm otherwise.

- DBSCAN with configurable `epsilon` (neighborhood radius) and `minPoints` (density threshold), returning per-point cluster labels (-1 = noise), cluster sizes, and a noise tally
- Border-point reclamation preserved identically across the native and managed paths for numeric parity
- Native C++ clustering kernel (`spatial_cluster_kernel`) exposing `Cluster_RunDbscan` over a contiguous point buffer and a preallocated label buffer
- Self-contained SVG cluster map with a per-cluster legend; no map-tile dependency

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, SVG

**API:** `POST /api/v1/clustering/dbscan`

---

### 👁️ Viewshed Analyzer
Line-of-sight analysis API that computes the viewshed — the set of terrain cells visible from an observer — over a dense elevation grid, then renders it as an interactive visibility map. Line-of-sight drives real siting decisions for cell towers, radar, wildfire lookouts, and scenic real estate. A native C++ ray-casting kernel handles the per-cell ray walk when the shared library is present; the managed fallback mirrors the same logic.

- Ray-casting viewshed that tracks the maximum vertical angle to intervening terrain, with configurable observer height that materially changes coverage
- Click-any-cell-to-place-observer interaction with an immediate re-computation and coverage KPIs
- Native C++ viewshed kernel (`viewshed_kernel`) exposing `Viewshed_Compute`, returning the visible-cell count and a row-major visibility grid
- Stateless per-request processing over generated DEMs; future paths include earth-curvature correction and GeoTIFF/COG ingestion

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke

**API:** `POST /api/v1/viewshed/compute`

---

### 🧩 Spatial Overlay / Zone Tagger
Point-in-polygon spatial join API that tags each point with the zone that contains it and rolls the results up into per-zone counts (a live choropleth). Spatial join is the workhorse of GIS analysis — which sales territory, census tract, flood zone, or precinct does each record fall in. A native C++ kernel runs the even-odd crossing-number test over a flat zone-vertex buffer when the shared library is present; the managed fallback reproduces the identical test.

- Even-odd ray-casting containment with deterministic first-match assignment when zones overlap
- Flat vertex ABI — all zone rings packed into one contiguous buffer plus a per-zone ring-size array — keeping P/Invoke marshalling cheap
- Native C++ overlay kernel (`spatial_overlay_kernel`) exposing `Overlay_AssignPointsToZones`, returning per-point zone indices and the assigned-point count
- Self-contained SVG choropleth shaded by point density with a per-zone count table

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, SVG

**API:** `POST /api/v1/overlay/spatial-join`

---

### 🔥 Catastrophe Risk Analyzer
Property-insurance catastrophe model for wildfire exposure. Answers the three questions that decide whether a carrier stays solvent: where the hazard is, where the book is concentrated, and what a bad year could cost. Produces the same deliverables a real CAT model does — exposure accumulation, average annual loss, probable maximum loss, and the occurrence exceedance probability curve.

- **Ring accumulation** — for each location, the summed Total Insured Value within a radius, flagging concentration-limit breaches. Brute-force O(n²) haversine with a conservative bounding-box reject that skips the trigonometry when a pair is provably too far apart
- **Monte Carlo event-loss simulation** — a 5,000-event stochastic wildfire catalog against ~900 insured locations (4.5M site-event evaluations). Intensity decays linearly from the epicenter, scales by site hazard, maps through a `1 − e^(−α·i)` vulnerability curve to a mean damage ratio, then applies a percentage deductible and a limit
- **OEP curve, AAL, and PML** — exceedance rate is the summed frequency of every event exceeding a loss level, so sorting by loss and accumulating rate yields the curve directly. Benchmark losses are interpolated log-linearly at 10/25/50/100/250/500 years; PML is the 250-year loss
- Deterministic synthetic book across six San Bernardino / Riverside wildland-urban-interface communities, generated from a fixed-seed LCG with terrain-driven site hazard — no database, no persistence
- **Benchmarked against its own managed fallback: ~1.1× speedup, bit-identical results.** The Details page carries the measurement table and explains why the gap is small
- Self-contained SVG throughout — an exposure map (marker size = TIV, colour = site hazard) and a log-scale EP curve, both theme-aware

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, SVG

**API:** `GET /api/v1/catrisk/book`, `POST /api/v1/catrisk/accumulation`, `POST /api/v1/catrisk/simulate`

---

### 🛰️ Raster Change Detection
Two satellite passes over the same ground, weeks apart — what changed? The classic multitemporal remote-sensing chain, end to end, framed civil-forward: new construction, burn scars, reservoir drawdown, flood extent.

- **Change Vector Analysis** — per-pixel Euclidean magnitude of the spectral difference vector across all bands. Robust to which band carries the change, and to the uniform illumination offset epoch B deliberately carries
- **Otsu automatic threshold** — a 128-bin histogram walked once, maximizing between-class variance `w₀·w₁·(μ₀ − μ₁)²`. No magic number; the threshold visibly moves as sensor noise rises
- **Morphological open** — erode then dilate with a 3×3 structuring element to kill speckle. With `n > 1` iterations that is erode×n *then* dilate×n, one open with an n-scaled element
- **Connected components** — two-pass union-find with path compression over a flat parent array, 8-connectivity, accumulating area, centroid, mean magnitude and bounding box in the second sweep
- **Scored against ground truth** — four changes are planted in the synthetic epoch B (a subdivision, a burn scar, a reservoir drawdown, a solar array), so the page reports "4 of 4 recovered" rather than just drawing a picture
- **Benchmarked against its own managed fallback: 1.24× across the compute stages, bit-identical results** — 2.11× on the branch-free Otsu pass, 1.09× on the branch-heavy morphological open. The Details page carries the table and what the gradient means
- Every raster is drawn to a `<canvas>` via `ImageData`, not to 65,536 DOM nodes; a pointer-drag before/after swipe over a false-colour NIR/Red/Green composite, and an SVG histogram with the threshold marked

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, Canvas, SVG

**API:** `GET /api/v1/change/scene`, `POST /api/v1/change/detect`
### ⚡ Outage Manager & Network Trace
Electric distribution outage management — the flagship GIS application in utilities. Answers the three questions a control room needs within a minute of a recloser locking out: who is out, what isolates the fault, and who can be restored by backfeeding from an adjacent feeder.

- **Downstream fault trace** — breadth-first sweep from the faulted element's downstream node, stopping at open devices, yielding the de-energized section and the customers affected
- **Upstream trace and isolation** — one sweep from the substation tags every node with the element that energizes it, so the path back to the breaker falls out in a single pass. Isolation returns the nearest upstream protective device (fuse before recloser before breaker) plus the *frontier* of closed downstream switches — not every switch, which would fragment the feeder and leave nothing for a tie to reach
- **Tie-switch restoration** — a managed search over every normally-open tie, each candidate evaluated by a native connectivity sweep, rejecting any plan that would backfeed into the fault. Emits an ordered switching plan and an estimated SAIDI-minutes-avoided figure
- **Directed storage, undirected energization** — `from`/`to` record nominal radial flow so upstream and downstream have meaning, but a closed tie backfeeds against that direction, so energization is pure connectivity. Tracing it directionally would report a successful backfeed as reaching nobody
- Deterministic synthetic circuit over Redlands: one substation, two radial feeders (267 elements, 2,495 customers), 22 fused laterals, three reclosers, seven sectionalizing switches, one normally-open tie — no database, no persistence
- **Benchmarked against its own managed fallback: 1.29× on the shipped circuit, 0.87× at 4,603 elements, byte-identical results.** Three P/Invoke calls per trace each re-marshal the element array, so the overhead crosses over the gain as the circuit grows. The Details page carries the table and the explanation
- Self-contained SVG single-line diagram — click a conductor to fault it, hover any element for its label, device type, state and customer count

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, SVG

**API:** `GET /api/v1/outage/network`, `POST /api/v1/outage/trace`, `POST /api/v1/outage/restore`
### 🚑 Emergency Response Coverage Optimizer
Public-safety deployment analysis over the real Redlands street network. Answers the question a fire chief has to defend in front of a city council — where the stations go — and scores the answer against **NFPA 1710**, the career-department standard of a first-due engine on scene within four minutes of travel time for 90% of incidents.

- **Drive-time isochrones** — not a circle on a map, but graded travel-time bands over the actual road graph. The graph engine's one-to-all pass keeps the per-node cost, which is what banding needs; every reachable node lands in exactly one band and unreachable nodes are counted, not dropped
- **p-median station siting** — greedy seeding followed by Teitz-Bart vertex substitution over a candidate-to-demand travel-time matrix. The substitution loop caches the *nearest* and *second-nearest* open facility per demand point, which turns each of the thousands of swap trials per pass from `O(demand × p)` into `O(demand)`
- **Three objectives that actually diverge** — weighted mean, weighted 90th percentile, and maximum coverage. Switching between them moves the chosen stations, which is the demo: on the shipped scenario at four stations the mean objective picks a different site set than the p90 objective
- **Weighted p90, done correctly** — demand points are ordered by response time and call volume is accumulated until it crosses 90% of the total. It is *not* the p90 of the unweighted list; a single heavy neighbourhood can set it alone, which is exactly what the standard is written to capture
- **Baseline versus optimized, side by side** — today's two stations reach 76.4% of call volume within four minutes and fail NFPA 1710 at a p90 of 4.68 minutes; four optimized stations reach 94.1% at a p90 of 2.42 minutes and pass. That comparison is the whole point of the page
- 450 clustered demand points and 24 candidate sites, all snapped to real network nodes so the matrix has no unreachable pairs; generated from a fixed-seed LCG with no database
- **Benchmarked against its own managed fallback: 1.2×–2.2× depending on the objective, with identical chosen stations, assignments, and statistics.** The Details page carries the measurement table and explains why the spread exists
- Leaflet map with call-volume-scaled demand circles, an assignment starburst that makes the districting legible, clickable isochrone painting, and a theme-aware SVG response-time histogram overlaying today's distribution against the optimized one

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, Leaflet, SVG

**API:** `GET /api/v1/response/scenario`, `POST /api/v1/response/isochrone`, `POST /api/v1/response/optimize`
### 🚚 Fleet Route Optimizer
Last-mile dispatch planning as an actual optimization problem rather than a shortest-path lookup. Given a depot, 40 deliveries with load and delivery windows, and a fleet with a capacity limit and a shift end, it decides which truck serves which stops, in what order, and when each one arrives — the **Capacitated Vehicle Routing Problem with Time Windows**, solved over the real Redlands street network.

- **Clarke-Wright parallel savings construction** — one route per stop, then merge route ends in descending `d(0,i) + d(0,j) − d(i,j)` order while capacity and every time window still hold
- **2-opt and Or-opt local search** — segment reversal within a route and relocation of 1–3 consecutive stops into any position of any route, first-improvement, with the objective recorded after every pass so the convergence curve is real data rather than decoration
- **Time windows that actually bind** — the feasibility walk waits on early arrival and fails on late arrival, and the return leg is checked against the shift end. `ArrivalMinutes` is emitted parallel to `StopIds` so the schedule timeline proves the windows are respected
- **A fixed cost per vehicle** in the objective, which is what makes the solver choose four trucks over five. Three calibrated presets: 2 of 3 vans on loose windows, 4 of 5 trucks on a full day, all 6 trucks once the windows narrow to an hour
- **Real road geometry** — one `(n+1)²` road-distance matrix built server-side, then every solved leg expanded back into a street-following polyline via A*. Distance becomes travel minutes *outside* the graph search, because A*'s haversine heuristic is admissible only while edge costs are kilometres
- Client sends the scenario (~2 KB), not the graph (~425 KB) — deliberately unlike the older Route Planner page
- **Benchmarked against its own managed fallback: ~2.3× speedup, bit-identical solutions.** The first version was 1.7× *slower*; the Details page explains what changed and why there is no SIMD in it

**Stack:** ASP.NET Core, C++20/CMake, P/Invoke, Leaflet, SVG

**API:** `GET /api/v1/fleet/scenario`, `POST /api/v1/fleet/optimize`

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, C# 14 |
| Frontend | Bootstrap 5, Font Awesome, Vanilla JavaScript, DataTables, D3.js |
| Razor Pages | ASP.NET Core Razor Pages |
| Database | PostgreSQL via Npgsql + EF Core |
| ORM | Entity Framework Core (code-first, fluent mappings, migrations) |
| GIS | ArcGIS JavaScript API, ArcGIS REST `sampleserver6.arcgisonline.com` |
| Authentication | Google OAuth 2.0, Cookie Authentication |
| Caching | `IDistributedCache` — Redis in production, `MemoryDistributedCache` in dev (geocoding + job state) |
| Native Performance | C++20 native-kernel pattern with stable C ABI, P/Invoke bridge, `IsAvailable` guard, and managed C# fallback across thirteen kernels (`portfolio_scoring`, `geostream_processor`, `spatial_geometry_kernel`, `raster_terrain_kernel`, `spatial_graph_engine`, `spatial_cluster_kernel`, `viewshed_kernel`, `spatial_overlay_kernel`, `cat_risk_kernel`, `change_detection_kernel`, `network_trace_kernel`, `facility_location_kernel`, `vrp_solver_kernel`) |
| API Docs | Scalar / OpenAPI (all environments), XML doc comments |
| Styling | Custom CSS with dark/light mode support |
| Hosting | Render (continuous deployment from GitHub) |
| Database Hosting | Neon.tech PostgreSQL |
| Containerization | Docker (multi-stage build), Docker Compose (local dev with Redis) |
| Orchestration | Kubernetes manifests (Deployment, Service, HPA, ConfigMap, Secret) |

---

## Redis Integration

Redis is optional in local development but becomes the shared distributed infrastructure when `Redis:ConnectionString` is configured through `Redis__ConnectionString`:

- **Batch Geocoding cache:** normalized address strings are cached through `IDistributedCache` to deduplicate repeated ArcGIS `findAddressCandidates` calls.
- **Batch Geocoding job state:** `RedisBatchJobStore` stores full `BatchJob` snapshots with a 24-hour TTL so any horizontally scaled replica can return polling status and completed results.
- **Reverse Geocoding cache:** snapped latitude/longitude keys cache ArcGIS `reverseGeocode` results with sliding expiration for repeated map-click lookups.
- **Data Protection key ring:** production deployments persist ASP.NET Core Data Protection keys to Redis under `DataProtection-Keys`, keeping cookies and antiforgery tokens valid across replicas.
- **Containers and Kubernetes:** Docker Compose includes a `redis:7-alpine` sidecar for local/staging parity, and the Kubernetes manifests include a Redis service for demo/staging scale-out. Production cloud deployments should prefer managed Redis.

When Redis is not configured, geocoding cache falls back to `MemoryDistributedCache`, batch job state falls back to `InMemoryBatchJobStore`, and local/container Data Protection keys can be persisted on the filesystem.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [Google Developer Console](https://console.developers.google.com/) project with OAuth 2.0 credentials
- Access to a [Neon.tech](https://neon.tech/) PostgreSQL database (or any PostgreSQL instance)
- Visual Studio 2022+ or VS Code

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/brianpcoffey/portfolio_v2.git
   cd portfolio_v2
   ```

2. **Configure credentials** — create `appsettings.Development.json` (never commit secrets):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=your_neon_host;Port=5432;Database=Portfolio;Username=your_user;Password=your_password"
     },
     "Authentication": {
       "Google": {
         "ClientId": "YOUR_CLIENT_ID",
         "ClientSecret": "YOUR_CLIENT_SECRET"
       }
     }
   }
   ```

3. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

4. **Apply database migrations** (runs automatically at startup, but can also be run manually):
   ```bash
   dotnet ef database update --project Portfolio.Repositories --startup-project Portfolio.Web
   ```

5. **Run the application:**
   ```bash
   dotnet run --project Portfolio.Web
   ```

6. Open `https://localhost:5001` in your browser.

### API Documentation

The project uses **OpenAPI** for API metadata and **Scalar** as the interactive API documentation UI. It does not currently use Swagger UI.

The OpenAPI/Scalar endpoints are mapped **unconditionally** in `Program.cs` via `MapOpenApi()` and `MapScalarApiReference()`, so they are available in all environments (intentionally public API documentation):

- Interactive API reference: `/scalar` (e.g. `https://localhost:5001/scalar` locally)
- Raw OpenAPI document: `/openapi/v1.json`

### Running Tests

```bash
dotnet test
```

675 tests, all passing (xUnit + Moq; no integration/DB tests). The native parity tests execute the managed C# fallback, so the full suite passes with no native shared libraries present.

---

## Configuration Reference

Key settings in `appsettings.json`:

```json
{
  "ArcGis": {
    "BaseUrl": "https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/MapServer"
  },
  "BatchGeocoding": {
    "MaxConcurrency": 4,
    "MinMatchScore": 80.0,
    "CacheTtlMinutes": 60
  },
  "ReverseGeocoding": {
    "GridResolutionDegrees": 0.001,
    "CacheSlidingExpirationMinutes": 30
  },
  "Redis": {
    "ConnectionString": ""
  }
}
```

Leaving `Redis:ConnectionString` empty activates the in-process `MemoryDistributedCache` fallback — Redis is only required in production. Set it via the `Redis__ConnectionString` environment variable (double-underscore is the .NET config hierarchy separator).

---

## Deployment

### Render (current production)
- Continuous deployment from the [`main` branch on GitHub](https://github.com/brianpcoffey/portfolio_v2).
- Database hosted on **Neon.tech** PostgreSQL.
- EF Core migrations run automatically at startup — no manual migration step required.
- HSTS enabled in non-development environments; HTTPS redirection runs only in development (Render terminates TLS at its proxy, so the container serves plain HTTP).
- Secrets (`DATABASE_URL`, `Redis__ConnectionString`, Google OAuth credentials) are injected as environment variables.

### Docker (local dev + staging)
```bash
# Build and run with a local Redis sidecar
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up
```
The base `docker/docker-compose.yml` defines the `portfolio` app and a `redis:7-alpine` sidecar with a health check. The override injects `DATABASE_URL` and sets `Redis__ConnectionString=redis:6379,abortConnect=false`.

### Kubernetes (manifests in `k8s/`)
See [`k8s/README.md`](k8s/README.md) for full deploy order. Quick reference:
```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml   # fill in real values first
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/hpa.yaml
```
The HPA scales between 2 and 10 replicas based on CPU (70%) and memory (80%) utilization. All replicas share geocoding cache and job state via Redis.

---

## License

This project is public. You may use or adapt it for personal portfolios or learning purposes.
