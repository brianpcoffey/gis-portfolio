# Use the official .NET 7 SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.sln ./
COPY Portfolio.Web/*.csproj ./Portfolio.Web/
RUN dotnet restore

# Copy the rest of the source code
COPY Portfolio.Web/. ./Portfolio.Web/
WORKDIR /app/Portfolio.Web

# Publish the app to the /out directory
RUN dotnet publish -c Release -o /out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production

# Bind to the port provided by Render.com
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Expose the port (Render sets $PORT, but EXPOSE is good practice)
EXPOSE 10000

# Start the app
ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]