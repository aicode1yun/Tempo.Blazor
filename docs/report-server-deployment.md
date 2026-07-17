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

## Fáze 7 — Provozní hardening

### EF migrace pipeline + seed (idempotentní deploy)

Schéma se aplikuje **autorovanými EF Core migracemi**, ne `EnsureCreated`. Host to na SQL Serveru dělá
sám při startu (`EnsureTempoReportServerDatabaseAsync` → `Database.MigrateAsync`), takže „deploy = spustit
proces". Pro CI/CD, kde se schéma aplikuje odděleně od běhu aplikace (např. samostatný migrační krok před
rolloutem), použij jednu z variant:

```bash
# Varianta A — EF nástroj přímo proti DB (vyžaduje dotnet-ef na runneru)
dotnet ef database update \
  --project src/Tempo.ReportServer.Api \
  --context ReportServerDbContext \
  --connection "$ConnectionStrings__ReportServer"

# Varianta B — idempotentní SQL script (žádný .NET na DB runneru; přehráš přes sqlcmd)
# Script je verzovaný v repu a regeneruje se příkazem:
dotnet ef migrations script --idempotent \
  --project src/Tempo.ReportServer.Api --context ReportServerDbContext \
  -o src/Tempo.ReportServer.Api/Storage/Migrations/Sql/reportserver-migrations.idempotent.sql
sqlcmd -S "$SqlHost" -d TempoReportServer -i \
  src/Tempo.ReportServer.Api/Storage/Migrations/Sql/reportserver-migrations.idempotent.sql
```

Idempotentní script (`Storage/Migrations/Sql/reportserver-migrations.idempotent.sql`) obaluje každou
migraci `IF NOT EXISTS (… __EFMigrationsHistory …)`, takže se dá bezpečně přehrát vícekrát.

**Seed minimálních dat** je opt-in a idempotentní (`ReportServerSeeder`, spouští se po aplikaci schématu).
Zapíná se `Database:Seed:Enabled=true`; vytvoří kořenovou složku `/` pro `Database:Seed:TenantId` a
volitelně owner grant pro `Database:Seed:OwnerSubject`. Každý prvek se vloží jen když chybí, takže opakovaný
start nezduplikuje data.

| Klíč | Význam |
| --- | --- |
| `Database:Seed:Enabled` | `true` = spustit seed při startu. Default `false`. |
| `Database:Seed:TenantId` | Tenant, pro který se provisionuje baseline. |
| `Database:Seed:OwnerSubject` | OIDC `sub`, který dostane owner grant na kořenové složce (volitelné). |
| `Database:Seed:OwnerRole` | Role grantu (`Admin`/`Author`/`Viewer`), default `Admin`. |

### Render limity, fronty a telemetrie

Synchronní render (`POST /api/render`) prochází `ReportRenderExecutor`, který aplikuje: **bounded
concurrency** (sdílený `SemaphoreSlim`), **bounded frontu čekatelů**, **timeout** (linked
`CancellationToken`) a **limit velikosti výstupu**. Mapování na HTTP:

| Situace | HTTP |
| --- | --- |
| Překročena stránková kvóta | `413 Payload Too Large` |
| Výstup > `MaxOutputBytes` | `413 Payload Too Large` |
| Render přesáhl `Timeout` | `504 Gateway Timeout` |
| Fronta plná (přetížení) | `429 Too Many Requests` |

Konfigurace v sekci `Rendering`:

| Klíč | Default | Význam |
| --- | --- | --- |
| `Rendering:MaxConcurrentRenders` | `4` | Max souběžně běžících renderů (napříč tenanty). |
| `Rendering:MaxRenderQueueLength` | `50` | Max čekatelů na slot; při překročení `429`. |
| `Rendering:Timeout` | `00:00:30` | Timeout jednoho synchronního renderu. |
| `Rendering:MaxOutputBytes` | `52428800` | Max velikost payloadu (50 MB). |
| `Rendering:MaxSynchronousPages` | `20` | Stránková kvóta synchronního renderu. |

**Telemetrie:** metriky jsou publikované přes `System.Diagnostics.Metrics.Meter`
`Tempo.ReportServer.Rendering` (OpenTelemetry-kompatibilní, bez extra závislosti — napoj libovolný OTel
exporter nebo `dotnet-counters`): `reportserver.renders.total`, `reportserver.renders.failed`,
histogram `reportserver.render.duration` (ms) a observable gauge `reportserver.render.queue.depth`.
Render pipeline loguje strukturovaně přes `ILogger` scope (`TenantId`/`ReportId`/`Format`).

Zátěžový běh (in-process TestServer, `MaxConcurrentRenders=4`, tento stroj): 50 souběžných renderů
300řádkového reportu — 0 selhání, wall ~1,75 s, p50 ~904 ms, p95 ~1,7 s, bez OOM. Harness:
`RenderConcurrencyLoadHarness` (spustí se s `REPORTSERVER_LOADTEST=1`).

### Webhook delivery — SSRF ochrana

Webhook doručení scheduled reportů (`Scheduling:Webhook`) validuje cílovou URL před odesláním
(`ScheduledReportWebhookGuard`): povolená schémata (default jen `https`) a odmítnutí neveřejných cílů
(loopback, link-local včetně `169.254.169.254`, RFC 1918 `10/8`/`172.16/12`/`192.168/16`, IPv6
unique-local). DNS jména se resolvují a kontrolují všechny výsledné adresy. HttpClient má vynucený
`Timeout`.

| Klíč | Default | Význam |
| --- | --- | --- |
| `Scheduling:Webhook:AllowedSchemes` | `["https"]` | Povolená schémata cílové URL. |
| `Scheduling:Webhook:AllowPrivateNetworks` | `false` | `true` jen pro důvěryhodné on-prem cíle. |
| `Scheduling:Webhook:Timeout` | `00:00:30` | Timeout webhook HTTP requestu. |

### Scheduling scale-out

Řádek schedule nese `RowVersion` (optimistic-concurrency token). Když dva workeři zpracují stejný
schedule současně, `ApplyRunOutcomeAsync` prohrávajícího vyhodí `ReportScheduleConcurrencyException`;
processor pass přeskočí (log `Information`), takže se **nezduplikuje run historie ani neporuší stav**.
To ale **nebrání duplicitnímu doručení** (render+deliver proběhne u obou před zápisem). Pro produkci
platí předpoklad **single-instance scheduling workeru** (ostatní hosty spouštěj se `Scheduling:Enabled=false`);
plný multi-instance bez duplicit vyžaduje lease/claim na schedule (budoucí rozšíření).

### Token store scale-out (Web/BFF host)

Server-side token store BFF hostu je defaultně `IMemoryCache` (single-host). Pro více instancí za load
balancerem přepni `Authentication:Oidc:TokenStore=Distributed` — použije se `IDistributedCache`
(`DistributedCacheReportServerTokenStore`). Produkční host zaregistruje sdílenou cache, např.
`AddDistributedSqlServerCache(...)` (SQL backing) nebo Redis; když žádnou nezaregistruje, použije se
dev-only in-memory distributed cache jako fallback.

## Carry-forward (mimo Fázi 7)

- Reálný Keycloak OIDC flow — Fáze 4 (hotovo); JIT provisioning je fail-open (transientní výpadek DB
  neshodí autentizovaný request).
- Dockerfile — Fáze 9.
- Multi-instance scheduling bez duplicitního doručení (lease/claim) — viz výše.
