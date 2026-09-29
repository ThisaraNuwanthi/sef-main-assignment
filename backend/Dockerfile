# Multi-stage build: the big SDK image compiles, the small runtime image runs.
# Build from the backend/ folder:  docker build -t skca-enrol-api .

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, on its own layer, so package downloads are cached between builds.
COPY src/SkcaEnrol.Api/SkcaEnrol.Api.csproj src/SkcaEnrol.Api/
RUN dotnet restore src/SkcaEnrol.Api/SkcaEnrol.Api.csproj

COPY src/ src/
RUN dotnet publish src/SkcaEnrol.Api/SkcaEnrol.Api.csproj -c Release -o /app --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Photo uploads folder, writable by the non-root user below.
RUN mkdir -p /app/uploads && chown app /app/uploads

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Run as the image's built-in non-root "app" user.
USER app
ENTRYPOINT ["dotnet", "SkcaEnrol.Api.dll"]
