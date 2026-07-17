# Tempo Report Server — nasazení a provoz (Fáze 1)

Tato kapitola popisuje, jak spustit **Tempo Report Server API host** (`src/Tempo.ReportServer.Api`)
jako samostatnou službu, jak ho provozovat vedle Blazor Webu a jak ho ověřit v CI. Docker je
záměrně odložen do Fáze 9 (viz ADR-0001, ADR-0002).

## Přehled hostů

| Host | Projekt | Role | Default dev URL |
| --- | --- | --- | --- |
| **API** | `src/Tempo.ReportServer.Api` | REST API (katalog, render, render jobs, data sources), auth, health | `http://localhost:5001` |
| **Web** | `src/Tempo.ReportServer.Web` | Blazor host (dogfooding UI a embedding demo) | `http://localhost:5000` |

API a Web běží jako **dva samostatné procesy na různých originech** (decoupled model dle
ADR-0002): prohlížeč / server volá API přímo bearer tokenem, bez BFF proxy. Přepnutí API z class
library na spustitelný `Microsoft.NET.Sdk.Web` host **nemění** Web — Web má vlastní `Program`
(top-level statements, globální namespace), API host je explicitní třída
`Tempo.ReportServer.Api.Host.Program`, takže ke kolizi entry-pointů nedochází.

## Endpointy

| Endpoint | Auth | Popis |
| --- | --- | --- |
| `GET /health` | **anonymní** | Liveness/readiness probe (health checks), vrací `200 Healthy`. |
| `GET /version` | **anonymní** | Informational + assembly verze běžícího hostu (`ReportServerVersionDto`). |
| `GET /openapi/v1.json` | anonymní | OpenAPI dokument (dev). |
| `/api/**` | **vyžaduje auth** | Katalog, revize, render, render jobs, data sources. |

`/api/**` je chráněné autorizační politikou `ReportServerApi`, která přijme **kterékoliv
podporované schéma**:

- **JWT Bearer** — `Authority`/`Audience` z konfigurace (Keycloak realm, ADR-0002). Reálný OIDC
  flow (login, token handout, refresh) přijde ve Fázi 4; teď je vpravena jen validační kostra.
- **Vlastní API klíč** — hlavička `X-Api-Key`, ověření proti `IReportApiKeyStore`.

Bez tokenu i bez klíče vrací chráněný endpoint `401`. Neexistující report vrací `404`.

## Konfigurace (`appsettings.json` + prostředí)

Konfigurace se čte standardně: `appsettings.json` → `appsettings.{Environment}.json` →
proměnné prostředí → argumenty příkazové řádky. Klíče:

| Klíč | Env proměnná | Význam |
| --- | --- | --- |
| `ConnectionStrings:ReportServer` | `ConnectionStrings__ReportServer` | Připojení k DB (dnes SQLite; MSSQL provider je carry-forward). |
| `Authentication:Jwt:Authority` | `Authentication__Jwt__Authority` | Keycloak realm issuer URL. Prázdné = bearer se neověřuje (jen dev bez tokenů). |
| `Authentication:Jwt:Audience` | `Authentication__Jwt__Audience` | Očekávaná audience tokenu (audience-restricted na Report Api). |
| `Authentication:Jwt:RequireHttpsMetadata` | `Authentication__Jwt__RequireHttpsMetadata` | `false` jen pro lokální dev. |
| `Cors:FrontendOrigin` | `Cors__FrontendOrigin` | Přesný FE origin. Bez `AllowCredentials`. Prázdné = CORS vypnuto. |

Nic z toho se nehardcoduje v kódu — vše je per-deployment konfigurace.

## Lokální / CI běh přes `dotnet run`

API MUSÍ běžet dřív než Web:

```bash
# terminál 1 — API
dotnet run --project src/Tempo.ReportServer.Api --urls http://localhost:5001
# terminál 2 — Web
dotnet run --project src/Tempo.ReportServer.Web --urls http://localhost:5000
```

Playwright `webServer` (readiness probe míří na anonymní `/health`):

```ts
webServer: [
  { command: 'dotnet run --project src/Tempo.ReportServer.Api --urls http://localhost:5001',
    url: 'http://localhost:5001/health', reuseExistingServer: !process.env.CI },
  { command: 'dotnet run --project src/Tempo.ReportServer.Web --urls http://localhost:5000',
    url: 'http://localhost:5000', reuseExistingServer: !process.env.CI },
],
```

### CI smoke test proti `/health`

```bash
dotnet run --project src/Tempo.ReportServer.Api --urls http://localhost:5001 &
API_PID=$!
until curl -sf http://localhost:5001/health >/dev/null; do sleep 1; done
curl -sf http://localhost:5001/version
test "$(curl -s -o /dev/null -w '%{http_code}' 'http://localhost:5001/api/folders?tenantId=t1')" = "401"
kill $API_PID
```

## Nasazení jako Windows služba (Kestrel self-host)

1. Publish: `dotnet publish src/Tempo.ReportServer.Api -c Release -o C:\Services\TempoReportServer`
2. Registrace služby:

```powershell
New-Service -Name "TempoReportServerApi" `
  -BinaryPathName "C:\Services\TempoReportServer\Tempo.ReportServer.Api.exe --urls http://localhost:5001" `
  -DisplayName "Tempo Report Server API" -StartupType Automatic
Start-Service TempoReportServerApi
```

Tajné hodnoty (connection string, Keycloak Authority) přes proměnné prostředí služby, ne v repu.

> Pozn.: Pro čistou SCM integraci lze později přidat `Microsoft.Extensions.Hosting.WindowsServices`
> + `builder.Host.UseWindowsService()`. Není to podmínka Fáze 1.

## Nasazení za IIS (ASP.NET Core Module)

1. Nainstaluj ASP.NET Core Hosting Bundle.
2. `dotnet publish -c Release` (výstup obsahuje `web.config` s `AspNetCoreModuleV2`).
3. IIS Site → publish složka; app pool „No Managed Code". IIS jako reverzní proxy před Kestrelem.

## Carry-forward (mimo Fázi 1)

- MSSQL provider (ADR-0001) — dnes SQLite; connection string je konfigurovatelný.
- Reálný Keycloak OIDC flow — Fáze 4.
- Perzistentní API key / audit store — dnes in-memory (Fáze 3).
- Dockerfile — Fáze 9.
