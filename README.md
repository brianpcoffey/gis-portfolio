# Brian Patrick Coffey – Software Engineering Portfolio

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat&logo=csharp&logoColor=white)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?style=flat&logo=bootstrap&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon.tech-4169E1?style=flat&logo=postgresql&logoColor=white)
![ArcGIS](https://img.shields.io/badge/ArcGIS-JS_API-2C7AC3?style=flat&logo=esri&logoColor=white)
![Hosted on Render](https://img.shields.io/badge/Hosted_on-Render-46E3B7?style=flat&logo=render&logoColor=white)

## Overview

A modern software engineering portfolio showcasing projects in geographic information systems (GIS), geocoding pipelines, smart home data applications, and industrial operations dashboards. Built with an emphasis on clean architecture, secure authentication, responsive design, and cloud deployment.

The portfolio is built with **ASP.NET Core 10**, **Razor Pages**, and **Bootstrap 5**, featuring Google OAuth authentication, dark/light mode toggle, and layouts optimized for desktop and mobile. It is hosted on **Render**, with a **Neon.tech PostgreSQL** database powering project data and backend functionality.

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
- RESTful JSON API controllers under `api/` with Scalar/OpenAPI docs
- Structured logging with `ILogger<T>` across all services, repositories, and controllers
- `ApiExceptionMiddleware` for centralized error handling
- All async methods accept and forward `CancellationToken`
- `IDistributedCache` shared across geocoding services for deduplication and result caching (Redis in production, in-memory fallback locally)
- EF Core migrations run automatically at startup via `db.Database.Migrate()`

---

## Projects Highlighted

### 🗺️ US State Explorer
Explore, compare, and analyze U.S. states with an interactive ArcGIS-powered dashboard and ASP.NET Core backend. Users can save GIS features to their profile and organize them into named collections. Saved features and collections are persisted per-user in PostgreSQL.

**Stack:** ArcGIS JS API, ASP.NET Core, PostgreSQL

---

### 🏠 Redlands Smart Home Finder
GIS-powered tool to discover and rank the top 10 homes in Redlands, CA based on weighted preferences and a composite scoring algorithm. Users set preference weights and the backend scores and ranks properties accordingly.

**Stack:** ArcGIS JS API, ASP.NET Core, PostgreSQL

---

### 🗂️ Batch Geocoding
Upload a CSV of addresses and geocode them in bulk via the ArcGIS `findAddressCandidates` endpoint. Results are shown in an interactive DataTables grid with match rates, scores, coordinates, and CSV export support.

- Producer/consumer pipeline using `System.Threading.Channels` with configurable concurrency (`BatchGeocoding:MaxConcurrency`, default 4)
- `IDistributedCache` deduplication — repeated addresses in the same upload hit the cache instead of ArcGIS (Redis in production, in-process fallback locally)
- Configurable minimum match score (`BatchGeocoding:MinMatchScore`, default 80)
- Sample CSV files included under `wwwroot/samples/batch-geocoding/`

**Stack:** ArcGIS, C#, .NET, Channels

**API:** `POST /api/batchgeocoding` (accepts `multipart/form-data` CSV)

---

### 📍 Reverse Geocoding
Click anywhere on an interactive ArcGIS map to instantly resolve the address at that location. Supports manual coordinate entry and displays a history of recent lookups.

- Coordinate grid-snapping before cache lookup so nearby clicks share cached results (`ReverseGeocoding:GridResolutionDegrees`, default 0.001°)
- Sliding-expiration `IDistributedCache` (`ReverseGeocoding:CacheSlidingExpirationMinutes`, default 30 min)
- Validates lat (−90..90) and lng (−180..180) with descriptive error messages

**Stack:** ArcGIS, C#, .NET, Maps

**API:** `GET /api/reversegeocoding?lat={lat}&lng={lng}`

---

### 🪪 Address Standardization & Validation
Parse freeform address strings into structured components using NLP-style regex extraction, then validate and score them against ArcGIS geocoding. Returns a `ConfidenceTier` result (`High` / `Medium` / `Low` / `Unresolved`).

- **Parse** — normalizes whitespace/case, expands street suffix abbreviations (e.g. `St` → `Street`), extracts house number, street name, unit designator, city, state (50 states + territories), and ZIP; computes a `ParseConfidence` score (0–1)
- **Validate** — geocodes the standardized address via ArcGIS; falls back to City+State+ZIP query if the first candidate scores below 75; maps score to `ConfidenceTier` (≥90 High, ≥75 Medium, ≥50 Low, else Unresolved)

**Stack:** ArcGIS, Address Parsing, C#, .NET

**API:**
- `POST /api/addressstandardization/parse`
- `POST /api/addressstandardization/validate`

---

### 🏭 Plant Operations Dashboard
Industrial plant operations dashboard for a fiberglass ducting manufacturer. Provides live revenue analytics, GIS-based shipment routing with basemap switching, order management, inventory tracking with transaction audit logs, and interactive DataTables grids.

**Stack:** ASP.NET Core, DataTables, Esri GIS, PostgreSQL

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
| API Docs | Scalar / OpenAPI (always-on, XML doc comments) |
| Styling | Custom CSS with dark/light mode support |
| Hosting | Render (continuous deployment from GitHub) |
| Database Hosting | Neon.tech PostgreSQL |
| Containerization | Docker (multi-stage build), Docker Compose (local dev with Redis) |
| Orchestration | Kubernetes manifests (Deployment, Service, HPA, ConfigMap, Secret) |

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

6. Open `https://localhost:5001` in your browser. The API docs are available at `/scalar`.

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
