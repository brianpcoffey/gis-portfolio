# AI Agent README - Portfolio Solution

> Authoritative guide for AI coding agents working in this repo. Only patterns
> **observed in code** are documented. Anything else is flagged as
> *"No consistent pattern found"*.

---

## 1. Solution Overview

A .NET 10 Razor Pages web application that lets visitors explore ArcGIS map
features (the "State Explorer" project), save those features, and organize
them into named collections. It also exposes a small claims/profile API for
anonymous users, plus three ArcGIS-backed geocoding microservices, a fiber
order management domain, and a home-finder property scoring feature.

**Projects (5):**

| Project | Role |
|---|---|
| `Portfolio.Web` | ASP.NET Core host: Razor Pages + API controllers + middleware + DI composition root |
| `Portfolio.Services` | Business logic and orchestration |
| `Portfolio.Repositories` | EF Core `DbContext`, repositories, fluent mappings, migrations |
| `Portfolio.Common` | POCO entity models, DTOs, enums, and ArcGIS wire models (no behavior) |
| `Portfolio.Tests` | xUnit + Moq unit tests for services and controllers |

**Architecture:** Classic 4-layer (Razor Pages / Controllers -> Services ->
Repositories -> EF Core / PostgreSQL). DTOs cross every boundary leaving the
service layer. All API controllers are versioned under `api/v{version}` using
`Asp.Versioning`.

**Key domains/features:**
- Anonymous user identity (cookie-based GUID, established in middleware)
- Saved Features (per-user GIS features pulled from ArcGIS)
- Collections (per-user grouping of saved features, requires authentication)
- User Profile + arbitrary key/value Claims (per anonymous user)
- ArcGIS proxy querying `sampleserver6.arcgisonline.com`
- Google OAuth login (Cookie + Google authentication schemes)
- **Batch Geocoding** - CSV upload -> async job pipeline -> ArcGIS `findAddressCandidates` -> `IBatchJobStore` + Polly circuit breaker
- **Reverse Geocoding** - lat/lng -> grid-snapped `IDistributedCache` -> ArcGIS `reverseGeocode`
- **Address Standardization & Validation** - freeform parse -> suffix normalization -> ArcGIS validation with fallback and `ConfidenceTier`
- **Fiber Order Management** - CRUD for orders, materials, shipments, and clients; dashboard aggregation (MTD revenue, open orders, active shipments, low-stock alerts, top clients, orders-by-status)
- **Home Finder** - preference-based property scoring, saved searches, property detail lookup

---

## 2. Architecture & Layer Rules

### Flow

```
Razor Page / API Controller
        |
        v
   Service (interface)
        |
        v
 Repository (interface)
        |
        v
  PortfolioDbContext (EF Core)  /  HttpClient (ArcGIS)
```

### Controllers (`Portfolio.Web/Controllers/Api/*`)
- MUST inherit `ControllerBase` and be decorated with `[ApiController]`,
  `[Route("api/[controller]")]` (or an explicit lowercase route).
- MUST be decorated with `[Authorize(Policy = "Authenticated")]` **or**
  `[AllowAnonymous]` -- never left undecorated.
- MUST only:
  - Validate trivial input shape (null/whitespace, presence of keys).
  - Translate service exceptions into HTTP status codes.
  - Return DTOs (never EF entities).
- MUST NOT:
  - Touch `PortfolioDbContext` or any repository directly.
  - Contain business rules, mapping logic, or domain validation.
  - Read or trust any user-supplied `UserId`/`OwnerId` in the request body.

### Services (`Portfolio.Services/Services/*`)
- Implement the matching `Portfolio.Services.Interfaces.I{Name}Service`.
- Own all business rules: required-field checks, uniqueness checks,
  default values, timestamp assignment (`DateTime.UtcNow`).
- Resolve the current user identity themselves (see Section 7) -- they MUST NOT
  accept user IDs as parameters from controllers.
- Map `Entity -> DTO` via a `private static MapToDto(...)` method.
- Throw typed exceptions to signal outcomes:
  - `ArgumentException` / `ArgumentNullException` -> 400
  - `InvalidOperationException` -> 409 (conflict)
  - `KeyNotFoundException` -> 404
  - `UnauthorizedAccessException` -> not currently caught by controllers
- MUST NOT use EF Core types (`DbContext`, `IQueryable`, `Include`, etc.).

### Repositories (`Portfolio.Repositories/Repositories/*`)
- Implement the matching `Portfolio.Repositories.Interfaces.I{Name}Repository`.
- Take `PortfolioDbContext` via constructor.
- Own all EF Core querying, `Include`, `AsNoTracking`, `SaveChangesAsync`.
- Every read/write that targets per-user data MUST filter by the owner key
  (`UserId` for anonymous-user data, `OwnerId` for authenticated-user data).
- MUST NOT throw business exceptions; return `null`/`false` for "not found".
- MUST NOT map to DTOs.

---

## 3. Coding Conventions & Patterns

### Naming
- Interfaces: `I{Name}Service`, `I{Name}Repository`.
- Async methods always end in `Async`.
- DTO suffixes:
  - `{Entity}Dto` -- read/return shape.
  - `{Entity}CreateDto` or `Create{Entity}Dto` -- write shape (both forms exist;
    follow the form already used by the surrounding feature).
  - `{Entity}UpdateDto` -- partial update shape.
- Private fields are prefixed with `_` (e.g. `_repo`, `_httpContextAccessor`).
- Files: one public type per file, file name = type name.

### Async/await
- Every public service and repository method is `async Task` / `async Task<T>`.
- Every such method accepts a final `CancellationToken cancellationToken = default`
  and forwards it to EF Core / `HttpClient` calls.
- No `.Result`, `.Wait()`, or `async void` in production code.

