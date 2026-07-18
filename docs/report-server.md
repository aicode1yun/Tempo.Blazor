# Tempo Report Server

Tempo Report Server je samostatný, **on-premises** reportovací server postavený nad hotovým Tempo
rendering enginem. Cílem je otevřená, self-hostovaná alternativa k **Telerik Report Serveru**: katalog
reportů, verzování, render do PDF/XLSX/CSV/PNG, per-folder oprávnění, plánované doručování a strojové
API — bez závislosti na cloudové službě nebo Dockeru.

- **Engine je hotový** (fáze F0–F19 reporting enginu). Report Server přidává nad engine
  multi-tenant katalog, autentizaci/autorizaci, audit a scheduling.
- **Provoz cílí na Windows Server bez Dockeru** (Windows služba / IIS), viz [ADR-0001](adr/0001-databaze-mssql.md).
- **Autentizace přes standardní OIDC (Keycloak)**, autorizace ve vlastní DB, viz [ADR-0002](adr/0002-oidc-keycloak.md).

## Co Report Server umí

| Oblast | Popis |
| --- | --- |
| Katalog | Složky + reporty per tenant, revize, publish/rollback, přesuny, fulltext hledání. |
| Render | Synchronní (`POST /api/render`) i asynchronní joby (`POST /api/render/jobs`) do PDF / XLSX / CSV / PNG / viewer snapshot. |
| Datové zdroje | Pojmenované SQL a REST/JSON zdroje s náhledem schématu a test-connection. |
| Oprávnění | Per-folder ACL (Admin / Author / Viewer) keyované na OIDC `sub`; platí i pro strojové principály. |
| API klíče | Hashované klíče se scopes a expirací pro skripty a třetí strany (hlavička `X-Api-Key`). |
| Audit | Každé autorizované i odmítnuté volání se zapisuje do auditní stopy (filtrovatelné v UI). |
| Scheduling | Cron plány s doručením e-mailem / do úložiště / webhookem, retry a missed-run politikou. |

## Architektura

Report Server běží jako **dva samostatné procesy na různých originech** (decoupled model, ADR-0002):
prohlížeč se přihlásí přes Keycloak na Web hostu (confidential client, cookie session), Web vydává
svému uživateli krátkodobý access token přes same-origin `GET /auth/token` a datová volání jdou přímo
na API host bearer tokenem (WASM) nebo server→API (circuit). Skripty a třetí strany volají API přímo
hlavičkou `X-Api-Key`.

| Projekt | SDK | Role | Balí se? |
| --- | --- | --- | --- |
| `src/Tempo.Reporting.Abstractions` | class library | Kontrakty, JSON schéma definice reportu, DTO, validace. | **Ano** (NuGet) |
| `src/Tempo.Reporting.Engine` | class library | Processing, layout, PDF/PNG, CSV/XLSX export. | **Ano** (NuGet) |
| `src/Tempo.Blazor.Reporting` | Razor library | Blazor viewer / designer / explorer + remote/embedded zdroje. | **Ano** (NuGet) |
| `src/Tempo.ReportServer.Api` | `Microsoft.NET.Sdk.Web` | REST API host, EF Core (MSSQL), scheduling worker, auth. | Ne (spustitelný host) |
| `src/Tempo.ReportServer.Web` | `Microsoft.NET.Sdk.Web` | Blazor host (InteractiveAuto), OIDC cookie auth, token handout. | Ne (spustitelný host) |
| `src/Tempo.ReportServer.Web.Client` | Blazor WASM | Klientská část UI (stránky katalogu, designer, admin). | Ne (součást hostu) |

Perzistence (katalog, revize, API klíče, audit, schedules) je v MSSQL přes EF Core (Integrated
Security). Podrobný přehled autentizačního toku, tří auth schémat (uživatelský JWT / M2M
client-credentials / API klíč) a hardeningu je v [ADR-0002](adr/0002-oidc-keycloak.md). Volba MSSQL a
Integrated Security je v [ADR-0001](adr/0001-databaze-mssql.md).

## Report Server stránky a komponenty

