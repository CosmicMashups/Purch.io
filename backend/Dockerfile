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

# Runs as the image's unprivileged user (APP_UID) — a bug in the API must not be a root shell in the container.
# Local mode writes uploads under wwwroot/uploads, so that one directory is made writable and is where the
# installer mounts its persistent volume (see docker-compose.local.yml, which also fixes a mounted volume's
# ownership so volumes created by earlier, root-running versions keep working).
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID:$APP_UID /app/wwwroot
USER $APP_UID

EXPOSE 8080

# Render injects its own $PORT and expects the container to listen on it;
# the Local installer's docker-compose doesn't set one, so this falls back
# to 8080 either way — one image, no per-deployment-mode build variant.
# `exec` so the API is PID 1's direct child process and receives SIGTERM on shutdown.
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet Purch.Api.dll"]
