# Tempo Report Server — end-to-end tutoriál

Tento návod provede kompletním workflow **od nuly k naplánovanému e-mailu**: vytvoření reportu,
nahrání do katalogu přes Api, render do PDF, vydání API klíče, naplánování doručení e-mailem a
ověření doručené zprávy v smtp4dev. Každý krok používá reálné endpointy a stránky Report Serveru.

Že tento tok skutečně funguje proti živému stacku (render PDF 536 276 B, API klíč, e-mail v smtp4dev
s přílohou `application/pdf`), dokládá [report-server-e2e-results.md](report-server-e2e-results.md)
(scénáře S2, S3, S5).

> **Konvence.** `<...>` jsou placeholdery, které dosaď dle svého prostředí. **Žádné secrety
> (client secret, connection string, API klíč) nepatří do gitu** — drž je v user-secrets /
> proměnných prostředí. Ukázková URL: Api `https://localhost:7001`, Web `https://localhost:7150`,
> Keycloak `http://localhost:8080`, smtp4dev SMTP `:2525` / REST+UI `:5050`. Tenant v příkladech je
> `demo` — musí odpovídat `Database:Seed:TenantId` a tvým oprávněním.

## Přehled toku

```
report JSON  --->  POST /api/folders + /api/reports   --->  katalog (MSSQL)
                                                             |
                          bearer / X-Api-Key                v
             POST /api/render (Format=Pdf)  ----------->  PDF bytes
                                                             |
   /admin/api-keys  --->  POST /api/apikeys  --->  X-Api-Key +-->  render bez UI (skript / 3. strana)
                                                             |
   /admin/schedules --->  POST /api/schedules (Email)  ----->  worker render + SMTP  --->  smtp4dev
```

## Předpoklady

- .NET 10 SDK; běžící **MSSQL** (`localhost\SQLEXPRESS`, Integrated Security).
- **Keycloak 26.x** s importovaným realmem `tempo-reports` — postup, test uživatelé a
  secret-placeholdery viz [`deploy/keycloak/README.md`](../deploy/keycloak/README.md). Test uživatelé:
  `admin1` / `author1` / `viewer1`, heslo `Pass123!` (dev).
- **smtp4dev** pro dev e-mail (SMTP `:2525`, web/REST `:5050`).
- Přehled hostů, endpointů a všech konfiguračních klíčů:
  [report-server-deployment.md](report-server-deployment.md).

## Krok 0 — Spuštění stacku

API host musí běžet **dřív** než Web (Web na něj volá). Pro tento tutoriál zapneme seed baseline
složky a nasměrujeme scheduler SMTP na smtp4dev — vše přes prostředí, nic do gitu.

```powershell
# Terminál 1 — API host
$env:Database__Seed__Enabled       = "true"
$env:Database__Seed__TenantId      = "demo"
$env:Database__Seed__OwnerSubject  = "<OIDC-sub-uzivatele-admin1>"   # z Keycloak tokenu (claim sub)
$env:Database__Seed__OwnerRole     = "Admin"
$env:Scheduling__Enabled           = "true"
$env:Scheduling__Smtp__Host        = "localhost"
$env:Scheduling__Smtp__Port        = "2525"
$env:Scheduling__Smtp__FromAddress = "reports@tempo.local"
dotnet run --project src/Tempo.ReportServer.Api --urls https://localhost:7001

# Terminál 2 — Web portál (InteractiveAuto)
dotnet run --project src/Tempo.ReportServer.Web --urls https://localhost:7150
```

Ověření, že Api žije (anonymní endpointy):

```bash
curl -k https://localhost:7001/health     # -> 200 Healthy
curl -k https://localhost:7001/version    # -> { "version": ..., "assemblyVersion": ... }
```

Chráněné `/api/**` bez tokenu i bez klíče vrací `401`.

## Krok 1 — Získání access tokenu (bootstrap oprávnění)

Katalogové zápisy a administrace (API klíče, plány) vyžadují autentizaci. Nejdřív si jako admin
vytáhneme krátkodobý access token z Keycloacku. V devu má klient `tempo-report-web` povolený direct
grant; díky default scope `tempo-report-api-audience` token nese `aud=tempo-report-api`.

```powershell
$token = (Invoke-RestMethod -Method Post `
  -Uri "http://localhost:8080/realms/tempo-reports/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    grant_type    = "password"
    client_id     = "tempo-report-web"
    client_secret = "<TEMPO_REPORT_WEB_SECRET>"   # z prostředí / user-secrets, ne z gitu
    username      = "admin1"
    password      = "<heslo-admin1>"
  }).access_token
