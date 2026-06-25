# Tempo Report Server — kompletní TDD todo list (2026-06-12)

Cíl: vybudovat obdobu Telerik Report Serveru — reporty se dají **vytvářet a prohlížet z různých
aplikací**. Komponenty (designer + viewer) jako NuGet, kontrakty + typed HTTP client jako NuGet,
report engine jako čistá .NET knihovna (NuGet), k tomu API server a FE (Blazor InteractiveAuto).

**Role:** Senior Full-stack Developer + UX specialista + UI expert + specialista na AI.

---

## Uzavřená architektonická rozhodnutí (diskuse 2026-06-12)

| # | Rozhodnutí |
|---|---|
| 1 | **Reuse z document editoru = snapshot kontrakt + JS canvas painter + viewer infrastruktura** (virtualizace, page cache). Layout se NEsdílí — report má vlastní bandový layout v C#. |
| 2 | **Embedded režim přes providery**: `IReportDataProvider` + `IReportDefinitionStore` v Abstractions od 1. dne; report server je sám implementuje (dogfooding). Viewer má `ReportSource` = `Remote` \| `Embedded`. MVP primárně ladí remote. |
| 3 | **Pixel/print-based bandový model** (ReportHeader/Footer, PageHeader/Footer, GroupHeader/Footer, Detail). Žádný flow/responsive režim. |
| 4 | **Multi-tenancy v jádru**: TenantId first-class všude, tenant context součástí provider kontraktů (`ReportExecutionContext`), per-folder ACL, render job queue s férovostí + kvótami per tenant. |
| 5 | **Grafy kreslí engine** do vektorových primitiv ve snapshotu (path/polyline/arc/fill/clip) → identické ve vieweru i PDF. MVP: column/bar, line/area, pie. |
| 6 | Naming: `Tempo.Reporting.*` (ne-UI balíčky), `Tempo.Blazor.Reporting` (komponenty). |
| 7 | **Report engine celý v C#** (processing + layout + rendering). Žádný Node/Jint. Font metriky = build-time předpočítané tabulky z dodávaných TTF; PDF přes SkiaSharp `SKDocument` (NE QuestPDF — kreslíme absolutní primitiva). Fidelity ve vieweru: vlastní webfonty + run-width scaling (`scaleX = width/measureText`). |

Klíčové bezpečnostní axiomy:
- Definice reportu je **untrusted vstup** — expression evaluator je čistý (žádná reflexe, žádný
  přístup k prostředí), SQL výhradně parametrizované, connection stringy NIKDY v definici
  (pojmenované datové zdroje registrované na serveru, scopované na tenanta).
- `schemaVersion` v definici od prvního dne + migrační pipeline (vzor: document model verze).

---

## ⚠️ KRITICKÁ PRAVIDLA — ABSOLUTNÍ ZÁKAZ PORUŠOVAT

### 1. TDD — TEST FIRST (Red-Green-Refactor) — STRIKTNĚ!
```
Krok 1: Napiš FAILING test (červený) — MUSÍ selhat před implementací (spusť a ověř!)
Krok 2: Napiš MINIMÁLNÍ kód pro průchod testu (zelený)
Krok 3: Refactoruj (čistý kód bez změny funkcionality)
Krok 4: Odškrtni checkbox v tomto souboru a pokračuj dalším taskem
```

### 2. PRŮBĚŽNÉ E2E + SCREENSHOT REVIEW — POVINNÁ BRÁNA KAŽDÉ FÁZE
- Každá fáze s vizuálním výstupem KONČÍ Playwright E2E testy, které ukládají screenshoty do
  `tests/Tempo.Blazor.E2E/__screenshots__/reporting/<faze>/NN-nazev.png`.
- Před odškrtnutím fáze proběhne **dvoukolové posouzení screenshotů**:
  1. **Funkční posouzení** — screenshot prokazuje splnění akceptačních kritérií fáze
     (kritéria jsou vyjmenovaná u každé fáze níže; posuzuje se proti nim, ne od oka).
  2. **UX expert review** — posoudit jako UX/UI expert: vizuální hierarchie, spacing a
     zarovnání dle design tokenů, typografie/čitelnost, afordance ovládacích prvků, stavy
     (hover/focus/disabled/loading/empty), konzistence s ostatními Tm* komponentami.
     Nálezy se opravují IHNED v rámci fáze (ne odkládat) a pořídí se nové screenshoty.
- E2E sada běží průběžně po každé fázi (ne až na konci projektu). Fáze bez UI (parser,
  processing) mají od chvíle existence harness stránky aspoň 1 E2E smoke test.