### DTOs vs Entities
- Controllers and services exchange **DTOs only**.
- Entities (`Portfolio.Common.Models.*`) never leave the repository layer
  except as inputs to a service-internal mapper.
- DTOs are plain property bags -- no methods, no validation attributes
  (validation is done imperatively in services).

### Error handling
- Services validate inputs at the top of the method using `ArgumentException`
  / `ArgumentNullException` / `ArgumentException.ThrowIfNullOrWhiteSpace`.
- Services throw `InvalidOperationException` for conflicts (duplicate name,
  duplicate saved feature) and `KeyNotFoundException` for missing entities.
- Controllers wrap calls in `try/catch` for the specific exception types they
  translate; uncaught exceptions fall through to `UseExceptionHandler("/Error")`.
- Error response body shape used everywhere: `new { error = "<message>" }`.

### Logging
- **All services, all repositories, and all API controllers carry `ILogger<T>`.**
  Structured templates are used throughout: `_logger.LogInformation("... {Key}", value);`.
- Log `Warning` for expected failures: not-found during delete, duplicate
  conflicts, missing anonymous identity.
- Log `Error` for unexpected failures: caught by `ApiExceptionMiddleware` and
  within `ArcGisService` HTTP error paths.
- **Do not log on every method entry.** Log errors and significant state
  changes only (creation, deletion, conflict). No `Debug`/`Trace` levels exist.

### Mapping
- Hand-rolled `private static MapToDto(...)` per service.
- No AutoMapper, no Mapster.

### Time
- All persisted timestamps are `DateTime.UtcNow`. Never use `DateTime.Now`.

---

## 4. Dependency Injection Rules

All DI is wired in `Portfolio.Web/Program.cs`. The pattern is:

```csharp
builder.Services.AddScoped<IXxxRepository, XxxRepository>();
builder.Services.AddScoped<IXxxService, XxxService>();
```

- **Lifetime: `Scoped` for every repository and service.** No `Singleton`,
  no `Transient` registrations exist for app code.
- `IHttpContextAccessor` registered via `AddHttpContextAccessor()`.
- `ArcGisService` is registered **twice** -- as a scoped service and via
  `AddHttpClient<IArcGisService, ArcGisService>()`. Preserve both lines if
  you touch DI registration; the typed `HttpClient` is what the service
  actually consumes.
- `PortfolioDbContext` is registered via `AddDbContext` with **PostgreSQL** (Npgsql provider).
- New services/repositories MUST be registered in `Program.cs` immediately
  next to the existing block under `// Dependency Injection`.
- The three geocoding services are registered as both a scoped service **and**
  via `AddHttpClient<IXxxService, XxxService>()`, matching the `ArcGisService`
  pattern, because each uses a typed `HttpClient` internally:
  - `IBatchGeocodingService` / `BatchGeocodingService`
  - `IReverseGeocodingService` / `ReverseGeocodingService`
  - `IAddressStandardizationService` / `AddressStandardizationService`
- `IDistributedCache` is registered conditionally: when `Redis:ConnectionString` is non-empty,
  `AddStackExchangeRedisCache` is used; otherwise `AddDistributedMemoryCache` provides an
  in-process fallback. Both `BatchGeocodingService` and `ReverseGeocodingService` consume
  `IDistributedCache` for geocoding deduplication.
- `AddMemoryCache()` is still registered for any remaining `IMemoryCache` consumers.
- `IBatchJobStore` is registered conditionally as **Singleton**:
  - `RedisBatchJobStore` when `Redis:ConnectionString` is non-empty (cross-pod job state).
  - `InMemoryBatchJobStore` otherwise (local dev / Render free tier).
  Both implementations are the only singletons in the app.
- Fiber services and repositories (`IFiberOrderService`, `IFiberMaterialService`,
  `IFiberShipmentService`, `IFiberClientService`, `IFiberDashboardService`, and
  their repository counterparts) are all registered as `Scoped`.
- Home Finder services (`IHomeScoringService`, `ISavedSearchService`) and
  `IPropertyRepository` are registered as `Scoped`.
- `UserProfileController` is versioned under `api/v{version:apiVersion}/users`
  and registered via standard MVC; no extra DI wiring required beyond the
  existing `IUserProfileService` registration.
- API versioning is enabled via `Asp.Versioning` (`AddApiVersioning` +
  `AddVersionedApiExplorer`). All new versioned controllers MUST carry
  `[ApiVersion("1.0")]`.

---

## 5. API Design Standards

### Routing
- Controllers: `[Route("api/[controller]")]`. One controller uses an explicit
  lowercase route (`[Route("api/features")]`); match the surrounding style
  when adding new endpoints.
- Route constraints used where the id is integer: `[HttpGet("{id:int}")]`.
- Mixed-shape ids are handled in the action body (see
  `SavedFeaturesController.Delete` which accepts both an int DB id and a
  string feature key).

### Action signatures
- Either `Task<IActionResult>` or `Task<ActionResult<T>>` -- both are present.
  Prefer `Task<ActionResult<T>>` when the success type is known, otherwise
  `Task<IActionResult>`.
- Always accept `CancellationToken cancellationToken` as the last parameter.
- Always decorate with `[ProducesResponseType(typeof(T), 200)]` plus every
  error code the action can return.

### Status codes
| Situation | Response |
|---|---|
| Success with body | `Ok(dto)` |
| Created | `CreatedAtAction(nameof(Get), new { id = created.Id }, created)` |
| Success no body | `NoContent()` |
| Bad input | `BadRequest(new { error = "..." })` |
| Not found | `NotFound()` |
| Duplicate / conflict | `Conflict(new { error = "..." })` |

### Validation
- No `[Required]`/data-annotation validation, no `ModelState` checks. Input
  validation is `if (string.IsNullOrWhiteSpace(...)) return BadRequest(...)`
  for trivial cases, otherwise the controller delegates to the service and
  catches `ArgumentException` / `InvalidOperationException`.

