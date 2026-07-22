# ADR-0002: Autentizace a autorizace — Keycloak přes standardní OIDC

- **Status:** Přijato
- **Datum:** 2026-07-17
- **Kontext plánu:** Plán 2 „Tempo Report Server", Fáze 0, rozhodnutí O3
- **Souvisí:** [ADR-0001](0001-databaze-mssql.md)

## Kontext

„Plný server" má otevřené rozhodnutí O3: reálné OIDC (dnes dev JWT). Cílem je report
server konkurující Telerik Report Serveru — potřebuje přihlašování uživatelů, per-folder
oprávnění (kdo vidí/edituje jakou složku a report), spouštění schedulingu a M2M
integrace pro service účty.

Architektura Web/API je (podle skillů repa pro Blazor InteractiveAuto) **decoupled**:
frontend (`App.Web` host + `App.Web.Client` WASM) a API (`App.Api`) běží na různých
originech; volání jdou **přímo browser→API bearer tokenem, bez BFF proxy**. Každá
komponenta může běžet ve třech kontextech (SSR prerender / Server circuit / WASM), což
diktuje, jak se k tokenu dostat.

## Rozhodnutí

### IdP a hranice
1. **Keycloak** jako OIDC provider; perzistence do MSSQL (viz [ADR-0001](0001-databaze-mssql.md)).
2. **Standardní OIDC, žádné provider-specific SDK v business logice.** V ASP.NET Core
   `AddOpenIdConnect()` + cookie auth ve Web, `AddJwtBearer()` v Api. `Authority`,
   `ClientId`, `ClientSecret` jsou **per-deployment konfigurace**, ne kód.
3. **Autentizace v IdP, autorizace v aplikaci.** Z tokenu bereme jen hrubé claimy:
   `sub`, e-mail, role/skupiny. Detailní oprávnění drží vlastní DB.

### Tok tokenů (blessed bearer model dle skillu InteractiveAuto)
4. **Web = confidential client**, authorization code + PKCE, cookie session.
   Autentizace **končí na Web hostu**; tokeny žijí server-side v `ITokenStore`.
5. **`ITokenStore` je jediná IdP-specifická část** — hydratace při loginu a refresh
   callback (Keycloak token endpoint, refresh grant). Vše ostatní (`IAccessTokenProvider`
   per host, `/auth/token` handout pro WASM, `ApiClientBase` s 401→refresh→retry,
   auth-state serializace pro `[Authorize]`/`AuthorizeView`) je univerzální plumbing.
6. **Žádný BFF proxy.** Datová volání jdou přímo browser→API (WASM) / server→API
   (circuit). Web jen vydává svému cookie-autentizovanému uživateli krátkodobý access
   token přes same-origin `GET /auth/token`.
7. **Refresh token nikdy neopustí server.** Prohlížeč drží jen krátkodobý access token
   v paměti (ne localStorage/sessionStorage).

### Hardening
8. **Access token TTL = 5 min.**
9. **Audience-restricted token** — token vydávaný do prohlížeče je platný **jen pro
   Report Api** (Keycloak audience mapper / token targeting), ne pro cokoliv jiného.
10. **CSP** k omezení XSS; FE session cookie `HttpOnly`, `Secure`, `SameSite=Lax`.
11. **CORS na Api:** přesný FE origin, **bez** `AllowCredentials` (bearer nepotřebuje).

### Autorizace v aplikaci
12. **Per-folder ACL ve vlastní DB, keyováno na `sub`.** Role Admin/Author/Viewer
    **per složka**. `sub` je stabilní klíč (ne e-mail — ten se mění).
13. **JIT provisioning** — při prvním loginu se `sub` namapuje na interní user row.
14. **Keycloak role = capability tier** (strop schopností, coarse), konkrétní granty
    na složky/reporty = app DB.

### Autentizační schémata na Api
15. Api přijímá **tři schémata**, všechna ústí do jednoho „principal se scopes" →
    stejná autorizační vrstva → stejný audit:
    - **Uživatelský Keycloak JWT** (přes Web) → per-folder ACL podle `sub`.
    - **Client-credentials JWT** (Keycloak service účet, enterprise M2M, centrální
      rotace) → autorizace podle client rolí/scopes.
    - **Vlastní API klíč** (hash, scopes, expirace; pro skripty/třetí strany, co
      neumí OAuth) → autorizace podle scopes klíče.
16. **ACL model zahrnuje strojové principály** (service účty i API klíče), nejen lidi.
17. **`/health` a `/version` jsou anonymní** (readiness probe, E2E webServer URL).

### Nasazení
18. Windows služby / IIS bez Dockeru (viz [ADR-0001](0001-databaze-mssql.md)); Keycloak
    jako standalone Windows služba (`kc.bat`, vlastní JDK). Docker odložen do backlogu.

## Důsledky

**Pozitivní**
- Portovatelnost: výměna IdP = jen konfigurace + `ITokenStore` implementace.
- Sedí na paved road repa (skilly + E2E harness, který testuje `Authorization: Bearer`
  na každém API-origin requestu a „no token in web storage").
- Granularita oprávnění (per-folder) je v DB, kde ji lze libovolně rozšiřovat — IdP ji
  neřeší.

**Negativní / rizika**
- Prohlížeč drží reálný Keycloak access token v paměti — XSS by ho po dobu jeho života
  (≤5 min) mohl exfiltrovat. Mitigace: krátké TTL, audience-restricted na Report Api,
  CSP, refresh jen na serveru, per-folder autorizace (i ukradený token je omezen
  granty uživatele), audit každého volání.
- Tři auth schémata = větší plocha; nutná disciplína, aby **všechna** procházela stejnou
  autorizací a auditem (API klíč nesmí být zadní vrátka mimo ACL).
- Keycloak client-credentials i vlastní API klíče se v M2M překrývají — vědomě, cílí na
  různé konzumenty (enterprise vs. quick-and-dirty).

## Alternativy, které jsme zamítli

- **BFF proxy** (token nikdy do prohlížeče) — bezpečnostně o něco silnější proti XSS
  exfiltraci access tokenu, ale jde proti architektonickému skillu i E2E harnessu
  (bespoke cesta), přidává hop a chokepoint na Web hostu a boří InteractiveAuto model
  přímých volání. Rozdíl dorovnáváme hardeningem uvnitř bearer modelu.
- **Provider-specific Keycloak SDK v business logice** — zamčení na IdP, zbytečné.
- **Autorizace v IdP (per-folder role v Keycloacku)** — IdP nemá řešit granularitu
  „Author složky X"; nešlo by to vyjádřit claimem a míchalo by identitu s doménovými
  oprávněními.
