# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY Portfolio.Web/*.csproj ./Portfolio.Web/

# Restore dependencies
RUN dotnet restore

# Copy remaining source code
COPY Portfolio.Web/. ./Portfolio.Web/

WORKDIR /src/Portfolio.Web

# Publish app
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

COPY --from=build /app/publish .

# Production environment
ENV ASPNETCORE_ENVIRONMENT=Production

# Bind to Render's assigned port
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

EXPOSE 10000

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]