### Swagger
- Swagger is always on (no environment guard). XML doc comments are emitted
  and surfaced -- every public controller action MUST have an XML
  `<summary>` plus `<param>`/`<returns>` matching the surrounding style.

---

## 6. Data Access Rules

- ORM: **Entity Framework Core** (PostgreSQL/Npgsql provider). No Dapper, no raw SQL.
- `PortfolioDbContext` is the only `DbContext`. Never instantiate it
  directly outside the repository layer.
- Entity <-> table mapping lives in `Portfolio.Repositories/Mappings/*Map.cs`
  via `IEntityTypeConfiguration<T>`. New entities follow the same pattern
  and are registered in `OnModelCreating` with
  `modelBuilder.ApplyConfiguration(new XxxMap())`.
- Tracking conventions actually used in the code:
  - **Reads that just return data:** use `.AsNoTracking()`
    (see `SavedFeatureRepository.GetAllAsync`,
    `UserProfileRepository.GetClaimsAsync/GetClaimAsync`).
  - **Reads that will be mutated and saved:** use default tracking or
    `.AsTracking()` explicitly (see `AnonymousUserMiddleware`).
- `Include(...)` is used to eagerly load `Collection` and `UserNotes` on
  saved-feature queries. Keep that shape if you add new read paths.
- `SaveChangesAsync(cancellationToken)` is called at the end of any write
  method. Multi-step writes in `SavedFeatureService.CreateAsync` are wrapped
  in `BeginTransactionAsync / CommitAsync / RollbackAsync` to ensure atomicity.
  There is **no Unit-of-Work abstraction** beyond that explicit transaction.
- Schema changes go through `dotnet ef migrations add ...`; migrations live
  in `Portfolio.Repositories/Migrations`. `db.Database.Migrate()` runs at
  startup in `Program.cs`.

---

## 7. Security Practices

### Identity model -- **two parallel identities exist; do not confuse them**

1. **Anonymous user (GUID)** -- established by `AnonymousUserMiddleware`:
   - Sets a secure cookie `AnonUserId` (HttpOnly, Secure, SameSite=Lax,
     1-year expiry, `IsEssential=true`).
   - Stores the GUID in `HttpContext.Items["AnonUserId"]`.
   - Used by `SavedFeatureService`, `UserProfileService`, `ProfileController`,
     `SavedFeaturesController`.
   - Services read it via `IUserProfileService.GetCurrentUserId()`.
   - Controllers/services MUST NOT accept `UserId` from request payloads.

2. **Authenticated user (OAuth / cookie)** -- Google sign-in via
   `CookieAuthenticationDefaults` + `GoogleDefaults`:
   - Used by `CollectionService`, which reads `ClaimTypes.NameIdentifier`
     from `HttpContext.User`.
   - Endpoints requiring this identity use
     `[Authorize(Policy = "Authenticated")]`.

The three geocoding controllers (`BatchGeocodingController`,
`ReverseGeocodingController`, `AddressStandardizationController`) are
`[AllowAnonymous]` -- they do not touch either identity system.

`HomeFinderController` is also `[AllowAnonymous]`; it receives `userId` as a
query parameter for saved-search operations (not from the cookie identity system).
The Fiber controllers (`FiberOrdersController`, `FiberMaterialsController`,
`FiberShipmentsController`, `FiberClientsController`, `FiberDashboardController`)
all require `[Authorize(Policy = "Authenticated")]` and resolve the current user
via `IUserProfileService.GetCurrentUserId()`.

When adding a new feature, decide which identity it belongs to and follow
the matching service's pattern exactly. Mixing the two (e.g. using
`HttpContext.User` in an "anonymous" service) is an error.

### Authorization
- `Authenticated` policy = `RequireAuthenticatedUser()`. No other policies
  are defined -- do not invent new policy names without explicit instruction.
- Cookie auth: `SecurePolicy = Always`, `SameSite = None`. Login path
  `/Login`, logout `/Logout`.

### Input safety
- All EF queries use parameterized LINQ -- **no string-concatenated SQL**.
- ArcGIS request URLs encode `layerId` and `bbox` with `Uri.EscapeDataString()`
  before interpolation. Always apply the same encoding to any caller-supplied
  value used in outbound URLs.

### Other
- HTTPS redirection + HSTS enabled in non-development.
- Session enabled with HttpOnly, IsEssential cookie, 30-min idle timeout.
- Google client id/secret read from configuration; never hard-code secrets.

---

## 8. Portfolio.Common -- Model Location Rule

**`Portfolio.Common` is the only project allowed to contain model/data classes.**
This covers entity models, DTOs, enums, and ArcGIS HTTP wire-format types.
No model, DTO, or data class may be defined inside `Portfolio.Services`,
`Portfolio.Repositories`, or `Portfolio.Web`.

### Folder structure inside `Portfolio.Common`

```
Portfolio.Common/
  Models/         -- EF entity POCOs (SavedFeature, Collection, UserProfile, ...)
  DTOs/           -- Service/controller data transfer objects
  Enums/          -- Shared enumerations (e.g. ConfidenceTier)
  ArcGis/         -- ArcGIS HTTP response wire models
  Serialization/  -- Shared JsonSerializerOptions (PortfolioJsonOptions.cs)
```

### ArcGIS wire models (`Portfolio.Common/ArcGis/`)

Every class that maps to a raw ArcGIS JSON response lives here, decorated
with explicit `[JsonPropertyName("...")]` attributes to guard against
property-name mismatches during deserialization:

