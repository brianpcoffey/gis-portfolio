# AI Agent README â€” Portfolio Solution

> Authoritative guide for AI coding agents working in this repo. Only patterns
> **observed in code** are documented. Anything else is flagged as
> *â€œNo consistent pattern foundâ€*.

---

## 1. ðŸ§­ Solution Overview

A .NET 8 Razor Pages web application that lets visitors explore ArcGIS map
features (the "State Explorer" project), save those features, and organize
them into named collections. It also exposes a small claims/profile API for
anonymous users.

**Projects (5):**

| Project | Role |
|---|---|
| `Portfolio.Web` | ASP.NET Core host: Razor Pages + API controllers + middleware + DI composition root |
| `Portfolio.Services` | Business logic and orchestration |
| `Portfolio.Repositories` | EF Core `DbContext`, repositories, fluent mappings, migrations |
| `Portfolio.Common` | POCO entity models and DTOs (no behavior) |
| `Portfolio.Tests` | xUnit + Moq unit tests for services |

**Architecture:** Classic 4-layer (Razor Pages / Controllers â†’ Services â†’
Repositories â†’ EF Core / SQL Server). DTOs cross every boundary leaving the
service layer.

**Key domains/features:**
- Anonymous user identity (cookie-based GUID, established in middleware)
- Saved Features (per-user GIS features pulled from ArcGIS)
- Collections (per-user grouping of saved features, requires authentication)
- User Profile + arbitrary key/value Claims (per anonymous user)
- ArcGIS proxy querying `sampleserver6.arcgisonline.com`
- Google OAuth login (Cookie + Google authentication schemes)

---

## 2. ðŸ—ï¸ Architecture & Layer Rules

### Flow

```
Razor Page / API Controller
        â”‚
        â–¼
   Service (interface)
        â”‚
        â–¼
 Repository (interface)
        â”‚
        â–¼
  PortfolioDbContext (EF Core)  /  HttpClient (ArcGIS)
```

### Controllers (`Portfolio.Web/Controllers/Api/*`)
- MUST inherit `ControllerBase` and be decorated with `[ApiController]`,
  `[Route("api/[controller]")]` (or an explicit lowercase route).
- MUST be decorated with `[Authorize(Policy = "Authenticated")]` **or**
  `[AllowAnonymous]` â€” never left undecorated.
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
- Resolve the current user identity themselves (see Â§7) â€” they MUST NOT
  accept user IDs as parameters from controllers.
- Map `Entity â†’ DTO` via a `private static MapToDto(...)` method.
- Throw typed exceptions to signal outcomes:
  - `ArgumentException` / `ArgumentNullException` â†’ 400
  - `InvalidOperationException` â†’ 409 (conflict)
  - `KeyNotFoundException` â†’ 404
  - `UnauthorizedAccessException` â†’ not currently caught by controllers
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

## 3. ðŸ§© Coding Conventions & Patterns

### Naming
- Interfaces: `I{Name}Service`, `I{Name}Repository`.
- Async methods always end in `Async`.
- DTO suffixes:
  - `{Entity}Dto` â€” read/return shape.
  - `{Entity}CreateDto` or `Create{Entity}Dto` â€” write shape (both forms exist;
    follow the form already used by the surrounding feature).
  - `{Entity}UpdateDto` â€” partial update shape.
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
- DTOs are plain property bags â€” no methods, no validation attributes
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

## 4. ðŸ”Œ Dependency Injection Rules

All DI is wired in `Portfolio.Web/Program.cs`. The pattern is:

```csharp
builder.Services.AddScoped<IXxxRepository, XxxRepository>();
builder.Services.AddScoped<IXxxService, XxxService>();
```

- **Lifetime: `Scoped` for every repository and service.** No `Singleton`,
  no `Transient` registrations exist for app code.
- `IHttpContextAccessor` registered via `AddHttpContextAccessor()`.
- `ArcGisService` is registered **twice** â€” as a scoped service and via
  `AddHttpClient<IArcGisService, ArcGisService>()`. Preserve both lines if
  you touch DI registration; the typed `HttpClient` is what the service
  actually consumes.
- `PortfolioDbContext` is registered via `AddDbContext` with SQL Server.
- New services/repositories MUST be registered in `Program.cs` immediately
  next to the existing block under `// Dependency Injection`.

---

## 5. ðŸŒ API Design Standards

### Routing
- Controllers: `[Route("api/[controller]")]`. One controller uses an explicit
  lowercase route (`[Route("api/features")]`); match the surrounding style
  when adding new endpoints.
- Route constraints used where the id is integer: `[HttpGet("{id:int}")]`.
- Mixed-shape ids are handled in the action body (see
  `SavedFeaturesController.Delete` which accepts both an int DB id and a
  string feature key).