- Vzor: harness přístup z `core-engine-harness.html` + screenshot konvence
  `__screenshots__/document-library/phaseN/NN-name.png`.

### 3. ŽÁDNÉ HARDCODED TEXTY — VŠE Z RESOURCES!
- ❌ ZAKÁZÁNO: `"Uložit"`, `"Parametry reportu"` v C#/Razor kódu
- ✅ `@Localizer["Reporting_Viewer_Export"]`; UI texty v
  `src/Tempo.Blazor.Reporting/Resources/TmReportingResources.resx` (+ `.cs.resx`, `.fr.resx`),
  validační zprávy v `src/Tempo.Reporting.Abstractions/Resources/ReportingValidationResources.resx`,
  chybové hlášky expression parseru TAKÉ lokalizované (autor reportu je uvidí v designeru).

### 4. FLUENT VALIDACE — FE i BE
- Backend (Abstractions): validátory s `IStringLocalizer<ReportingValidationResources>`
- Frontend: `Tempo.Blazor.FluentValidation` (FluentValidationValidator + EditContext extensions)
- Async validace s debouncingem 300 ms (unikátnost názvu reportu ve složce přes API)

### 5. ŽÁDNÉ ZJEDNODUŠENÉ IMPLEMENTACE
- ❌ placeholdery, mock data v produkčním kódu, TODO/FIXME, `// implement later`
- ✅ produkční kód od prvního řádku; demo data jen v seed/demo projektech

### 6. PRAVIDLO TESTŮ
- ❌ NIKDY neměnit test se správnou logikou — test je specifikace
- ✅ VŽDY opravit příčinu v implementačním kódu

### 7. DOTAZY PŘI NEJISTOTĚ
- Když si nejsi jistý specifikací → zeptej se uživatele, nehádej

### Další závazná pravidla
- ⚠️ Scoped CSS (`.razor.css`), CSS design tokeny Tempo.Blazor (viz `DesignTokensPage`),
  žádné hardcoded barvy/spacing
- ⚠️ Používat Tm* komponenty; nejistota ohledně API → MCP server tempo-blazor docs
- ⚠️ DTO vždy do `Tempo.Reporting.Abstractions`; žádné wrapper třídy pro API odpovědi (HTTP kódy)
- ⚠️ Atomické transakce — Unit of Work v server úložišti
- ⚠️ `dotnet test` NIKDY paralelně (OOM, exit 137) → `-- xUnit.parallelizeTestCollections=false`
  nebo po projektech
- ⚠️ Po změně sdílených .mjs painter modulů VŽDY přebuildit bundle (`npm run build:...`)
- ⚠️ Servery: WASM demo `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https` (7106),
  API `--launch-profile Tempo.Blazor.Demo.Api` (5100); po změně C# seed RESTARTOVAT i API
- ⚠️ Licence: fonty jen OFL/Apache (Noto/Inter…), žádný AGPL kód, ClosedXML=MIT OK

---

## Cílová architektura

```
src/Tempo.Reporting.Abstractions    ← NuGet: model definice reportu (bandy, elementy), JSON
        serializace + schemaVersion + migrace, expression AST kontrakty, DTOs, FluentValidation
        validátory, ITempoReportServerClient (typed HttpClient s pluggable token providerem),
        IReportDataProvider, IReportDefinitionStore, ReportExecutionContext (tenant+user),
        StaticDataProvider (pro testy/embedded). Bez Blazor/ASP.NET závislostí.
src/Tempo.Reporting.Engine          ← NuGet: expression evaluator, processing (grouping,
        agregace, band instantiation), font metriky (tabulky), text layout (line breaker),
        bandový layout + stránkování, tablix, snapshot generátor, chart geometrie,
        SkiaSharp PDF renderer, XLSX/CSV export. Běží na serveru i ve WASM.
src/Tempo.Blazor.Reporting          ← NuGet RCL: TmReportViewer, TmReportParameterPanel,
        TmReportExplorer, TmReportDesigner (+ podkomponenty). JS: reporting viewer bundle
        reusující canvas painter moduly z document editoru. Deps: Tempo.Blazor,
        Tempo.Reporting.Abstractions, Tempo.Blazor.FluentValidation.
src/Tempo.ReportServer.Api          ← app: ASP.NET Core minimal API — definice/složky/revize,
        render joby (queue + kvóty), datové zdroje (SQL/REST provider implementace),
        tenancy middleware, schedules. EF Core úložiště (SQLite dev, provider-agnostic).
src/Tempo.ReportServer.Web          ← app: Blazor InteractiveAuto FE — explorer, viewer,
        designer, správa datových zdrojů/schedulů; dogfooduje NuGety výše.
tests/Tempo.Reporting.Abstractions.Tests   ← xUnit + FluentAssertions
tests/Tempo.Reporting.Engine.Tests         ← xUnit + FluentAssertions (golden testy layoutu)
tests/Tempo.Blazor.Reporting.Tests         ← bUnit (vzor Tempo.Blazor.Tests)
tests/Tempo.ReportServer.Api.Tests         ← integrace (WebApplicationFactory, vzor Demo.Api.Tests)
tests/Tempo.Blazor.E2E                     ← rozšířit: Reporting* E2E + __screenshots__/reporting/
src/Tempo.Blazor.Demo.SharedUI             ← rozšířit: demo stránka „cizí aplikace" s embedded
                                              viewerem (důkaz multi-app příběhu)
```

