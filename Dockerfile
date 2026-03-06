# --------------------------
# Build stage
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Copy solution + central package props FIRST
COPY *.sln Directory.Packages.props ./

# 2. Copy every .csproj so restore can resolve the full dependency graph
COPY Portfolio.Common/Portfolio.Common.csproj           Portfolio.Common/
COPY Portfolio.Repositories/Portfolio.Repositories.csproj Portfolio.Repositories/
COPY Portfolio.Services/Portfolio.Services.csproj       Portfolio.Services/
COPY Portfolio.Web/Portfolio.Web.csproj                 Portfolio.Web/
COPY Portfolio.Tests/Portfolio.Tests.csproj             Portfolio.Tests/

# 3. Restore (this layer is cached until a .csproj or props file changes)
RUN dotnet restore

# 4. Copy everything else and publish
COPY . .
WORKDIR /src/Portfolio.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# --------------------------
# Runtime stage
# --------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create folder for DataProtection keys
RUN mkdir -p /app/DataProtection-Keys

# Copy published app from build stage
COPY --from=build /app/publish .

# Render.com uses PORT env var
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]