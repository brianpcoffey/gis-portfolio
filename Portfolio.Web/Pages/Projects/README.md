# Project Technical Walkthrough Documentation

This folder contains the portfolio project pages and their interview-oriented technical detail pages. Each `Details.cshtml` page should read like a concise system design walkthrough for a senior or lead engineer evaluating backend/API/cloud engineering depth.

## Audience

Write for a technical lead reviewing architecture, scalability, service boundaries, distributed-system choices, and operational maturity. Avoid marketing language and avoid implementation claims that are not present in the current codebase.

## Required Structure

Each project technical details page should use these sections in order:

1. `High-Level System Overview`
   - Purpose of the application
   - Business problem solved
   - Core architecture and service boundaries
   - Main technologies
   - Current deployment model

2. `Frontend Architecture`
   - Razor Pages structure
   - JavaScript module organization
   - State management approach
   - API communication through `api-config.js` and `api-fetch.js`
   - Authentication handling and UX/performance choices

3. `API Layer`
   - Controller name and versioned route
   - Authentication/authorization behavior
   - Request/response DTOs
   - Validation, error handling, and OpenAPI metadata
   - Security considerations

4. `Services and Business Logic`
   - Service responsibilities
   - Dependency injection and typed `HttpClient` usage
   - Background workflows where applicable
   - External integrations such as ArcGIS
   - Reusable abstractions and typed exception patterns

5. `Data Access Layer`
   - Repository and EF Core responsibilities
   - PostgreSQL/Npgsql usage
   - Owner filtering and transaction handling
   - Caching strategy
   - Query/scalability considerations

6. `Infrastructure and Deployment`
   - Current hosting environment: Render unless code/config changes
   - Docker and Docker Compose support
   - Kubernetes manifest support
   - Configuration and secrets conventions
   - Cloud portability, including Azure options only as future architecture unless implemented

7. `Engineering Decisions and Tradeoffs`
   - Why the current technology or pattern was chosen
   - Tradeoffs and known limitations
   - Performance and reliability considerations
   - Future scalability roadmap

8. `Interview Discussion Points`
   - Concise bullets that prompt system design discussion
   - Include API scalability, distributed caching, geocoding/search, cloud deployment, and operational concerns where relevant

## Current Terminology

Use consistent terminology across pages:

- `.NET 10`, `ASP.NET Core`, `Razor Pages`, and `versioned API controllers`
- `PostgreSQL via EF Core/Npgsql`, not SQL Server
- `IDistributedCache` with Redis when configured and in-memory fallback locally
- `typed HttpClient` plus Polly timeout/retry/circuit breaker policies for ArcGIS integrations
- `Render` as the current production host
- `Docker`, `Docker Compose`, and `Kubernetes manifests` as deployment assets
- `Azure`, `AKS`, `Azure Container Apps`, and `Azure Cache for Redis` only as portability or future-state discussion unless implemented

## Current API Route Reference

- State Explorer features: `GET /api/v1/features`
- State Explorer saved features: `/api/v1/features/saved`
- Collections: `/api/v1/collections`
- Batch geocoding: `POST /api/v1/geocoding/batch`, `GET /api/v1/geocoding/batch/{jobId}/status`
- Reverse geocoding: `GET /api/v1/geocoding/reverse`
- Address standardization: `POST /api/v1/addresses/parse`, `POST /api/v1/addresses/validate`
- Home Finder: `/api/v1/homefinder/search`, `/api/v1/homefinder/property/{id}`, `/api/v1/homefinder/searches`
- Fiber operations: `/api/v1/fiber/orders`, `/api/v1/fiber/materials`, `/api/v1/fiber/shipments`, `/api/v1/fiber/dashboard/stats`

## Accuracy Rules

- Do not describe health endpoints, managed cloud services, queues, search indexes, or observability stacks as implemented unless corresponding code/config exists.
- Keep future improvements clearly labeled as future improvements.
- Controller and route descriptions must match `Portfolio.Web/Controllers/Api` and `wwwroot/js/api-config.js`.
- Data access descriptions must match the repository/service layering documented in `.github/copilot-instructions.md`.
