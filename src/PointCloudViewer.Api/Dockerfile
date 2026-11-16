# syntax=docker/dockerfile:1
# Build context: repository root (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY Directory.Build.props .editorconfig ./
COPY src/PointCloudViewer.Core/PointCloudViewer.Core.csproj src/PointCloudViewer.Core/
COPY src/PointCloudViewer.Infrastructure/PointCloudViewer.Infrastructure.csproj src/PointCloudViewer.Infrastructure/
COPY src/PointCloudViewer.Api/PointCloudViewer.Api.csproj src/PointCloudViewer.Api/
RUN dotnet restore src/PointCloudViewer.Api/PointCloudViewer.Api.csproj

COPY src/ src/
RUN dotnet publish src/PointCloudViewer.Api/PointCloudViewer.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Local-disk dataset storage; mount a volume here to keep uploads across restarts.
RUN mkdir -p /app/data && chown "$APP_UID" /app/data
VOLUME /app/data

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__LocalDisk__RootPath=/app/data
EXPOSE 8080

# Non-root user provided by the .NET 8 base images.
USER $APP_UID
ENTRYPOINT ["dotnet", "PointCloudViewer.Api.dll"]
