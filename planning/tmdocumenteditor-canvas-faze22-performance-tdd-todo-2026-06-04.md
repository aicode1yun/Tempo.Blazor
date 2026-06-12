# Canvas engine - Faze 22: Performance a velké dokumenty (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 22** · Stav: hotovo · Priorita: P1

## Proč

Editor musí zvládnout velké dokumenty: virtualizované stránky, tile/canvas cache, inkrementální layout aktivního odstavce, idle reconciliation, measurement cache bounds. Realizuje master princip „incremental recalculation" a „per-page canvas cache". Bez toho psaní seká.

## Cílový stav

- Virtualized visible pages; tile/canvas cache invalidation.
- Incremental active paragraph layout (jen dirty odstavec); idle full-document reconciliation.
- Text measurement cache bounds.
- Large doc first paint target; typing latency p50/p95 metriky.
- Scroll smoothness metriky; memory leak testy přes opakované open/close.

## Clean-room
- [x] Vlastní; ONLYOFFICE recalc-info/page-cache jen koncept.

## Znovupoužití
- [x] R.4.1 virtualizace (render-host first-paint 100p ~486ms = baseline k optimalizaci); R.4.6 layout cache; font-metrics LRU.
- [x] `.github/workflows/document-editor-performance.yml` (perf CI, již existuje).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/perf/page-virtualizer.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/perf/tile-cache.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/perf/recalc-info.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/perf/__tests__/incremental-layout.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/perf/__tests__/cache-invalidation.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasPerformanceE2ETests.cs
```

## DoD
- [x] First paint není blank; visible pages se plní progresivně.
- [x] Typing latency p50/p95 pod targetem; žádný memory leak přes open/close.

## Faze 22.1: Recalc-info incremental layout

### 22.1.1 RED
- [x] `incremental-layout.test.mjs`: editace označí dirty odstavec; recalc začíná od prvního dirty bloku; flow objekty reflow jen když nutné; nedirty bloky se nepřepočítají.

### 22.1.2 GREEN + akceptace
- [x] `recalc-info.mjs`; immediate path pro aktivní odstavec; idle reconciliation zbytku.

## Faze 22.2: Page virtualizace

### 22.2.1 RED
- [x] `page-virtualizer`: jen visible (+buffer) stránky se layoutují/renderují; scroll plní progresivně; first paint ne blank.

### 22.2.2 GREEN + screenshot + akceptace
- [x] `page-virtualizer.mjs`; E2E velký dokument first paint + scroll fill.

## Faze 22.3: Tile/canvas cache invalidation

### 22.3.1 RED
- [x] `cache-invalidation.test.mjs`: page content cache se invaliduje jen pro dirty stránky; overlay (caret/selection) nepřekresluje content cache.

### 22.3.2 GREEN + akceptace
- [x] `tile-cache.mjs`; per-page cache + overlay passy (master princip).

## Faze 22.4: Measurement cache bounds + metriky

### 22.4.1 RED
- [x] Measurement cache má bounded velikost (LRU eviction); typing latency p50/p95 měřená; scroll smoothness; memory stabilní přes open/close.

### 22.4.2 GREEN + akceptace fáze 22
- [x] Cache bounds; perf metriky v CI (existující workflow); memory leak test; E2E perf screenshot (progresivní fill).

## Poznámky
- Tohle je fáze, kde se vyplatí incremental recalc zavedený už od Faze 8 (immediate path).
- Worker offload (layout/measure v workeru) jako P2 optimalizace.
- Cíl: psaní plynulé i v dokumentu se stovkami stran.