| File | Purpose |
|---|---|
| `ArcGisGeocodeResponse.cs` | Top-level `findAddressCandidates` response; `Candidates` collection |
| `ArcGisGeocodeCandidate.cs` | Single geocode candidate: `Address`, `Score`, nullable `Location` |
| `ArcGisLocation.cs` | Coordinate pair `X`/`Y` from a geocode candidate |
| `ArcGisReverseGeocodeResponse.cs` | Top-level `reverseGeocode` response; `Address` payload |
| `ArcGisReverseAddress.cs` | Reverse-geocode address fields: `LongLabel`, `Match_addr`, `AddNum`, `StAddr`, `City`, `Region`, `Postal`, `CountryCode`, `Addr_type` |

Rules for ArcGIS wire models:
- Every property MUST have `[JsonPropertyName("exactArcGisFieldName")]`.
- They are **read-only deserialization targets** -- no business logic, no
  methods, no validation attributes.
- Two services may share the same wire model only when both target the
  **identical ArcGIS endpoint** (e.g. `BatchGeocodingService` and
  `AddressStandardizationService` both consume `findAddressCandidates`).
- Private pipeline state (e.g. `CsvRow`, `GeocodeCacheEntry` in
  `BatchGeocodingService`) that never crosses a layer boundary may remain
  as private nested classes inside the service; it is not considered a
  "model class" for this rule.

### Enums (`Portfolio.Common/Enums/`)

| File | Values |
|---|---|
| `ConfidenceTier.cs` | `High`, `Medium`, `Low`, `Unresolved` |

---

## 9. Geocoding Features

### 9a. Batch Geocoding

**Controller:** `Portfolio.Web/Controllers/Api/BatchGeocodingController.cs`
- Route: `[Route("api/v{version:apiVersion}/geocoding/batch")]`, `[AllowAnonymous]`
- `POST /api/v1/geocoding/batch` -- accepts `IFormFile` (CSV), returns `202 Accepted`
  with `BatchJobAcceptedDto` (`JobId`, `StatusUrl`); enqueues the job via
  `IBatchGeocodingService.EnqueueAsync`.
- `GET  /api/v1/geocoding/batch/{jobId}/status` -- polls job progress; returns
  `BatchJob` (status, metrics, and results when completed); 404 when not found.
- `POST /api/batchgeocoding/sync` *(deprecated)* -- synchronous path kept for test
  compatibility; returns `List<BatchGeocodingResultDto>` directly; decorated
  `[Obsolete]`.
- Catches `ArgumentException` -> 400; `BrokenCircuitException` -> 503 with
  `ProblemDetails` and `retryAfterSeconds = 15`.

**Service interface:** `IBatchGeocodingService` in `Portfolio.Services/Interfaces/`
- `GeocodeAsync(IFormFile, CancellationToken)` -- synchronous geocoding path (used by legacy `/sync` endpoint).
- `EnqueueAsync(IFormFile, CancellationToken)` -- creates a `BatchJob`, stores it via
  `IBatchJobStore`, starts background processing, and immediately returns the `JobId`.

**Service:** `Portfolio.Services/Services/BatchGeocodingService.cs`
- Reads and validates the uploaded CSV (must have at least a header + 1 data row).
- **`IFormFile` stream is read to `byte[]` on the request thread** before any `Task.Run`
  closure, so the stream lifetime never crosses an async boundary.
- Uses `System.Threading.Channels.Channel<T>` as a producer/consumer pipeline
  for concurrent geocoding without creating unbounded threads.
- Concurrency limit and minimum match score are read from configuration:
  - `BatchGeocoding:MaxConcurrency` (default: 4)
  - `BatchGeocoding:MinMatchScore` (default: 80)
  - `BatchGeocoding:CacheTtlMinutes` (default: 60)
- Deduplicates repeated addresses using `IDistributedCache` (keyed on normalized address,
  serialized to JSON via `PortfolioJsonOptions.Default`).
- Calls ArcGIS `findAddressCandidates` via typed `HttpClient`.
- Updates the `BatchJob` record at each state transition (`Queued → Processing → TotalRows set → Completed/Failed`)
  via `IBatchJobStore.UpdateAsync` so Redis-backed replicas always read the latest snapshot.
- Protected by a **Polly resilience pipeline** around ArcGIS HTTP calls.

**Job store abstraction:** `Portfolio.Services/Abstractions/IBatchJobStore`
- `CreateAsync`, `GetAsync`, `UpdateAsync` -- create/read/update a `BatchJob`.
- **`InMemoryBatchJobStore`** (`Portfolio.Services/`) -- thread-safe `ConcurrentDictionary<string, BatchJob>`;
  registered as Singleton when `Redis:ConnectionString` is empty (local dev / Render).
- **`RedisBatchJobStore`** (`Portfolio.Services/`) -- serializes the full `BatchJob` to JSON
  on every write via `PortfolioJsonOptions.Default`; 24-hour TTL per job; registered as
  Singleton when `Redis:ConnectionString` is non-empty.

**Models / DTOs** (all in `Portfolio.Common/`):
- `BatchJob` (`Models/`) -- `JobId`, `Status` (`BatchJobStatus` enum), `SubmittedAt`,
  `CompletedAt`, `FileName`, `TotalRows`, `ProcessedRows`, `CacheHits`, `FailedRows`,
  `AverageScore`, `ThroughputPerSecond`, `Results`.
- `BatchJobAcceptedDto` (`DTOs/`) -- sealed record: `JobId`, `StatusUrl`.
- `BatchGeocodingResultDto` (`DTOs/`) -- `OriginalAddress`, `Matched`, `MatchedAddress`,
  `Score`, `Latitude`, `Longitude`.

**Configuration** (`appsettings.json`):
```json
"BatchGeocoding": {
  "MaxConcurrency": 4,
  "MinMatchScore": 80,
  "CacheTtlMinutes": 60
}
```

### 9b. Reverse Geocoding

