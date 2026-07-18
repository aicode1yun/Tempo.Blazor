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
