# Canvas engine - E2: Tab stops a pravítko (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E2** · Stav: dokončeno · Priorita: P2 (nad rámec legacy)

## Proč

Tab stops (left/center/right/decimal/bar) s leadery a interaktivní pravítko jsou standard Word/GDocs. Legacy je neměl. Navazuje na ruler vizuál z Faze 10.5 a layout z Faze 6.

## Cílový stav

- Tab stop model na odstavci: pozice, alignment, leader; default tab width.
- Tab posune caret na další stop; decimal tab zarovná čísla na desetinnou čárku.
- Ruler: tab type picker, klik vloží tab, drag tab, double-click → Tabs dialog.
- Indent markery (first-line/hanging/left/right) drag na pravítku.

## Clean-room
- [x] Tab/ruler logika vlastní.

## Znovupoužití
- [x] `render/ruler.mjs` (Faze 10.5 vizuál); layout options (Faze 6); line-breaker pro tab advance.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/tab-stops.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/ruler-interaction.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/tab-stops.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasTabStopsE2ETests.cs
```

## DoD
- [x] Decimal tab zarovná sloupec čísel; leader dots viditelné.
- [x] Save/reload tab stops; undo gate.

## Faze E2.1: Tab stop model + default width

### E2.1.1 RED
- [x] `tab-stops.test.mjs`: model (pozice/alignment/leader); default tab width; znak Tab posune caret na další stop.

### E2.1.2 GREEN + akceptace
- [x] `tab-stops.mjs`; tab advance v line-breaker; converter round-trip.

## Faze E2.2: Alignment varianty + leader

### E2.2.1 RED
- [x] left/center/right/decimal/bar zarovnání obsahu k tab stopu; decimal na desetinnou čárku; leader (dot/dash/underline) vykreslen.

### E2.2.2 GREEN + screenshot + akceptace
- [x] Alignment + leader render; E2E sloupec čísel s decimal tab + leader dots.

## Faze E2.3: Ruler interakce

### E2.3.1 RED
- [x] `ruler-interaction`: tab type picker; klik na pravítko vloží tab; drag tab; double-click → Tabs dialog; indent markery drag.

### E2.3.2 GREEN + screenshot + akceptace fáze E2
- [x] Ruler interakce + dialog; ruler px ↔ document units; E2E nastavit decimal tab přes pravítko + reload; undo gate.

## Poznámky
- Tabs dialog (clear all, set position) jako Blazor komponenta.
- Bar tab (svislá čára) jen vizuální.
- Undo gate je ověřený runtime command testem `tab stop runtime transaction supports undo and redo`; E2E ověřuje decimal tab, leader dots, ruler dialog, save/reload a screenshoty.