### Action signatures
- Either `Task<IActionResult>` or `Task<ActionResult<T>>` â€” both are present.
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
  and surfaced â€” every public controller action MUST have an XML
  `<summary>` plus `<param>`/`<returns>` matching the surrounding style.

---

## 6. ðŸ—„ï¸ Data Access Rules

- ORM: **Entity Framework Core** (SQL Server provider). No Dapper, no raw
  SQL.
- `PortfolioDbContext` is the only `DbContext`. Never instantiate it
  directly outside the repository layer.
- Entity â†” table mapping lives in `Portfolio.Repositories/Mappings/*Map.cs`
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

## 7. ðŸ” Security Practices

### Identity model â€” **two parallel identities exist; do not confuse them**

1. **Anonymous user (GUID)** â€” established by `AnonymousUserMiddleware`:
   - Sets a secure cookie `AnonUserId` (HttpOnly, Secure, SameSite=Lax,
     1-year expiry, `IsEssential=true`).
   - Stores the GUID in `HttpContext.Items["AnonUserId"]`.
   - Used by `SavedFeatureService`, `UserProfileService`, `ProfileController`,
     `SavedFeaturesController`.
   - Services read it via `IUserProfileService.GetCurrentUserId()`.
   - Controllers/services MUST NOT accept `UserId` from request payloads.

2. **Authenticated user (OAuth / cookie)** â€” Google sign-in via
   `CookieAuthenticationDefaults` + `GoogleDefaults`:
   - Used by `CollectionService`, which reads `ClaimTypes.NameIdentifier`
     from `HttpContext.User`.
   - Endpoints requiring this identity use
     `[Authorize(Policy = "Authenticated")]`.

When adding a new feature, decide which identity it belongs to and follow
the matching serviceâ€™s pattern exactly. Mixing the two (e.g. using
`HttpContext.User` in an "anonymous" service) is an error.

### Authorization
- `Authenticated` policy = `RequireAuthenticatedUser()`. No other policies
  are defined â€” do not invent new policy names without explicit instruction.
- Cookie auth: `SecurePolicy = Always`, `SameSite = None`. Login path
  `/Login`, logout `/Logout`.

### Input safety
- All EF queries use parameterized LINQ â€” **no string-concatenated SQL**.
- ArcGIS request URLs encode `layerId` and `bbox` with `Uri.EscapeDataString()`
  before interpolation. Always apply the same encoding to any caller-supplied
  value used in outbound URLs.

### Other
- HTTPS redirection + HSTS enabled in non-development.
- Session enabled with HttpOnly, IsEssential cookie, 30-min idle timeout.
- Google client id/secret read from configuration; never hard-code secrets.

---

## 8. âœ… Best Practices for AI Agents

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
  (anonymous flows) or `HttpContext.User` claims (authenticated flows) â€”
  matching the surrounding feature.
- DO filter every per-user query by the user/owner id at the EF layer.
- DO use `DateTime.UtcNow` for any persisted timestamp.
- DO add `[ProducesResponseType]` for every status code an action can return.
- DO add an XML `<summary>` for every public controller action and DI-exposed
  service/repository member; Swagger consumes them.

### DO NOT
- DO NOT inject `PortfolioDbContext`, `DbSet<>`, or any EF Core type into
  controllers or services, **except** when a service needs an explicit
  database transaction (e.g. `SavedFeatureService` injects `PortfolioDbContext`
  solely to call `BeginTransactionAsync`). Never inject it into controllers.
- DO NOT return EF entities (`SavedFeature`, `Collection`, `UserProfile`,
  â€¦) from controllers.
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
- DO NOT log on every method entry â€” log errors and significant state
  changes only (creation, deletion, conflict, unexpected exceptions).
- DO NOT edit `Portfolio.Repositories/SavedFeatureRepository.cs` (the
  duplicate at the project root).

---

## 9. ðŸ§ª Testing & Validation Expectations

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

---

## 10. ðŸ“ Formatting & Style Rules

- **Indentation:** 4 spaces, no tabs.
- **Braces:** Allman style (opening brace on its own line) â€” used uniformly.
- **`using` directives:** at top of file, outside the namespace.
- **Namespaces:** block-scoped (`namespace Foo { ... }`), not file-scoped.
  Match this style in new files.
- **One public type per file.**
- **No `#region` blocks** anywhere â€” do not introduce them.
- **Comment style:**
  - Public controller actions and class summaries: XML doc comments
    (`/// <summary> â€¦ </summary>`).
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

## 11. ðŸ§  Inferred â€œUnwritten Rulesâ€

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
