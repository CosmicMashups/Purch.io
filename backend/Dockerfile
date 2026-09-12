# Multi-stage build for the Purch.io backend — shared by both deployment
# modes: Local/on-prem (installer/docker-compose.local.yml) and Cloud
# (render.yaml, Render's own Docker runtime). Which mode an instance runs in
# is entirely env-var driven (see IDeploymentContext) — this image is
# identical either way. Build context is this `backend/` directory.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Purch.sln Directory.Build.props ./
COPY src/Purch.Domain/Purch.Domain.csproj src/Purch.Domain/
COPY src/Purch.Application/Purch.Application.csproj src/Purch.Application/
COPY src/Purch.Infrastructure/Purch.Infrastructure.csproj src/Purch.Infrastructure/
COPY src/Purch.Api/Purch.Api.csproj src/Purch.Api/
RUN dotnet restore src/Purch.Api/Purch.Api.csproj

COPY src/ src/
RUN dotnet publish src/Purch.Api/Purch.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Local-mode data (LOCAL_STORAGE_PATH) is expected to live on a mounted
# volume outside the container — see docker-compose.local.yml.
EXPOSE 8080

# Render injects its own $PORT and expects the container to listen on it;
# the Local installer's docker-compose doesn't set one, so this falls back
# to 8080 either way — one image, no per-deployment-mode build variant.
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Purch.Api.dll"]