**Controller:** `Portfolio.Web/Controllers/Api/ReverseGeocodingController.cs`
- Route: `[Route("api/reversegeocoding")]`, `[AllowAnonymous]`
- `GET /api/reversegeocoding?lat={lat}&lng={lng}`
- Returns `ReverseGeocodingResultDto`
- Catches `ArgumentException` -> 400, `KeyNotFoundException` -> 404

**Service interface:** `IReverseGeocodingService` in `Portfolio.Services/Interfaces/`
**Service:** `Portfolio.Services/Services/ReverseGeocodingService.cs`
- Validates lat (-90..90) and lng (-180..180); throws `ArgumentException` on bad input.
- Snaps lat/lng to a configurable grid before using as a cache key, so nearby
  coordinates share a cached result.
- Caches results in `IDistributedCache` with a sliding expiration; value is serialized
  to JSON using `PortfolioJsonOptions.Default`.
- Calls ArcGIS `reverseGeocode` via typed `HttpClient`.
- Throws `KeyNotFoundException` when ArcGIS returns no usable address.
- Maps to `ReverseGeocodingResultDto` via `private static MapToDto(...)`.

**DTOs** (all in `Portfolio.Common/DTOs/`):
- `ReverseGeocodingResultDto` -- `LongLabel`, `MatchAddress`, `HouseNumber`,
  `Street`, `City`, `Region`, `PostalCode`, `CountryCode`, `LocationType`

**Configuration** (`appsettings.json`):
```json
"ReverseGeocoding": {
  "GridResolutionDegrees": 0.001,
  "CacheSlidingExpirationMinutes": 30
}
```

### 9c. Address Standardization & Validation

**Controller:** `Portfolio.Web/Controllers/Api/AddressStandardizationController.cs`
- Route: `[Route("api/addressstandardization")]`, `[AllowAnonymous]`
- `POST /api/addressstandardization/parse` -- accepts `AddressParseRequestDto`,
  returns `AddressParsedDto`
- `POST /api/addressstandardization/validate` -- accepts `AddressParseRequestDto`,
  returns `AddressValidationResultDto`
- Catches `ArgumentException` -> 400

**Service interface:** `IAddressStandardizationService` in `Portfolio.Services/Interfaces/`
**Service:** `Portfolio.Services/Services/AddressStandardizationService.cs`
- `ParseAsync` -- normalizes whitespace/case, expands abbreviated street
  suffixes (e.g. "St" -> "Street"), extracts house number, street name,
  unit designator, city, state abbreviation (validated against a 50-state +
  territory set), and ZIP code via regex. Computes `ParseConfidence` (0-1).
- `ValidateAsync` -- calls `ParseAsync`, then geocodes the standardized address
  via ArcGIS `findAddressCandidates`. If the first candidate score is below 75,
  falls back to a City+State+ZIP query. Maps the score to a `ConfidenceTier`.
- Both methods throw `ArgumentException` when `RawAddress` is null/empty.
- `DetermineConfidenceTier`: score >= 90 -> `High`, >= 75 -> `Medium`,
  >= 50 -> `Low`, else `Unresolved`.

**DTOs** (all in `Portfolio.Common/DTOs/`):
- `AddressParseRequestDto` -- `RawAddress`
- `AddressParsedDto` -- `HouseNumber`, `StreetName`, `StreetSuffix`, `Unit`,
  `City`, `State`, `PostalCode`, `StandardizedAddress`, `ParseConfidence`
- `AddressValidationResultDto` -- `Parsed` (`AddressParsedDto`), `MatchedAddress`,
  `Score`, `ConfidenceTier` (`ConfidenceTier` enum)

### 9d. Fiber Order Management

**Controllers** (all in `Portfolio.Web/Controllers/Api/`, versioned `[ApiVersion("1.0")]`):

| Controller | Route | Auth |
|---|---|---|
| `FiberOrdersController` | `api/v1/fiberorders` | `[Authorize(Policy="Authenticated")]` |
| `FiberMaterialsController` | `api/v1/fibermaterials` | `[Authorize(Policy="Authenticated")]` |
| `FiberShipmentsController` | `api/v1/fibershipments` | `[Authorize(Policy="Authenticated")]` |
| `FiberClientsController` | `api/v1/fiberclients` | `[Authorize(Policy="Authenticated")]` |
| `FiberDashboardController` | `api/v1/fiberdashboard` | `[Authorize(Policy="Authenticated")]` |

Each controller exposes standard CRUD (`GET` all, `GET {id}`, `POST`, `PUT {id}`,
`DELETE {id}`) delegating entirely to the corresponding service.

**Services / interfaces:**
- `IFiberOrderService` / `FiberOrderService` -- CRUD for orders; sets `UserId` from
  `IUserProfileService.GetCurrentUserId()` on create; throws `InvalidOperationException`
  when user is unidentified.
- `IFiberMaterialService` / `FiberMaterialService` -- CRUD for materials; same
  user-identity enforcement.
- `IFiberShipmentService` / `FiberShipmentService` -- CRUD for shipments.
- `IFiberClientService` / `FiberClientService` -- CRUD for clients.
- `IFiberDashboardService` / `FiberDashboardService` -- aggregates across all four
  repositories to produce `FiberDashboardDto` containing:
  - `MtdRevenue` (current-month `UnitPrice × Quantity`)
  - `OpenOrders` (status not "Shipped" or "Delivered")
  - `ActiveShipments` (status "In Transit")
  - `LowStockAlerts` (`QtyOnHand <= ReorderPoint`)
  - `OrdersByStatus` (all 5 known statuses, counts default to 0)
  - `TopClients` (top 5 by revenue, current year)
  - `InventoryByCategory` (grouped material counts)

