# Tempo Report Server — Post-MVP backlog

Položky mimo rozsah Plánu 2 (MVP „plný server"). Každá s hrubým odhadem a prerekvizitami.
MVP (F0–F8 + 5b) je hotový a end-to-end ověřený (viz [report-server-e2e-results.md](report-server-e2e-results.md)).

## A. Reálné mezery odhalené během MVP (priorita)

| # | Položka | Odhad | Prerekvizity / pozn. |
| --- | --- | --- | --- |
| A1 | **UI ↔ OIDC principal wiring** — portál UI se dnes gate-uje na in-memory demo session (tenant/role z dema), ne na Keycloak claimech. Napojit identitu/tenant/role z OIDC principalu, aby UI odpovídalo přihlášenému uživateli a katalog ukazoval jeho tenant. **Hlavní zbývající krok „plného serveru".** | M–L | F4/F5 hotové; rozhodnout mapování tenant claim → data tenant |
| A2 | **Audit renderu a použití API klíče** — dnes se audit píše jen při změně ACL. Navěsit `WithReportAudit` na render/catalog endpointy (kdo/kdy/co renderoval). | S–M | F3 audit store hotový |
| A3 | **Per-folder ACL enforcement na živých /api/** endpointech** — `RequireReportPermission` na folder/report/render endpointy + koordinovaná úprava API-key kontraktních testů (dnes vytvářejí složky klíčem bez author role). | M | F4 ACL vrstva hotová |
| A4 | **Upload-report UI** — dnes se report zakládá jen přes Api; přidat UI upload/create stránku. | S–M | katalog cutover (5b) hotový |
| A5 | **Commitnutý E2E test suite** — živé E2E proběhlo přes scratch Node skripty; převést do repo E2E projektu s `webServer` konfigem (Api+Web+smtp4dev) pro CI. | M | Playwright + live stack (vše k dispozici) |
| A6 | **EF ACL: Deny / Role / Application granty** — EF permission store je dnes allow-only user-subject (nepodporované kombinace → 400). Implementovat plný ACL model. | M | F4/5b |

## B. Provozní / hardening follow-upy

| # | Položka | Odhad | Prerekvizity / pozn. |
| --- | --- | --- | --- |
| B1 | **Webhook DNS-rebinding pin** — guard resolvuje+validuje DNS, ale HttpClient dělá vlastní lookup; pin validované IP přes `SocketsHttpHandler.ConnectCallback` + cílený test [veřejná,privátní] mix. | S | F7 SSRF guard hotový |
| B2 | **Scheduling lease/claim pro multi-instance** — dnes single-instance předpoklad (concurrency ošetřen výjimkou, ale při >1 workeru hrozí duplicitní delivery). Atomický claim na schedule. | M | F6/F7 |
| B3 | **SmtpClient → MailKit** — scheduled delivery dnes `System.Net.Mail.SmtpClient` (SYSLIB0014) kvůli příloham; produkčně MailKit implementace `IScheduledReportEmailSender`. | S | F6 |
| B4 | **Zátěžový test proti nasazenému Kestrel hostu** — F7 harness běžel in-process TestServer+SQLite; změřit end-to-end HTTP proti nasazenému hostu na MSSQL. | S | F7 |
| B5 | **Lokalizace Web hostu** — `Tempo.ReportServer.Web` používá hardcoded EN (host nemá ITmLocalizer); zavést lokalizaci celého hostu. | M | — |
| B6 | **nuget push reporting balíčků do CI** — pack hotový (F8), doplnit push krok do workflow. | S | F8 |

## C. Post-MVP featury (z původního Fáze 9 zadání)

| # | Položka | Odhad | Prerekvizity / pozn. |
| --- | --- | --- | --- |
| C1 | **Volitelný Dockerfile / Linux-kontejner deploy** — pro kontejnerový prod; tam **SQL login** místo integrated security (viz ADR-0001). | M | rozhodnutí o prod topologii |
| C2 | **Distribuovaná render fronta (více nodů)** — dnes bounded channel v procesu; škálování na více render nodů. | L | F7 render executor |
| C3 | **Distribuovaný ITokenStore** — abstrakce + SQL-backed varianta hotová (F7); plné multi-instance ověření. | S–M | F7 |
| C4 | **RDL import** — import Telerik/SSRS RDL definic do katalogu. | L | katalog model |
| C5 | **Drill-through** — navigace mezi reporty parametry. | M | engine |
| C6 | **Stacked charts** — rozšíření TmChart. | S–M | engine |
| C7 | **RTL podpora** — pravo-levé jazyky v renderu. | M | engine/PDF |

Odhady: S = do ~1 dne, M = ~1–3 dny, L = ~týden+.