$auth = @{ Authorization = "Bearer $token" }
```

> V produkci se token nezískává direct grantem — uživatel projde interaktivním OIDC login flow na
> Web portálu (`/login` -> Keycloak) a Web volá Api server-side / vydává krátkodobý token přes
> `GET /auth/token`. Direct grant je jen dev zkratka pro skript.

## Krok 2 — Report od nuly (definice)

Report je JSON dle kanonického schématu (`schemaVersion: 1`,
[`report-definition.schema.json`](../src/Tempo.Reporting.Abstractions/docs/report-definition.schema.json)).
Minimální jednostránkový report s titulkem:

```json
{
  "schemaVersion": 1,
  "name": "Hello Report",
  "description": "Minimal single-page report",
  "pageSetup": {
    "pageSize": { "width": 595, "height": 842, "unit": "point" },
    "orientation": "portrait",
    "margins": { "left": 36, "top": 36, "right": 36, "bottom": 36 }
  },
  "parameters": [],
  "dataSets": [],
  "styles": [],
  "bands": {
    "reportHeader": {
      "kind": "reportHeader",
      "height": 80,
      "elements": [
        {
          "type": "textBox",
          "id": "title",
          "x": 0, "y": 0, "width": 523, "height": 40,
          "text": "Hello from Tempo Report Server",
          "horizontalAlignment": "center",
          "verticalAlignment": "middle"
        }
      ]
    }
  }
}
```

Datově vázaný report (parametry, `dataSets`, `detail` band s výrazy `=Fields.*`, tablix, stránkování)
staví na stejném schématu; kompletní příklady jsou v E2E testech
`tests/Tempo.Blazor.E2E/ReportingF6InvoicePaginationE2ETests.cs` a `ReportingF7TablixE2ETests.cs`.
Report lze také poskládat interaktivně na stránce **`/designer`** portálu (`ReportDesignerPage`).

## Krok 3 — Nahrání do katalogu přes Api

Katalog je složky + reporty per tenant. Nejdřív vytvoř (nebo použij seedovanou kořenovou) složku,
pak do ní nahraj report. `definitionJson` je JSON z kroku 2 jako **string**.

```powershell
# 3a) složka
$folder = Invoke-RestMethod -Method Post -Uri "https://localhost:7001/api/folders" `
  -Headers $auth -ContentType "application/json" -Body (@{
    tenantId = "demo"; parentId = $null; name = "Sales"
  } | ConvertTo-Json)

# 3b) report (definici načti ze souboru hello-report.json z kroku 2)
$definitionJson = [System.IO.File]::ReadAllText("hello-report.json")
$report = Invoke-RestMethod -Method Post -Uri "https://localhost:7001/api/reports" `
  -Headers $auth -ContentType "application/json" -Body (@{
    tenantId       = "demo"
    folderId       = $folder.folderId
    name           = "Hello Report"
    description    = "Tutorial report"
    definitionJson = $definitionJson
  } | ConvertTo-Json)

$report.reportId   # <- identifikátor pro render / plán
```

Katalog jde procházet i v UI na stránce **`/reports`** (`ReportsPage`): složky, reporty, vytvoření
složky, fulltext. Revize a publish/rollback jsou na **`/admin/revisions`** (`RevisionsPage`).

## Krok 4 — Render do PDF

Synchronní render je `POST /api/render`. Odpověď je `RenderReportResultDto` s poli `fileName`,
`contentType`, `pageCount` a `bytes` (payload; v JSON base64). Pro PDF nastav `format: "Pdf"`.

```powershell
$render = Invoke-RestMethod -Method Post -Uri "https://localhost:7001/api/render" `
  -Headers $auth -ContentType "application/json" -Body (@{
    tenantId    = "demo"
    reportId    = $report.reportId
    format      = "Pdf"
    cultureName = "en-US"
    parameters  = @()
  } | ConvertTo-Json)

[System.IO.File]::WriteAllBytes("hello-report.pdf", [System.Convert]::FromBase64String($render.bytes))
# hlavička souboru musí být "%PDF"; $render.pageCount >= 1
```

Podporované formáty: `Snapshot` (viewer JSON), `Pdf`, `Xlsx`, `Csv`, `Png`. Pro dlouhé reporty použij
asynchronní job: `POST /api/render/jobs` -> `202` + `jobId`, stav přes `GET /api/render/jobs/{jobId}`.
V UI report otevřeš v embedded vieweru na **`/reporting`** (`ReportViewerPage`, `TmReportViewer`),
kde je i print a export do PDF/CSV/XLSX.

> Reálný render tímto tokem vyprodukoval **PDF 536 276 B** začínající `%PDF` — scénář S2 v
> [report-server-e2e-results.md](report-server-e2e-results.md).

## Krok 5 — Vytvoření API klíče (`/admin/api-keys`)

Pro strojový přístup (skript, 3. strana) bez interaktivního loginu slouží **hashovaný API klíč** se
scopes a expirací. Vydává se na stránce **`/admin/api-keys`** (`ApiKeysPage`) nebo přes
`POST /api/apikeys` (vyžaduje admin scope `ManagePermissions`). Scopes jsou flag hodnoty
`View=1`, `Render=2`, `Export=4` -> `View|Render = 3`.