**DTOs** (`Portfolio.Common/DTOs/`):
- `FiberOrderDto`, `CreateFiberOrderDto`, `UpdateFiberOrderDto`
- `FiberMaterialDto`, `CreateFiberMaterialDto`, `UpdateFiberMaterialDto`
- `FiberShipmentDto`, `CreateFiberShipmentDto`, `UpdateFiberShipmentDto`
- `FiberClientDto`, `CreateFiberClientDto`, `UpdateFiberClientDto`
- `FiberDashboardDto` (with nested `OrdersByStatusDto`, `TopClientDto`, `InventoryByCategoryDto`)

**Models** (`Portfolio.Common/Models/`):
- `FiberOrder`, `FiberMaterial`, `FiberShipment`, `FiberClient`
- `FiberOrder` has a navigation property `ICollection<FiberShipment> Shipments`.

### 9e. Home Finder

**Controller:** `Portfolio.Web/Controllers/Api/HomeFinderController.cs`
- Route: `[Route("api/v{version:apiVersion}/homefinder")]`, `[AllowAnonymous]`
- `POST /api/v1/homefinder/score` -- accepts `HomeSearchPreferencesDto`, optional
  `top` query param (default 10); returns `List<ScoredPropertyDto>`.
- `GET  /api/v1/homefinder/properties/{id}` -- returns `ScoredPropertyDto` or 404.
- `POST /api/v1/homefinder/searches` -- saves a named search; accepts
  `CreateSavedSearchDto`; requires `userId` query param; returns `SavedSearchDto`.
- `GET  /api/v1/homefinder/searches` -- lists saved searches for a user; requires
  `userId` query param.
- `DELETE /api/v1/homefinder/searches/{id}` -- deletes a saved search; 204 on success.
- Catches `ArgumentException` -> 400, `KeyNotFoundException` -> 404.

**Services / interfaces:**
- `IHomeScoringService` / `HomeScoringService` -- scores all properties from
  `IPropertyRepository` against caller-supplied preferences; returns top-N ranked
  `ScoredPropertyDto` list; `GetPropertyByIdAsync` returns a single property or null.
- `ISavedSearchService` / `SavedSearchService` -- CRUD for named Home Finder
  searches; owner enforced via `userId` parameter (passed from controller).

**DTOs** (`Portfolio.Common/DTOs/`):
- `HomeSearchPreferencesDto` -- scoring preference sliders/filters
- `ScoredPropertyDto` -- property fields plus computed `Score`
- `SavedSearchDto` -- `Id`, `Name`, `Preferences`, `PropertyIds`, `CreatedAt`
- `CreateSavedSearchDto` -- `Name`, `Preferences`, `PropertyIds`

---

## 14. Deployment & Containerization

### Docker
- **`Dockerfile`** (repo root) — multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` build stage
  → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime stage. Sets `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT=Production`,
  `DOTNET_RUNNING_IN_CONTAINER=true`. Creates `/app/DataProtection-Keys` directory.
- **`docker/docker-compose.yml`** (base) — defines `portfolio` app service + `redis:7-alpine` sidecar with
  `redis-cli ping` healthcheck, named volume `redis-data`, and `portfolio-net` bridge network.
- **`docker/docker-compose.override.yml`** (local dev) — extends the base: sets
  `ASPNETCORE_ENVIRONMENT=Development`, passes `DATABASE_URL` from the host environment,
  sets `Redis__ConnectionString=redis:6379,abortConnect=false`, and mounts
  `./data/dataprotection:/app/DataProtection-Keys`.

Local dev command:
```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up
```

### Kubernetes (`k8s/`)

| File | Purpose |
|---|---|
| `namespace.yaml` | `portfolio` namespace |
| `configmap.yaml` | Non-secret env vars (double-underscore `__` convention for .NET config hierarchy) |
| `secret.yaml` | Stub secrets — **never commit real values**; use Sealed Secrets or External Secrets in production |
| `deployment.yaml` | 2-replica Deployment, resource limits/requests, liveness + readiness probes at `/health/live` and `/health/ready`, `emptyDir` volume for DataProtection-Keys (keys live in Redis) |
| `service.yaml` | ClusterIP, port 80 → targetPort 8080 |
| `hpa.yaml` | autoscaling/v2 HPA: min 2 / max 10 replicas, CPU 70% + memory 80% |
| `redis.yaml` | Single-replica Redis Deployment + ClusterIP Service (staging/demo; use managed Redis in production) |
| `k8s/README.md` | Full deploy order, connection string, health endpoint table, secrets guidance |

### Configuration / secrets conventions
- **Double-underscore (`__`)** is the .NET config hierarchy separator for environment variables.
  `Redis__ConnectionString` maps to `Redis:ConnectionString` in `IConfiguration`.
- **`.env.example`** (repo root) documents all environment variables. Copy to `.env` for local dev; never commit `.env`.
- `Redis:ConnectionString` left empty → in-memory fallback (no Redis required locally).
- `DATABASE_URL` accepts both Npgsql format (`Host=...`) and Postgres URI format
  (`postgres://user:pass@host/db`); `Program.cs` parses both.

### Data Protection key ring
- **Local dev / Docker (no Redis):** keys persisted to `/app/DataProtection-Keys` on the filesystem.
- **Production (Redis configured):** keys stored in Redis via `PersistKeysToStackExchangeRedis`
  under the `DataProtection-Keys` key. All pods share the same key ring, so encrypted cookies
  and anti-forgery tokens are valid across any replica.
- The `emptyDir` volume mount in `k8s/deployment.yaml` is intentional — it is not the source
  of truth when Redis is active.

---

## 10. Best Practices for AI Agents

### DO
- DO put new business logic in a service that lives in
  `Portfolio.Services/Services` and is fronted by an interface in
  `Portfolio.Services/Interfaces`.
- DO put new data access in a repository under
  `Portfolio.Repositories/Repositories` with an interface in
  `Portfolio.Repositories/Interfaces`.
