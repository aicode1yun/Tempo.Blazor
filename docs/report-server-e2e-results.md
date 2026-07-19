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
