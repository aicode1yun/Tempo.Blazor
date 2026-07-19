# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# Tempo Report Server — Web (BFF / InteractiveAuto) host container image.
#
# Publishing the Blazor Web App host (Tempo.ReportServer.Web) also publishes the
# referenced WASM client (Tempo.ReportServer.Web.Client) into wwwroot/_framework,
# so the browser (WebAssembly) leg is served from the same image.
#
# IMPORTANT — split config surface:
#   * The SERVER (host) leg reads IConfiguration from environment variables at
#     runtime (Api__BaseUrl, Authentication__Oidc__*, ...). Those are set in compose.
#   * The BROWSER (WASM) leg downloads a STATIC wwwroot/appsettings.json — container
#     env vars never reach it. Its browser-facing values (Api:BaseUrl, OIDC
#     Authority/ClientId) must therefore be baked at image build time via the ARGs
#     below. Defaults keep the image a self-contained demo (empty OIDC authority ->
#     demo session in the browser); compose overrides them to wire OIDC + the API.
#
# Build context MUST be the repository root (compose build.context: ../..).
# ---------------------------------------------------------------------------

# ------------------------------- build stage -------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

# Browser (WASM) config baked into the published static appsettings.json.
#   PUBLIC_API_BASEURL  -> the API base URL the browser calls (must be browser-reachable).
#   OIDC_AUTHORITY      -> Keycloak realm issuer the browser redirects to (empty = demo).
#   OIDC_CLIENT_ID      -> public client id gate value (also needed in the WASM leg).
ARG PUBLIC_API_BASEURL=http://localhost:5000
ARG OIDC_AUTHORITY=
ARG OIDC_CLIENT_ID=tempo-report-web

RUN printf '{\n  "Api": {\n    "BaseUrl": "%s"\n  },\n  "Authentication": {\n    "Oidc": {\n      "Authority": "%s",\n      "ClientId": "%s"\n    }\n  }\n}\n' \
        "$PUBLIC_API_BASEURL" "$OIDC_AUTHORITY" "$OIDC_CLIENT_ID" \
        > src/Tempo.ReportServer.Web.Client/wwwroot/appsettings.json

RUN dotnet publish src/Tempo.ReportServer.Web/Tempo.ReportServer.Web.csproj \
        -c Release \
        -o /app/publish \
        --nologo \
        -p:NuGetAudit=false \
        -p:TreatWarningsAsErrors=false \
        -p:UseAppHost=false

# ------------------------------ runtime stage ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# The Web host can render reports server-side (InteractiveServer leg via the
# reporting components), so it needs the same SkiaSharp native + font stack as the API.
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

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "Tempo.ReportServer.Web.dll"]