- DO register every new service/repository as `Scoped` in
  `Program.cs` next to the existing registrations.
- DO accept and forward `CancellationToken cancellationToken = default`
  on every async public method.
- DO return DTOs from controllers and services; map via a
  `private static MapToDto(...)` method.
- DO derive the current user from `IUserProfileService.GetCurrentUserId()`
  (anonymous flows) or `HttpContext.User` claims (authenticated flows) --
  matching the surrounding feature.
- DO filter every per-user query by the user/owner id at the EF layer.
- DO use `DateTime.UtcNow` for any persisted timestamp.
- DO add `[ProducesResponseType]` for every status code an action can return.
- DO add an XML `<summary>` for every public controller action and DI-exposed
  service/repository member; Swagger consumes them.
- DO place ALL model/data classes (entities, DTOs, enums, ArcGIS wire types)
  exclusively in `Portfolio.Common`. No model may be defined in any other project.
- DO annotate every ArcGIS wire-model property with `[JsonPropertyName("...")]`
  using the exact field name returned by ArcGIS.

### DO NOT
- DO NOT inject `PortfolioDbContext`, `DbSet<>`, or any EF Core type into
  controllers or services, **except** when a service needs an explicit
  database transaction (e.g. `SavedFeatureService` injects `PortfolioDbContext`
  solely to call `BeginTransactionAsync`). Never inject it into controllers.
- DO NOT return EF entities (`SavedFeature`, `Collection`, `UserProfile`,
  ...) from controllers.
- DO NOT accept `UserId` / `OwnerId` from request bodies or query strings.
- DO NOT call `.Result`, `.Wait()`, use `async void`, or use
  `Task.Run` to wrap EF calls.
- DO NOT introduce AutoMapper, MediatR, FluentValidation, Dapper, or any
  other framework not already referenced.
- DO NOT add new authorization policies or authentication schemes without
  explicit instruction.
- DO NOT use `DateTime.Now`.
- DO NOT add validation attributes to DTOs; validate imperatively in the
  service.
- DO NOT log on every method entry -- log errors and significant state
  changes only (creation, deletion, conflict, unexpected exceptions).
- DO NOT edit `Portfolio.Repositories/SavedFeatureRepository.cs` (the
  duplicate at the project root).
- DO NOT define model or DTO classes outside `Portfolio.Common` -- not in
  services, not in controllers, not in repositories.

---

## 11. Testing & Validation Expectations

- Framework: **xUnit** + **Moq**. Service tests live in
  `Portfolio.Tests/Services/`; controller tests live in
  `Portfolio.Tests/Controllers/`.
- One test class per service or controller: `XxxServiceTests` / `XxxControllerTests`.
- Pattern actually used:
  - Mocks declared as `private readonly Mock<IXxx>` fields.
  - Constructor builds the SUT with the mocks.
  - `Arrange / Act / Assert` comments mark sections.
  - `[Fact]` for single cases, `[Theory] + [InlineData(...)]` for
    parameterised input validation.
  - Negative cases assert with `Assert.ThrowsAsync<TException>(...)` (services)
    or inspect the returned `IActionResult`/`ActionResult<T>` type (controllers).
- Authenticated-user identity is faked by setting up
  `_userProfileServiceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);`
- **No integration tests or DB tests exist.** Do not
  invent a fixture pattern; ask first if integration tests are needed.
- Any new service method added MUST have at least:
  - A happy-path `[Fact]`.
  - Negative cases matching the validation it performs (typically a
    `[Theory]` with `[InlineData]` for missing fields).
- Any new controller action added MUST have at least:
  - A happy-path `[Fact]`.
  - A not-found / null-input case.
  - Any authorization-dependent branch.

### Geocoding test patterns

Services that use `HttpClient` are tested with a custom `DelegatingHandler`
subclass (a fake HTTP handler) wired into `new HttpClient(fakeHandler)`.
Do not mock `HttpClient` directly.

Services that use `IDistributedCache` are tested with a real `MemoryDistributedCache`:
```csharp
var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
```
Do not use `IMemoryCache` / `MemoryCache` in new geocoding or job-store tests.

- `BatchGeocodingServiceTests` -- covers all-matched path, partial/unmatched,
  null/empty/header-only files, no-candidate response, and cancellation.
- `ReverseGeocodingServiceTests` -- covers happy path, cache-hit deduplication
  (request count assertion), invalid lat/lng, and no-result (`KeyNotFoundException`).
- `RedisBatchJobStoreTests` -- Create/Get round-trip, unknown key returns null,
  three-stage Update progression, all four `BatchJobStatus` enum values surviving
  JSON round-trip (confirms `JsonStringEnumConverter` is wired correctly).
- `AddressStandardizationServiceTests` -- covers parse happy path, partial input,
  empty input, validation confidence tiers, fallback to City+State+ZIP,
  unresolved result, and a `[Theory]` covering all supported suffix abbreviations.
- `AddressStandardizationControllerTests` -- covers `/parse` and `/validate`
  happy paths, empty-input 400, and service-exception branches.
- `ReverseGeocodingControllerTests` -- covers happy path, invalid lat/lng 400,
  and not-found 404.

### Fiber & Home Finder test patterns

- `FiberOrdersControllerTests`, `FiberMaterialsControllerTests`,
  `FiberShipmentsControllerTests` -- CRUD happy paths, not-found 404 on get/update/delete,
  user-not-identified 500.
- `FiberDashboardControllerTests` -- happy path and user-not-identified branches.
- `FiberOrderServiceTests` -- GetAll, GetById, CreateAsync (verifies `UserId` scoping),
  UpdateAsync (applies partial fields, `KeyNotFoundException` for missing order),
  DeleteAsync (true / false / user-not-identified).
