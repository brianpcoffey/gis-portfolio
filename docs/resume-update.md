# Résumé — what changed, and the one step left

The **General SWE** variant in Google Drive has already been updated in place
([open it](https://docs.google.com/document/d/1H9jWTXLoWiexH_4a-3E2FGKZAKJs6ZcB/edit)).
Four Project bullets and five Skills lines were replaced; bold labels and the one-page fit
are intact.

**Published:** the updated export was committed to `Portfolio.Web/wwwroot/files/Brian-Coffey-Resume.pdf`
on 2026-07-19 (commit `59cfd48`, "Publish the updated résumé PDF"). After any future edit to the
Drive source: File → Download → PDF, then overwrite that file; the home page reads the file's real
size, page count and date, so nothing else needs editing. The PDF can lag the Drive source until
that is done, and this repo cannot show whether it currently does.

---

## Project bullets — before and after

| Before | After |
|---|---|
| Designed and built a full-stack application platform… repository/service patterns, dependency injection, asynchronous data pipelines | Built and deployed a full-stack web platform on ASP.NET Core 10 and C#, delivering 18 interactive applications backed by secure REST APIs with Google OAuth 2.0, role-based authorization, Redis caching, and Entity Framework Core over PostgreSQL |
| Engineered secure, production-grade RESTful APIs with policy-based authorization, cancellation tokens… | Developed 13 high-performance C++ modules integrated with C# through native interop, powering route optimization, geospatial clustering, terrain visibility analysis, risk simulation, and satellite image change detection |
| Delivered a containerized, continuously deployed platform using Docker, GitHub Actions, and Render… | Created an automated performance benchmarking suite comparing each C++ module against its C# equivalent, publishing measured results and surfacing two defects that had previously gone undetected |
| Modeled complex relational data structures using EF Core code-first migrations… | Implemented a CI/CD pipeline in GitHub Actions that runs 680 automated tests, validates the Docker production image, and gates deployment on every commit |

> **Fact check against the repo (the wording above is quoted as written; the résumé text is the
> owner's to change).** Four phrases do not match what the code and CI show today:
> - *"high-performance C++ modules"* (bullet 2) — the benchmarks measured 0.37×–2.26× (9 wins /
>   5 losses / 7 ties at ±15%, see the root `README.md`, section "Measured speedup"), and the rule further down in this document
>   rules out "high-performance computing" claims.
> - *"role-based authorization"* (bullet 1) — there are no roles; the one authorization policy is
>   `"Authenticated"` (a signed-in Google user).
> - *"runs 680 automated tests"* (bullet 4) — the figure is stale and drifts; `dotnet test` prints
>   the current count, which is larger.
> - *"gates deployment on every commit"* (bullet 4) — true only while Render's Auto-Deploy is off
>   and the `RENDER_DEPLOY_HOOK` secret is set, so that CI's `deploy` job is what ships `main`
>   (see `docs/render-deployment.md`); with Auto-Deploy on, a red build still ships.
>
> What does match: 18 interactive applications, 13 native C++ kernels each with a managed
> fallback, a CI pipeline that compiles the C++ and runs the suite with and without it, and the
> benchmark suite that caught two native defects.

The originals described work that could apply to any competent CRUD application. The
replacements lead with what is actually uncommon: native interop with verified fallbacks,
published benchmarks, and a CI pipeline that compiles C++ on every push. Bullets 1 and 4
absorbed the OAuth / EF Core / Docker keywords the old bullets 3 and 4 carried, so nothing
ATS-relevant was lost.

## Skills — added

`native interop (P/Invoke)` · `Polly` · `Redis` · `Kubernetes` · `GitHub Actions` ·
`Leaflet` · `ArcGIS` · `Moq` · `automated testing` · `REST APIs` (from "RESTful APIs") ·
`OpenAPI/Swagger` (from "Swagger")

Dropped `SSMS` for space — it is implied by SQL Server and was the least load-bearing item.
`Languages` and `Methodology` are unchanged.

## Deliberately kept plain

Written for a recruiter screening on keywords, not a hiring manager reading for depth. So
"route optimization" rather than *CVRPTW metaheuristic*, "geospatial clustering" rather than
*DBSCAN*, "terrain visibility analysis" rather than *viewshed ray casting*, "satellite image
change detection" rather than *CVA magnitude with Otsu thresholding*. The specifics live on
the portfolio site and in the Details pages, which is where an interested engineer will go.

No "SIMD", "AVX2", or "high-performance computing" claims — the benchmarks measured 0.37×–2.26×
and do not support them. A resume line that collapses under one follow-up question costs more
than it gains. The benchmarking bullet is framed around rigor and defect-finding, which is
both true and harder to challenge.

## Syncing the GIS and Defense variants

The Project bullets apply verbatim. For **GIS**, reorder bullet 2 to lead with the spatial
work (terrain visibility, geospatial clustering, mapping). For **Defense**, lead bullet 2 with
satellite image change detection, and consider surfacing the active Secret clearance higher in
the summary.
