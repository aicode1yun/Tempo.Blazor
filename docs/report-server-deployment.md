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
(`ScheduledReportWebhookGuard`): povolená schémata (default jen `https`) a odmítnutí neveřejných cílů —
loopback, link-local včetně `169.254.169.254`, RFC 1918 `10/8`/`172.16/12`/`192.168/16`, CGNAT
`100.64/10`, `0/8`, multicast `224/4`, reserved `240/4`, IPv6 unique-local/link-local/multicast a NAT64
`64:ff9b::/96` (aby vnořená IPv4 neprošla). DNS jména se resolvují a kontrolují **všechny** výsledné
adresy. HttpClient má vynucený `Timeout`.

**DNS-rebinding (TOCTOU) je uzavřen:** primární handler webhook klienta (`SocketsHttpHandler`
`ConnectCallback` → `ScheduledReportWebhookConnector`) resolvuje host **jednou**, zvaliduje všechny
adresy a socket **připne na již zvalidovanou adresu** — místo aby handler při connectu dělal nový
(rebindovatelný) DNS lookup. TLS/SNI/cert validace proti hostname zůstává. Útočník tak nemůže odpovědět
guardu veřejnou a connectu privátní adresou.

| Klíč | Default | Význam |
| --- | --- | --- |
| `Scheduling:Webhook:AllowedSchemes` | `["https"]` | Povolená schémata cílové URL. |
| `Scheduling:Webhook:AllowPrivateNetworks` | `false` | `true` jen pro důvěryhodné on-prem cíle. |
| `Scheduling:Webhook:Timeout` | `00:00:30` | Timeout webhook HTTP requestu. |

### Scheduling scale-out

Scheduling worker je bezpečné provozovat **na více instancích současně**. Před jakýmkoli
render+deliver si worker schedule **atomicky nárokuje (lease/claim)**:

- Schedule řádek nese aditivní sloupce `LeaseOwner` (identita worker procesu) a `LeasedUntil`
  (UTC expirace lease). Každý host má stabilní `LeaseOwner` (viz `ReportSchedulingInstanceIdentity`).
- `IReportScheduleStore.TryClaimScheduleAsync` provede **jeden podmíněný `UPDATE`**
  (`ExecuteUpdateAsync`), který uspěje jen když je řádek stále due (`NextRunUtc<=now` nebo
  `RetryAfterUtc<=now`) a **nenárokovaný nebo s expirovanou lease** (`LeasedUntil IS NULL OR
  LeasedUntil<=now`). SQL Server serializuje souběžné `UPDATE`y na zámku řádku, takže z N workerů
  claim vyhraje **právě jeden**; ostatní pass pro daný schedule přeskočí.
- Lease se uvolní (`LeaseOwner=NULL`, `LeasedUntil=NULL`) při zápisu výsledku běhu
  (`ApplyRunOutcomeAsync`), takže je schedule ihned znovu-nárokovatelný ve svém dalším due/retry čase.
- Spadlý worker: jeho lease vyprší po `Scheduling:LeaseDuration` (default 5 min) a schedule je opět
  nárokovatelný — žádné ruční odemykání.
- `RowVersion` (optimistic-concurrency token) je druhá pojistka: kdyby dva workeři přesto zapisovali
  výsledek stejného schedule, prohrávající dostane `ReportScheduleConcurrencyException` a pass
  přeskočí, takže se **nezduplikuje run historie ani neporuší stav**.

**Reálná záruka doručení je AT-LEAST-ONCE, ne exactly-once.** Zbytkový (úzký) případ duplicitního
doručení: když render+deliver **živého** workeru přesáhne `LeaseDuration`, jeho lease mezitím vyprší,
druhý worker schedule znovu nárokuje a report **doručí znovu** (např. odešle druhý e-mail). `RowVersion`
tomu **nezabrání** — doručení proběhne *před* zápisem výsledku u obou. Proto nastav
`Scheduling:LeaseDuration` **komfortně nad worst-case dobu render+deliver** daného reportu.

| Klíč | Default | Význam |
| --- | --- | --- |
| `Scheduling:LeaseDuration` | `00:05:00` | Doba držení lease na nárokovaném schedule. Nastav nad worst-case render+deliver. |

Konzumenti downstreamu, pro které je duplicita nepřijatelná (webhook / storage), by měli být
idempotentní vůči `X-Tempo-Schedule-Id` + occurrence.

### Token store scale-out (Web/BFF host)

Server-side token store BFF hostu je defaultně `IMemoryCache` (single-host). Pro více instancí za load
balancerem přepni `Authentication:Oidc:TokenStore=Distributed` — použije se `IDistributedCache`
(`DistributedCacheReportServerTokenStore`). Produkční host zaregistruje sdílenou cache, např.
`AddDistributedSqlServerCache(...)` (SQL backing) nebo Redis; když žádnou nezaregistruje, použije se
dev-only in-memory distributed cache jako fallback.

## Carry-forward (mimo Fázi 7)

- Reálný Keycloak OIDC flow — Fáze 4 (hotovo); JIT provisioning je fail-open (transientní výpadek DB
  neshodí autentizovaný request).

## Fáze 16 — Nasazení v kontejnerech (Docker Compose)

Kompletní kontejnerová topologie je v `deploy/docker/`: `Api.Dockerfile`, `Web.Dockerfile`,
`docker-compose.yml`, `keycloak-import.sh`, `.env.example`. Ověření běhu zajišťuje CI workflow
`.github/workflows/report-server-container-smoke.yml` (na tomto stroji Docker není).