- `FiberDashboardServiceTests` -- active shipments, open orders, low-stock alerts,
  MTD revenue (current-month only), all-statuses present with zero counts, per-status
  counts, top-5 client truncation.
- `HomeScoringServiceTests` -- top-N list, empty list, `GetPropertyByIdAsync` found / not-found.
- `HomeFinderControllerTests` -- score endpoint, property detail, saved-search CRUD.

### UserProfile test patterns

- `UserProfileControllerTests` -- `GetCurrentProfile`, `UpdateCurrentProfile`,
  `GetById`, `GetByGoogleId`, `GetClaims`, `SetClaim` (valid + `[Theory]` for blank
  type + null value + all 4 protected Google claim types), `RemoveClaim`, `Delete`
  (self, cross-user Forbid, not-found).
- `UserProfileServiceTests` -- claim CRUD, `GetProfileByIdAsync`, `GetProfileByGoogleIdAsync`,
  `GetCurrentProfileAsync`, `UpdateCurrentProfileAsync` (set/remove display name),
  `DeleteProfileAsync`, `IsGoogleLinkedAsync`, `CreateOrUpdateFromGoogleAsync`.

### API versioning attribute tests

- `ApiVersioningAttributeTests` -- reflection-based tests that assert every
  versioned controller carries `[ApiVersion("1.0")]` and the correct route prefix.

Total test count as of last verified run: **351 passing, 0 failing**.

---

## 12. Formatting & Style Rules

- **Indentation:** 4 spaces, no tabs.
- **Braces:** Allman style (opening brace on its own line) -- used uniformly.
- **`using` directives:** at top of file, outside the namespace.
- **Namespaces:** block-scoped (`namespace Foo { ... }`), not file-scoped.
  Match this style in new files.
- **One public type per file.**
- **No `#region` blocks** anywhere -- do not introduce them.
- **Comment style:**
  - Public controller actions and class summaries: XML doc comments
    (`/// <summary> ... </summary>`).
  - Repositories/services/middleware: short `//` line comments above
    methods describing intent and edge cases (see `UserProfileRepository`,
    `UserProfileService`, `AnonymousUserMiddleware`).
  - Section dividers in `Program.cs` use the
    `// --------------------------` banner style.
- **Nullable reference types:** enabled (`?` annotations are used
  throughout). Honour them.
- **C# 12 features in active use:** collection expressions (`[]`,
  `[.. ...]`), target-typed `new()`. All service, repository, and controller
  files use classic constructors. Do not introduce primary constructors in new files.

---

## 13. Inferred "Unwritten Rules"

These are **not documented anywhere** but are followed consistently and
must be preserved:

1. **Per-user filtering happens in the repository, not the service.**
   Every repository method that touches per-user data takes the
   user/owner id as a parameter and includes it in the EF predicate.
2. **Services never leak EF entities.** Every public service method
   returns a DTO (or `void` / `bool`).
3. **Error response shape is fixed:** `new { error = "<string>" }`.
   Use this shape for every BadRequest/Conflict body.
4. **Conflicts are signalled with `InvalidOperationException`** in
   services and translated to 409 in controllers.
5. **"Not found" is signalled by `null` / `false` from repositories**, then
   converted to either `NotFound()` or a service-level
   `KeyNotFoundException`. Repositories themselves never throw.
6. **Anonymous identity is read-only from `HttpContext.Items["AnonUserId"]`.**
   Never write to it outside `AnonymousUserMiddleware`. Never accept it
   from request input.
7. **Timestamps:** `DateSaved`/`CreatedDate`/`CreatedAt` are set on insert;
   `LastModified`/`LastActiveDate` are set on update or activity. Always
   `DateTime.UtcNow`.
8. **Default colour for a `Collection`** when the caller omits one is
   `"#6c757d"`. Preserve this when extending `CollectionService.CreateAsync`.
9. **Trim before persist:** string fields like `Collection.Name` and
   `Collection.Color` are `.Trim()`ed in the service before reaching the
   repository.
10. **Eager-load `Collection` and `UserNotes`** on every saved-feature read
    that returns to the caller (so the `MapToDto` can populate
    `CollectionName`).
11. **Middleware ordering in `Program.cs`:** `ApiExceptionMiddleware` runs
    first (after `UseSession`), then `AnonymousUserMiddleware`, then
    `UseAuthentication`/`UseAuthorization`. Preserve this ordering.
12. **Swagger XML comments are part of the public contract.** New public
    controller members without `<summary>` will silently degrade the
    Swagger UI; treat missing XML comments as a defect.
13. **ArcGIS wire models belong only in `Portfolio.Common/ArcGis/`** with
    explicit `[JsonPropertyName]` on every property. Private pipeline-only
    state (e.g. `CsvRow`, `GeocodeCacheEntry`) that never crosses a layer
    boundary may remain as private nested classes inside the owning service.
14. **Channel-based pipelines** (`System.Threading.Channels`) are the
    approved pattern for bounded concurrent workloads (see `BatchGeocodingService`).
    Do not use `Parallel.ForEachAsync` or `Task.WhenAll` for unbounded fan-out
    when a concurrency limit is required.
15. **`IDistributedCache` for geocoding caches** -- injected via constructor; keyed
    on normalized address string (batch geocoding) or snapped lat/lng string
    (reverse geocoding); serialized to/from JSON using `PortfolioJsonOptions.Default`.
    `IMemoryCache` is still registered but is no longer used by the geocoding services.
    Do not regress these services back to `IMemoryCache`.
16. **ArcGIS reverseGeocode returns the street line in `StAddr` consistently
    across all `Addr_type` values.** `Address` (capital A) is populated for
    some types but empty for others (e.g. PointAddress). `MapToDto` must
    prefer `StAddr` with `Address` as fallback: `StAddr ?? Address`.
