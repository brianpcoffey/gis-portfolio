## Sample multi-stage Dockerfile for building and running the Portfolio.Web app
## Uses official .NET 10 SDK to build and the .NET 10 ASP.NET runtime to run.
## Adjust tags if you target a different OS variant (buster/slim/bullseye).

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY ./Portfolio.Web/Portfolio.Web.csproj ./Portfolio.Web/
COPY ./Portfolio.Services/Portfolio.Services.csproj ./Portfolio.Services/
COPY ./Portfolio.Repositories/Portfolio.Repositories.csproj ./Portfolio.Repositories/
COPY ./Portfolio.Common/Portfolio.Common.csproj ./Portfolio.Common/

RUN dotnet restore "Portfolio.Web/Portfolio.Web.csproj"

# copy everything else and publish
COPY . .
RUN dotnet publish "Portfolio.Web/Portfolio.Web.csproj" -c Release -o /app/publish --no-restore

# Runtime stage (Debian-based image)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install native dependency required by some Postgres builds (GSSAPI/Kerberos support)
# This adds libgssapi_krb5.so.2 which fixes "Cannot load library libgssapi_krb5.so.2" runtime errors.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]
