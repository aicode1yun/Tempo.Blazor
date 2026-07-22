# Tempo Report Server — výsledky živého E2E (Fáze 5b)

Reálný end-to-end běh proti **živému stacku** (nic nefejknuto; doloženo screenshoty v
`docs/report-server-e2e/`, DB dotazy a smtp4dev REST). Spuštěno v rámci Fáze 5b.

## Prostředí

| Služba | Endpoint | Pozn. |
| --- | --- | --- |
| Keycloak | `http://localhost:8080` realm `tempo-reports` | users admin1/author1/viewer1 (report-admin/author/viewer) |
| Api | `https://localhost:7001` | Database:Provider=SqlServer, DB `TempoReportServerE2E`, JWT authority = Keycloak, audience `tempo-report-api` |
| Web | `https://localhost:7150` | InteractiveAuto, reálné OIDC (`/account/login` → Keycloak PAR), `Api:BaseUrl`=7001 |
| MSSQL | `localhost\SQLEXPRESS` (1433) | integrated security |
| smtp4dev | SMTP `:2525`, REST `:5050` | doručení scheduled reportů |

Web client secret byl vytažen z běžícího Keycloacku a nastaven přes prostředí (necommitnuto).
Playwright: Node 1.51 + chromium-1161 (reuse), artefakty na `Z:\e2e-artifacts`.

## Scénáře — výsledky

| # | Scénář | Výsledek | Důkaz |
| --- | --- | --- | --- |
| S1 | Render mody (Server / WASM / prerender) | **PASS** | `s1-server-mode.png` (data-mode=Server), `s1-wasm-mode.png` (WebAssembly), prerender SSR HTML obsahuje marker |
| S2 | Katalog → render → download | **PASS** | folder+report v DB (Folders/Reports=1/1), render **PDF 536 276 B** (`%PDF`); UI create-folder → DB assert |
| S3 | API klíč → render → revokace → 401 | **PASS** | ApiKeys řádek v DB, render přes `X-Api-Key` 200, revoke 204, poté **401** (RevokedAt v DB) |
| S4 | Keycloak login role-based (author1/viewer1) | **PASS (token)** | token `aud=tempo-report-api`, realm role report-author/viewer; **žádný `eyJ` ve web storage**; server-side autentizované volání Api. Screenshoty `s4-*` |
| S5 | Scheduling → e-mail v smtp4dev | **PASS** | reálný e-mail (smtp4dev REST rowCount≥1), MIME příloha `application/pdf`; DB ScheduleRuns Status=`Delivered`, 536 276 B |

## Nálezy (reálné mezery odhalené E2E — ne selhání testu)

1. **Audit se nepíše pro render / použití API klíče.** `AuditEvents` zůstává 0 při renderu i
   render-přes-klíč; audit se dnes emituje jen při změně ACL/permissions. Aby „kdo/kdy renderoval"
   bylo auditované, je třeba navěsit audit i na render/catalog endpointy (viz Fáze 3 manual-task).
2. **Upload reportu přes UI neexistuje** — katalogový zápis přes UI je create-folder; report +
   render se vytváří přes autentizované Api. Upload-report UI je follow-up.
3. **Role-based UI rozdíly nejsou napojené na reálný OIDC principal.** Web UI se stále gate-uje na
   in-memory demo session (demo tenanty northwind/contoso), ne na Keycloak roli/tenantu. Login a
   autorizace fungují na úrovni tokenu a řídí server-side Api autorizaci, ale UI se dle KC role
   zatím neliší; demo-session tenant ≠ Api data tenant (`default`), takže portál katalog po loginu
   ukazuje prázdno. Napojení UI na OIDC principal (tenant/role z claimů) je hlavní zbývající
   integrační krok „plného serveru".
4. **Bearer v prohlížeči** je v InteractiveServer legu strukturálně splněn (BFF volá Api server-side,
   chráněný Api vrátil data ⇒ platný bearer; žádný token ve storage); WASM-leg browser-bearer
   interception se v okně nezachytil (nedošlo k re-fetchi při handoffu).
