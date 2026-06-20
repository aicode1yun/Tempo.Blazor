# Canvas engine - Faze 6: Text measurement, line breaking a pagination (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 6** · Stav: hotovo · Priorita: P0 (jádro layoutu)

## Proč

Skutečný dokumentový layout: měření fontů, segmentace, lámání řádek, zarovnání, spacing/indent, list labels a stránkování. Toto je nejnáročnější jádro; bez něj text přetéká nebo se překrývá. Staví na display listu z Faze 5 a je předpokladem caretu (7) a vstupu (8).

## Cílový stav

- Font resolver: rodina + fallback, velikost, weight, style.
- Měření přes canvas measurement cache (žádné DOM měření).
- Grapheme segmentace; word breaking; soft wrap; hard break.
- Paragraph layout: line boxy, ascender/descender, line height.
- Alignment left/center/right/justify; spacing before/after; indent + hanging indent.
- Lists: layout bullet/number labelu.
- Pagination: kapacita stránky, page breaky; widow/orphan jako P2.
- Žádný overlap textu ani pro dlouhé odstavce.

## Clean-room
- [x] Line-breaking i pagination vlastní; OOXML/Unicode jako standard, ONLYOFFICE jen inspirace.

## Znovupoužití (zásadní — nepřepisovat)
- [x] `layout/font-metrics.mjs`, `text-measurement.mjs`, `test-text-measurer.mjs`.
- [x] `layout/paragraph-engine.mjs` (71KB), `line-breaker.mjs` (19KB) + `line-breaker-helpers.mjs`, `line-breaker-fallback.mjs`, `line-draft.mjs`, `line-box-scorer.mjs`.
- [x] `layout/paragraph-tokenizer.mjs`, `grapheme.mjs`, `bidi.mjs`, `paragraph-alignment.mjs`, `paragraph-runs.mjs`.
- [x] `layout/page-metrics.mjs`, `paragraph-layout-tree.mjs`, `paragraph-layout-options.mjs`, `scoped-layout-metadata.mjs`.
- [x] `core-engine/list-model.mjs` pro marker rules; `list-layout.mjs` post-shift pass nebyl použit, protože canvas adapter řádky rovnou re-wrapuje do list text measure.
- [x] Intervaly/exclusions (`text-exclusion*.mjs`, `available-intervals-cache.mjs`) pro obtékání (využije Faze 15/E7).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/pagination.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/line-breaking.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/alignment-spacing.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/pagination.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasTextLayoutE2ETests.cs
```

## DoD
- [x] `AssertNoTextOverlapAsync` pro dlouhé odstavce.
- [x] Layout deterministický; metriky testované číselně.
- [x] Screenshot: text vypadá jako dokument.

## Faze 6.1: Font resolver a measurement cache

### 6.1.1 RED
- [x] Font resolver vrací fallback chain; measurement cache vrací stabilní šířky (LRU, žádné DOM).

### 6.1.2 GREEN + akceptace
- [x] Resolver + cache (reuse font-metrics); cache bounds (Faze 22 doladí).

## Faze 6.2: Segmentace a word breaking

### 6.2.1 RED
- [x] `line-breaking.test.mjs`: grapheme clustery, slova, break opportunities (mezery, pomlčky), CJK per-char break.

### 6.2.2 GREEN + akceptace
- [x] Reuse tokenizer + grapheme; break opportunity model.

## Faze 6.3: Lámání řádek (soft wrap + hard break)

### 6.3.1 RED
- [x] Dlouhý odstavec se zalomí do více řádek v rámci body width; hard break (Shift+Enter) dělí řádku; žádný overlap; line height/ascender/descender korektní.

### 6.3.2 GREEN + screenshot + akceptace
- [x] Reuse paragraph-engine/line-breaker; E2E screenshot wrapu; `AssertNoTextOverlapAsync`.

## Faze 6.4: Alignment

### 6.4.1 RED
- [x] `alignment-spacing.test.mjs`: left/center/right pozice; justify roztáhne mezery (kromě poslední řádky).

### 6.4.2 GREEN + screenshot + akceptace
- [x] Reuse paragraph-alignment; justify spacing; E2E 4 zarovnání.

## Faze 6.5: Spacing, indent, hanging indent

### 6.5.1 RED
- [x] Spacing before/after; left/right indent; first-line a hanging indent.

### 6.5.2 GREEN + akceptace
- [x] Layout options aplikované; metriky testované.

## Faze 6.6: List labels

### 6.6.1 RED
- [x] Bullet/number label má šířku, zarovnání, hanging indent; text nezačíná pod labelem (žádný overlap).

### 6.6.2 GREEN + screenshot + akceptace
- [x] Reuse list marker model; E2E screenshot seznamu.

## Faze 6.7: Pagination

### 6.7.1 RED
- [x] `pagination.test.mjs`: odstavce naplní stránku, přetečení jde na další stránku; explicit page break vynutí novou stránku; kapacita počítá margins.

### 6.7.2 GREEN + screenshot + akceptace fáze 6
- [x] `pagination.mjs` (reuse page-metrics); E2E: dlouhý dokument se láme přes stránky; widow/orphan označit P2.
- [x] UX review: řádky a stránky působí jako dokument.

## Poznámky
- Toto je největší riziková fáze; držet sub-fáze malé a každou s číselným metrik testem.
- Bidi/RTL alignment a hyphenation: bidi reuse z `bidi.mjs`; plná RTL paragraph alignment a hyphenation = E12 / follow-up.
- Inkrementální recalc (jen dirty odstavec) = Faze 22; tady plný layout stačí.

## Implementační evidence

- Přidáno `layout/canvas-text-style.mjs` a `layout/pagination.mjs`; canvas model se adaptuje do existujícího paragraph/line breaker enginu, měří přes `font-metrics.mjs`, používá grapheme/tokenizer/alignment stack a vytváří vícestránkový layout.
- `display-list.mjs` generuje textové příkazy z line/segment layoutu místo jedné řádky; podporuje justify spacing, list labely, page fragments, tabulky/obrázky ve flow a `textRects` metadata pro E2E overlap gate.
- `canvas-stack.mjs` renderuje více stránek podle display listu a synchronizuje skrytou text-rect metadata vrstvu; renderer kreslí i `listLabel`.
- Demo seed `phase-6-canvas-text-layout` a E2E `DocumentEditorCanvasTextLayoutE2ETests` ověřují wrap, pagination, list labels, non-blank canvas a `AssertNoTextOverlapAsync`.
- Screenshot evidence: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase6-text-layout/2026-06-04/desktop-1440x1000/`.
