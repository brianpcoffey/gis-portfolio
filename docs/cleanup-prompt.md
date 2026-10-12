# Prompt: Comprehensive cleanup of the Portfolio codebase

> Paste everything below the line into a fresh Claude Code session started in the repo root,
> with `docs/agent-context.md` attached (or tell the agent to read it first).

---

You are leading a comprehensive, **behavior-preserving** cleanup of this repository: a .NET 10
Razor Pages + API portfolio with 13 native C++ kernels. Read `docs/agent-context.md` first, then
`README.md`, `native/README.md`, and `docs/plans/README.md`. Where `.github/copilot-instructions.md`
disagrees with the source or the context file, the source wins.

The owner wants a review **and** a solution, not a report. Deliver a plan first, then implement it.

## Goals (in priority order)

1. **Security and compliance (vulnerabilities / STIGs).** Review against OWASP ASVS/Top 10 and the
   DISA **Application Security and Development STIG** (plus the ASP.NET/.NET-relevant checks, and
   the Container Platform / Docker / Kubernetes guidance for `Dockerfile`, `docker/`, `k8s/`).
   Produce a finding table: ID, STIG/ASVS reference, location, severity, fix, or an explicit
   "accepted / not applicable" with a reason. Starting points I already know about, not a ceiling:
   - CSP is minimal (`frame-ancestors/base-uri/object-src` only) — design a real `script-src`/`style-src`
     policy (nonces or hashes; the ArcGIS JS API and Leaflet load from CDNs, so allow-list them and
     consider Subresource Integrity).
   - `AllowedHosts: "*"` in both appsettings files.
   - 17 JS files use `innerHTML` — audit each for unescaped API/user data (XSS) and fix with
     `textContent`/DOM building or a single vetted escape helper.
   - Forwarded-headers trust is fully open (`KnownProxies/KnownIPNetworks` cleared) and rate limiting
     partitions on the resulting IP — assess spoofing risk and whether it can be narrowed on Render.
   - Seven spatial controllers (`geostream, geometry, raster, network, clustering, viewshed, overlay`)
     lack `[EnableRateLimiting("expensive")]`; check every endpoint has sensible request-size limits.
   - Public OpenAPI/Scalar in production, cookie/session/antiforgery settings, CORS posture, error
     detail leakage in `ApiExceptionMiddleware`, logging of sensitive data, Docker image (non-root user,
     pinned digests, no secrets in layers), k8s (securityContext, resource limits, NetworkPolicy,
     secret handling), CI (action pinning to SHAs, token permissions), dependency audit.
   - Input validation on every request DTO (size caps, NaN/Infinity, collection lengths) — look for
     DoS via large payloads on the compute endpoints.
2. **Cyclomatic / cognitive complexity.** Measure first (use `dotnet` analyzers/metrics or a script;
   report the worst 25 methods with their scores and file:line). Then reduce: extract methods,
   guard clauses, table-driven logic, split god classes (largest: `UserProfileSeedService`, `Program.cs`,
   `FleetRoutingService`, `ResponseCoverageService`, `ChangeDetectionService`, `SpatialGraphService`,
   and the 600–700-line JS files). Move `Program.cs` registration into focused extension methods
   (`AddPortfolioResilience`, `AddPortfolioAuth`, `AddPortfolioRedis`, ...) while **preserving the
   documented middleware order**. Set a target (e.g. no method above 15) and enforce it with an analyzer
   rule if feasible.
3. **Refactor and simplify.** Remove duplication (e.g. the four near-identical ArcGIS resilience
   pipelines in `Program.cs`, the duplicated project-card markup in `Index.cshtml`, repeated
   controller try/catch blocks, repeated native-bridge boilerplate across 13 bridges — only if the
   shared abstraction is clearly simpler than what it replaces). Prefer deleting code to adding
   abstraction. Remove dead code, unused usings, unused packages (`Microsoft.EntityFrameworkCore.InMemory`
   in the Repositories project and the stray `<Folder Include>` entries are candidates), and obsolete
   endpoints only if confirmed unused by the frontend and tests — flag anything public-facing for my
   decision instead of deleting it.
4. **Comments and XML docs.**
   - Remove comments that restate the code, banner dividers, commented-out code, and stale TODOs.
   - **Keep** a short "why" comment where code would otherwise look like a bug (e.g. why the default
     challenge scheme is Cookie, why rate limiting sits before auth, why HTTPS redirection is dev-only,
     native/managed parity caveats, the `IsNativeInvocationException` wart, the one-based node IDs).
     List the ones you propose to keep in the plan so I can veto them.
   - XML docs: shorten every `<summary>` to one sentence, drop `<param>`/`<returns>` that only repeat
     the name. Do **not** delete summaries on public controller actions and DTO members — they feed the
     OpenAPI/Scalar schema. Keep `GenerateDocumentationFile` on.
5. **Frontend consistency, responsiveness, UI/UX, accessibility.**
   - Audit all 18 demo pages and the home/layout at 360, 768, 1280 px, in light and dark mode, with
     keyboard-only navigation, and `prefers-reduced-motion`. Check WCAG 2.2 AA (contrast, focus order
     and visible focus, labels, ARIA landmarks, live regions for async results, touch targets).
   - Unify the design system: one set of tokens in `base.css`, no hard-coded colors, one button/card/
     form/table/KPI/legend pattern, consistent page header + control panel + results layout across
     demos, consistent loading / empty / error states (a single toast + inline-error helper in
     `api-fetch.js`/`toasts.js`), consistent number and unit formatting.
   - Responsiveness: no horizontal page scroll, wide tables/maps scroll or reflow inside their own
     container, maps and SVG/canvas visualizations resize correctly.
   - Reduce per-page CSS/JS duplication: shared modules for fetch + error handling, theme-aware chart
     colors, SVG/canvas helpers, Leaflet setup. Keep vanilla JS and no build step unless you make a
     strong case in the plan.
   - Performance: avoid reloading Leaflet/ArcGIS where unused, lazy-load heavy scripts, `defer`,
     image/font weight, caching headers for static assets.
