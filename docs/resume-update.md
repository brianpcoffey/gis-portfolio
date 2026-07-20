# Résumé — what changed, and the one step left

The **General SWE** variant in Google Drive has already been updated in place
([open it](https://docs.google.com/document/d/1H9jWTXLoWiexH_4a-3E2FGKZAKJs6ZcB/edit)).
Four Project bullets and five Skills lines were replaced; bold labels and the one-page fit
are intact.

**Still to do:** File → Download → PDF, then overwrite
`Portfolio.Web/wwwroot/files/Brian-Coffey-Resume.pdf`. The site currently serves the previous
export, so the download link works but is one revision behind.

---

## Project bullets — before and after

| Before | After |
|---|---|
| Designed and built a full-stack application platform… repository/service patterns, dependency injection, asynchronous data pipelines | Built and deployed a full-stack web platform on ASP.NET Core 10 and C#, delivering 18 interactive applications backed by secure REST APIs with Google OAuth 2.0, role-based authorization, Redis caching, and Entity Framework Core over PostgreSQL |
| Engineered secure, production-grade RESTful APIs with policy-based authorization, cancellation tokens… | Developed 13 high-performance C++ modules integrated with C# through native interop, powering route optimization, geospatial clustering, terrain visibility analysis, risk simulation, and satellite image change detection |
| Delivered a containerized, continuously deployed platform using Docker, GitHub Actions, and Render… | Created an automated performance benchmarking suite comparing each C++ module against its C# equivalent, publishing measured results and surfacing two defects that had previously gone undetected |
| Modeled complex relational data structures using EF Core code-first migrations… | Implemented a CI/CD pipeline in GitHub Actions that runs 680 automated tests, validates the Docker production image, and gates deployment on every commit |

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