5. **Handoff Server→WASM** byl prokázán na úrovni render-módů, ne samostatně jako autentizovaný
   handoff bez re-loginu.

## Reprodukce

E2E proběhlo přes lehké Node Playwright skripty (mimo repo, `C:\tmp\rs-e2e`) proti ručně
spuštěnému stacku — verifikace, ne commitnutý test suite. **Follow-up:** převést scénáře do
commitnutého E2E projektu (`tests/Tempo.Blazor.E2E` nebo nový ReportServer E2E) s `webServer`
konfigurací (Api+Web+smtp4dev), aby běžely v CI.

---

# Fáze 10 (A1) — portál UI napojeno na OIDC principal (řeší nález #3)

Nález #3 z Fáze 5b (UI se gate-uje na demo session, ne na Keycloak roli/tenant) je **vyřešen**.
Portál je nyní dual-mode: bez konfigurace `Authentication:Oidc` běží beze změny jako demo
(in-memory session, tenant switcher, plný nav); s nakonfigurovaným OIDC se `IPortalIdentity`
resolvuje na `OidcPortalIdentity`, který čte roli/tenant z principalu. Gating je pouze UX
pohodlí — autorizační autoritou zůstává chráněné Api.

## Kritický nález odhalený živým E2E (a opravený)

Cookie principal se staví z **id tokenu + userinfo**, ale Keycloak vkládá `realm_access` /
`resource_access` / `tenant_id` jen do **access tokenu**. Bez projekce tedy portál skryl
role-bearing uživateli **každou** nav položku (všechny false, tenant „default"). Opraveno v
`OnTokenValidated` (`ReportServerAccessTokenClaims.Project`) — base64url dekóduje payload access
tokenu a projektuje claimy do cookie identity, aniž by přepisoval existující. bUnit tuto mezeru
zachytit nemohl (injektuje claimy přímo) → regresní guard `AccessTokenClaimsProjectionTests`.

## E2E scénáře — výsledky (živý stack: Api :7001 SqlServer + Web :7150 reálné OIDC/Keycloak)

| # | Scénář | Výsledek | Důkaz |
| --- | --- | --- | --- |
| F10-demo | OIDC off → portál běží bez loginu (demo) | **PASS** | plný nav + tenant switcher „Northwind Finance", „Pavel Author" (`f10-demo-2-reports.png`) |
| F10-author | OIDC on → `author1` (report-author) | **PASS** | nav = reports/designer/schedules/revisions **true**, datasources/permissions/apikeys **false**; tenant-switcher **absent**, tenant-display „default"; user „author1" (`f10-author-2-reports.png`, `f10-author-result.json`) |
| F10-viewer | OIDC on → `viewer1` (viewer) | **PASS** | nav = **jen** reports true, vše ostatní false; tenant-display „default"; user „viewer1" (`f10-viewer-2-reports.png`, `f10-viewer-result.json`) |

**Role-based UI proof:** author ≠ viewer — author vidí designer/schedules/revisions navíc k reports,
viewer vidí jen reports. Rozdíl vzniká z projektované Keycloak role (dříve oba all-false).

## Ověřeno unit/bUnit testy (Web.Tests, net10.0)

- `PortalIdentityTests` (8) — čtení role/tenant/display z Keycloak-shaped principalu, hierarchie rolí, `CanAccess`.
- `ReportServerShellAuthTests` (4) — viditelnost nav testid dle role (custom `StubAuthenticationStateProvider`, ne bUnit `AddTestAuthorization`, aby prošly custom claimy).
- `CommonServicesGateTests` (3) — DI gate: OIDC konfig → `OidcPortalIdentity`, jinak `ReportServerSessionState` (chrání WASM leg před tichým pádem do demo).
- `AccessTokenClaimsProjectionTests` (5) — projekce `realm_access`/`tenant_id`, nepřepsání existujícího, ignorace malformed tokenu.

Skripty: `scratchpad/f10-e2e.mjs` (MODE=demo|author|viewer). Poznámka: driver záměrně blokuje
`**/*.wasm` (procvičí Server render leg), proto jsou v `errors[]` očekávané `Failed to fetch` na
`_framework/*.wasm` — nejsou to funkční chyby.

---

# Fáze 12 (A4) — portál: upload/create reportu, favorites, historie běhů, parametrický render

Full-server varianta (rozhodnutí uživatele): server-side favorites + persistované ad-hoc render
runs. Implementováno ve 3 passech (backend `1c8d6cf6`, UI `d43bc283`, fixy+E2E tento commit).

## E2E scénáře — výsledky (živý stack: Api :7001 SqlServer DB `TempoReportServer` + Web :7150 OIDC/Keycloak, login author1/viewer1)

Všech 6 scénářů **PASS s přímým DB důkazem** (author1 sub `a5534aed-…`):

| # | Scénář | Výsledek | DB důkaz / screenshot |
| --- | --- | --- | --- |
| 1 | Create blank report přes New Report form | **PASS** | `Reports` řádek (default/finance/„E2E Ledger"); redirect na `/designer/{id}` — `f12-s1-*.png` |
| 2 | Upload edge case | **PASS** | broken `{"broken":` → inline chyba, submit blokován; validní ReportDefinition → vytvořeno — `f12-s2-*.png` |
| 3 | Favorites (server-side per-user) | **PASS** | `Favorites` řádek (default/author1 sub/reportId); `/favorites` → klik → **resolve round-trip**; un-favorite → COUNT 0 — `f12-s3*.png` |
| 4 | Parametrický render → historie běhů | **PASS** | `RenderRuns` řádek (author1/Pdf/Succeeded/1 str/883 B/419 ms/`ParametersJson=[{AsOfDate:2026-07-19}]`); `/history` sedí — `f12-s4-*.png` |
| 5 | viewer1 role gating | **PASS** | `new-report-open` ABSENT; nav-favorites/history/reports přítomné — `f12-s5-viewer1-shell.png` |
| 6 | Prázdné stavy | **PASS** | favorites/history empty — `f12-s6-*.png` |

## Vady odhalené živým E2E (unit testy je nechytily — fakes) — všechny opraveny

1. **Designer/viewer ukazovaly DEMO „Sales Register" content** místo reálné vytvořené definice
   (`DemoReportSourceFactory` fallback). Opraveno: designer i viewer parsují reálný
   `_resolved.DefinitionJson` přes kanonický `ReportDefinitionJsonSerializer`; non-demo report bez
   dat → vlastní prázdná struktura nebo `viewer-preview-unavailable` (nikdy cizí obsah).
   **Live re-verify PASS**: designer ukázal „Fix Verify Ledger 42", viewer „Fix Verify Viewer 77"
   (`f12-fix-designer.png`, `f12-fix-viewer.png`).
2. **`/resolve` 404 → `HttpRequestException`** místo `KeyNotFoundException` → dev exception page.
   Opraveno: klient překládá 404 → `KeyNotFoundException` → graceful `report-not-found`.
3. **Root-folder reporty nedosažitelné deep-linkem** (`BuildDeepLink` bez folder segmentu).
   Opraveno: `ResolveByPathAsync` řeší single-segment path tenant-wide id-or-name fallbackem.
4. **Blank report ve vieweru 500** — `GET /reports/{id}/parameters` neuměl deserializovat blank
   definici (plain `JsonSerializer` vs kanonický reader, `ReportPageSize.unit` enum). Opraveno:
   `NewReportForm` serializuje blank definici kanonickým `ReportDefinitionJsonSerializer`.
5. Deep-link↔resolve round-trip byl rozbitý app-wide (`BuildDeepLink` dává ReportId, `/resolve`
   matchoval jen Name) → reporty přes `POST /reports` (generované id ≠ name) 404. Opraveno aditivně:
   `ResolveByPathAsync` matchuje poslední segment na **ReportId NEBO Name**.

UX nits opraveny: run-history header grid zarovnání, singulár/plurál („1 run"), Create tlačítko
disabled při file-error.

## Ověřeno testy

Web.Tests **84/0**, Api.Tests **136/0/1skip** (12 MSSQL testů reálně proti živému SQL Serveru).
Klíčové regresní guardy: `DesignerPage_ShowsTheRealCreatedReport_NotADifferentDemoReport`,
`ViewerPage_ShowsPreviewUnavailable_WhenNoUsableDefinitionForNonDemoReport`,
`Resolve_ByFolderQualifiedIdPath_ResolvesReportCreatedViaApi`,
`Resolve_BySingleSegmentDeepLink_ResolvesRootStyleReport`,
`GetParameters_ForBlankReportCreatedViaCanonicalDefinition_Returns200`,
`Submit_Blank_ProducesDefinitionJson_ThatRoundTripsThroughCanonicalSerializer`.

Drivery: `f12-e2e-author.mjs`, `f12-e2e-viewer.mjs` (block `**/*.wasm` → Server leg).

## Carry-forward (mimo rozsah A4)

- In-process preview **s daty** pro libovolné reporty — self-contained portál nemá živý datový
  zdroj; preview ukazuje reálnou strukturu s prázdnými daty (reálný render s daty jde přes API
  `/render`). Data preview zůstává jen pro demo seedy.
- Portál-wide lokalizace (portál chrome je hardcoded EN; předchází této fázi).
- `Outcome="Failed"` RenderRun při neočekávané výjimce executoru (dnes rezervováno).

---

# Fáze 13 (PASS A) — commitnutý C# E2E lane pro report server (CI)

Převod ad-hoc Node driverů (`f10-e2e.mjs`, `f12-e2e-*.mjs`) do **commitnutého .NET Playwright**
suite v `tests/Tempo.Blazor.E2E`. Aplikuje koncepty E2E skillu (functional-server / functional-wasm
render-mode split) v C#. Dvě dráhy: **PASS A** = CI DEMO lane (bez Keycloacku), **PASS B** (později)
= plný Keycloak-login + scheduling→smtp4dev.

## Architektonický nález, který určil návrh dráhy

Portálové stránky (explorer / favorites / historie / nový report) jsou **čistí HTTP konzumenti**
typového `ITempoReportServerClient` proti `Api:BaseUrl`. Self-contained demo Web **nemapuje**
katalogové/favorites/render-run endpointy (mapuje jen render/metadata/export) a v OIDC-off režimu
volá Api **anonymně** — po loginu je katalog prázdný (viz nález #3 Fáze 5b). Katalogové scénáře
proto **nelze** provést proti samotnému demo Webu; potřebují běžící Api (EF/`ReportServerDbContext`)
a autentizovaný principal. Keycloak-free cesta = **aditivní, konfiguračně gate-ované dev-auth schéma
na Api** (`Authentication:Dev:Enabled=true`), které autentizuje anonymní portálová volání jako pevný
dev principal (tenant `northwind`, role `report-admin` → TenantAdmin). Mimo tuto konfiguraci je
chování Api beze změny (JWT bearer + API key). DB asserty čtou **přímo SQLite databázi, kterou Api
zapisuje**.

## Jak lane běží

| Složka | Endpoint | Konfigurace (přes env při `dotnet run`) |
| --- | --- | --- |
| Api | `http://localhost:7001` | `Database__Provider=Sqlite`, `ConnectionStrings__ReportServer=Data Source=<Z:\rs-e2e\reportserver-e2e.db>`, `Authentication__Dev__Enabled=true`, `Authentication__Dev__TenantId=northwind`, `Authentication__Dev__Roles=report-admin` |
| Web | `https://localhost:7150` | OIDC **off** (Authority prázdné), `Api__BaseUrl=http://localhost:7001` |

Oba hosty startují přes `dotnet run --project … --urls …` z `ReportServerE2ETestBase`
(EnsureHostAsync pattern, readiness přes `/health` resp. root, kill na ProcessExit). Web běží na
**HTTPS 7150** (shoduje se s baked `Api:BaseUrl` WASM legu — browser-side klient resolvuje proti
tomuto originu), Web→Api hop je server-side **HTTP** (žádný dev-cert trust). DB soubor jde na `Z:`
(šetří C:), před čerstvým startem Api se smaže.

## Spuštění (CI DEMO lane)

```
set TM_RS_E2E=1
set TM_E2E_SELF_HOST=false          # přeskočí nesouvisející demo hosty
set TM_E2E_TRACE_ON_FAILURE=false   # při málu místa na disku
set NUGET_PACKAGES=Z:\nuget-rs      # rozbitá výchozí NuGet cache
dotnet test tests/Tempo.Blazor.E2E --filter TestCategory=ReportServerE2E
```

Bez `TM_RS_E2E` jsou testy **Inconclusive** (hosty nestartují) — výchozí demo E2E běh je nedotčen.

## Třídy a scénáře

- `ReportServerCatalogServerE2ETests` (`[TestCategory("ReportServerE2E")]`, functional-**server**;
  blokuje `**/*.wasm` — `dotnet.js` loader se ponechá, aby blazor.web.js vyjednal Server fallback;
  app zůstává na Server circuitu). Plné scénáře s **přímým DB důkazem**:
  1. Katalog: portál loads, explorer renders; create folder přes UI → assert `Folders` řádek.
  2. Nový report: New Report form → blank report → `/designer/{id}`, designer ukazuje **reálný**
     report (ne demo Sales Register) → assert `Reports` řádek.
  3. Favorites: toggle → assert `Favorites` řádek; `/favorites` list → klik round-trip (bez
     report-not-found); un-favorite → řádek pryč + empty state.
  4. Render → historie: parametrický report, zadání `AsOfDate`, Run → assert `RenderRuns` řádek
     s parametrem; `/history` ukazuje běh.
  5. Edge cases: nevalidní JSON upload → inline chyba + blokovaný submit; not-found path → graceful
     degradace (v OIDC-off demu full-nav založí nový neautentizovaný circuit → shell přesměruje na
     login, **ne** exception page). Autentizovaný `report-not-found` panel kryje Web.Tests bUnit
     `ReportViewerPageTests`.

Viewery se otevírají přes **SPA navigaci** (login → klik na složku ve stromu → klik na report):
demo session je per-circuit in-memory, takže přímý full-nav na `/reports/{id}` by založil nový
neautentizovaný circuit a byl by přesměrován na login.
- `ReportServerCatalogWasmE2ETests` (`[TestCategory("ReportServerE2E")]`, functional-**wasm**; naprimuje
  WASM cache a reloaduje, než InteractiveAuto přejde na WebAssembly leg): ověří render-mode
  (`data-mode=WebAssembly`) a **čistý běh** portálu na WASM legu (login shell interaktivní, žádný
  `#blazor-error-ui`). Katalogové write-flows i graceful not-found se ověřují na **Server legu**: na
  WASM legu čte klient z prohlížeče **baked** `Api:BaseUrl` (Web origin), který katalog/resolve
  nehostí — stejný důvod, proč projektové f12 drivery pro tyto flow blokují WASM.

Render-mode se asertuje v každé třídě (`#render-mode-marker[data-interactive=true]` → `data-mode`).
`[TestCategory("ReportServerFullStack")]` je **rezervována pro PASS B** (Keycloak + scheduling→smtp4dev).

## DB asserty

`ReportServerE2ETestBase.CreateDbContext()` otevře `ReportServerDbContext` nad tím samým SQLite
souborem, do kterého Api zapisuje. `Folders`/`Reports` mají tenant global query filter → čteno přes
`IgnoreQueryFilters()`; `Favorites`/`RenderRuns` filter nemají. Čtení má krátký retry na přechodné
„database is locked" (Api drží tentýž soubor).

## Výsledek živého běhu (7/7 PASS)

`TM_RS_E2E=1 dotnet test --filter TestCategory=ReportServerE2E` — **Passed: 7, Failed: 0** (Api SQLite
na Z:, dev-auth, Web HTTPS 7150). Přímé DB důkazy ověřeny (folder/report/favorite/render-run řádky
čteny z `ReportServerDbContext` nad běžícím souborem).

| Test | Leg | Výsledek |
| --- | --- | --- |
| `Catalog_PortalLoads_CreateFolder_PersistsRow` | server | PASS (`Folders` řádek) |
| `NewReport_CreatesRealReport_ShownInDesigner_AndPersisted` | server | PASS (`Reports` řádek, designer = reálný report) |
| `Favorites_ToggleRoundTrips_AndPersists` | server | PASS (`Favorites` řádek, round-trip, un-favorite → 0) |
| `Render_WithParameter_RecordsRunInHistory` | server | PASS (`RenderRuns` řádek s `AsOfDate`, /history) |
| `EdgeCases_InvalidUploadBlocks_AndDirectReportHitBouncesToLogin` | server | PASS (upload chyba blokuje submit; přímý hit → graceful login-redirect) |
| `LoginPage_BootsTo_WebAssembly` | wasm | PASS (`data-mode=WebAssembly`) |
| `Portal_RunsCleanly_OnWasm` | wasm | PASS (WASM leg, žádný `#blazor-error-ui`) |

## Deliverable 1 — RenderModeMarker

Marker `#render-mode-marker` (`data-mode` = Static|Server|WebAssembly, `data-interactive` = true|false)
**už existoval** v `src/Tempo.ReportServer.Web.Client/Components/RenderModeMarker.razor` a je zapojen
v `MainLayout`. PASS A: přidán `id="render-mode-marker"` a `data-interactive` znormalizován na
lowercase (`true`/`false`) pro kontrakt skillu. bUnit guard: `RenderModeMarkerTests` (Web.Tests, 3/3).

---

# Fáze 13 (PASS B) — NIGHTLY full-stack C# E2E lane (Keycloak + smtp4dev)

PASS A commitl CI DEMO dráhu (bez Keycloacku). **PASS B** přidává plnou dráhu proti **živému stacku**:
reálný **Keycloak** (OIDC login + JWT bearer), **Api na SQL Serveru** s EF security store a zapnutým
scheduling workerem, **Web s OIDC ON** a **smtp4dev** pro doručení scheduled reportů. Scénáře jsou
převodem ad-hoc Fáze 5b/10/12 driverů (`f10/f12-*.mjs`) do commitnutého .NET Playwright suite.

## Gating a spuštění

Všechny testy nesou `[TestCategory("ReportServerFullStack")]` a jsou **gate-ované za `TM_RS_FULLSTACK`**
(jako PASS A gate-uje na `TM_RS_E2E`). Bez flagu jsou **Inconclusive/Skipped** a **nespustí žádné
Keycloak-závislé hosty** → normální `dotnet test` / **CI je nedotčené** (CI běží jen `ReportServerE2E`).

```
# Prerekvizity: běžící Keycloak service (realm tempo-reports, users admin1/author1/viewer1 / Pass123!),
# dosažitelný SQL Server (localhost\SQLEXPRESS). smtp4dev dráha spustí sama (nebo reuse, pokud běží).
set TM_RS_FULLSTACK=1
set TM_E2E_SELF_HOST=false          # přeskočí nesouvisející demo hosty
set TM_E2E_TRACE_ON_FAILURE=false   # při málu místa na disku
set NUGET_PACKAGES=Z:\nuget-rs      # rozbitá výchozí NuGet cache
dotnet test tests/Tempo.Blazor.E2E --filter TestCategory=ReportServerFullStack
```

Volitelné env: `TM_RS_KC_WEB_SECRET` (jinak se secret klienta `tempo-report-web` **stáhne za běhu**
z Keycloak admin REST — necommitnuto), `TM_RS_KC_ADMIN_USER`/`_PASS` (default admin/admin),
`TM_SMTP4DEV_DIR` (default `C:\work\smtp4dev-master\Rnwood.Smtp4dev`). Nightly wrapper:
`scripts/run-report-server-e2e.ps1 -FullStack`.

## Jak dráha běží (hosty startuje `ReportServerFullStackE2ETestBase`)

| Složka | Endpoint | Konfigurace (přes env při `dotnet run`) |
| --- | --- | --- |
| Keycloak | `http://localhost:8080` realm `tempo-reports` | **musí běžet** (jinak jasná chyba); secret klienta stažen za běhu |
| smtp4dev | SMTP `:2525`, REST/web `:5050` | spuštěn s WorkingDirectory = projektový dir (jinak „Unable to find wwwroot") |
| Api | `https://localhost:7011` | `Database__Provider=SqlServer` (DB `TempoReportServerFullStackE2E`), `Security__Persistence=Ef`, `Authentication__Jwt__Authority=<KC realm>`, `Authentication__Dev__Enabled=false`, `Scheduling__Enabled=true`, `Scheduling__PollInterval=00:00:05`, `Scheduling__Smtp__Host=localhost` `Port=2525` |
| Web | `https://localhost:7150` | OIDC **ON** (`Authentication__Oidc__Authority=<KC realm>`, `ClientId=tempo-report-web`, `ClientSecret=<za běhu>`, `RequireHttpsMetadata=false`), `Api__BaseUrl=https://localhost:7011` |

Web běží na **7150**, protože to je jediná redirect URI registrovaná na KC klientu `tempo-report-web`
(login redirect jinam selže). Api je na **7011** (odlišné od PASS A `7001`), HTTPS, aby browser/WASM leg
uměl volat Api přes TLS bez mixed-contentu. HTTP-level scénáře získávají **reálný admin/author/viewer
bearer** přes direct-grant (`aud=tempo-report-api`). Hosty se killnou na ProcessExit; Keycloak service
zůstává. `[DoNotParallelize]`.

## Scénáře — výsledky živého běhu (9/9 PASS)

`TM_RS_FULLSTACK=1 dotnet test --filter TestCategory=ReportServerFullStack` — **Passed: 9, Failed: 0**
(2 m 42 s, jeden proces, hosty nastartované jednou). Efektivní data-tenant Keycloak uživatelů je
`default` (bez `tenant_id` claimu).

| Třída / test | Leg | Výsledek | Důkaz |
| --- | --- | --- | --- |
| `ReportServerApiKeyE2ETests` — `ApiKey_Render_Audits_ThenRevoke_Yields401` | HTTP | **PASS** | admin vytvoří klíč (`tmr_…`), render přes `X-Api-Key` **200**, `/api/audit` má `RenderReport` řádek (actor `api:{app}`, Allowed), revoke **204**, `/api/apikeys` má `RevokedAt`+`IsActive=false`, render revokovaným klíčem **401** |
| `ReportServerSchedulingE2ETests` — `Schedule_FiresSoon_DeliversEmailWithPdf_AndRecordsDeliveredRun` | HTTP + worker | **PASS** | cron `* * * * *`, worker (5s poll) doručí; **reálný e-mail v smtp4dev** (subject = report name), MIME příloha `application/pdf` (1 355 B), `/api/schedules/{id}/runs` má `Status=Delivered` |
| `ReportServerAuthRoleE2ETests` — `Author1_SeesAuthorNav_NotAdminNav` | server | **PASS** | nav reports/favorites/history/**designer/schedules/revisions** viditelné, **datasources/permissions/apikeys ABSENT**; žádný `eyJ` ve web storage |
| `ReportServerAuthRoleE2ETests` — `Viewer1_SeesOnlyViewerNav` | server | **PASS** | **jen** reports/favorites/history; vše ostatní ABSENT; žádný `eyJ` ve storage |
| `ReportServerAuthRoleE2ETests` — `BearerLeg_ApiRequiresToken_AndBrowserHoldsNoToken` | server | **PASS** | anonymní `/api/folders` **401**; `/auth/token` (BFF) vydá JWT, který chráněné Api přijme **200**; ve storage žádný token |
| `ReportServerAuthRoleE2ETests` — `Logout_ClearsSession_AndProtectedPagesRequireLogin` | server | **PASS** | před loginem `/auth/token` vydá JWT; po `/account/logout` už **nevydá** (BFF session zrušena); fresh cookies → protected page → **Keycloak login** |
| `ReportServerHandoffE2ETests` — `ServerToWasm_Handoff_StaysAuthenticated_AndBearerSurvives` | server→wasm | **PASS** | `data-mode` Server → **WebAssembly**; **bez KC re-loginu** (`#username` absent); `/auth/token` na WASM legu stále vydá **author1** JWT (`preferred_username`), chráněné Api **200** + seedovaná folder — **bearer leg přežil handoff** (reálné WASM catalog pokrytí odložené z PASS A) |
| `ReportServerPrerenderE2ETests` — `Reports_PrerenderedHtml_ContainsRealPortalContent` | server | **PASS** | autentizovaný **raw HTML** `/reports` (200, ne login-redirect) obsahuje `report-server-shell` + `nav-reports` + `author1` — reálný SSR obsah, ne prázdný shell |
| `ReportServerPrerenderE2ETests` — `Reports_ServerCircuit_EmitsNoBrowserSideCatalogFetch` | server | **PASS** | na interaktivním Server circuitu **0** browser-side katalogových requestů (`/api/folders|reports|catalog`) — potvrzuje, že katalog běží server-to-server přes circuit; **není to** sám o sobě důkaz reuse prerendered stavu (reálný double-fetch by šel pozorovat jen na WASM legu, který má doloženou auth-rehydratační mezeru) |

## Reálný nález odhalený živým handoffem (ne selhání testu)

**WASM klient nerehydratuje in-browser auth-state → SPA po handoffu routuje na vlastní `/login`.** BFF
**session (cookie) handoff přežije** — doloženo tím, že `/auth/token` na WASM legu **stále vydá platný
author1 bearer** a chráněné Api ho **přijme (200)** — ale WASM klient nerozezná přihlášení a naviguje na
portálový login (žádný KC re-login; server-side bearer leg je nedotčen). Navazuje na Fáze 5b nález #4/#5
(browser-bearer/handoff). Handoff test proto asertuje **load-bearing fakta** (Server→WASM, žádný KC
re-login, přežití bearer legu přes reálné autorizované Api volání), nikoli WASM UI nav.

## DB asserty

Proti **reálné SqlServer DB** (`TempoReportServerFullStackE2E`) přes **admin-autentizované Api endpointy**,
které čtou tytéž EF tabulky, do nichž render/scheduling zapisují: `RenderReport` audit řádek
(`/api/audit`), `RevokedAt` (`/api/apikeys`), `ScheduleRuns Status=Delivered` (`/api/schedules/{id}/runs`).
Bez závislosti na SqlServer EF provideru v testovém projektu.

## Deliverables PASS B

Přidáno aditivně (nic z PASS A / CI DEMO dráhy se nezměnilo): `ReportServerFullStackE2ETestBase` +
5 tříd (`ReportServerApiKeyE2ETests`, `ReportServerSchedulingE2ETests`, `ReportServerAuthRoleE2ETests`,
`ReportServerHandoffE2ETests`, `ReportServerPrerenderE2ETests`), nightly wrapper
`scripts/run-report-server-e2e.ps1`. Žádné secrets v repu (secret klienta stažen za běhu).
