# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# Tempo Report Server — API host (Tempo.ReportServer.Api) container image.
#
# Multi-stage: .NET 10 SDK builds/publishes ONLY the API project (and its
# ProjectReferences: Tempo.Reporting.Abstractions + Tempo.Reporting.Engine), so
# the huge Tempo.Blazor solution is never restored. The runtime layer is the
# ASP.NET 10 image plus the SkiaSharp Linux native prerequisites + fonts that
# the PDF renderer (Tempo.Reporting.Engine, SkiaSharp.NativeAssets.Linux.NoDependencies)
# needs — without libfontconfig1 + a real font package the SKTypeface.FromFamilyName
# fallback renders no glyphs on Linux.
#
# Build context MUST be the repository root (see deploy/docker/docker-compose.yml
# build.context: ../..), because the ProjectReferences live under ../src.
# ---------------------------------------------------------------------------

# ------------------------------- build stage -------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the whole repo (the .dockerignore trims bin/obj/.git). Publishing a single
# project restores only its dependency graph, so this stays cheap.
COPY . .

# NuGetAudit=false clears the transitive advisory (e.g. AngleSharp NU1902) that a
# warnings-as-errors CI profile would otherwise turn into a build break. The API
# project itself does not reference AngleSharp, but we keep the flag for parity
# with the Web image and future dependency drift.
RUN dotnet publish src/Tempo.ReportServer.Api/Tempo.ReportServer.Api.csproj \
        -c Release \
        -o /app/publish \
        --nologo \
        -p:NuGetAudit=false \
        -p:TreatWarningsAsErrors=false \
        -p:UseAppHost=false

# ------------------------------ runtime stage ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# SkiaSharp native prerequisites + fonts (root layer — apt needs root):
#   libfontconfig1 -> pulls libfreetype6; required by libSkiaSharp.so (NoDependencies
#                     variant ships the .so but not its system deps).
#   fontconfig      -> the fc-* tooling / font cache.
#   fonts-dejavu-core + fonts-liberation -> actual glyph outlines so text renders
#                     (SKTypeface.FromFamilyName fallback needs installed fonts).
#   curl            -> container healthcheck against /health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 \
        fontconfig \
        fonts-dejavu-core \
        fonts-liberation \
        curl \
    && rm -rf /var/lib/apt/lists/* \
    && fc-cache -f

WORKDIR /app
COPY --from=build --chown=app:app /app/publish ./

# Kestrel listens on all interfaces inside the container (compose maps the port).
# ASPNETCORE_URLS is overridable from compose; 8080 is the image default.
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Drop privileges — the aspnet image ships a non-root "app" user (UID 1654).
USER app

ENTRYPOINT ["dotnet", "Tempo.ReportServer.Api.dll"]