6. **Test and CI quality.** Fill coverage gaps exposed by the refactor (do not lower the 80% line /
   60% branch ratchets; raise them if coverage rises). Add tests for any security fix. Add regression
   tests for behavior you touch before you touch it (characterization tests). Consider adding an
   analyzer/`dotnet format` check and a dependency/vulnerability check to CI.
7. **Documentation hygiene.** Fix the stale `.github/copilot-instructions.md` (nonexistent
   `FiberClientsController`, old routes, 5-kernel scope, test count, `SameSite`), reconcile the
   `portfolio_v2` vs `gis-portfolio` repo name in `README.md` / `docs/render-deployment.md`, and keep
   `docs/agent-context.md` current. Anything else you spot worth improving: suggest it in the plan
   under "Additional recommendations" with effort/impact, and don't do it unless it's low-risk.

## Hard constraints (do not violate)

- **Behavior-preserving.** Public routes, JSON shapes, and `NativeAccelerated` semantics don't change
  unless a security fix requires it; if one does, update `wwwroot/js/api-config.js`, the callers, and
  the tests together and call it out.
- **Never hand-edit** `Portfolio.Services/Data/RedlandsRoadNetwork.cs` (OSM-generated) and don't edit
  EF migrations or the model snapshot except via `dotnet ef`.
- **Keep every managed fallback** and the native/managed parity guarantees. A C ABI struct change
  must be made in the C header, the C# struct, the native sources, and the parity tests together.
  Production ships no native code; don't change that.
- Keep the two identity systems separate (anonymous GUID vs Google), the `"Authenticated"` policy as
  the only policy, and the pipeline order in `Program.cs`. No new frameworks (AutoMapper, MediatR,
  FluentValidation, Dapper) without asking.
- All models/DTOs/enums stay in `Portfolio.Common`.
- **Site copy and benchmark claims must stay honest** (measured 0.37×–2.26×; no SIMD/"high-performance"
  claims). Don't rewrite marketing or résumé content beyond consistency fixes.
- `main` auto-deploys to production. **Work on a branch** (`cleanup/<area>`), small reviewable commits
  per area, never push to `main`, never force-push. Open PRs; don't merge.
- No secrets in commits, logs, or output. Don't submit anything to external services.
- Don't change the visible design language wholesale — improve consistency and usability within the
  existing look. Propose any larger visual change as an option in the plan.

## How to run this — two stages, different models

**Stage 1 — Plan (Opus 5.5, max effort).** Spawn a planning agent with `model: "opus"` and
`effort: "max"`. It does read-only analysis and writes the plan to disk — **the plan must be a durable
file, not a chat reply**: `docs/cleanup/PLAN.md` plus `docs/cleanup/findings-security.md`,
`docs/cleanup/findings-complexity.md`, and `docs/cleanup/findings-frontend.md`. Parallelize analysis
with read-only subagents (one per layer/area) where it helps. The plan must contain:

1. Baseline measurements: build warnings, test count and result, coverage %, complexity top-25, LOC by
   project, `innerHTML` sites, CSS/JS duplication hot spots.
2. Findings tables (severity, evidence with `file:line`, recommended fix, risk of the change).
3. A work breakdown of **independent, ordered work packages** — each with: goal, exact files, steps,
   acceptance criteria, tests to add/run, risk, rollback, and an estimated size (S/M/L). Mark which
   packages can run in parallel (disjoint files) and which depend on others. Security fixes and
   characterization tests come first; mechanical comment/doc trimming comes last (it touches the most
   files and would conflict with everything else).
4. Decisions needed from me, each with a recommended default.
5. A verification protocol per package.

**Stop after Stage 1 and show me the plan summary and the decisions list. Wait for my go-ahead before
changing any source file.**

**Stage 2 — Implement (Sonnet 5.5, medium effort).** After I approve, execute the work packages with
subagents using `model: "sonnet"` and `effort: "medium"`, one package per agent, in the dependency
order from the plan (run disjoint packages in parallel, each in its own git worktree via
`isolation: "worktree"`). Each implementer gets: the package spec, `docs/agent-context.md`, and the
constraints above — and nothing else to re-derive. After each package:

- `dotnet build -c Release` with `-p:ContinuousIntegrationBuild=true` (warnings as errors),
- `dotnet test` (and again with `PORTFOLIO_DISABLE_NATIVE=1`), coverage not below the ratchets,
- if native code or bridges changed: build the kernels with CMake and run the suite with them present,
- for frontend changes: run the app (`.claude/launch.json` → `portfolio-web`, or a scratch `-o` build on
  an alternate port if `bin/` is locked) and verify with DOM/geometry assertions via the browser
  tooling (**screenshots hang on OSM tiles — don't rely on them**), at the three viewport widths, light
  and dark, plus console/network error checks,
- a short self-review of the diff against the package's acceptance criteria.

Then run an independent review pass (a fresh agent that sees only the diff and the constraints, not the
implementer's reasoning) before the package is committed. Fix what it finds.

## Reporting

At the end, give me: what changed per package (one line each), before/after metrics (complexity top-25,
warnings, tests, coverage, LOC, security findings closed vs accepted vs open), anything skipped and why,
the PR links, and a short list of follow-ups. Be explicit about anything you could not verify. If a test
fails or a step was skipped, say so with the output; don't report success you didn't observe.

Begin with Stage 1.