Render pipeline:
```
definice + parametry ─► PROCESSING (IReportDataProvider → grouping → výrazy → band instance)
                     ─► LAYOUT (bandy, tablix, stránkování; metriky z tabulek)
                     ─► SNAPSHOT (JSON: stránky, text runy se šířkami, obrázky, primitiva)
                          ├─► JS canvas painter (TmReportViewer)
                          ├─► SkiaSharp SKDocument → PDF (fonty embed+subset)
                          └─► SkiaSharp raster → PNG thumbnaily
        XLSX/CSV jde z PROCESSING výstupu (ne ze snapshotu) — standardní přístup.
```

---

## Otevřené otázky (pravidlo 7 — zeptat se, návrhy defaultů)

- [ ] **O1: Produkční DB report serveru** — návrh: EF Core, dev/testy SQLite, produkce PostgreSQL.
      Potvrdit s uživatelem před fází F10.
- [ ] **O2: Sada dodávaných fontů** — návrh: Inter (UI/sans), Noto Serif, JetBrains Mono,
      Noto Sans pro širší pokrytí znaků (vše OFL). Potvrdit před F0.4.
- [ ] **O3: Identita pro FE** — návrh: dev JWT issuer v Api (vzor demo), produkčně externí OIDC IdP.
      Potvrdit před F10.
- [ ] **O4: Umístění server aplikací** — návrh: v tomto repu (sdílená test infra); případné
      vyčlenění do vlastního repa až po stabilizaci. Potvrdit před F10.
- [ ] **O5: RTL/komplexní skripty** — návrh: MVP latinka/azbuka/řečtina/CJK; RTL+shaping
      (HarfBuzzSharp server-side) jako post-MVP fáze. Potvrdit (ovlivní F0/F5 scope).

---

## F0 — Základy + fidelity spike (risk-first) ✅=hotovo

