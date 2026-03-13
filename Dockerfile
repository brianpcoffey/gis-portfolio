# --------------------------
# Build stage
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.sln Directory.Packages.props ./

COPY Portfolio.Common/Portfolio.Common.csproj Portfolio.Common/
COPY Portfolio.Repositories/Portfolio.Repositories.csproj Portfolio.Repositories/
COPY Portfolio.Services/Portfolio.Services.csproj Portfolio.Services/
COPY Portfolio.Web/Portfolio.Web.csproj Portfolio.Web/
COPY Portfolio.Tests/Portfolio.Tests.csproj Portfolio.Tests/

RUN dotnet restore

COPY . .
WORKDIR /src/Portfolio.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# --------------------------
# Runtime stage
# --------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/DataProtection-Keys

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]