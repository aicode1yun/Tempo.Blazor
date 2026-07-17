# ADR-0001: Databáze — Microsoft SQL Server (dev i prod)

- **Status:** Přijato
- **Datum:** 2026-07-17
- **Kontext plánu:** Plán 2 „Tempo Report Server", Fáze 0, rozhodnutí O1
- **Souvisí:** [ADR-0002](0002-oidc-keycloak.md)

## Kontext

Rendering engine (F0–F19) je hotový, ale „plný server" má otevřené rozhodnutí O1:
produkční databáze. Dosavadní stav používá SQLite (dev dogfooding), původní návrh
zvažoval PostgreSQL. Report server potřebuje perzistentní katalog (reporty, verze,
složky, datové zdroje, parametry), úložiště API klíčů, audit a scheduling joby.

Provozní realita:

- Vývojový server je **Windows Server bez Dockeru** (a Docker tam nebude).
- Produkční topologie zatím není fixní, ale reálnější je rovněž **Windows host bez
  Dockeru**.
- V dosahu je instance `WIN-5F7V1OKQ0GO\SQLEXPRESS`.
- Identita OIDC (viz [ADR-0002](0002-oidc-keycloak.md)) poběží na Keycloacku, který
  bude perzistovat do stejného DB serveru.

## Rozhodnutí

1. **Microsoft SQL Server pro dev i prod**, instance `WIN-5F7V1OKQ0GO\SQLEXPRESS`.
2. **Přístup aplikace k DB = Integrated Security** (Windows auth). Connection string
   aplikace tak neobsahuje žádné tajemství; identita běhu (Windows služba / IIS
   AppPool) musí mít na SQL Serveru přidělený přístup.
3. **Keycloak = dedikovaný SQL login** (username/password), ne integrated security —
   JVM s integrated security je provozně křehká a v budoucím kontejneru nemožná.
4. **Oddělené databáze** na téže instanci: Keycloak vlastní svoje schéma; aplikace
   vlastní katalog / API klíče / audit / scheduling. Tabulky se **nikdy nesdílejí**.
5. **SQLite se opouští** jako perzistentní úložiště serveru.
6. **Testy proti reálné DB = MSSQL / LocalDB + [Respawn](https://github.com/jbogard/Respawn)**
   na čištění mezi testy. **Testcontainers se nepoužijí** (vyžadují Docker, který na
   dev/CI Windows serveru není).
7. **EF Core s MSSQL providerem**; migrace přes `dotnet ef` (viz Fáze 2 a 7 plánu).

## Důsledky

**Pozitivní**
- Soulad s Windows/MSSQL stackem, žádný Docker požadavek na dev/prod.
- Integrated security = o jedno tajemství (DB heslo aplikace) méně.
- Jedna DB instance pro app i Keycloak, ale s čistou izolací schémat.

**Negativní / rizika**
- Integrated security **neplatí v kontejneru** — kdyby prod skončil na Linuxu/Dockeru,
  je nutné přejít na SQL login (poznamenáno v backlogu, Fáze 9).
- Keycloak na MSSQL je oficiálně podporovaný, ale komunitně méně častý než PostgreSQL
  (většina návodů cílí na Postgres) — počítat s tím při troubleshootingu.
- Test DB přes LocalDB/SQLEXPRESS místo testcontainers = o něco méně izolace mezi
  běhy; řeší Respawn.
- ITokenStore (viz [ADR-0002](0002-oidc-keycloak.md)) drží tokeny v `IMemoryCache` —
  stačí pro single-host prod; při scale-out přejít na distribuovanou (SQL-backed)
  cache (Fáze 7).

## Alternativy, které jsme zamítli

- **PostgreSQL** — technicky v pořádku, ale mimo cílový Windows/MSSQL stack a bez
  přidané hodnoty pro toto nasazení.
- **Ponechat SQLite v produkci** — nevhodné pro multi-tenant server s konkurenčními
  zápisy, schedulingem a auditem.
- **Testcontainers** — nejde, na dev/CI není Docker.
