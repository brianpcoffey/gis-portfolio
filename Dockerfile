# --------------------------
# Build stage
# --------------------------
# sdk:10.0 resolved 2026-10-12 (manifest list digest, from MCR Docker-Content-Digest)
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src

COPY Directory.Packages.props ./

# Only the projects Portfolio.Web actually publishes: Web -> Services -> Repositories
# -> Common. Restore is scoped to Portfolio.Web.csproj rather than the solution, so
# adding a project to Portfolio.sln can no longer break the image build — which is
# exactly what happened when Portfolio.Benchmarks was added without a matching COPY
# line here. Portfolio.Tests and Portfolio.Benchmarks are never published, so neither
# their sources nor their package graphs belong in this image.
COPY Portfolio.Common/Portfolio.Common.csproj Portfolio.Common/
COPY Portfolio.Repositories/Portfolio.Repositories.csproj Portfolio.Repositories/
COPY Portfolio.Services/Portfolio.Services.csproj Portfolio.Services/
COPY Portfolio.Web/Portfolio.Web.csproj Portfolio.Web/

RUN dotnet restore Portfolio.Web/Portfolio.Web.csproj

COPY . .
WORKDIR /src/Portfolio.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# --------------------------
# Runtime stage
# --------------------------
# aspnet:10.0 resolved 2026-10-12 (manifest list digest, from MCR Docker-Content-Digest)
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS runtime
WORKDIR /app

# Install Kerberos GSS library required by Npgsql's SSPI/Kerberos negotiation
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# Create DataProtection-Keys directory for fallback when Redis is not configured
# In production with Redis, keys are stored in Redis instead
RUN mkdir -p /app/DataProtection-Keys && chown $APP_UID /app/DataProtection-Keys

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=10000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Run as the image's built-in non-root user (UID 1654). Everything above that writes runs as root;
# the only runtime-writable path is /app/DataProtection-Keys, chowned above.
USER $APP_UID

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]