> **Předpoklad:** Docker + Compose v2 (`docker compose`).

### Služby a porty

| Služba | Image | Host port | Role |
| --- | --- | --- | --- |
| `mssql` | `mcr.microsoft.com/mssql/server:2022-latest` | `1433` | Katalogová DB (SQL-auth login `sa`, ADR-0001). |
| `keycloak` | `quay.io/keycloak/keycloak:26.x` | `8080` | OIDC provider, importuje realm `tempo-reports`. |
| `api` | build `Api.Dockerfile` | `8081` | REST API, EF migrace při startu, PDF přes SkiaSharp. |
| `web` | build `Web.Dockerfile` | `5000` | Blazor Web App — BFF + InteractiveAuto portál. |

Issuer i URL sdílené prohlížečem a kontejnery jsou `http://keycloak:8080` a `http://api:8081`.
Web musí být v prohlížeči na `http://localhost:5000` (odpovídá `redirectUris` v realm exportu).

### Tajné hodnoty (`.env`)

Přes `deploy/docker/.env` (git-ignored, template `.env.example`) — **nic reálného se necommituje**.

```bash
cp deploy/docker/.env.example deploy/docker/.env   # vyplň hesla a client secrety
```

`MSSQL_SA_PASSWORD`, `KEYCLOAK_ADMIN(_PASSWORD)`, `TEMPO_REPORT_WEB_SECRET`, `TEMPO_REPORT_M2M_SECRET`.
`TEMPO_REPORT_WEB_SECRET` se musí shodovat mezi službou `keycloak` (import) a `web` (BFF).

- **SQL-auth (ADR-0001):** Linux neumí Windows integrated auth → API používá SQL login (injektováno
  přes `ConnectionStrings__ReportServer`, ne v image):
  `Server=mssql,1433;Database=TempoReportServer;User Id=sa;Password=***;TrustServerCertificate=true;Encrypt=true`.
- **Browser vs. server konfigurace Webu:** serverová noha čte proměnné prostředí za běhu; prohlížečová
  (WASM) noha čte **statický `wwwroot/appsettings.json`**, jehož hodnoty (`Api:BaseUrl`, OIDC
  `Authority`/`ClientId`) se **pečou do image** přes build-args `Web.Dockerfile`
  (`PUBLIC_API_BASEURL`, `OIDC_AUTHORITY`, `OIDC_CLIENT_ID`).

### Import Keycloak realmu

Realm export `deploy/keycloak/tempo-reports-realm.json` je připojen read-only. Entrypoint
`keycloak-import.sh` nahradí placeholdery `${TEMPO_REPORT_WEB_SECRET}` / `${TEMPO_REPORT_M2M_SECRET}`
hodnotami z prostředí, zapíše do `/opt/keycloak/data/import/` a spustí `kc.sh start-dev --import-realm`.
Test users (heslo `Pass123!`): `admin1`, `author1`, `viewer1`.

### Spuštění

```bash
cd deploy/docker
docker compose build api web
docker compose up -d
docker compose ps
```

API při startu aplikuje **EF Core migrace** (`Database.MigrateAsync`) — žádný samostatný migrační krok;
DB se vytvoří, pokud neexistuje. `restart: on-failure` jistí okno „port otevřen, DB ještě neodpovídá".

| Co | URL |
| --- | --- |
| Portál (Web) | `http://localhost:5000` (Login je anonymní, přihlášení přes Keycloak) |
| API health | `http://localhost:8081/health` (`Healthy` až po migraci) |
| Keycloak konzole | `http://localhost:8080` |
| OIDC discovery | `http://localhost:8080/realms/tempo-reports/.well-known/openid-configuration` |

**Hosts soubor pro interaktivní přihlášení:** prohlížeč používá stejné URL jako kontejnery, proto přidej
`127.0.0.1 keycloak api` do hosts souboru. **CI smoke to nepotřebuje** (testuje jen `localhost` porty).

### SkiaSharp / fonty (kritické pro PDF)

Renderer PDF používá SkiaSharp s `SkiaSharp.NativeAssets.Linux.NoDependencies` (dodá `libSkiaSharp.so`,
ale ne jeho systémové závislosti/fonty). Runtime image `api` i `web` proto přes `apt` instalují
`libfontconfig1` + `fontconfig` (nutné pro `libSkiaSharp.so`) a `fonts-dejavu-core` + `fonts-liberation`
(glyfy — fallback `SKTypeface.FromFamilyName` bez fontů vykreslí prázdno). Bez nich selže render textu za běhu.

### CI smoke (`report-server-container-smoke.yml`)

Trigger: `workflow_dispatch`, noční `schedule`, push/PR měnící kontejnerové artefakty / report-server hosty.
Kroky: `docker compose config` (validace) → `build api web` → `up -d` → čekání na **API `/health` = `Healthy`**,
**Web `/` = `200`**, **OIDC discovery = `200`**, **anonymní `/api/folders` = `401`**; při selhání vypíše logy,
vždy `docker compose down -v`. Smoke záměrně neprovádí plný browser OIDC login (pokrývá E2E z Fáze 10).

### Teardown

```bash
docker compose down       # zastaví/odstraní kontejnery
docker compose down -v    # + smaže volume mssql-data
```

Poznámka: tag Keycloaku je parametrizovaný (`KEYCLOAK_IMAGE`, default `26.3`) — zvol tag odpovídající
verzi realm exportu. Compose běží v HTTP (dev/demo); pro produkci předřaď TLS reverzní proxy a nastav
`RequireHttpsMetadata=true`.