Report Server UI jsou **aplikační stránky hostů** (`Tempo.ReportServer.Web` / `Web.Client`), ne
knihovní `Tm*` komponenty. Nemají proto vlastní JSON-dokumentační overlay (viz [poznámka níže](#json-dokumentace)).
Přehled routovaných stránek:

| Route | Stránka | Popis |
| --- | --- | --- |
| `/login` | `Login` | Zahájení OIDC login flow (přesměrování na Keycloak). |
| `/` | `ReportingDemo` | Vstupní přehled / dogfooding. |
| `/reports` a `/reports/{*Path}` | `ReportsPage` | Procházení katalogu (složky, reporty), vytvoření/nahrání reportu. |
| `/reporting` | `ReportViewerPage` | Vložený `TmReportViewer` nad remote zdrojem. |
| `/designer/{ReportId?}` | `ReportDesignerPage` | Lehký designer definice reportu. |
| `/admin/datasources` | `DataSourcesPage` | Správa pojmenovaných SQL/REST datových zdrojů. |
| `/admin/permissions` | `PermissionsPage` | Per-folder ACL (Admin/Author/Viewer). |
| `/admin/revisions` | `RevisionsPage` | Historie revizí, publish/rollback. |
| `/admin/schedules` | `SchedulesPage` | Cron plány a jejich stav/běhy. |
| `/admin/api-keys` | `ApiKeysPage` | Vydání/rotace/revokace API klíčů + audit trail. |

Sdílené komponenty:

- **`ReportServerShell`** — layout shell (navigace mezi sekcemi, `Title`, `ActiveSection`).
- **`RenderModeMarker`** — diagnostická značka aktuálního render módu (SSR prerender / Server circuit /
  WASM); slouží E2E testům InteractiveAuto k ověření, ve kterém kontextu komponenta běží.
- **`MainLayout`** — kořenový layout Web.Client.

## Quickstart (dev)

Předpoklady: .NET 10 SDK, běžící MSSQL (`localhost\SQLEXPRESS`, Integrated Security), Keycloak 26.x.

```bash
# 1) API host — MUSÍ běžet dřív než Web (Web na něj volá)
dotnet run --project src/Tempo.ReportServer.Api --urls http://localhost:5001

# 2) Web host (druhý terminál)
dotnet run --project src/Tempo.ReportServer.Web --urls http://localhost:5000
```

- API health/liveness: `GET http://localhost:5001/health` → `200 Healthy` (anonymní).
- API verze: `GET http://localhost:5001/version` (anonymní).
- Chráněné `/api/**` bez tokenu/klíče → `401`.

### Keycloak (OIDC)

Realm export je verzovaný v repu: [`deploy/keycloak/tempo-reports-realm.json`](../deploy/keycloak/tempo-reports-realm.json).
Postup importu (dev), test uživatelé a **placeholdery pro client secrety** jsou popsané v
[`deploy/keycloak/README.md`](../deploy/keycloak/README.md). Secrety se do gitu necommitují — dodávají se
při importu / přes user-secrets. Produkční hodnoty (`Authority`, `ClientSecret`, connection stringy) jsou
vždy per-deployment konfigurace, nikdy hardcoded v kódu ani v repu.

## Konfigurace a nasazení

Kompletní konfigurační klíče (auth, CORS, rendering limity, seed, scheduling, webhook SSRF ochrana,
scale-out) a návody na nasazení jako Windows služba nebo za IIS jsou v
[report-server-deployment.md](report-server-deployment.md).

## Packaging (NuGet)

Report Server hosty (`Api`, `Web`, `Web.Client`) jsou **spustitelné artefakty**, nebalí se jako NuGet
(`<IsPackable>false</IsPackable>`). Znovupoužitelné jsou **reporting knihovny**, které už mají v `.csproj`
plná packaging metadata (`PackageId`, `Version`, `Authors`, `Description`, `RepositoryUrl`, `PackageTags`,
`PackageLicenseExpression`, `PackageReadmeFile`):

| Balíček | Projekt |
| --- | --- |
| `Tempo.Reporting.Abstractions` | `src/Tempo.Reporting.Abstractions` |
| `Tempo.Reporting.Engine` | `src/Tempo.Reporting.Engine` |
| `Tempo.Blazor.Reporting` | `src/Tempo.Blazor.Reporting` |

Konzument, který embeduje viewer / volá Report Server API, referencuje tyto balíčky — viz sekci
*Embedding do vaší aplikace* v [README](../README.md#embedding-do-vaší-aplikace).

### CI krok `dotnet pack`

Balíčky se produkují standardním `dotnet pack` (jen packable projekty; hosty se přeskočí díky
`IsPackable=false`):

```bash
# Zabalí packable reporting projekty do ./artifacts (per-TFM build je řízen v .csproj)
dotnet pack src/Tempo.Reporting.Abstractions -c Release -o artifacts
dotnet pack src/Tempo.Reporting.Engine      -c Release -o artifacts
dotnet pack src/Tempo.Blazor.Reporting      -c Release -o artifacts
```

Publikace na GitHub Packages / nuget.org řeší existující workflow
(`.github/workflows/publish-nuget.yml`, `publish-nuget-org.yml`) a návod
[NUGET_GITHUB_PACKAGES.md](../NUGET_GITHUB_PACKAGES.md). **[TODO]** Reporting balíčky zatím do těchto
workflow nejsou zapojené — přidání `dotnet pack` + `dotnet nuget push` kroku pro Reporting.* a bump verzí
je mimo rozsah Fáze 8.

## JSON dokumentace

Repo má JSON-dokumentační overlay (`JsonDocumentation/`) pro **knihovní `Tm*` komponenty a balené DTO**
(např. `JsonDocumentation/Packages/Tempo.Reporting.Abstractions/…`, `Tempo.Blazor.Reporting/…`). Ten je
už pokrytý fázemi reporting knihoven. Report Server **stránky** (`ReportsPage`, `ApiKeysPage`,
`RenderModeMarker`, …) jsou aplikační stránky spustitelných hostů, **ne** knihovní komponenty ani balené
DTO, takže se JSON-dokumentačního systému netýkají a žádný guard test je nevyžaduje. Overlay se pro ně
proto vědomě **nezakládá**; jejich referenční přehled je výše v sekci
[Report Server stránky a komponenty](#report-server-stránky-a-komponenty).

## Související dokumenty

- [ADR-0001 — Databáze MSSQL](adr/0001-databaze-mssql.md)
- [ADR-0002 — OIDC / Keycloak](adr/0002-oidc-keycloak.md)
- [Nasazení a provoz](report-server-deployment.md)
- [End-to-end tutoriál](report-server-tutorial.md)
- [Keycloak realm import](../deploy/keycloak/README.md)
