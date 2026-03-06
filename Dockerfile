# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore only the web project
RUN dotnet restore Portfolio.Web/Portfolio.Web.csproj

# Publish the web project
RUN dotnet publish Portfolio.Web/Portfolio.Web.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

EXPOSE 10000

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]