# --------------------------
# Build stage
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all csproj files and restore
COPY Portfolio.Common/Portfolio.Common.csproj Portfolio.Common/
COPY Portfolio.Repositories/Portfolio.Repositories.csproj Portfolio.Repositories/
COPY Portfolio.Services/Portfolio.Services.csproj Portfolio.Services/
COPY Portfolio.Web/Portfolio.Web.csproj Portfolio.Web/
RUN dotnet restore Portfolio.Web/Portfolio.Web.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish Portfolio.Web/Portfolio.Web.csproj -c Release -o /app/publish --no-restore

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