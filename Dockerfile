# --------------------------
# Build stage
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install Kerberos GSS library required by Npgsql's SSPI/Kerberos negotiation
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# Create DataProtection-Keys directory for fallback when Redis is not configured
# In production with Redis, keys are stored in Redis instead
RUN mkdir -p /app/DataProtection-Keys

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]