```powershell
$key = Invoke-RestMethod -Method Post -Uri "https://localhost:7001/api/apikeys" `
  -Headers $auth -ContentType "application/json" -Body (@{
    tenantId      = "demo"
    applicationId = "nightly-export"
    permissions   = 3            # View | Render
    expiresAt     = (Get-Date).AddMonths(3).ToString("o")
  } | ConvertTo-Json)

$plain = $key.plainTextKey   # <- vrací se JEN teď; server ukládá pouze hash
```

> `plainTextKey` se vrací **právě jednou** a už nikdy — ulož ho bezpečně (ne do gitu). Rotace je
> `POST /api/apikeys/{keyId}/rotate`, revokace `POST /api/apikeys/{keyId}/revoke`. Po revokaci
> vrací render přes klíč `401` (scénář S3 v e2e-results).

Render **bez uživatelského tokenu**, jen strojovým klíčem přes hlavičku `X-Api-Key`:

```powershell
$render2 = Invoke-RestMethod -Method Post -Uri "https://localhost:7001/api/render" `
  -Headers @{ "X-Api-Key" = $plain } -ContentType "application/json" -Body (@{
    tenantId = "demo"; reportId = $report.reportId; format = "Pdf"; cultureName = "en-US"; parameters = @()
  } | ConvertTo-Json)
```

## Krok 6 — Naplánování doručení e-mailem (`/admin/schedules`)

Plán renderuje report podle cron výrazu (5-pole, **UTC**) a doručuje výsledek. Vytvoř ho na stránce
**`/admin/schedules`** (`SchedulesPage`) nebo přes `POST /api/schedules`. Pro e-mail nastav
`deliveryKind: "Email"` a `deliveryTarget` na příjemce; scheduler odešle přes SMTP z kroku 0
(smtp4dev na `:2525`).

```powershell
$schedule = Invoke-RestMethod -Method Post -Uri "https://localhost:7001/api/schedules" `
  -Headers $auth -ContentType "application/json" -Body (@{
    tenantId        = "demo"
    ownerUserId     = "admin1"
    name            = "Hello nightly"
    reportId        = $report.reportId
    cronExpression  = "* * * * *"          # každou minutu (jen pro rychlé ověření; produkčně např. "0 8 * * 1")
    format          = "Pdf"
    cultureName     = "en-US"
    deliveryKind    = "Email"
    deliveryTarget  = "ops@example.com"
    missedRunPolicy = "Skip"
    maxAttempts     = 5
    isEnabled       = $true
  } | ConvertTo-Json)
```

Stav a historii běhů plánu vidíš na `SchedulesPage` nebo přes `GET /api/schedules/{scheduleId}/runs`
(pole `Status`, `ArtifactByteCount`, `ArtifactContentType`). Plán lze pozastavit
(`POST /api/schedules/{scheduleId}/enabled`). Kromě e-mailu podporuje delivery i `Storage` a
`Webhook` (webhook má SSRF ochranu, viz deployment doc `Scheduling:Webhook`).

## Krok 7 — Ověření doručení v smtp4dev

Po nejbližším cron tiknutí worker report vyrenderuje a odešle. Zprávu ověříš:

- **UI:** otevři `http://localhost:5050` — v inboxu je nová zpráva s přílohou `application/pdf`.
- **REST:**

```powershell
(Invoke-RestMethod -Uri "http://localhost:5050/api/Messages").results |
  Select-Object -First 1 id, from, to, subject
```

Doručení odpovídá běhu se `Status = Delivered` v `GET /api/schedules/{scheduleId}/runs` a MIME
příloze `application/pdf`. Přesně tento tok byl ověřen živě — reálný e-mail v smtp4dev s PDF přílohou
536 276 B, `ScheduleRuns.Status = Delivered` — scénář **S5** v
[report-server-e2e-results.md](report-server-e2e-results.md).

## Shrnutí a další kroky

| Krok | Endpoint / stránka | Výsledek |
| --- | --- | --- |
| Report | JSON schéma / `/designer` | validní definice `schemaVersion 1` |
| Katalog | `POST /api/folders`, `/api/reports`, `/reports` | report v katalogu (MSSQL) |
| Render | `POST /api/render` (`Pdf`) | PDF bytes, `%PDF` |
| API klíč | `POST /api/apikeys`, `/admin/api-keys` | `X-Api-Key` pro strojový render |
| Plán | `POST /api/schedules`, `/admin/schedules` | cron plán s e-mail delivery |
| Doručení | smtp4dev `:5050` | e-mail s PDF přílohou |

Související dokumentace:

- [Přehled Report Serveru](report-server.md) — architektura, stránky, packaging.
- [Nasazení a konfigurace](report-server-deployment.md) — všechny konfigurační klíče, Windows služba / IIS.
- [Živé E2E výsledky](report-server-e2e-results.md) — důkazy, že tento tok funguje.
- [Keycloak realm import](../deploy/keycloak/README.md) — realm, test uživatelé, secret placeholdery.
- [ADR-0001 (MSSQL)](adr/0001-databaze-mssql.md), [ADR-0002 (OIDC/Keycloak)](adr/0002-oidc-keycloak.md).