Cíl: zabít největší riziko (parita C# metrik vs. prohlížeč) DŘÍV, než se postaví cokoli dalšího.

- [x] F0.1 Projekty + csproj + solution + prázdné test projekty (Abstractions, Engine, + E2E místo)
- [x] F0.2 Analýza snapshot kontraktu: co přesně konzumuje canvas painter document editoru
      (render moduly v `src/Tempo.Blazor/wwwroot/js/document-editor/render/`); rozhodnout
      podobu `Tempo.Reporting` snapshot JSON schématu (text runy s x/y/width/font/barvou,
      obrázky, čáry, vektorová primitiva, clip regiony, stránky s rozměry)
- [x] F0.3 C# snapshot model (Engine) + serializace + verze — unit testy round-trip
- [x] F0.4 Výběr fontů (O2) + build-time **generátor metrických tabulek** z TTF
      (advance widths, kerning páry, ascent/descent/lineGap; kompaktní binární formát
      + loader) — unit testy proti známým hodnotám glyfů
- [x] F0.5 `ITextMeasurer` (Engine) nad tabulkami: šířka runu (string, font, size, bold/italic,
      letterSpacing) — unit testy vč. fallback fontu a chybějících glyfů
- [x] F0.6 Harness stránka `reporting-harness.html` (vzor core-engine-harness): načte JS painter
      bundle, přijme snapshot JSON, vykreslí na canvas
- [x] F0.7 JS: minimální reporting painter bundle — reuse low-level draw rutin z document
      editoru (extrakce sdílených modulů, `npm run build:reporting`); kreslí text runy
      s **run-width scalingem** (`scaleX = width/measureText`), čáry, obdélníky
- [x] F0.8 **GOLDEN E2E BRÁNA (Playwright):** stránka s webfonty změří sadu řetězců
      (různé fonty/řezy/velikosti/diakritika/CJK) přes `canvas.measureText` vs. C# vypočtené
      šířky — tolerance ≤ 0,5 % šířky runu; + screenshot vykresleného vzorku
      → `__screenshots__/reporting/f0/01-fidelity-sample.png`
- [x] F0.9 Posouzení screenshotů: (1) funkčně — text sedí na očekávané pozice, žádné překryvy;
      (2) UX — typografie vzorku je čistá (baseline, řádkování)

## F1 — Schéma definice reportu (Abstractions)

- [x] F1.1 Model: `ReportDefinition` (schemaVersion, name, page setup: velikost/orientace/okraje,
      parameters, dataSets, styles), bandy (ReportHeader/Footer, PageHeader/Footer,
      GroupHeader/Footer, Detail) — unit testy konstrukce + validace
- [x] F1.2 Elementy: TextBox (výraz/text, rich style, zarovnání, padding, border, růst výškou),
      Image (zdroj: url/embedded/výraz, sizing), Shape/Line, Table (struktura, detail v F7),
      Chart (struktura, geometrie v F14), SubReport (odkaz + mapování parametrů)
- [x] F1.3 Parametry reportu: typy (string/number/date/bool/list), default výrazem, available
      values (statické/z datasetu), multi-value, hidden — validátory
- [x] F1.4 JSON serializace: stabilní, deterministická (round-trip byte-parity testy),
      `schemaVersion` + **migrační pipeline** (registr migrací, test migrace v1→v1 no-op)
- [x] F1.5 FluentValidation validátory celé definice (lokalizované zprávy) — testy všech pravidel
- [x] F1.6 DTOs pro API (folder/report/revision/render request…) — žádné doménové typy v API

## F2 — Expression language (Engine)

Syntaxe: `=Fields.Price * Parameters.Rate`, `=Sum(Fields.Total)`, `=IIf(cond, a, b)`,
`=Format(Fields.Date, "d.M.yyyy")`. Čistý evaluator — ŽÁDNÁ reflexe, žádný přístup k prostředí.

- [x] F2.1 Lexer (čísla, stringy, identifikátory, operátory, závorky, tečková cesta) — testy
      vč. chybových pozic (řádek/sloupec)
- [x] F2.2 Parser → AST (precedence, unární minus, volání funkcí, member access) — testy
      vč. error recovery (srozumitelná lokalizovaná chyba)
- [x] F2.3 Typový systém + koerce (number/string/bool/date/null propagace) — testy
- [x] F2.4 Evaluator nad `ExpressionContext` (Fields, Parameters, Globals: PageNumber/TotalPages/
      ExecutionTime/UserName/TenantName) — testy; PageNumber/TotalPages = deferred placeholdery
      vyhodnocované až v layoutu
- [x] F2.5 Built-in funkce: matematické, string (Trim/Upper/…/Format), datum (Year/AddDays/…),
      logické (IIf/Switch/IsNull), konverze — testy každé funkce vč. edge cases
- [x] F2.6 Agregátové funkce jako AST uzly (Sum/Count/CountDistinct/Min/Max/Avg/First/Last,
      scope argument group/page/report) — vyhodnocení dodá processing (F4), tady parsování
      + validace umístění — testy
- [x] F2.7 Bezpečnostní testy: limity hloubky/délky výrazu, žádný přístup mimo context,
      deterministický výsledek, timeout ochrana

## F3 — Datová vrstva (Abstractions kontrakty + provider implementace)

- [x] F3.1 `ReportExecutionContext` (TenantId, UserId, claims, culture, cancellation) +
      `IReportDataProvider` (`GetDataAsync(dataSetName, query, parameters, context)` →
      schema + řádky, streaming-friendly) — kontraktové testy
- [x] F3.2 `StaticDataProvider` (inline data v definici / dodaná aplikací) — testy
- [x] F3.3 `IReportDefinitionStore` (load/save/list/folders/revize, tenant-scoped) +
      in-memory implementace pro testy — kontraktové testy
- [x] F3.4 REST/JSON provider (Api projekt): URL šablona s parametry (URL-encoding, žádná
      interpolace credentials), JSONPath/pointer výběr pole, auth hlavičky ze server konfigurace
      — testy s fake HTTP handlerem
- [x] F3.5 SQL provider (Api projekt): výhradně `DbParameter`, mapování parametrů reportu,
      timeout/řádkový limit z kvót — testy (SQLite in-memory), **testy odmítnutí interpolace**
- [x] F3.6 Registr pojmenovaných datových zdrojů (tenant-scoped, connection stringy v server
      konfiguraci/secret store, NIKDY v definici) — testy izolace tenantů

## F4 — Processing engine

- [x] F4.1 Dataset runtime: schema + typované sloupce, řádkový kurzor — testy
- [x] F4.2 Filtrování + sortování výrazy — testy (vč. null ordering, culture-aware collation)
- [x] F4.3 Grouping: víceúrovňové skupiny, group výrazy, sort skupin (i podle agregátu) — testy
- [x] F4.4 Agregáty: Sum/Count/…/running totals, scope group/page/report; dvouprůchodové
      vyhodnocení (page agregáty až v layoutu) — testy
- [x] F4.5 Band instantiation: definice + data → strom instancí bandů s VYHODNOCENÝMI hodnotami
      (TextBox → konkrétní rich-text runy; visibility výrazy; sub-report expanze s limitem
      hloubky) — testy
- [x] F4.6 Parametry: validace hodnot, available values z datasetu, kaskádové parametry
      (parametr závislý na jiném) — testy
- [x] F4.7 Výkonnostní smoke: 100k řádků, 3 úrovně skupin < 2 s processing (benchmark test,
      neblokující CI gate — jen log + assert horní hranice 10 s)

## F5 — Text layout core (Engine)

- [x] F5.1 Rich-text model runu (font, size, bold/italic/underline/strike, barva, highlight) —
      sdílený se snapshot modelem — testy
- [x] F5.2 Line breaker nad `ITextMeasurer`: word-wrap (UAX#14 zjednodušeně: mezery, spojovníky,
      CJK), hard breaks, hyphenation NE (post-MVP) — golden testy (vstup → přesné řádky+šířky)
- [x] F5.3 Odstavec: zarovnání left/center/right/justify (justify = rozpočítání mezer do run
      šířek), řádkování, mezery před/za — testy
- [x] F5.4 TextBox layout: padding, border, vertikální zarovnání, růst výškou (CanGrow),
      ořez/ellipsis (CanGrow=false) — testy
- [x] F5.5 E2E: harness vykreslí sadu TextBoxů (všechna zarovnání, justify, růst, ořez)
      → `__screenshots__/reporting/f5/NN-*.png`; brána: (1) funkčně — řádky lámou dle golden
      testů, justify bez „řek" mezer; (2) UX — typografická čistota

## F6 — Bandový layout + stránkování (Engine) → PRVNÍ VIZUÁLNÍ VÝSTUP

- [x] F6.1 Page composer: page setup → content rect; PageHeader/Footer na každé stránce,
      ReportHeader/Footer, Detail flow přes stránky — testy (počty stránek, pozice)
- [x] F6.2 KeepTogether na bandu + minimální sirotci (band se nerozlomí, přesun na další
      stránku) — testy
- [x] F6.3 PageNumber/TotalPages substituce (deferred výrazy z F2.4) — testy
- [x] F6.4 Snapshot generátor: strom instancí + layout → snapshot stránky (absolutní pozice,
      run šířky, primitiva) — round-trip testy, deterministický výstup (byte-parity)
- [x] F6.5 Ukázkový report „Faktura" (statická data): hlavička s logem (Image), adresy,
      tabulka položek zatím TextBoxy, patička s čísly stránek — fixture pro E2E
- [x] F6.6 E2E: harness vykreslí fakturu (2 stránky) → `__screenshots__/reporting/f6/01-invoice-p1.png`,
      `02-invoice-p2.png`; brána: (1) funkčně — bandy na správných místech, page footer s čísly,
      žádné překryvy; (2) UX — defaultní okraje/typografie působí profesionálně (tohle budou
      defaulty produktu!)

## F7 — Tablix (nejrizikovější layout kus)

- [x] F7.1 Model tabulky: sloupce (šířky fixed/proporční), header/detail/footer řádky, buňky
      s elementy, border model (collapse) — testy
- [x] F7.2 Detail řádky z datasetu, růst výšky řádku podle obsahu buněk (CanGrow) — testy
- [x] F7.3 Group headers/footers v tabulce + agregáty ve footerech — testy
- [x] F7.4 Stránkování: lom mezi řádky, **RepeatHeaderOnNewPage**, KeepTogether řádku,
      group keep-with — testy (golden: přesné rozdělení řádků na stránky)
- [x] F7.5 Row visibility výrazy, zebra styling (výraz na pozadí řádku) — testy
- [x] F7.6 Faktura fixture přepnutá na skutečný Table + nový fixture „Prodeje dle regionu"
      (3 úrovně skupin, agregáty, 30+ stránek)
- [x] F7.7 E2E: oba fixtures → `__screenshots__/reporting/f7/NN-*.png` (první/prostřední/poslední
      stránka, lom skupiny přes stránku, opakovaný header); brána: (1) funkčně — header se
      opakuje, agregáty sedí (porovnat s unit vypočtenými), žádný osiřelý group header na konci
      stránky; (2) UX — tabulka čitelná, zarovnání čísel doprava, rozumné paddingy

## F8 — PDF renderer (SkiaSharp)

- [x] F8.1 Snapshot → `SKDocument`: text runy (font, scaling na run width), čáry/obdélníky/
      primitiva, obrázky, clip — unit testy přes parsování PDF obsahu (počty stránek, MediaBox)
- [x] F8.2 Font embedding + subsetting (SkiaSharp typefaces z dodávaných TTF) — test: PDF
      obsahuje embedded subset, ne systémový font
- [x] F8.3 **Golden image brána:** PDF stránka → raster (Skia) vs. canvas screenshot téže
      stránky z harness — pixel diff s tolerancí (anti-aliasing) < 1 % rozdílných pixelů;
      artefakty do `__screenshots__/reporting/f8/01-canvas-vs-pdf-diff.png`
- [x] F8.4 Posouzení: (1) funkčně — faktura i prodeje vizuálně identické canvas vs. PDF;
      (2) UX — PDF vypadá tiskově profesionálně (testovací výtisk do souboru)

## F9 — TmReportViewer (Tempo.Blazor.Reporting)

- [x] F9.1 RCL projekt + JS viewer bundle (painter z F0.7 + virtualizace stránek + zoom +
      page cache — reuse vzorů z document editor vieweru) — Node testy painter logiky
- [x] F9.2 `TmReportViewer` komponenta: `ReportSource` (Remote/Embedded), lifecycle
      (OnAfterRenderAsync mount, dispose), loading/empty/error stavy — bUnit testy
- [x] F9.3 Toolbar: stránkování (první/předchozí/N z M/další/poslední), zoom (fit width/page/%),
      export menu (PDF; XLSX/CSV po F15), refresh, print (PDF stream) — bUnit + lokalizace
- [x] F9.4 `TmReportParameterPanel`: auto-generace z metadat parametrů (text/number/date/
      select/multi-select/bool), kaskádové parametry, validace, „Zobrazit report" — bUnit
- [x] F9.5 Embedded režim: `EmbeddedReportSource(definition, dataProvider)` — engine běží
      lokálně (WASM/server render mode agnostické) — bUnit + integrace
- [x] F9.6 Interaktivita stateless: toggle visibility/drill-down stav jako token v render
      requestu — testy
- [x] F9.7 Demo stránka v `Tempo.ReportServer.Web` (zatím proti in-memory store) + E2E:
      otevření reportu, zadání parametrů, listování, zoom, export PDF
      → `__screenshots__/reporting/f9/01-viewer-default.png`, `02-parameters.png`,
      `03-zoom-fit.png`, `04-export-menu.png`, `05-loading-state.png`
- [x] F9.8 Brána: (1) funkčně — parametry řídí obsah, stránkování funguje, export stáhne PDF;
      (2) UX — toolbar konzistentní s Tm* komponentami, design tokeny, klávesová ovladatelnost,
      prázdné/chybové stavy hezké

## F10 — Report Server API (Tempo.ReportServer.Api)

- [x] F10.1 Skeleton + EF Core úložiště (SQLite dev; O1/O3/O4 potvrdit) — migrace, UoW
- [x] F10.2 Tenancy middleware: tenant claim z JWT → `ReportExecutionContext`; EF global query
      filters — testy izolace (tenant A NIKDY nevidí data B — negativní testy povinné)
- [x] F10.3 Endpoints složky/reporty: CRUD, přesuny, vyhledávání, revize (immutable, publish/
      draft, rollback) — integrační testy (WebApplicationFactory)
- [x] F10.4 `/api/reports/{id}/parameters` — metadata pro parameter panel — testy
- [x] F10.5 Render pipeline: `/api/render` synchronní pro malé reporty (limit stránek z kvóty),
      `/api/render/jobs` asynchronní (Channel-based queue, **férovost per tenant** round-robin,
      kvóty: max stránek/timeout/velikost, job status polling) — testy vč. fairness testu
- [x] F10.6 Endpoints datových zdrojů: CRUD (tenant-scoped), test connection, schema discovery
      + preview top N (pro designer) — testy
- [x] F10.7 `ITempoReportServerClient` v Abstractions: typed client na všechny endpoints,
      pluggable token provider (cookie/server vs. bearer/WASM) — testy proti TestServeru
- [x] F10.8 E2E smoke: viewer z F9 přepnutý na RemoteReportSource proti běžícímu API
      → `__screenshots__/reporting/f10/01-remote-viewer.png`; brána funkční

## F11 — Multi-tenancy + permissions (rozšíření Api)

- [x] F11.1 Role: TenantAdmin/Author/Viewer; per-folder ACL s dědičností (explicit deny >
      allow) — unit testy resolveru oprávnění (tabulkové testy všech kombinací)
- [x] F11.2 Enforcement na všech endpoints (CRUD dle role, render dle Viewer, datové zdroje
      dle Author+grant) — integrační testy, negativní testy povinné
- [x] F11.3 API klíče pro embedding aplikace (tenant+aplikace, revokace, scope omezení) — testy
- [x] F11.4 Audit log (kdo/kdy/co: render, export, změna definice, změna ACL) — testy

## F12 — Tempo.ReportServer.Web (Blazor InteractiveAuto FE)

Pozn.: aktuální implementace používá ověřený `InteractiveServer` host; přímý přepínač na
`InteractiveAuto` bez samostatného WASM client projektu nehydratuje.

- [x] F12.1 App shell: InteractiveAuto, layout, navigace, přihlášení (O3), tenant switcher
      (pro uživatele ve více tenantech) — bUnit + E2E screenshot
- [x] F12.2 `TmReportExplorer` (komponenta v Tempo.Blazor.Reporting): strom složek + seznam/grid
      reportů, vyhledávání, thumbnaily (PNG z F8), CRUD složek, přesuny, kontextové menu —
      bUnit; reuse vzorů z document-library
- [x] F12.3 Stránka vieweru (route `/reports/{path}`) s parameter panelem a deep-linkem
      parametrů v URL — E2E
- [x] F12.4 Správa: datové zdroje (CRUD + test connection UI), permissions editor (ACL),
      revize reportu (historie, diff metadat, rollback) — bUnit + E2E
- [x] F12.5 E2E + screenshoty celého FE: `__screenshots__/reporting/f12/01-login.png`,
      `02-explorer-grid.png`, `03-explorer-list.png`, `04-viewer-page.png`,
      `05-datasources.png`, `06-permissions.png`, `07-revisions.png`
- [x] F12.6 Brána: (1) funkčně — celý flow přihlášení→nalezení→zobrazení→export; (2) UX —
      explorer působí jako moderní file manager, konzistence, prázdné stavy, responsivita
      šířek, dark mode pokud Tempo tokeny podporují

## F13 — TmReportDesigner (MVP) — po podfázích, každá se screenshot bránou

- [x] F13.1 **D1 plátno+bandy:** band layout s rulery, resize výšek bandů, zoom, grid+snap,
      page setup dialog — bUnit + E2E `__screenshots__/reporting/f13/d1-*.png`
- [x] F13.2 **D2 elementy:** paleta (drag&drop TextBox/Image/Shape/Table/Chart), selection +
      move/resize handles (reuse vzorů wireframe editoru), properties panel (per-element,
      FluentValidation), multi-select, kopírování, undo/redo (command pattern, reuse vzorů) —
      E2E `d2-*.png`
- [x] F13.3 **D3 data:** field list (datasety → drag pole do TextBoxu vytvoří výraz), datasety
      UI (výběr zdroje, query editor s preview top N), parametry UI, expression editor
      s našeptáváním (AST z F2 → completion Fields/Parameters/funkce) + inline validací —
      E2E `d3-*.png`
- [x] F13.4 **D4 náhled+uložení:** záložka Preview (render přes API se sample parametry),
      uložení revizí (draft/publish), validační panel (všechny chyby definice) — E2E `d4-*.png`
- [x] F13.5 Brána každé podfáze: (1) funkčně dle podfáze; (2) UX — designer srovnatelný
      s moderními nástroji (Figma-like selection, jasné afordance), TOHLE je vlajková loď UX
      Hotovo: `TmReportDesigner` MVP + `/designer/{ReportId?}` v `Tempo.ReportServer.Web`.
      Screenshot brána: `d1-canvas-bands.png`, `d1-page-setup.png`,
      `d2-elements-properties.png`, `d3-data-expression.png`, `d4-preview-publish.png`
      + `manifest.json`. Ověřeno: `TmReportDesignerTests`, `ReportDesignerPageTests`,
      `ReportingF13ReportDesignerE2ETests`, build `src/Tempo.ReportServer.Web`.

## F14 — Grafy (engine-drawn)

- [x] F14.1 Vektorová primitiva ve snapshotu (path/polyline/arc/fill/clip) + painter (JS)
      + PDF (Skia) podpora — golden testy + pixel diff
- [x] F14.2 Chart model: série z datasetu (kategorie/hodnota výrazy), barvy z palety tokenů —
      testy
- [x] F14.3 Geometrie: column/bar (skupiny, stacked NE v MVP), line/area, pie/donut — unit
      testy geometrie (přesné souřadnice pro známá data)
- [x] F14.4 Osy + mřížka + popisky (nice-step algoritmus, formátování čísel/dat dle culture,
      rotace popisků při kolizi) + legenda (pozice, zalamování) — testy
- [x] F14.5 Designer: chart properties panel + preview — bUnit
- [x] F14.6 E2E: fixture „Dashboard prodejů" (3 grafy + tabulka) → `__screenshots__/reporting/f14/NN-*.png`
      + canvas-vs-PDF diff; brána: (1) funkčně — hodnoty odpovídají datům (kontrolní součty);
      (2) UX — grafy hezké na úrovni moderních BI nástrojů, barvy z design tokenů

## F15 — XLSX/CSV export

- [x] F15.1 CSV z processing výstupu (culture, oddělovač, quoting, BOM volba) — testy
- [x] F15.2 XLSX (ClosedXML): tabulková data se styly (header, number formats z definice),
      žádná snaha o pixel layout — testy (otevření + obsah přes ClosedXML read-back)
- [x] F15.3 Export menu vieweru + API endpoint — integrační test + E2E stažení

## F16 — Scheduling + subscriptions

- [x] F16.1 Model: schedule (cron, parametry, formát, příjemci), subscription per uživatel —
      testy; queue reuse z F10.5
- [x] F16.2 Worker: cron trigger → render job → doručení; retry s backoff — testy s fake clock
- [x] F16.3 **E-mail doručení přes Tempo.Blazor.EmailTemplates** (report jako PDF příloha,
      šablona e-mailu z galerie) — integrace + demo flow přes smtp4dev
- [x] F16.4 FE správa schedulů + E2E `__screenshots__/reporting/f16/NN-*.png` (UX brána)

## F17 — Embedded demo + multi-app příběh

- [x] F17.1 Demo stránka v `Tempo.Blazor.Demo.SharedUI`: „cizí aplikace" s `TmReportViewer`
      v OBOU režimech — Embedded (lokální engine + vlastní `IReportDataProvider` nad demo
      daty) a Remote (proti ReportServer.Api) — E2E obojí
- [x] F17.2 README sekce „Embedding do vaší aplikace" s oběma scénáři (kód, auth, API klíče)
- [x] F17.3 E2E screenshoty `__screenshots__/reporting/f17/NN-*.png` + UX brána

## F18 — MCP tooly (vzor Tempo.Blazor.Mcp wireframe)

- [x] F18.1 Tooly: list_reports, get_report_definition, create_report, update_report_elements,
      validate_report, render_report_preview (PNG) — testy (vzor Mcp.Tests)
- [x] F18.2 PromptHelper dokumentace schématu definice pro AI — test úplnosti (každý element
      typu má popis)
- [x] F18.3 E2E: AI-friendly flow create→validate→preview — smoke test

## F19 — Dokumentace + pack

- [x] F19.1 README všech balíčků + COMPONENTS.md záznamy (TmReportViewer, TmReportDesigner,
      TmReportExplorer, TmReportParameterPanel)
- [x] F19.2 XML doc komentáře public API + JSON schema dokumentace definice reportu
- [x] F19.3 NuGet pack metadata + ověření `dotnet pack` všech 3 balíčků
- [x] F19.4 Finální regrese: všechny unit/bUnit/integrační/E2E + kompletní screenshot review
      (závěrečný UX audit celého produktu, nálezy → opravit)

---

## Post-MVP backlog (vědomě odložené)
- RTL + komplexní skripty (HarfBuzzSharp, port UBA z JS) — dle O5
- Hyphenation, stacked/kombinované grafy, scatter/bubble
- Drill-through mezi reporty, document map/záložky, interaktivní sort ve vieweru
- Distribuovaná render queue (víc worker instancí), Redis cache snapshotů
- Snapshot/archiv vyrenderovaných reportů (audit „co přesně viděl uživatel X")
- Designer: copy stylu, style sheets/themes, zarovnávací guides jako ve Figmě
- Import z RDL/RDLC (SSRS migrace) — silný prodejní argument, ale až po stabilizaci schématu
