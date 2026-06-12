# Canvas engine - Faze 14: Tables (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 14** · Stav: implementováno · Priorita: P1

## Proč

Tabulky jsou velká subdoména: grid layout na canvasu, text uvnitř buněk (vnořený layout), cell hit-test/selection, navigace Tab, insert/delete řádků/sloupců, merge/split, resize, formátování buněk. Pokročilé (styly, repeat header, split across pages, nested) jsou E12.

## Cílový stav

- Render table grid na canvasu (borders, pozadí buněk).
- Text layout uvnitř buněk (reuse paragraph engine v cell rect).
- Cell hit-test; cell caret/selection; cell range selection.
- Tab/Shift+Tab navigace; insert/delete row/column.
- Merge/split horizontal/vertical; column resize drag.
- Cell formatting background/alignment/vAlign.
- Save/reload + DOCX roundtrip table parity.

## Clean-room
- [x] Table layout/merge algoritmy vlastní; ONLYOFFICE jen inspirace.

## Znovupoužití
- [ ] `core-engine/edit-table.mjs` (table ops), `objects/table-controller.mjs`.
- [x] Paragraph engine pro cell content; hit-test/caret/selection (Faze 7) v cell prostoru.
- [ ] C# `TmDocumentTablePropertiesPanel`, `TmDocumentCellPropertiesPanel`, `TmDocumentTableGridPicker`, `TmDocumentTableToolbar`.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/table-layout.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/table-selection.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/table-ops.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/__tests__/table-layout.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/tables/__tests__/table-ops.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasTableE2ETests.cs
```

## DoD
- [x] Save/reload table; DOCX roundtrip parity gate.
- [x] Tabulka vypadá jako editor table, ne HTML fallback.
- [x] Undo gate pro každou table op.

## Faze 14.1: Table grid layout + cell content

### 14.1.1 RED
- [x] `table-layout.test.mjs`: grid s row/col, šířky sloupců, výšky řádků z obsahu; text v buňce wrapuje v cell width; spans respektovány.

### 14.1.2 GREEN + screenshot + akceptace
- [x] `table-layout.mjs` (grid normalizace + cell paragraph layout); E2E render tabulky.

## Faze 14.2: Cell hit-test, caret, selection

### 14.2.1 RED
- [x] Klik do buňky umístí caret v cell prostoru; cell range selection (drag přes buňky); border mezi buňkami.

### 14.2.2 GREEN + screenshot + akceptace
- [x] `table-selection.mjs`; reuse hit-test/caret; cell range highlight overlay.

## Faze 14.3: Navigace + insert/delete row/column

### 14.3.1 RED
- [x] `table-ops.test.mjs`: Tab/Shift+Tab cyklí buňky (Tab na konci přidá řádek); insert/delete row/column zachová spans.

### 14.3.2 GREEN + screenshot + akceptace
- [ ] Reuse edit-table; E2E psát do buněk, Tab navigace, insert/delete.

## Faze 14.4: Merge/split + column resize

### 14.4.1 RED
- [x] Merge buněk horizontal/vertical; split; column resize drag mění šířky; grid zůstává konzistentní.

### 14.4.2 GREEN + screenshot + akceptace
- [x] Merge/split ops; resize drag preview; E2E merge + resize; undo gate.

## Faze 14.5: Cell formatting + perzistence

### 14.5.1 RED
- [x] Cell background, text alignment, vertical alignment; save/reload zachová tabulku; DOCX roundtrip.

### 14.5.2 GREEN + screenshot + akceptace fáze 14
- [x] Cell formatting commandy; provider save/reload; DOCX table parity; E2E + screenshot „vypadá jako editor table".

## Poznámky
- Implementace fáze 14 používá vlastní canvas `tables/table-ops.mjs`; starší `core-engine/edit-table.mjs` a `objects/table-controller.mjs` zůstaly nereusované kvůli odlišnému modelovému tvaru a clean-room požadavku této fáze.
- Table styly (banded), repeat header row, split across pages, nested tables, convert text↔table, sort, formula = E12.
- Velké tabulky přes stránky = interakce s pagination (Faze 6) + E12.
