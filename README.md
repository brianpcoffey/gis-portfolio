# Brian Patrick Coffey – Software Engineering Portfolio

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat&logo=csharp&logoColor=white)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?style=flat&logo=bootstrap&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon.tech-4169E1?style=flat&logo=postgresql&logoColor=white)
![ArcGIS](https://img.shields.io/badge/ArcGIS-JS_API-2C7AC3?style=flat&logo=esri&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Cache_%26_Job_State-DC382D?style=flat&logo=redis&logoColor=white)
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
- OpenAPI document generation via ASP.NET Core OpenAPI, with Scalar as the interactive API reference UI in development
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
Property scoring API that ranks Redlands homes using weighted preferences and stores named searches per user. The scoring service is isolated from controller and repository concerns so the ranking algorithm can evolve without changing the API surface.

- Preference-based scoring for property search and comparison workflows
- Saved-search CRUD with user-scoped persistence
- Repository abstraction that can evolve toward spatial indexes or search services

**Stack:** ArcGIS JS API, ASP.NET Core, PostgreSQL

**API:** `POST /api/v1/homefinder/score`, `GET /api/v1/homefinder/properties/{id}`, `/api/v1/homefinder/searches`

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
Authenticated operations dashboard for fiber orders, materials, shipments, clients, and KPI aggregation. Domain services enforce user identity, while repositories handle EF Core owner filtering and PostgreSQL persistence.

- CRUD workflows for orders, materials, shipments, and clients
- Dashboard aggregation for revenue, open orders, active shipments, and inventory alerts
- User-scoped service and repository boundaries for authenticated operations data

**Stack:** ASP.NET Core, DataTables, Esri GIS, PostgreSQL

**API:** `/api/v1/fiber/orders`, `/api/v1/fiber/materials`, `/api/v1/fiber/shipments`, `/api/v1/fiber/dashboard/stats`

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
| API Docs | Scalar / OpenAPI in development, XML doc comments |
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
   git clone https://github.com/brianpcoffey/portfolio.git
   cd portfolio
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

When running in the Development environment:

- Interactive API reference: `https://localhost:5001/scalar`
- Raw OpenAPI document: `https://localhost:5001/openapi/v1.json`

These endpoints are mapped only in development in `Program.cs` via `MapOpenApi()` and `MapScalarApiReference()`.

### Running Tests

```bash
dotnet test
```

351 tests — all passing (xUnit + Moq, no integration/DB tests).

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
- Continuous deployment from the [`main` branch on GitHub](https://github.com/brianpcoffey/portfolio).
- Database hosted on **Neon.tech** PostgreSQL.
- EF Core migrations run automatically at startup — no manual migration step required.
- HTTPS redirection and HSTS enabled in non-development environments.
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
