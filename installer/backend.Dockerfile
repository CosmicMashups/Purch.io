# Multi-stage build for a Local/on-prem Purch.io backend container.
# Build context is the `backend/` directory (see docker-compose.local.yml).
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
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Purch.Api.dll"]
