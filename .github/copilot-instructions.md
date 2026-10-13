# AI Agent Instructions - Portfolio Solution

> The rules that must not be broken, and nothing that drifts. Facts that change (counts, routes,
> kernels, rate-limit policies, CSP, CI jobs, file layout, conventions) live in
> **`docs/agent-context.md` - read it first.** Where any document and the source disagree, the source wins.

## What this repo is
- A .NET 10 Razor Pages + versioned JSON API (`/api/v1`) portfolio, live at brianpcoffey.com (Render, Neon
  PostgreSQL), with 18 interactive demo projects, mostly GIS / spatial compute. It is a hiring artifact for
  full-stack/backend .NET roles: engineering rigor shown honestly.
- Six projects in `Portfolio.sln`: `Portfolio.Common`, `Portfolio.Repositories`, `Portfolio.Services`,
  `Portfolio.Web`, `Portfolio.Tests`, `Portfolio.Benchmarks`.
- Flow: Razor Page / API controller -> service interface -> repository interface -> EF Core or a typed
  ArcGIS `HttpClient`. Spatial-compute services are stateless and database-free.

## Invariants
1. **Never hand-edit** `Portfolio.Services/Data/RedlandsRoadNetwork.cs` (generated from OpenStreetMap), EF
   migrations or the model snapshot. Schema changes only through `dotnet ef migrations add`.
2. **Native kernels:** the managed C# fallback is production behavior and is never removed. Native and
   managed results stay identical. Production ships no native code. A C ABI change updates the C header,
   the C# struct, the native sources and the parity tests together.
3. **Honest claims:** measured native speedups are 0.37x-2.26x (9 wins / 5 losses / 7 ties at +/-15%;
   `native/README.md` is the authority). No SIMD, AVX or "high-performance" claims in site copy, README or
   resume material.
4. **Models, DTOs, enums and ArcGIS wire types live only in `Portfolio.Common`.** DTOs are plain classes;
   validation is imperative in services.
5. **Layering:** controllers return DTOs (never entities) and call service interfaces; services own the
   business rules and throw `ArgumentException` (400), `KeyNotFoundException` (404) or
   `InvalidOperationException` (409); repositories return `null`/`false`, never throw business exceptions
   and never map to DTOs.
6. **Error text is public API.** User-facing validation must be `throw new ArgumentException(message,
   paramName)`; BCL throw helpers (`ArgumentNullException.ThrowIfNull`, ...) produce only the generic
   "The request was invalid." Keep exception messages byte-identical unless a test or spec changes them.
   The `{ "error": "..." }` 400 shape stays: the JavaScript reads `body.error`.
7. **Identity:** two separate systems (anonymous GUID cookie, Google sign-in). `"Authenticated"` is the
   only policy; add no auth schemes or policies. Every controller action is `[Authorize(Policy =
   "Authenticated")]` or `[AllowAnonymous]`. Never accept a user id from a request body or query string;
   scope per-user rows by the current profile id in the service/repository.
8. **Pipeline order** in `Portfolio.Web/Startup/Pipeline.cs` is a contract, and `UseSession` stays.
   New services register in `Startup/DataRegistration.cs`; `Program.cs` only composes.
9. **Rate limiting:** a new native-compute controller gets `[EnableRateLimiting("compute")]` + `[CpuBound]`;
   a new paid-ArcGIS or heavy endpoint gets `"expensive"` (`[CpuBound]` only when CPU-bound, never on I/O proxies).
10. **Public routes and JSON shapes do not change** without updating `wwwroot/js/api-config.js`, the callers
    and the contract/golden tests together.
11. **Security:** never print or commit secrets or connection strings (`appsettings.Development.json` and
    `.env` are untracked). Put untrusted text into markup only through `PortfolioDom` (`escapeHtml`/`el`).
    No inline event-handler attributes; inline `<script>`/`<style>` get their CSP nonce from a tag helper.
12. **No new frameworks** (AutoMapper, MediatR, FluentValidation, Dapper, npm, a bundler, TypeScript)
    without the owner's approval.

## Frontend
- Colors come from the CSS tokens in `wwwroot/css/base.css` (`--chart-*` on the page surface, the
  theme-independent `--map-*` for marks over map tiles); never hard-code a color. Shared modules
  (`PortfolioApi`/`apiFetch`, `PortfolioDom`, `PortfolioUi`, `PortfolioFormat`, `PortfolioCharts`, `PortfolioMap`,
  `PortfolioEsri`) replace per-page copies. Page scripts are not always a single `app.js`.
- Details pages follow one of two templates (8-section or 6-section); describe hosting as Render +
  PostgreSQL, and Azure/AKS only as future. Verify UI work in a browser with DOM/geometry assertions, not
  screenshots.

## Tests and CI
- One test class per service/controller; `Mock<IXxx>` fields in controller tests, inline SUTs for pure
  compute services; assert the computed result, never whether native ran. Every new action or method needs
  a happy path plus validation/not-found cases. Tests never reach arcgis.com. Details: agent-context section 9.
- CI treats warnings as errors and gates coverage at 80% line / 60% branch: never lower either. A push to
  `main` is a production deploy; work on `cleanup/*` or feature branches and open PRs.
- Build, test and tool commands: agent-context section 7.

## Do not
- Hand-edit generated code, or make claims the benchmarks and code do not back.
- Submit job applications or post anything on the owner's behalf; draft it for approval.
- Disable the `onrender.com` subdomain, or flip any public "open to work" visibility.
